using Godot;
using System;
using static Godot.TextServer;

public partial class PlayerMovementAddSurfaceGeneralization : CharacterBody3D
{
    [Export] public CameraPlayer CameraPlayer;
    [Export] private Player Player;
    [Export] private CsgBox3D TestVectorBox;

    public float MouseSensitivity = 0.001f;

    //Gravity
    private const float GravitySlidingAccelerationResistance = -6f; //maximum sliding acceleration from gravity that the player can resist

    //Momentum
    private Vector3 velocityPrevious = Vector3.Zero;
    private const float VelocityChangeSoundTriggerThreshold = 1f;

    //RUN
    private bool InputRunForward = false;
    private bool InputRunLeft = false;
    private bool InputRunRight = false;
    private bool InputRunBack = false;

    private const float RunAcceleration = 250f; //allows for fast direction change
    private const float RunAccelerationAirCoefficient = 0.4f; //reduces control while in-air

    private const float RunDragGround = 20f; //8f; //2.890365f; //1040f; //2.890365f; //20f; //higher values are higher drag. Takes any positive value
    private const float RunDragAirCoefficient = 0.01f; //0.05f; //72000f; //200f; //0.05f //higher values are higher drag. Takes any positive value

    private const float RunMaxSpeedGround = 10f; //run acceleration reduces as top speed is approached
    private const float RunMaxSpeedAir = 5f; //lower top speed in air to keep air movements strictly for direction change rather than to build speed

    //Jerk allows running acceleration to increase slowly over a few seconds - only applies on-ground
    private const float RunJerkMagnitude = 200f; //the maximum acceleration that jerk imparts on the player once fully developed

    private float RunJerkDevelopment = 0f; //no touchy :) develops from 0 up to the value of RunJerkDevelopmentPeriod and is used as the coefficient of RunJerkMagnitude
    private const float RunJerkDevelopmentRate = 2f; //How quickly jerk increases; i.e. jerk amount; i.e. how quickly acceleration increases
    private const float RunJerkDevelopmentPeriod = 2f; //time in seconds that jerk takes to fully develop

    private const float RunJerkDevelopmentDecayRate = 16f; //How many times faster jerk decreases rather than increases - jerk decay is exponential
    private const float RunJerkDevelopmentDecayRateAir = 4f; //How many times faster jerk decreases rather than increases - jerk decay is exponential

    private const float RunJerkMagnitudeSlideDump = 0.2f; //How much acceleration is dumped from RunJerkDevelopment the instant the player begins a slide

    //CLIMB/WALL-JUMPING/WALL-RUNNING
    private bool IsClimbing = false;

    private const float ClimbVerticalAccelerationCoefficient = 1.33f; //12.5f; //20f; //6f; //Multiple of gravity. Vertical acceleration applied when climbing
    private const float WallMovementPeriod = 4f; //maximum time in seconds you can theoretically perform wall movement for.
                                                 //Note that you'll be kicked off the wall at around 1/3rd of this value, because the acceleration reduces as exhaustion increases
    private float WallMovementRemaining = 0f; //no touchy :)
    private const float ClimbPenaltyWallJump = 0.5f; //climb time in seconds you lose for wall-bouncing
    private float ClimbReplenishDelay = 0f; //delay in seconds (elapsed when not climbing) until ClimbRemaining can recharge again
    private const float WallMovementRestCoefficient = 10f; //how many times faster climb replenishes than tires

    private bool IsClimbRested = false; //no touchy :) Can't climb after jumping off until landing on the ground again

    private const float WallJumpAcceleration = 20f; //instantaneous vertical acceleration

    private bool IsWallRunning = false;
    private float WallRunAccelerationCoefficient = 1f; //horizontal acceleration multiplier applied when wall-running
    private const float WallDragCoefficient = 1f;

    private const float WallRunVerticalAccelerationCoefficient = 1.25f; //1.5f; //Multiple of gravity, proportional to climb remaining. Vertical acceleration applied when wall-running
    
    //JUMP
    private bool InputTechJump = false;
    private const float JumpAcceleration = 10f; //instantaneous vertical acceleration

    private const float JumpCooldown = 0.2f; //the minimum time in seconds that must pass before the player can jump again (we compare this to JumpFatigueRecencyTimer)

    private float JumpFatigueOnGroundTimer = 0f; //no touchy :) halved immediately after jumping, counts up to JumpFatigueOnGroundTimerPeriod
    private const float JumpFatigueOnGroundTimerPeriod = 0.5f; //time in seconds on the ground until on-ground jump fatigue goes away entirely

