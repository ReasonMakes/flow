using System.Runtime.InteropServices;
using Godot;

public partial class PlayerMovement : RigidBody3D
{
    [Export] private Statistics Statistics;
    [Export] public CameraPlayer CameraPlayer;
    public float MouseSensitivity = 0.001f;

    //Thrust
    [Export] public CsgBox3D DiagnosticVector;
    private bool InputForward = false;
    private bool InputLeft = false;
    private bool InputRight = false;
    private bool InputBack = false;

    private const float ThrustMagnitude =  250f;
    private const float ThrustMagnitudeOnFlatCoefficient = 1f;
    private const float ThrustMagnitudeOnSlopeCoefficient = 0.4f;
    private const float ThrustMagnitudeInAirCoefficient = 0.002f;//0.4f;
    private const float ThrustMagnitudeCrouchedCoefficient = 0.05f;

    private const float MaxSpeedAlignedTwitch = 20f; //actual twitch speed will be about half of this since twitch acceleration decays towards 0 the closer it gets to this value
    private const float MaxSpeedAlignedTwitchInAir = 7.85f;

    private const float MaxSoftSpeedTotalInAir = 15f; //Soft-cap; the speed at which in-air thrusting magnitude begins to decrease. This is mostly used to nerf airstrafing.
    private const float MaxSpeedTotalInAir = 23f; //Hard-cap max speed for in-air thrusting. Once the player is moving at this speed, they can now longer accelerate at all - only decelerate

    //Jerk - Added acceleration which starts at 0 and increases up to JerkMagnitude. Only applies on flat surfaces
    private float Jerk = 0f; //value from 0f to 1f
    private const float JerkMagnitude = 200f; //maximum added acceleration (will be this after JerkPeriod elapsed)
    private const float JerkMagnitudeInAir = 0f;
    private const float JerkRate = 1f; //how quickly jerk is developed
    private const float JerkDecayRate = 16f; //how quickly developed jerk is lost if not actively developing it
    private const float JerkDecayRateInAir = 4f;

    //Slopes
    private const float SlopeDotUp = 0.70710678118f; //What angle is a flat surface (45 deg)
                                                     //LESS THAN this values is a slope and engages climbing/wallrunning
                                                     //This is the dot product of the normal of the surface to global up
                                                     //-1 is down, 0 is toward the horizon, 1 is up
                                                     //cos(angle) = dot :. angle = cos^-1(dot)
    
    private enum Surface
    {
        Air,
        Slope,
        Flat
    }
    private Surface SurfaceOn = Surface.Air;
    private Surface SurfaceWishingInto = Surface.Air;
    private Vector3? SurfaceOnNormal = null;
    private Vector3? SurfaceWishingIntoNormal = null;

    //Slope movement
    private float SlopeMovementEnergy = 0f;
    private const float SlopeMovementEnergyDenominator = 10f; //what amount of energy constitues a 1f factor on thrust magnitude

    //Drag
    private const float Drag = 20f;
    private const float DragOnFlatCoefficient = 1f;
    private const float DragOnSlopeCoefficient = 0.01f;
    private const float DragInAirCoefficient = 0f; //0.01f;
    private const float DragCrouchedCoefficient = 0.05f;

    private const float DragOnFlatStatic = 0.8f; //the speed below which the player's friction will be raised to very high levels to stop them from slipping,
                                                 //as long as they ar on a flat and aren't trying to thrust
                                                 //0.68f was the last measured slipping speed when standing on something that's barely a flat rather than a slope

    //Crouch
    private bool InputCrouch = false;
    private bool IsCrouched = false;

    //Jump
    private bool InputJump = false;
    private const float JumpVSpeed = 10f;
    private bool JumpedAndStillOnFlat = false;
    private float JumpedResetForcibly = 0f;
    private const float JumpedResetForciblyPeriod = 1f; //how long in seconds after a jump is the ability to jump re-enabled, even if the player never left the ground

