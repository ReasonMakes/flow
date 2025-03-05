using Godot;
using static Godot.WebSocketPeer;

public partial class PlayerMovement : RigidBody3D
{
    //Diagnostics
    [Export] private Label Statistic2;
    [Export] private Label Statistic3;
    [Export] private Label Statistic4;
    [Export] private Label Statistic5;
    [Export] private Label Statistic6;
    [Export] private Label Statistic7;
    [Export] private Label Statistic8;
    [Export] private Label Statistic9;
    [Export] private Label Statistic10;
    [Export] private Label Statistic11;
    [Export] private Label Statistic12;
    [Export] private Label Statistic13;
    [Export] private Label Statistic14;
    [Export] private Label Statistic15;
    [Export] private CsgBox3D TestBox;

    //Look
    [Export] public CameraPlayer CameraPlayer;
    public float MouseSensitivity = 0.001f;

    //Thrust
    private bool InputForward = false;
    private bool InputLeft = false;
    private bool InputRight = false;
    private bool InputBack = false;

    public float ThrustForce = 5e4f; //1000f; //public variable because slider attached

    private float ThrustForceTiredWallClimbCoefficient = 1f; //this is dynamically modified when tired-climbing

    private const float ThrustMaxSpeedOnFlat = 10f;
    private const float ThrustMaxSpeedInAir = 5f;

    //Twitch?
    private const float ThrustAcceleration = 250f; //2.5e4f; //250f;
    private const float ThrustAccelerationInAirCoefficient = 0.4f;
    private const float ThrustAccelerationSlidingCoefficient = 0.075f;

    //Jerk
    private float Jerk = 0f;
    private const float JerkMagnitude = 200f;
    private const float JerkPeriod = 2f;
    private const float JerkRate = 2f;
    private const float JerkDecayRate = 16f;
    private const float JerkDecayRateInAir = 4f;

    //Drag
    private const float DragOnFlat = 1e4f; //20f;
    private const float DragInAirCoefficient = 0.01f;
    private const float DragCrouchedCoefficient = 0.05f;
    private const float DragWallrunningCoefficient = 1f;

    //Wall/ceiling thrusting
    private const float SlopeDotUp = 0.70710678118f; //What angle is a flat surface
                                                     //LESS THAN this values is a slope and engages climbing/wallrunning
                                                     //This is the dot product of the normal of the surface to global up
                                                     //-1 is down, 0 is toward the horizon, 1 is up
                                                     //cos(angle) = dot :. angle = cos^-1(dot)
    private bool OnFlat = false;
    private bool OnSlope = false;

    private bool IsWishingIntoSlope = false;
    private bool IsWallRunning = false;
    private Vector3 WallNormal = Vector3.Up;

    private bool IsClimbing = false;

    private float ClimbEnergy = 1f;
    private const float ClimbRestRate = 1f; //multiplied with delta
    private const float ClimbTireRate = 0.1f; //multiplied with delta

    private const float ThrustAccelerationClimbCoefficient = 0.5f;
    private const float ClimbMaxVSpeed = 10f;

    //Crouching/sliding
    private bool InputCrouch = false;
    private bool IsCrouched = false;

    //Jump
    private bool InputJump = false;
    private const float JumpForce = 2e3f; //2000f * 360f;

    private float JumpCooldown = 0f;
    private const float JumpCooldownPeriod = 1f; //how long after jumping until you can jump again, in seconds
    private const float JumpNoDragPeriod = 0.2f; //how long after jumping that there is no drag even if on a surface, in seconds
                                                 //this helps to prevent jump height from being dependent on update rate

    private float MaxAchievedHeight = 0f;

    //Dash
    private bool InputDash = false;