    private float JumpFatigueRecencyTimer = 0f; //no touchy :) 0 immediately after jumping, counts up to JumpFatigueRecentTimerPeriod
    private const float JumpFatigueRecencyTimerPeriod = 0.5f; //time in seconds after a jump until recency jump fatigue goes away entirely

    private const float JumpFatigueMinimumCoefficient = 0.08f; //the minimum coefficient that jump acceleration can be multiplied by (applies if jump fatigue is extreme)

    //CROUCH/SLIDE
    [Export] private CollisionShape3D ColliderCapsule;
    [Export] private CollisionShape3D ColliderSphere;

    private bool InputTechCrouchOrSlide = false;
    private bool IsSliding = false;
    private const float RunAccelerationSlidingCoefficient = 0.075f; //larger values are higher acceleration

    private const float RunDragSlidingCoefficient = 0.05f; //7200f; //20f; //0.05f; ////higher values are higher drag. Also affects slide-jump speed. Takes any positive value.

    private float CameraYTarget = 1.5f; //no touchy :) Target camera y position
    private float CameraY = 1.5f; //no touchy :) Current camera y position
    private const float CameraYAnimationDuration = 25f; //rate that the camera moves towards the target y position, proportional to the distance

    //DASH
    private bool InputTechDash = false;
    private const float DashAcceleration = 20f; //300f; //dash acceleration magnitude
    private float DashCooldown = 0f; //no touchy :)
    private const float DashCooldownPeriod = 5f; //time in seconds until you can use the tech again

    private float DashFadeSpeed = 5f; //How fast it fades in/out
    private float DashOpacity = 0f; //Start fully transparent

    public override void _Input(InputEvent @event)
    {
        //Run Direction
        InputRunForward = Input.IsActionPressed("move_run_dir_forward");
        InputRunLeft = Input.IsActionPressed("move_run_dir_left");
        InputRunRight = Input.IsActionPressed("move_run_dir_right");
        InputRunBack = Input.IsActionPressed("move_run_dir_back");

        //Tech
        InputTechJump = Input.IsActionPressed("move_jump");
        InputTechCrouchOrSlide = Input.IsActionPressed("move_crouch");
        InputTechDash = Input.IsActionJustPressed("move_dash");

        //Look
        if (Player.IsAlive && @event is InputEventMouseMotion mouseMotion)
        {
            //Yaw
            Rotation = new Vector3(
                Rotation.X,
                Rotation.Y - mouseMotion.Relative.X * MouseSensitivity,
                Rotation.Z
            );

            //Pitch, clamp to straight up or down
            CameraPlayer.Rotation = new Vector3(
                Mathf.Clamp(CameraPlayer.Rotation.X - mouseMotion.Relative.Y * MouseSensitivity,
                    -0.25f * Mathf.Tau,
                    0.25f * Mathf.Tau
                ),
                CameraPlayer.Rotation.Y,
                CameraPlayer.Rotation.Z
            );
        }
    }

    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //Slide
        if (InputTechCrouchOrSlide || !Player.IsAlive)
        {
            if (!IsSliding)
            {
                //Sliding

                //Instantaneous subtraction from jerk
                RunJerkDevelopment = Mathf.Max(0f, RunJerkDevelopment - RunJerkMagnitudeSlideDump);

                //Switch colliders
                ColliderCapsule.Disabled = true;
                ColliderSphere.Disabled = false;

                //Set camera to new height
                CameraYTarget = 0.5f;

                //Play audio
                if (IsOnFloor())
                {
                    Player.Foley.AudioSlide.VolumeDb = Player.Foley.AudioSlideVolume - ((1f - Mathf.Min(RunMaxSpeedGround, Velocity.Length()) / RunMaxSpeedGround) * 80f);
                    Player.Foley.AudioSlide.Play();
                }

                //Update bool
                IsSliding = true;
            }
        }
        else if (IsSliding)
        {
            //Standing
            //Switch colliders
            ColliderCapsule.Disabled = false;
            ColliderSphere.Disabled = true;

            //Set camera to new height
            CameraYTarget = 1.5f;

            //Update bool
            IsSliding = false;
        }

        //Audio fade out
        if (!IsSliding)
        {
            //Stop audio
            Player.Foley.AudioSlide.VolumeDb -= Player.Foley.AudioSlideVolumeFadeOutRate * delta;
        }