    public override void _Input(InputEvent @event)
    {
        //Run Direction
        InputForward = Input.IsActionPressed("thrust_run_dir_forward");
        InputLeft = Input.IsActionPressed("thrust_run_dir_left");
        InputRight = Input.IsActionPressed("thrust_run_dir_right");
        InputBack = Input.IsActionPressed("thrust_run_dir_back");
    
        InputCrouch = Input.IsActionPressed("crouch");
    
        InputJump = Input.IsActionPressed("thrust_jump");
    
        //InputDash = Input.IsActionJustPressed("thrust_dash");
    
        //Look
        if (@event is InputEventMouseMotion mouseMotion)
        {
            //Yaw
            CameraPlayer.CameraParent.Rotation = new Vector3(
                CameraPlayer.CameraParent.Rotation.X,
                CameraPlayer.CameraParent.Rotation.Y - mouseMotion.Relative.X * MouseSensitivity,
                CameraPlayer.CameraParent.Rotation.Z
            );
    
            //Pitch, clamp to straight up or down
            CameraPlayer.CameraParent.Rotation = new Vector3(
                Mathf.Clamp(CameraPlayer.CameraParent.Rotation.X - mouseMotion.Relative.Y * MouseSensitivity,
                    -0.24f * Mathf.Tau,
                    0.24f * Mathf.Tau
                ),
                CameraPlayer.CameraParent.Rotation.Y,
                CameraPlayer.CameraParent.Rotation.Z
            );
        }
    }

    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //Diagnostics
        //Speed
        string statSpeed = LinearVelocity.Length() != 0f && LinearVelocity.Length() < 0.1f ? "<0.1" : $"{LinearVelocity.Length():F2}";
        float hSpeed = new Vector3(LinearVelocity.X, 0f, LinearVelocity.Z).Length();
        string statHSpeed = hSpeed != 0f && hSpeed < 0.1f ? "<0.1" : $"{hSpeed:F2}";
        string statVSpeed = LinearVelocity.Y != 0f && Mathf.Abs(LinearVelocity.Y) < 0.1f ? "<+/-0.1" : $"{LinearVelocity.Y:F2}";

        Statistics.Statistic2.Text = $"Speed: {statSpeed}";
        Statistics.Statistic3.Text = $"HSpeed: {statHSpeed}";
        Statistics.Statistic4.Text = $"VSpeed: {statVSpeed}";
        Statistics.Statistic5.Text = $"Jerk: {Jerk}, Surface factor: {GetThrustPerSurface()}";

        //Surface
        Statistics.Statistic7.Text = $"SurfaceOn: {SurfaceOn}";
        Statistics.Statistic8.Text = $"SurfaceWishingInto: {SurfaceWishingInto}";
        Statistics.Statistic9.Text = $"SurfaceOnNormal: {SurfaceOnNormal}";
        Statistics.Statistic10.Text = $"SurfaceWishingIntoNormal: {SurfaceWishingIntoNormal}";

        //Slope movement
        Statistics.Statistic11.Text = $"SlopeMovementEnergy: {SlopeMovementEnergy}";
        Statistics.Statistic12.Text = $"JumpedAndStillOnFlat: {JumpedAndStillOnFlat}";
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float delta = state.Step;

        //SLOPE MOVEMENT PT. 1 (climbing, wallrunning)
        bool noPreviousSlopeMovement = SurfaceOn != Surface.Slope && SurfaceWishingInto != Surface.Slope;
        float speedPreviously = LinearVelocity.Length(); //TODO: align this

        //THRUST
        //Direction
        Vector3 thrustDirection = GetThrustDirection(state);

        //Surface factor
        float thrustMagnitude = GetThrustPerSurface();
        
        //Alignment factor
        float alignmentFactor;
        if (SurfaceOn == Surface.Flat)
        {
            alignmentFactor = Mathf.Max(0f, MaxSpeedAlignedTwitch - thrustDirection.Dot(LinearVelocity)) / MaxSpeedAlignedTwitch;
        }
        else
        {
            alignmentFactor = Mathf.Max(0f, MaxSpeedAlignedTwitchInAir - thrustDirection.Dot(LinearVelocity)) / MaxSpeedAlignedTwitchInAir;
        }
        thrustMagnitude *= alignmentFactor;
        Statistics.Statistic6.Text = $"Alignment: {alignmentFactor:F2}";

        //Jerk term
        //TODO: this should apply to wallrunning but not climbing
        JerkDevelop(thrustDirection, alignmentFactor, delta);
        if (SurfaceOn == Surface.Flat)
        {
            thrustMagnitude += (Jerk * JerkMagnitude);
        }
        else
        {
            thrustMagnitude += (Jerk * JerkMagnitudeInAir);
        }

        //Sum
        Statistics.Statistic6.Text += $", thrustMagnitude: {thrustMagnitude:F2}";
        Vector3 thrustVector = thrustDirection * thrustMagnitude;

