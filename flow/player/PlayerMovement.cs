using Godot;
using System;

public partial class PlayerMovement : CharacterBody3D
{
    [Export] public CameraPlayer CameraPlayer;
    [Export] private Player Player;
    [Export] private CsgBox3D TestVectorBox;

    private float MouseSensitivity = 0.001f;

    private bool IsInAir = false;

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

    private const float ClimbOrWallrunMinSpeed = 1f; //Minimum speed to be able to climb or wallrun

    private const float ClimbAccelerationGravityCoefficient = 1f; //12.5f; //20f; //6f; //Multiple of gravity. Vertical acceleration applied when climbing
    private const float ClimbPeriod = 2f; //time in seconds you can accelerate upwards on the wall for
    private float ClimbRemaining = 2f; //no touchy :)
    private const float ClimbPenaltyWallJump = 0.5f; //climb time in seconds you lose for wall-bouncing
    private float ClimbReplenishDelay = 0f; //delay in seconds (elapsed when not climbing) until ClimbRemaining can recharge again

    private bool IsClimbRested = false; //no touchy :) Can't climb after jumping off until landing on the ground again

    private const float WallJumpAcceleration = 20f; //instantaneous vertical acceleration

    private bool IsWallRunning = false;
    private float WallRunAccelerationCoefficient = 1f; //horizontal acceleration multiplier applied when wall-running
    private const float ClimbCoefficientWallRunVerticalAcceleration = 0.25f; //1.5f; //Multiple of gravity, proportional to climb remaining. Vertical acceleration applied when wall-running
    private const float WallDragCoefficient = 0.8f;


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

        //Audio
        //Landing
        if (IsInAir && IsOnFloor())
        {
            //Play landing sound
            Player.Foley.AudioLand.Play();

            IsInAir = false;
        }
        IsInAir = !IsOnFloor();

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
        //Gravity
        Vector3 accelerationVector = GetGravity();

        //Wall movement
        Vector3 moveOnWallVector = MoveOnWall(delta);
        accelerationVector += moveOnWallVector;

        //Running
        Vector3 moveRunVector = Run(delta);
        if (
               moveOnWallVector == Vector3.Zero                            //no wish direction on wall
            || moveOnWallVector.Normalized() == moveRunVector.Normalized() //same direction
        )
        {
            accelerationVector += moveRunVector;
        }

        ApplyAccelerationAndDragOverTime(accelerationVector, delta);

        //APPLY
        MoveAndSlide();
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
        if (IsOnFloor())
        {
            //Ground
            float slidingCoefficient = 1f;
            if (IsSliding)
            {
                slidingCoefficient = RunDragSlidingCoefficient;
            }

            dragComponent = RunDragGround * slidingCoefficient;
        }
        else if (IsWallRunning)
        {
            //Wall-running
            dragComponent = RunDragGround * WallDragCoefficient;
        }
        else
        {
            //Air
            dragComponent = RunDragGround * RunDragAirCoefficient;
        }