    public override void _Input(InputEvent @event)
    {
        //Run Direction
        InputForward = Input.IsActionPressed("thrust_run_dir_forward");
        InputLeft = Input.IsActionPressed("thrust_run_dir_left");
        InputRight = Input.IsActionPressed("thrust_run_dir_right");
        InputBack = Input.IsActionPressed("thrust_run_dir_back");

        InputCrouch = Input.IsActionPressed("crouch");

        InputJump = Input.IsActionPressed("thrust_jump");

        InputDash = Input.IsActionJustPressed("thrust_dash");

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

    //public void UpdateThrustForce(float val)
    //{
    //    ThrustForce = val;
    //}

    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //Unfreeze once game started
        if (Time.GetTicksMsec() > 6000f)
        {
            Freeze = false;
            //GlobalPosition = new Vector3(GlobalPosition.X, 1f, GlobalPosition.Z);
        }

        //Process climb energy
        if (OnFlat)
        {
            ClimbEnergy = Mathf.Min(1f, ClimbEnergy + (delta * ClimbRestRate));
        }
        else if (IsWishingIntoSlope || IsWallRunning)
        {
            ClimbEnergy = Mathf.Max(0f, ClimbEnergy - (delta * ClimbTireRate));
        }

        //JUMP
        if (InputJump && OnFlat && JumpCooldown <= 0f)
        {
            ApplyImpulse(Vector3.Up * JumpForce);
            JumpCooldown = JumpCooldownPeriod;

            //Diagnose
            MaxAchievedHeight = 0f;
        }
        JumpCooldown = Mathf.Max(0f, JumpCooldown -= delta);

        //DIAGNOSTICS
        //Speed
        string testSpeed = LinearVelocity.Length() != 0f && LinearVelocity.Length() < 0.1f ? "<0.1" : $"{LinearVelocity.Length():F2}";
        Statistic2.Text = $"Speed: {testSpeed}";
        float hSpeed = new Vector3(LinearVelocity.X, 0f, LinearVelocity.Z).Length();
        string testHSpeed = hSpeed != 0f && hSpeed < 0.1f ? "<0.1" : $"{hSpeed:F2}";
        Statistic3.Text = $"HSpeed: {testHSpeed}";
        string testVSpeed = LinearVelocity.Y != 0f && Mathf.Abs(LinearVelocity.Y) < 0.1f ? "<+/-0.1" : $"{LinearVelocity.Y:F2}";
        Statistic4.Text = $"VSpeed: {testVSpeed}";
        Statistic5.Text = $"Angular velocity: {AngularVelocity}";

        //Climbing
        Statistic7.Text = $"IsWishingIntoSlope: {IsWishingIntoSlope}";
        Statistic8.Text = $"Climb energy: {ClimbEnergy}";

        //Jumping
        MaxAchievedHeight = Mathf.Max(MaxAchievedHeight, GlobalPosition.Y);
        Statistic11.Text = $"MaxAchievedHeight: {MaxAchievedHeight}";
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float delta = state.Step;

        //Thrust/run
        Vector3 wishDirectionRaw = GetWishDirectionRaw();

        Vector3 thrustDirection = GetDirection(wishDirectionRaw, state);
        Vector3 finalThrustVector = ProcessMovementAndGetVector(thrustDirection, delta);
        ApplyForce(finalThrustVector * (ThrustForce * ThrustForceTiredWallClimbCoefficient * delta));

        //Drag
        if ((OnFlat || OnSlope) && JumpCooldown < JumpCooldownPeriod - JumpNoDragPeriod)
        {
            //Surface drag
            ApplyForce(-LinearVelocity * (Mass * GetDrag() * delta)); //F = ma
        }
        
        //Wallrunning anti-gravity
        if (IsWallRunning) ApplyForce(Mass * -GetGravity()); //F = ma TODO: make this start to weaken only right before climb energy runs out

        //Diagnostics
        TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + thrustDirection * 2.0f;
    }

    private void ApplyAccelerationAndDragOverTime(Vector3 accelerationVector, float delta)
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

        float dragComponent = GetDrag();
        