        //Update camera height
        CameraPlayer.Transform = new Transform3D(CameraPlayer.Basis, new Vector3(0f, CameraY, 0f));
        CameraY += (CameraYTarget - CameraY) * CameraYAnimationDuration * delta;

        //ACCELERATION IMPULSE
        ProcessDash(delta);
        ProcessJumpFromGround(delta);

        //ACCELERATION/TIME
        ApplyAccelerationAndDragOverTime(ProcessMovementAndGetVector(delta) + ProcessGravityAndGetVector(delta), delta);
        Player.Statistics.LabelHSpeed.Text = $"HSpeed: {new Vector3(Velocity.X, 0f, Velocity.Z).Length():F2} ({Velocity.X:F2}, {Velocity.Y:F2}, {Velocity.Z:F2})";

        //APPLY
        MoveAndSlide();

        //Audio
        //Landing
        if (
            (IsOnFloor() || IsOnWall() || IsOnCeiling())
            && (velocityPrevious - Velocity).Length() >= VelocityChangeSoundTriggerThreshold
        )
        {
            //Play landing sound
            Player.Foley.AudioLand.Play();
        }
        velocityPrevious = Velocity;
    }

    private void ApplyAccelerationAndDragOverTime(Vector3 acceleration, float delta)
    {
        //DON'T MULTIPLY BY DELTA IN THE ACCELERATION ARGUMENT OF THIS METHOD!
        //Correct example usage:
        //float magnitude = 10f;
        //Vector3 direction = -GlobalBasis.Z;
        //ApplyAcceleration(magnitude * direction, delta);

        //This actually uses a force formula, but we assume the mass is 1, thus it ends up applying acceleration, and the vector is titled acceleration
        //F = ma
        //F = (1)a
        //F = a

        //Don't accelerate if dead
        if (!Player.IsAlive)
        {
            acceleration = Vector3.Zero;
        }

        float dragComponent = GetDrag();

        //Apply drag friction with an exponential decay expression to account for users with throttled physics update rates
        float decayFactor = Mathf.Exp(-dragComponent * delta);
        Velocity = acceleration / dragComponent * (1f - decayFactor) + (Velocity * decayFactor);
    }

    private float GetDrag()
    {
        //Dynamic drag amount
        float dragComponent;
        if (IsOnFloor() || IsOnWall() || IsOnCeiling())
        {
            //Ground
            if (IsSliding)
            {
                dragComponent = RunDragGround * RunDragSlidingCoefficient;
            }
            else
            {
                dragComponent = RunDragGround;
            }
        }
        else
        {
            //Air or climbing
            dragComponent = RunDragGround * RunDragAirCoefficient;
        }

        return dragComponent;
    }

    private Vector3 ProcessGravityAndGetVector(float delta)
    {
        bool isTesting = true;

        Vector3 gravityVector = GetGravity();

        if (IsOnFloor() || IsOnWall())
        {
            //Get the floor normal (may be slanted)
            Vector3 surfaceNormal = IsOnFloor() ? GetFloorNormal(): GetWallNormal();

            //Calculate direction along the floor
            gravityVector -= surfaceNormal * gravityVector.Dot(surfaceNormal);

            //Allow the player to resist weak sliding forces
            if (gravityVector.Y > GravitySlidingAccelerationResistance) //down is -Y
            {
                if (IsSliding)
                {
                    if (InputRunForward || InputRunLeft || InputRunRight || InputRunBack)
                    {
                        //Crouch-walking
                        gravityVector = Vector3.Zero;
                    }
                }
                else
                {
                    //Standing
                    gravityVector = Vector3.Zero;
                }
            }
        }

        //Testing
        if (isTesting) TestVectorBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + gravityVector.Normalized() * 2.0f;

        return gravityVector;
    }

    private void ProcessDash(float delta)
    {
        //Act
        if (InputTechDash && DashCooldown == 0f && !IsOnFloor() && !IsOnWall())
        {
            //Direction
            Vector3 wishDirection = GetWishDirection(GlobalBasis);
            Vector3 dashDirection = wishDirection.Length() == 0f ? -GlobalBasis.Z : wishDirection;

            //Add vector to velocity
            Velocity += dashDirection * DashAcceleration;

            //Reset cooldown
            DashCooldown = DashCooldownPeriod;

            //Play sound
            Player.Foley.AudioDash.Play();
        }

        //Decrement
        DashCooldown = Mathf.Max(DashCooldown - delta, 0f);

        //Label
        Player.Statistics.LabelDash.Text = $"Dash: {DashCooldown:F2}";

        //Ability UI
        Player.HUD.RectDash.Scale = new(DashCooldown / DashCooldownPeriod, 1f);
        Player.HUD.RectDashCooldown.Scale = new(1f, DashCooldown / DashCooldownPeriod);

        //Shader
        float to = 0f;
        if (DashCooldown >= DashCooldownPeriod - (DashCooldownPeriod / 16f))
        {
            to = 1f;
        }
        DashOpacity = Mathf.Lerp(DashOpacity, to, delta * DashFadeSpeed);
        Player.ScreenEffects.DashMaterial.Set("shader_parameter/opacity", DashOpacity);

        float startLinePosition = 0.6f;
        float dashLinesMovement = startLinePosition + ((1f - startLinePosition) - ((DashCooldown / DashCooldownPeriod) * (1f - startLinePosition)));
        Player.ScreenEffects.DashMaterial.Set("shader_parameter/movement", dashLinesMovement);
    }

    private Vector3 GetWishDirection(Basis basis)
    {
        Vector3 wishDirection = Vector3.Zero;
        if (InputRunForward) wishDirection -= basis.Z;
        if (InputRunLeft) wishDirection -= basis.X;
        if (InputRunRight) wishDirection += basis.X;
        if (InputRunBack) wishDirection += basis.Z;
        return wishDirection.Normalized();
    }

    private Vector3 ProcessMovementAndGetVector(float delta)
    {
        //Testing
        bool isTesting = true;

        //Get the camera-forward direction
        Vector3 wishDirection = GetWishDirection(CameraPlayer.GlobalBasis);

        //Default values
        IsWallRunning = false;
        IsClimbing = false;
        if (isTesting) TestVectorBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + wishDirection * 2.0f;

        //Run!
        return wishDirection * RunAcceleration;
    }

    private void ProcessWallJump(Vector3 wallNormal, Vector3 verticalDirectionAlongWall)
    {
        //Wall jump
        if (
            InputTechJump
            && JumpFatigueRecencyTimer >= JumpCooldown
        )
        {
            //Jump up and away from the wall
            Jump((wallNormal + wallNormal + verticalDirectionAlongWall).Normalized(), WallJumpAcceleration);

            //Reset timers
            JumpFatigueOnGroundTimer = Mathf.Max(JumpFatigueOnGroundTimer / 2f, JumpFatigueMinimumCoefficient);
            JumpFatigueRecencyTimer = 0f;

            //Tire
            WallMovementRemaining = Mathf.Max(WallMovementRemaining - ClimbPenaltyWallJump, 0f);
        }
    }

    private void ProcessJumpFromGround(float delta)
    {
        //Increment recency timer
        JumpFatigueRecencyTimer = Mathf.Min(JumpFatigueRecencyTimer + delta, JumpFatigueRecencyTimerPeriod);

        //Floor jump (different from wall-bounce)
        if (IsOnFloor())
        {
            //Increment on-ground timer
            JumpFatigueOnGroundTimer = Mathf.Min(JumpFatigueOnGroundTimer + delta, JumpFatigueOnGroundTimerPeriod);

            //Act
            if (InputTechJump && JumpFatigueRecencyTimer >= JumpCooldown)
            {
                //Jump upwards
                Jump(Vector3.Up, JumpAcceleration);

                //Reset timers
                JumpFatigueOnGroundTimer = Mathf.Max(JumpFatigueOnGroundTimer / 2f, JumpFatigueMinimumCoefficient);
                JumpFatigueRecencyTimer = 0f;
            }
        }

        //Label
        Player.Statistics.LabelJumpFatigueRecency.Text = $"Jump fatigue recency: {JumpFatigueRecencyTimer:F2}";
        Player.Statistics.LabelJumpFatigueOnGround.Text = $"Jump fatigue on-ground: {JumpFatigueOnGroundTimer:F2}";
    }

    private void Jump(Vector3 direction, float magnitude)
    {
        if (Player.IsAlive)
        {
            //Determine fatigue
            float fatigue = Mathf.Max(
                JumpFatigueMinimumCoefficient,
                Mathf.Min(
                    JumpFatigueRecencyTimer / JumpFatigueRecencyTimerPeriod,     //recency jump fatigue
                    JumpFatigueOnGroundTimer / JumpFatigueOnGroundTimerPeriod    //on-ground jump fatigue
                )
            );

            //Act
            Velocity += direction * (magnitude * fatigue);

            //Sound
            Player.Foley.AudioJump.Play();
        }
    }
}