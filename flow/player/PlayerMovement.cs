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

    private const float ThrustMagnitude = 250f;
    private const float ThrustMagnitudeOnFlatCoefficient = 1f;
    private const float ThrustMagnitudeOnSlopeCoefficient = 0.4f;
    private const float ThrustMagnitudeInAirCoefficient = 0.4f;
    private const float ThrustMagnitudeCrouchedCoefficient = 0.05f;

    //Twitch
    private const float MaxTwitchSpeed = 20f; //actual twitch speed will be about half of this since twitch acceleration decays towards 0 the closer it gets to this value
    private const float MaxTwitchSpeedInAir = 7.85f;

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

    //Drag
    private const float Drag = 20f;
    private const float DragOnFlatCoefficient = 1f;
    private const float DragOnSlopeCoefficient = 0.01f;
    private const float DragInAirCoefficient = 0f; //0.01f;
    private const float DragCrouchedCoefficient = 0.05f;

    //Crouch
    private bool InputCrouch = false;
    private bool IsCrouched = false;

    //Jump
    private bool InputJump = false;
    private const float JumpVSpeed = 10f;

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
            alignmentFactor = Mathf.Max(0f, MaxTwitchSpeed - thrustDirection.Dot(LinearVelocity)) / MaxTwitchSpeed;
        }
        else
        {
            alignmentFactor = Mathf.Max(0f, MaxTwitchSpeedInAir - thrustDirection.Dot(LinearVelocity)) / MaxTwitchSpeedInAir;
        }
        thrustMagnitude *= alignmentFactor;
        Statistics.Statistic6.Text = $"Alignment: {alignmentFactor}";

        //Jerk term
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
        Statistics.Statistic6.Text += $", thrustMagnitude: {thrustMagnitude}";
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
            LinearVelocity += thrustVector;
        }
        Statistics.Statistic6.Text += $", drag: {drag}";

        //JUMP
        if (InputJump && SurfaceOn == Surface.Flat)
        {
            LinearVelocity = new Vector3(
                LinearVelocity.X,
                Mathf.Max(LinearVelocity.Y + JumpVSpeed, JumpVSpeed), //VSpeed will reset if negative, otherwise add to it
                LinearVelocity.Z
            );
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
        if (SurfaceOn == Surface.Air)
        {
            acceleration *= ThrustMagnitudeInAirCoefficient;
        }
        else if (SurfaceOn == Surface.Slope)
        {
            acceleration *= ThrustMagnitudeOnSlopeCoefficient;
        }
        else if (SurfaceOn == Surface.Flat)
        {
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
        if (SurfaceOn == Surface.Air)
        {
            drag *= DragInAirCoefficient;
        }
        else if (SurfaceOn == Surface.Slope)
        {
            drag *= DragOnSlopeCoefficient;
        }
        else if (SurfaceOn == Surface.Flat)
        {
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