        return dragComponent;
    }

    private Vector3 Run(float delta)
    {
        //Run Direction
        Vector3 wishDirection = GetWishDirection(GlobalBasis);

        //--
        //Audio
        //Set time period between footsteps
        float runAudioTimerPeriod;
        Vector3 velocityHorizontal = new(Velocity.X, 0f, Velocity.Z);
        if (velocityHorizontal.Length() != 0f) //Don't divide by 0
        {
            //Footsteps period proportional to hspeed
            runAudioTimerPeriod = 2.5f / velocityHorizontal.Length();
        }
        else
        {
            runAudioTimerPeriod = float.MaxValue;
        }

        //Tick timer
        //Don't wait forever to play the next footstep sound if we start running faster than before
        Player.Foley.RunAudioTimer = Mathf.Min(Player.Foley.RunAudioTimer, runAudioTimerPeriod);
        //Decrement
        Player.Foley.RunAudioTimer = Mathf.Max(Player.Foley.RunAudioTimer - delta, 0f);

        //GD.Print($"Timer: {Foley.RunAudioTimer}, period: {runAudioTimerPeriod}");

        //Play the sound if we're actually running and not sliding or something
        if (
            Player.Foley.RunAudioTimer == 0f
            && (
                (IsOnFloor() && wishDirection.Normalized().Length() == 1) || IsWallRunning
            )
            && (
                !IsSliding || (
                    IsSliding && Velocity.Length() <= 8f
                )
            )
        )
        {
            Player.Foley.AudioFootstep.Play();
            Player.Foley.RunAudioTimer = runAudioTimerPeriod;
        }
        //--

        float jerkCoefficient = (IsOnFloor() || IsOnWall()) ? RunJerkMagnitude : 1f;
        return GetVectorAlignedJerked(delta, wishDirection, RunAcceleration, jerkCoefficient);
        //ApplyAccelerationOverTime(GetVectorAlignedJerked(delta, wishDirection), delta);
    }

    private Vector3 GetVectorAlignedJerked(float delta, Vector3 wishDirection, float runDynamicAccelerationTwitch, float jerkCoefficient)
    {
        //ALIGNMENT
        //This prevents accelerating past a max speed in the input direction
        //(a prevalent problem when accelerating in air)
        //while allowing us to maintain responsive air acceleration

        //+ if aligned, - if opposite, 0 if perpendicular
        Vector3 velocityHorizontal = new(Velocity.X, 0f, Velocity.Z);
        Player.Statistics.LabelHSpeed.Text = $"HSpeed: {velocityHorizontal.Length():F2}";
        float runAlignment = velocityHorizontal.Dot(wishDirection);

        //Gradient value from 0 to 1, with:
        // * 0 if aligned and at max speed,
        // * 1 if not aligned,
        // * and a value between if not yet at max speed in that direction
        float runDynamicMaxSpeed = RunMaxSpeedGround;
        if (!IsOnFloor() && !IsOnWall())
        {
            runDynamicMaxSpeed = RunMaxSpeedAir;
        }
        float runAlignmentScaled = Mathf.Clamp(1f - runAlignment / runDynamicMaxSpeed, 0f, 1f);

        //TWITCH ACCELERATION
        if (InputTechCrouchOrSlide)
        {
            //Different acceleration when crouched/sliding
            runDynamicAccelerationTwitch *= RunAccelerationSlidingCoefficient;
        }
        else if (!IsOnFloor() && !IsOnWall())
        {
            //Ground vs Air Acceleration
            runDynamicAccelerationTwitch *= RunAccelerationAirCoefficient;
        }

        //JERK
        //Develop
        //Value from 0.5 to 1 depending on how aligned our running is with our current hVelocity
        float jerkAlignment = Mathf.Clamp(runAlignment / (runDynamicMaxSpeed / 2f), 0f, 1f);
        if (!IsSliding && wishDirection.Normalized().Length() == 1)
        {
            //Develop jerk - increase acceleration (i.e. make this jerk rather than simply accelerate)
            RunJerkDevelopment = Mathf.Min((RunJerkDevelopment + (delta * RunJerkDevelopmentRate)) * jerkAlignment, RunJerkDevelopmentPeriod);
        }
        else
        {
            float decayRate = RunJerkDevelopmentDecayRateAir;
            if (IsOnFloor() || IsOnWall())
            {
                decayRate = RunJerkDevelopmentDecayRate;
            }

            //Decrement
            RunJerkDevelopment = Mathf.Max(
                RunJerkDevelopment - (decayRate * delta),
                1f
            );
        }

        //Apply development
        float jerk = (RunJerkDevelopment / RunJerkDevelopmentPeriod) * jerkCoefficient;
        Player.Statistics.LabelJerk.Text = $"Jerk: {jerk:F2}";

        //COMBINE
        float runMagnitude = (runDynamicAccelerationTwitch * runAlignmentScaled) + jerk;
        Vector3 runVector = wishDirection * runMagnitude;

        return runVector;
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
            //ApplyAcceleration(dashDirection * dashMagnitude, delta);
            Velocity += dashDirection * DashAcceleration;
            //Velocity += dashDirection * dashMagnitude;

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

    private Vector3 MoveOnWall(float delta)
    {
        Vector3 finalMoveOnWallVector = Vector3.Zero;

        //Get the camera-forward direction
        Vector3 wishDirection = GetWishDirection(CameraPlayer.GlobalTransform.Basis);

        if (IsOnWall() && (InputRunForward || InputRunLeft || InputRunRight || InputRunBack))
        {
            //Only allow climbing or wall running if:
            //IsOnWall() && InputRunForward && ClimbRemaining > 0f && IsClimbRested;

            //Are we trying to move along the wall?
            Vector3 wallNormal = GetWallNormal();
            float wallDot = wishDirection.Dot(wallNormal);
            if (wallDot <= 0f) {
                //(Relative to wall) Remove the component that points into the wall - this gives us a vector along the wall that is 0 if we look straight at the wall
                Vector3 lookDirectionAlongWall = wishDirection - (wishDirection.Dot(wallNormal) * wallNormal);

                //For wallrunning AND climbing
                //Isolate horizontal component
                Vector3 horizontalDirection = lookDirectionAlongWall - (lookDirectionAlongWall.Dot(Vector3.Up) * Vector3.Up);

                //For climbing ONLY
                //Get the vertical direction along the wall - if the wall is slanted, this points up along the slant
                Vector3 wallTangent = wallNormal.Cross(Vector3.Up);
                Vector3 verticalDirectionAlongWall = wallTangent.Cross(wallNormal).Normalized();

                //Combine these two components based on whether looking sideways along the wall
                Vector3 direction;
                float acceleration;
                float jerkCoefficient;
                if (horizontalDirection.Dot(-CameraPlayer.GlobalTransform.Basis.Z) > 0.75f)
                {
                    //Wallrunning
                    IsWallRunning = true;
                    IsClimbing = false;

                    direction = horizontalDirection.Normalized();
                    acceleration = RunAcceleration * WallRunAccelerationCoefficient;
                    jerkCoefficient = RunJerkMagnitude;
                }
                else
                {
                    //Climbing
                    IsClimbing = true;
                    IsWallRunning = false;

                    direction = (horizontalDirection + verticalDirectionAlongWall).Normalized();
                    jerkCoefficient = 1f;
                    acceleration = ClimbAccelerationGravityCoefficient * GetGravity().Length();
                }

                //Move along the wall
                TestVectorBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + direction.Normalized() * 2.0f;
                finalMoveOnWallVector = GetVectorAlignedJerked(delta, direction, acceleration, jerkCoefficient);

                //Wall jump
                //Jump(verticalDirectionAlongWall, JumpAcceleration);
            }



            ////Climb direction
            //Vector3 climbDirection = Vector3.Zero;
            //if (InputRunForward) climbDirection -= GlobalBasis.Z;
            //if (InputRunLeft) climbDirection -= GlobalBasis.X;
            //if (InputRunRight) climbDirection += GlobalBasis.X;
            //if (InputRunBack) climbDirection += GlobalBasis.Z;
            //climbDirection = climbDirection.Normalized();
            //
            //climbDirection = CameraPlayer.GlobalRotation;
            //
            ////TestVectorBox.GlobalPosition = GlobalPosition + (climbDirection * 2f) + (Vector3.Up * 2f);





            //THIS WORKS but not how we'd like it to, ultimately
            //This basically rotates your acceleration as if the wall normal is the new ground
            ////Rotation from (world up) to (the vector of the wall normal)
            //var rotation = new Quaternion(Vector3.Up, GetWallNormal());
            //
            ////Apply the rotation to runVector
            //Vector3 newRunVector;
            //if (CameraPlayer.Rotation.X < 0f)
            //{
            //    //If we're looking down, then forward should go down (inverted)
            //    newRunVector = runVector * rotation; //Quaternions are not commutative - this is the inverse
            //}
            //else
            //{
            //    //If we're looking up, then forward should go up
            //    newRunVector = rotation * runVector;
            //}
            //
            ////Accelerate along the wall
            //ApplyAccelerationOverTime(newRunVector, delta);






            //Vector3 wallNormal = GetWallNormal();
            //
            //Vector3 projectedVector = runVector - runVector.Dot(wallNormal) * wallNormal;
            //
            //
            ////Vector3 projectedRunVector = runVector - runVector.Project(GetWallNormal());
            //
            ////Vector3 climbDirection = runVector.Normalized() * new Quaternion(GetWallNormal(), 90f);
            //
            //ApplyAccelerationOverTime(projectedVector, delta);
        }
        else
        {
            IsWallRunning = false;
            IsClimbing = false;

            TestVectorBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + wishDirection * 2.0f;
        }

        return finalMoveOnWallVector;






        //if (!IsOnWall())
        //    return;
        //
        //Vector3 wallNormal = GetWallNormal();
        //
        ////Project velocity
        ////Velocity = Velocity - Velocity.Project(wallNormal);
        //
        ////Base projected acceleration vector
        //Vector3 projectedRunVector = runVector - runVector.Project(wallNormal);
        //
        ////Vertical component
        //float climbEnergyCoefficient = (ClimbRemaining / ClimbPeriod);
        //Vector3 verticalAcceleration = Vector3.Up * (ClimbAcceleration * climbEnergyCoefficient);
        //
        ////Combine and apply
        //Vector3 wallClimbOrRunAcceleration = projectedRunVector + verticalAcceleration;
        //ApplyAccelerationOverTime(wallClimbOrRunAcceleration, delta);











        ////IsOnFloor()
        ////IsOnWall()
        ////InputTechJump
        //
        //bool isJumpRested = JumpFatigueRecencyTimer >= JumpCooldown; //can jump
        //bool isOnWall = !IsOnFloor() && IsOnWall();
        //
        //
        //
        //
        //if (IsOnWall())
        //{
        //    float dotToWall = GlobalBasis.Z.Dot(GetWallNormal());
        //    bool isLookingTowardWall = dotToWall > 0f;
        //
        //    //Wall jump
        //    if (
        //        isLookingTowardWall
        //        && IsClimbRested
        //        && isJumpRested
        //        && InputTechJump
        //    )
        //    {
        //        //Get tired
        //        ClimbRemaining = Mathf.Max(ClimbRemaining - ClimbPenaltyWallJump, 0f);
        //        ClimbReplenishDelay += 2f;
        //
        //        //Jump up and away from the wall
        //        Jump((GetWallNormal() + GetWallNormal() + Vector3.Up).Normalized(), WallJumpAcceleration);
        //    }
        //}
        //
        //
        //
        ////Climbing or wall-running
        //if (IsOnWall() && InputRunForward && !IsSliding && Velocity.Length() > ClimbOrWallrunMinSpeed)
        //{
        //    if (IsClimbRested)
        //    {
        //        if (ClimbRemaining > 0f)
        //        {
        //            float dotToWall = GlobalBasis.Z.Dot(GetWallNormal());
        //            float dotToWallClamped = Mathf.Max(dotToWall, 0f); // 0 to 1, where 1 is facing the wall
        //
        //            //Climb
        //            //Vector3 climbVector = -GetGravity() * (ClimbAcceleration * (ClimbRemaining / ClimbPeriod) * dotToWallClamped);
        //            float climbMagnitude = ClimbAcceleration * (ClimbRemaining / ClimbPeriod);
        //            Vector3 climbVector = runVector.Normalized() * climbMagnitude;
        //            ApplyAccelerationOverTime(climbVector, delta);
        //
        //            //Wall-run
        //            if (!IsOnFloor() && dotToWallClamped < 0.75f)
        //            {
        //                IsWallRunning = true;
        //
        //                //Get direction
        //                Vector3 wallTangent = GetWallNormal().Cross(Vector3.Up); //pretty much all the way there
        //                Vector3 projectedDirection = (wallTangent * runVector.Dot(wallTangent)).Normalized(); //consider which horizontal direction we're going along the wall
        //                //testBox.Position = new Vector3(Position.X, Position.Y + 1f, Position.Z) + (2f * projectedDirection);
        //
        //                //Horizontal acceleration
        //                Vector3 wallRunHorizontalVector = projectedDirection * (WallRunAcceleration * (1f - dotToWallClamped));
        //                ApplyAccelerationOverTime(wallRunHorizontalVector, delta);
        //
        //                //Vertical acceleration
        //                Vector3 wallRunVerticalVector = -GetGravity() * (ClimbAcceleration * ClimbCoefficientWallRunVerticalAcceleration * (ClimbRemaining / ClimbPeriod) * dotToWallClamped);
        //                ApplyAccelerationOverTime(wallRunVerticalVector, delta);
        //            }
        //
        //            //Decrement
        //            ClimbRemaining = Mathf.Max(ClimbRemaining - delta, 0f);
        //        }
        //        else
        //        {
        //            IsClimbRested = false;
        //        }
        //    }
        //}
        //else
        //{
        //    //Reset IsWallRunning boolean
        //    IsWallRunning = false;
        //
        //    if (IsOnFloor() || (IsClimbRested && ClimbReplenishDelay <= 0f))
        //    {
        //        //Replenish climb
        //        ClimbRemaining = Mathf.Min(ClimbRemaining + delta, ClimbPeriod);
        //    }
        //    else
        //    {
        //        ClimbReplenishDelay = Mathf.Max(ClimbReplenishDelay - delta, 0f);
        //    }
        //}
        //
        ////Wall-running camera roll
        //if (IsWallRunning)
        //{
        //    if (IsOnWall())
        //    {
        //        //Set roll
        //        CameraPlayer.Rotation = new Vector3(
        //            CameraPlayer.Rotation.X,
        //            CameraPlayer.Rotation.Y,
        //            Mathf.LerpAngle(
        //                CameraPlayer.Rotation.Z,
        //                Mathf.Tau / 16f * Mathf.Sign(GetWallNormal().Dot(-GlobalBasis.X)), //roll rotation
        //                10f * delta //interpolate speed
        //            )
        //        );
        //    }
        //}
        //else
        //{
        //    //Reset roll
        //    CameraPlayer.Rotation = new Vector3(
        //        CameraPlayer.Rotation.X,
        //        CameraPlayer.Rotation.Y,
        //        Mathf.LerpAngle(
        //            CameraPlayer.Rotation.Z,
        //            0f, //roll rotation
        //            10f * delta //interpolate speed
        //        )
        //    );
        //}
        //
        ////Label
        //Player.Statistics.LabelClimb.Text = $"Climb: {ClimbRemaining:F2}, CanClimb: {IsClimbRested}";
        ////Statistics.RectClimb.Scale = new(ClimbRemaining / ClimbPeriod, 1f);
        //
        ////Audio Climb - repeat one-shots
        //if (IsClimbingOrWallRunning && !IsWallRunning && !IsSliding)
        //{
        //    if (!Player.Foley.AudioClimb.Playing)
        //    {
        //        Player.Foley.AudioClimb.Play();
        //    }
        //}
        //else
        //{
        //    if (Player.Foley.AudioClimb.Playing)
        //    {
        //        Player.Foley.AudioClimb.Stop();
        //    }
        //}
        ////Audio Wall-run - loop fader
        //if (IsWallRunning)
        //{
        //    if (!Player.Foley.AudioWallrun.Playing)
        //    {
        //        Player.Foley.AudioWallrun.Play();
        //    }
        //
        //    Player.Foley.AudioWallrun.VolumeDb = Player.Foley.AudioWallrunVolume;
        //}
        //else
        //{
        //    Player.Foley.AudioWallrun.VolumeDb -= Player.Foley.AudioWallrunVolumeFadeOutRate * delta;
        //}
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
        //Statistics.RectJumpFatigueRecency.Scale = new(JumpFatigueRecencyTimer / JumpFatigueRecencyTimerPeriod, 1f);

        Player.Statistics.LabelJumpFatigueOnGround.Text = $"Jump fatigue on-ground: {JumpFatigueOnGroundTimer:F2}";
        //Statistics.RectJumpFatigueOnGround.Scale = new(JumpFatigueOnGroundTimer / JumpFatigueOnGroundTimerPeriod, 1f);
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