        //Apply drag friction with an exponential decay expression to account for users with throttled physics update rates
        float decayFactor = Mathf.Exp(-dragComponent * delta);
        LinearVelocity = accelerationVector / dragComponent * (1f - decayFactor) + (LinearVelocity * decayFactor);
    }

    private float GetDrag()
    {
        float drag = DragOnFlat;

        if (OnFlat)
        {
            //Ground
            if (IsCrouched)
            {
                drag *= DragCrouchedCoefficient;
            }
        }
        else if (IsWallRunning)
        {
            //Wallrunning
            drag *= DragWallrunningCoefficient;
        }
        else
        {
            //Air or climbing
            drag *= DragInAirCoefficient;
        }

        return drag;
    }

    private Vector3 ProcessMovementAndGetVector(Vector3 wishDirection, float delta)
    {
        //Default values
        Vector3 finalMoveVector = Vector3.Zero;

        //Do we WANT to do movement?
        if (
            InputForward || InputLeft || InputRight || InputBack
            || InputJump
        )
        {
            //Running on the ground
            finalMoveVector = Run(wishDirection, delta);
        }

        //Wall-running camera roll
        float wallrunCameraRollRate = 30f * delta; //Slerp() increments by this until reaching 1
        float wallrunCameraRollAmount = 1f/4f;
        Vector3 targetVector = IsWallRunning ? Vector3.Up.Slerp(WallNormal, wallrunCameraRollAmount) : Vector3.Up;

        CameraPlayer.CameraGrandparent.GlobalBasis = GetRotationSlerpTowardsVector
        (
            CameraPlayer.CameraGrandparent.GlobalBasis,
            targetVector,
            wallrunCameraRollRate
        );

        return finalMoveVector;
    }

    ////Very fancy lambda expression which isn't human readable AT ALL so sadly deprecating it in favour of the equivalent method below
    //private static Basis GetRotationSlerpTowardsVector(Basis basis, Vector3 target, float rate) =>
    //new(basis.GetRotationQuaternion().Slerp(new Quaternion(Vector3.Up, target), rate));
    private static Basis GetRotationSlerpTowardsVector(Basis subjectBasis, Vector3 targetVector, float rotationRate)
    {
        //Rotation direction example (Vector3): from Vector3.Up towards Vector3 WallNormal
        //Rotation subject example (Basis): CameraPlayer.CameraGrandparent.GlobalBasis
    
        //Rotation towards WallNormal
        Quaternion destinationRotation = new(Vector3.Up, targetVector);
        //Get current rotation
        Quaternion currentRotation = subjectBasis.GetRotationQuaternion();
    
        //Spherical linear interpolation from current rotation towards new rotation
        Quaternion interpolatedRotation = currentRotation.Slerp(destinationRotation, rotationRate);
    
        //Return for application
        return new Basis(interpolatedRotation);
    }

    private Vector3 Run(Vector3 wishDirection, float delta)
    {
        //--
        //Audio
        //Set time period between footsteps
        float runAudioTimerPeriod;
        Vector3 velocityHorizontal = new(LinearVelocity.X, 0f, LinearVelocity.Z);
        if (velocityHorizontal.Length() != 0f) //Don't divide by 0
        {
            //Footsteps period proportional to hspeed
            runAudioTimerPeriod = 2.5f / velocityHorizontal.Length();
        }
        else
        {
            runAudioTimerPeriod = float.MaxValue;
        }

        float jerkCoefficient = OnFlat ? JerkMagnitude : 1f;
        return GetVectorAlignedJerked(delta, wishDirection, ThrustAcceleration, jerkCoefficient);
    }

    private Vector3 GetVectorAlignedJerked(float delta, Vector3 wishDirection, float runDynamicAccelerationTwitch, float jerkCoefficient)
    {
        //ALIGNMENT
        //This prevents accelerating past a max speed in the input direction
        //(a prevalent problem when accelerating in air)
        //while allowing us to maintain responsive air acceleration

        //+ if aligned, - if opposite, 0 if perpendicular
        //float runAlignment = wishDirection.Dot(new(LinearVelocity.X, 0f, LinearVelocity.Z));
        float runAlignment = wishDirection.Dot(new(LinearVelocity.X, 0f, LinearVelocity.Z));

        //Gradient value from 0 to 1, with:
        // * 0 if aligned and at max speed,
        // * 1 if not aligned,
        // * and a value between if not yet at max speed in that direction
        float runDynamicMaxSpeed = ThrustMaxSpeedOnFlat;
        if (!OnFlat && !OnSlope)
        {
            runDynamicMaxSpeed = ThrustMaxSpeedInAir;
        }
        float runAlignmentScaled = Mathf.Clamp(1f - runAlignment / runDynamicMaxSpeed, 0f, 1f);

        Statistic15.Text = $"IsClimbing: {IsClimbing}";
        //TWITCH ACCELERATION
        if (IsCrouched)
        {
            //Different acceleration when crouched/sliding
            runDynamicAccelerationTwitch *= ThrustAccelerationSlidingCoefficient;
        }
        else if (IsWishingIntoSlope && !IsWallRunning)
        {
            //TODO: this needs to be aligned!!
            if (LinearVelocity.Y >= ClimbMaxVSpeed)
            {
                runDynamicAccelerationTwitch = 0f;
            }
            runDynamicAccelerationTwitch *= ThrustAccelerationClimbCoefficient;
        }
        else if (!OnFlat && !OnSlope)
        {
            //Air Acceleration
            runDynamicAccelerationTwitch *= ThrustAccelerationInAirCoefficient;
        }

        //JERK
        //Develop
        //Value from 0.5 to 1 depending on how aligned our running is with our current hVelocity
        float jerkAlignment = Mathf.Clamp(runAlignment / (runDynamicMaxSpeed / 2f), 0f, 1f);
        if (!IsCrouched && wishDirection.Normalized().Length() == 1)
        {
            //Develop jerk - increase acceleration (i.e. make this jerk rather than simply accelerate)
            Jerk = Mathf.Min((Jerk + (delta * JerkRate)) * jerkAlignment, JerkPeriod);
        }
        else
        {
            float decayRate = JerkDecayRateInAir;
            if (OnFlat || OnSlope)
            {
                decayRate = JerkDecayRate;
            }

            //Decrement
            Jerk = Mathf.Max(
                Jerk - (decayRate * delta),
                0f
            );
        }

        //Apply development
        float jerk = (Jerk / JerkPeriod) * jerkCoefficient;

        //COMBINE
        float runMagnitude = (runDynamicAccelerationTwitch * runAlignmentScaled) + jerk;
        Vector3 runVector = wishDirection * runMagnitude;

        return runVector;
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

    private Vector3 GetDirection(Vector3 wishDirectionRaw, PhysicsDirectBodyState3D state)
    {
        //Convert camera look to flat plane
        //this step MUST be skipped if we want to be able to climb straight-up or inverted inclines
        //yet, this step is ABSOLUTELY NECESSARY if we want to prevent staying on walls even when tired
        Vector3 wishDirection = (wishDirectionRaw - (Vector3.Up * wishDirectionRaw.Dot(Vector3.Up))).Normalized();

        //COLLISION DETECTION
        //Types of colliders:
        //- air          [no collider]
        //- slope        [Vector3.Up.Dot(collider) < SlopeDotUp]
        //- flat         [Vector3.Up.Dot(collider) >= SlopeDotUp]

        //Types of collisions:
        //- wishDirection   Wish vector into a collider         [we only use this one in this implementation]
        //- LinearVelocity  Velocity vector into a collider
        //- GetGravity()    Gravity vector into a collider

        //Surface flags
        OnFlat = false;
        bool onSlope = false;

        //Climbing/wallrunning flags
        bool isCurrentCheckAWishIntoASlope = false;
        IsWishingIntoSlope = false;

        //Surface
        float surfaceNormalDotWishSmallest = 1f;
        Vector3 surfaceNormalWishingInto = Vector3.Up;

        //Wallrunning surface (no default/nullable)
        bool isWallNormalAssigned = false;
        float wallNormalDot = 1f;
        Vector3? wallNormal = null;

        //Dynamic thrust force default (attenuated when tired-thrusting into a slope)
        float thrustForce = ThrustForce;

        //Collision detection
        int contactCount = state.GetContactCount();
        if (contactCount > 0)
        {
            for (int i = 0; i < contactCount; i++)
            {
                Vector3 surfaceNormal = state.GetContactLocalNormal(i);

                if (Vector3.Up.Dot(surfaceNormal) < SlopeDotUp)
                {
                    //Contacting a sloped surface
                    onSlope = true;

                    //Ensure this is the collider we're wishing into the most
                    float wishIntoDot = wishDirection.Dot(surfaceNormal);
                    if (wishIntoDot < 0f && wishIntoDot < surfaceNormalDotWishSmallest)
                    {
                        surfaceNormalDotWishSmallest = wishIntoDot;
                        surfaceNormalWishingInto = surfaceNormal;

                        isCurrentCheckAWishIntoASlope = true;
                    }

                    //Wall normal works similarly, but has no default
                    if (!isWallNormalAssigned)
                    {
                        wallNormal = surfaceNormal;
                    }
                    else if (wishIntoDot < 0f && wishIntoDot < wallNormalDot)
                    {
                        wallNormalDot = wishIntoDot;
                        wallNormal = surfaceNormal;
                    }
                }
                else
                {
                    //Contacting a flat surface
                    OnFlat = true;

                    //Ensure this is the collider we're wishing into the most
                    float wishIntoDot = wishDirection.Dot(surfaceNormal);
                    if (wishIntoDot < 0f && wishIntoDot < surfaceNormalDotWishSmallest)
                    {
                        surfaceNormalDotWishSmallest = wishIntoDot;
                        surfaceNormalWishingInto = surfaceNormal;

                        isCurrentCheckAWishIntoASlope = false;
                    }
                }
            }

            //The collider that we're wishing to move into the most (not just at least one collider) is a slope
            if (isCurrentCheckAWishIntoASlope)
            {
                IsWishingIntoSlope = true;
            }
        }

        OnSlope = onSlope;
        Statistic6.Text = $"onFlat: {OnFlat}, onSlope: {onSlope}, surfaceNormalDotWishSmallest: {surfaceNormalDotWishSmallest}, WallNormal: {WallNormal}";

        if (wallNormal.HasValue)
        {
            Statistic6.Text += $", wallNormal: {wallNormal}";
        }


        //RE-DIRECTION ALONG COLLIDER SURFACE TANGENT
        //Permutations:
        //- flat surface tangents [redirect wish tangent to surface]
        //- slope tangents (no up on slopes when tired) [if moving down, redirect wish tangent to surface; if moving up, redirect wish tangent to horizontal of surface]
        //- [maybe: can move away from slopes - or maybe wall jump away only, or maybe this is a non-issue]
        //- Default (!OnFlat && !onSlope): air movement [no redirection]

        //Allow climbing straight-up and inverted surfaces
        if (isCurrentCheckAWishIntoASlope && ClimbEnergy > 0f)
        {
            wishDirection = wishDirectionRaw;
        }

        //Get direction along (tangent to) surface (if surface is flat, this is the last step. If in air, surfaceNormalWishingInto defaults to Vector3.Up)
        Vector3 thrustDirection = (wishDirection - (surfaceNormalWishingInto * wishDirection.Dot(surfaceNormalWishingInto))).Normalized();

        //Default
        ThrustForceTiredWallClimbCoefficient = 1f;
        Statistic10.Text = "Not wallrunning";
        if (!OnSlope || thrustDirection == Vector3.Zero || ClimbEnergy <= 0f) {
            IsWallRunning = false;
        }

        if (contactCount > 0)
        {
            if (onSlope)
            {
                if (
                    ClimbEnergy > 0f
                    && !OnFlat
                    && thrustDirection != Vector3.Zero //wishing to thrust
                    && thrustDirection.Dot(Vector3.Up) < SlopeDotUp //not wishing to thrust up (climb). Could also use surfaceNormalWishingInto
                )
                {
                    Statistic9.Text = $"Wish-surface dot: {wishDirectionRaw.Dot(surfaceNormalWishingInto)}";
                    bool isLookingAtSurface = wishDirectionRaw.Dot(surfaceNormalWishingInto) < -0.5f;
                    if (!isLookingAtSurface)
                    {
                        //Wall-running flag
                        IsWallRunning = true;

                        //TODO: allow walljumping or moving directly into the wall to end wallrunning.
                    }
                }
                else if (
                    ClimbEnergy <= 0f
                    && thrustDirection.Dot(Vector3.Up) > 0f //wishing to thrust up
                )
                {
                    //Tired-wishing on slope (so we should limit the force)
                    //If wishing to thrust up, redirect wish to be tangent to the horizontal component of the surface

                    //Direction
                    //1. Get [the direction on the slope that points upward but is still tangent to it] by removing [the wall's normal] component from [global up].
                    Vector3 globalUpTangentToSurface = (Vector3.Up - (Vector3.Up.Dot(surfaceNormalWishingInto) * surfaceNormalWishingInto)).Normalized();
                    //2. Remove the globalUpTangentToSurface component from thrustDirection to get a purely horizontal direction
                    Vector3 horizontalAlongSlope = thrustDirection - globalUpTangentToSurface * thrustDirection.Dot(globalUpTangentToSurface);
                    thrustDirection = horizontalAlongSlope.Normalized();

                    //Force
                    float dotWishToNormal = wishDirection.Dot(surfaceNormalWishingInto);
                    float forceMultiplier = 1f - Mathf.Max(0f, -dotWishToNormal);
                    ThrustForceTiredWallClimbCoefficient = thrustForce * 1f - Mathf.Max(0f, -dotWishToNormal);
                }

                //Wall-running redirect
                if (IsWallRunning)
                {
                    //Get wall normal for camera tilt and for transforming the thrust direction to be tangent to the wall and the horizontal component only
                    if (wallNormal.HasValue)
                    {
                        WallNormal = (Vector3)wallNormal;
                    }

                    //Stick to wall
                    thrustDirection = (wishDirection - (WallNormal * wishDirection.Dot(WallNormal))).Normalized();

                    //Diagnostics
                    Statistic10.Text = $"Wallrunning, thrustDirection.Dot(Vector3.Up): {thrustDirection.Dot(Vector3.Up)}";
                }
            }
            else if ( //wishing to thrust down
                //-CameraPlayer.GlobalBasis.Z.Dot(Vector3.Up) < 0f //Looking generally-down
                thrustDirection.Dot(Vector3.Up) <= 0f         //Not wishing to go uphill; wish is flat (will be > 0f if going uphill) or downward (I don't think it will ever be downward)
            )
            {
                //onFlat

                //Tangent thrusting when on a flat surface (this is physically relevant when the surface is very slightly sloped)
                //So, we need to check if we're ON the flat surface - not whether we're THRUSTING into it or not

                //Get the normal of the surface (pick the collider that is the most flat if there are several colliders)
                Vector3 normalOfFlattestCollider = Vector3.Down;
                for (int i = 0; i < contactCount; i++)
                {
                    Vector3 normalChecking = state.GetContactLocalNormal(i);
                    if (Vector3.Up.Dot(normalChecking) > Vector3.Up.Dot(normalOfFlattestCollider))
                    {
                        //This collider is the flattest so far
                        normalOfFlattestCollider = normalChecking;
                    }
                }

                //Redirect along tangent
                thrustDirection = (wishDirection - (normalOfFlattestCollider * wishDirection.Dot(normalOfFlattestCollider))).Normalized();
            }
        }

        return thrustDirection;
    }
}