        //INTEGRATE DRAG
        float drag = GetDrag();
        if (drag != 0f)
        {
            float decayFactor = Mathf.Exp(-drag * delta);
            LinearVelocity = (LinearVelocity * decayFactor) + (thrustVector / drag * (1f - decayFactor));
        }
        else
        {
            //0 drag - such as if in air

            //Prevent air strafing past MaxSpeed
            bool isDecelerating = (LinearVelocity + thrustVector).Length() < LinearVelocity.Length();
            if (isDecelerating)
            {
                LinearVelocity += thrustVector;
            }
            else if (LinearVelocity.Length() < MaxSpeedTotalInAir)
            {
                if (LinearVelocity.Length() < MaxSoftSpeedTotalInAir)
                {
                    LinearVelocity += thrustVector;
                }
                else
                {
                    float airstrafeFactor = (MaxSpeedTotalInAir - LinearVelocity.Length()) / (MaxSpeedTotalInAir - MaxSoftSpeedTotalInAir);
                    LinearVelocity += thrustVector * airstrafeFactor;

                    Statistics.Statistic6.Text += $", airstrafeFactor: {airstrafeFactor:F2}";
                }
            }
        }
        Statistics.Statistic6.Text += $", drag: {drag:F2}";

        //Standing still drag
        if (SurfaceOn == Surface.Flat && LinearVelocity.Length() < DragOnFlatStatic && thrustDirection == Vector3.Zero)
        {
            PhysicsMaterialOverride.Friction = 1f;
        }
        else
        {
            PhysicsMaterialOverride.Friction = 0f;
        }

        //JUMP
        //Manage
        JumpedResetForcibly = Mathf.Max(0f, JumpedResetForcibly - delta);
        if (SurfaceOn != Surface.Flat || JumpedResetForcibly == 0f)
        {
            JumpedAndStillOnFlat = false;
        }

        //Jump
        if (InputJump && SurfaceOn == Surface.Flat && !JumpedAndStillOnFlat)
        {
            //Set vertical speed
            LinearVelocity = new Vector3(
                LinearVelocity.X,
                Mathf.Max(LinearVelocity.Y + JumpVSpeed, JumpVSpeed), //VSpeed will reset if negative, otherwise add to it
                LinearVelocity.Z
            );

            //Prevent geting extra height
            JumpedAndStillOnFlat = true;
            JumpedResetForcibly = JumpedResetForciblyPeriod;
        }

        //SLOPE MOVEMENT PT. 2 (climbing, wallrunning)
        if (SurfaceOn == Surface.Flat)
        {
            //Reset SlopeMovementEnergy
            SlopeMovementEnergy = 0f;
        }
        else if ((SurfaceOn == Surface.Slope || SurfaceWishingInto == Surface.Slope) && noPreviousSlopeMovement)
        {
            //Entered into a slope
            //Set SlopeMovementEnergy
            SlopeMovementEnergy = Mathf.Max(SlopeMovementEnergy, speedPreviously);
        }
        else
        {
            //Decrement SlopeMovementEnergy
            SlopeMovementEnergy = Mathf.Max(0f, SlopeMovementEnergy - delta);
        }

        //DIAGNOSTICS
        DiagnosticVector.GlobalPosition = CameraPlayer.GlobalTransform.Origin + thrustDirection * 2.0f;
    }
    
    private Vector3 GetThrustDirection(PhysicsDirectBodyState3D state)
    {
        Vector3 wishDirectionRaw = GetWishDirectionRaw();
        Vector3 wishDirectionTangentToUp = (wishDirectionRaw - (Vector3.Up * wishDirectionRaw.Dot(Vector3.Up))).Normalized();

        UpdateCollisionDetection(state, wishDirectionTangentToUp);

        //By default align to up
        Vector3 thrustDirection = wishDirectionTangentToUp;

        //Wishing into a surface?
        if (SurfaceWishingIntoNormal.HasValue)
        {
            //Align to surface
            thrustDirection = (wishDirectionRaw - ((Vector3)SurfaceWishingIntoNormal * wishDirectionRaw.Dot((Vector3)SurfaceWishingIntoNormal))).Normalized();
        }
        else
        {
            //Not wishing into a surface
            if (SurfaceOn == Surface.Flat)
            {
                //On a flat surface
                //Redirect so we can walk downhill
                thrustDirection = (wishDirectionTangentToUp - ((Vector3)SurfaceOnNormal * wishDirectionTangentToUp.Dot((Vector3)SurfaceOnNormal))).Normalized();
            }
        }
        
        return thrustDirection;
    }
    
    private Vector3 GetWishDirectionRaw()
    {
        Vector3 wishDirectionRaw = Vector3.Zero;
        if (InputForward) wishDirectionRaw -= CameraPlayer.GlobalBasis.Z;
        if (InputLeft) wishDirectionRaw -= CameraPlayer.GlobalBasis.X;
        if (InputRight) wishDirectionRaw += CameraPlayer.GlobalBasis.X;
        if (InputBack) wishDirectionRaw += CameraPlayer.GlobalBasis.Z;
        return wishDirectionRaw;
    }

    private void UpdateCollisionDetection(PhysicsDirectBodyState3D state, Vector3 wishDirectionTangentToUp)
    {
        float checkingSurfaceOnNormalDotWishSmallest = 1f;
        float checkingSurfaceWishingIntoNormalDotWishSmallest = 1f;

        //Defaults
        SurfaceOn = Surface.Air;
        SurfaceOnNormal = null;
        SurfaceWishingInto = Surface.Air;
        SurfaceWishingIntoNormal = null;

        //Collision detection
        int contactCount = state.GetContactCount();
        if (contactCount > 0)
        {
            for (int i = 0; i < contactCount; i++)
            {
                Vector3 checkingNormal = state.GetContactLocalNormal(i);

                //Standing on
                //Prefer flattest
                float standOnDot = Vector3.Down.Dot(checkingNormal);
                if (standOnDot < 0f && standOnDot < checkingSurfaceOnNormalDotWishSmallest)
                {
                    //Update smallest dot so far
                    checkingSurfaceOnNormalDotWishSmallest = standOnDot;

                    //Normal
                    SurfaceOnNormal = checkingNormal;

                    //Surface
                    if (Vector3.Up.Dot(checkingNormal) < SlopeDotUp)
                    {
                        SurfaceOn = Surface.Slope;
                    }
                    else
                    {
                        SurfaceOn = Surface.Flat;
                    }
                }

                //Wishing into
                //Prefer aligned to wish
                float wishIntoDot = wishDirectionTangentToUp.Dot(checkingNormal);
                if (wishIntoDot < 0f && wishIntoDot < checkingSurfaceWishingIntoNormalDotWishSmallest)
                {
                    //Update smallest dot so far
                    checkingSurfaceWishingIntoNormalDotWishSmallest = wishIntoDot;

                    //Normal
                    SurfaceWishingIntoNormal = checkingNormal;

                    //Surface
                    if (Vector3.Up.Dot(checkingNormal) < SlopeDotUp)
                    {
                        SurfaceWishingInto = Surface.Slope;
                    }
                    else
                    {
                        SurfaceWishingInto = Surface.Flat;
                    }
                }
            }
        }
    }

    private float GetThrustPerSurface()
    {
        float acceleration = ThrustMagnitude;

        //Surface
        if (SurfaceOn == Surface.Slope || (SurfaceOn == Surface.Air && SurfaceWishingInto == Surface.Slope))
        {
            //Slope movement
            acceleration *= ThrustMagnitudeOnSlopeCoefficient;

            float slopeMovementEnergyFactor = Mathf.Min(1f, SlopeMovementEnergy / SlopeMovementEnergyDenominator);
            acceleration *= slopeMovementEnergyFactor;
        }
        else if (SurfaceOn == Surface.Air)
        {
            //Aerial movement
            acceleration *= ThrustMagnitudeInAirCoefficient;
        }
        else if (SurfaceOn == Surface.Flat)
        {
            //Flat movement
            acceleration *= ThrustMagnitudeOnFlatCoefficient;
        }

        //Crouch
        if (IsCrouched)
        {
            acceleration *= ThrustMagnitudeCrouchedCoefficient;
        }

        return acceleration;
    }

    private float GetDrag()
    {
        float drag = Drag;

        //Surface
        if (SurfaceOn == Surface.Slope || (SurfaceOn == Surface.Air && SurfaceWishingInto == Surface.Slope))
        {
            //Slope
            drag *= DragOnSlopeCoefficient;
        }
        else if (SurfaceOn == Surface.Air)
        {
            //Aerial
            drag *= DragInAirCoefficient;
        }
        else if (SurfaceOn == Surface.Flat)
        {
            //Flat
            drag *= DragOnFlatCoefficient;
        }

        //Crouch
        if (IsCrouched)
        {
            drag *= DragCrouchedCoefficient;
        }

        return drag;
    }

    private void JerkDevelop(Vector3 thrustDirection, float alignmentFactor, float delta)
    {
        if (thrustDirection != Vector3.Zero)
        {
            if (!IsCrouched)
            {
                Jerk = Mathf.Min(Jerk + (delta * JerkRate * alignmentFactor), 1f);
            }
        }
        else
        {
            if (SurfaceOn == Surface.Air)
            {
                Jerk = Mathf.Max(Jerk - (delta * JerkDecayRateInAir), 0f);
            }
            else
            {
                Jerk = Mathf.Max(Jerk - (delta * JerkDecayRate), 0f);
            }
        }
    }
}