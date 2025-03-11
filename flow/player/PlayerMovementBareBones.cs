using Godot;

public partial class PlayerMovementBareBones : RigidBody3D
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

    //Thrust/twitch
    private bool InputForward = false;
    private bool InputLeft = false;
    private bool InputRight = false;
    private bool InputBack = false;
    
    private const float Thrust = 5e4f; //thrust multiplied in AFTER jerk is added and alignment is factored
    
    private const float ThrustMaxSpeedOnFlat = 10f;
    private const float ThrustMaxSpeedInAir = 5f;
    
    private const float Twitch = 250f; //base acceleration which will be modified by alignment
    private const float TwitchInAirCoefficient = 0.4f;
    
    //Jerk - Added acceleration which starts at 0 and increases up to JerkMagnitude. Only applies on flat surfaces
    private float Jerk = 0f;
    private const float JerkMagnitude = 200f; //maximum added acceleration (will be this after JerkPeriod elapsed)
    private const float JerkPeriod = 1f; //time it takes to develop jerk, in seconds
    private const float JerkDecayRate = 16f; //how quickly developed jerk is lost if not actively developing it
    private const float JerkDecayRateInAir = 4f;
    
    //Drag
    private const float DragOnFlat = 1e4f; //also the default drag
    private const float DragCrouchedCoefficient = 0.05f;
    private const float DragInAirCoefficient = 0.01f;
    
    //Slope thrusting
    private const float SlopeDotUp = 0.70710678118f; //What angle is a flat surface (45 deg)
                                                     //LESS THAN this values is a slope and engages climbing/wallrunning
                                                     //This is the dot product of the normal of the surface to global up
                                                     //-1 is down, 0 is toward the horizon, 1 is up
                                                     //cos(angle) = dot :. angle = cos^-1(dot)
    private bool OnFlat = false;
    private bool OnSlope = false;

    private bool IsWishingIntoSlope = false;
    
    private Vector3 WallNormal = Vector3.Up;

    private float SlopeMovementEnergy = 1f;

    private float ThrustTiredWishingIntoSlopeCoefficient = 1f; //changed by code at runtime!

    //Crouching/sliding
    private bool InputCrouch = false;
    private bool IsCrouched = false;
    
    private const float TwitchSlidingCoefficient = 0.075f;
    
    //Jump
    private bool InputJump = false;
    private const float JumpVSpeed = 10f;
    
    private float JumpCooldown = 0f;
    private const float JumpCooldownPeriod = 0.1f; //how long after jumping until you can jump again, in seconds
    //private const float JumpNoDragPeriod = 0f; //how long after jumping that there is no drag even if on a surface, in seconds
                                                 //this helps to prevent jump height from being dependent on update rate
    
    private float MaxAchievedHeight = 0f;
    private float MaxAchievedVSpeed = 0f;
    
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
    
    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;
    
        //Unfreeze once game started
        if (Time.GetTicksMsec() > 6000f)
        {
            Freeze = false;
            //GlobalPosition = new Vector3(GlobalPosition.X, 1f, GlobalPosition.Z);
        }
    
        //JUMP
        if (InputJump && OnFlat && JumpCooldown <= 0f)
        {
            //VSpeed will reset if negative, otherwise add to it
            LinearVelocity = new Vector3(
                LinearVelocity.X,
                Mathf.Max(LinearVelocity.Y + JumpVSpeed, JumpVSpeed),
                LinearVelocity.Z
            );
    
            JumpCooldown = JumpCooldownPeriod;
    
            //Diagnose
            MaxAchievedVSpeed = 0f;
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
    
        //Jumping
        MaxAchievedVSpeed = Mathf.Max(MaxAchievedVSpeed, LinearVelocity.Y);
        MaxAchievedHeight = Mathf.Max(MaxAchievedHeight, GlobalPosition.Y);
        Statistic11.Text = $"MaxAchievedHeight: {MaxAchievedHeight}, MaxAchievedVSpeed: {MaxAchievedVSpeed}";
    }
    
    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float delta = state.Step;
    
        //Thrust/run
        Vector3 wishDirectionRaw = GetWishDirectionRaw();
    
        Vector3 thrustDirection = GetDirection(wishDirectionRaw, state);
        Vector3 finalThrustVector = ProcessMovementAndGetVector(thrustDirection, delta);
        ApplyForce(finalThrustVector * delta);
    
        //Drag
        ApplyForce(-LinearVelocity * (Mass * GetDrag() * delta)); //F = ma
        
        //Wallrunning anti-gravity
        //if (IsWallrunning) ApplyForce(Mass * -GetGravity()); //F = ma TODO: make this start to weaken only right before climb energy runs out
    
        //Diagnostics
        TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + thrustDirection * 2.0f;
    }
    
    private float GetDrag()
    {
        float drag = DragOnFlat;
    
        //[Deprecated as at short jump heights this seems to no longer be a problem?]
        //[It was also causing a glitch where you would get an hSpeed burst from jumping due to air drag while on getting flat acceleration]
        //We check against the no drag period here so that
        //jump height isn't somewhat-randomly reduced
        //if the player collider is in contact with the ground for an extra tick
        //due to a high update rate
        if (OnFlat || OnSlope) //&& JumpCooldown < JumpCooldownPeriod - JumpNoDragPeriod)
        {
            //Default drag is DragOnFlat
            if (!OnFlat && OnSlope)
            {
                //Slope
                drag *= DragInAirCoefficient;
            }
        }
        else
        {
            //Air
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
    
            //Movement
            float jerkCoefficient = OnFlat ? JerkMagnitude : 1f; //don't jerk unless on a flat surface
            finalMoveVector = GetVectorAlignedJerked(delta, wishDirection, jerkCoefficient);
        }
        
        return finalMoveVector;
    }
    
    private static Basis GetRotationSlerpTowardsVector(Basis subjectBasis, Vector3 targetVector, float rotationRate)
    {
        //Rotation direction example (Vector3): from Vector3.Up towards Vector3 WallNormal
        //Rotation subject example (Basis): CameraPlayer.CameraGrandparent.GlobalBasis
    
        //Get current rotation
        Quaternion currentRotation = subjectBasis.GetRotationQuaternion();
        //Rotation towards target
        Quaternion destinationRotation = new(Vector3.Up, targetVector);
        
        //Spherical linear interpolation from current rotation towards new rotation
        Quaternion interpolatedRotation = currentRotation.Slerp(destinationRotation, rotationRate);
        
        //Return for application
        return new Basis(interpolatedRotation);
    }
    
    private Vector3 GetVectorAlignedJerked(float delta, Vector3 wishDirection, float jerkCoefficient)
    {
        //ALIGNMENT
        //This prevents accelerating past a max speed in the input direction
        //(a prevalent problem when accelerating in air)
        //while allowing us to maintain responsive air acceleration
    
        //+ if aligned, - if opposite, 0 if perpendicular
        float runAlignment = wishDirection.Dot(new(LinearVelocity.X, 0f, LinearVelocity.Z));
    
        //Set max speed - this prevents the player from being able to gain a ton of hsped by just adding a direction input right after jumping
        float runDynamicMaxSpeed = ThrustMaxSpeedOnFlat;
        if (!OnFlat && !OnSlope)
        {
            runDynamicMaxSpeed = ThrustMaxSpeedInAir;
        }
    
        //Gradient value from 0 to 1, with:
        // * 0 if aligned and at max speed,
        // * 1 if not aligned,
        // * and a value between if not yet at max speed in that direction
        float runAlignmentScaled = Mathf.Clamp(1f - runAlignment / runDynamicMaxSpeed, 0f, 1f);
    
        //TWITCH ACCELERATION
        float runDynamicAccelerationTwitch = Twitch;
        if (IsCrouched)
        {
            //Different acceleration when crouched/sliding
            runDynamicAccelerationTwitch *= TwitchSlidingCoefficient;
        }
        else if (!OnFlat && !OnSlope)
        {
            //Air Acceleration
            runDynamicAccelerationTwitch *= TwitchInAirCoefficient;
        }
    
        //JERK
        //Develop
        //Value from 0.5 to 1 depending on how aligned our running is with our current hVelocity
        float jerkAlignment = Mathf.Clamp(runAlignment / (runDynamicMaxSpeed / 2f), 0f, 1f);
        if (!IsCrouched && wishDirection.Normalized().Length() == 1)
        {
            //Develop jerk - increase acceleration (i.e. make this jerk rather than simply accelerate)
            Jerk = Mathf.Min((Jerk + delta) * jerkAlignment, JerkPeriod);
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
    
        //WALLRUN
        float slopeCoefficient = 1f;
    
        //COMBINE
        float runMagnitude = ((runDynamicAccelerationTwitch * runAlignmentScaled) + jerk) * (Thrust * slopeCoefficient);
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

        //Surface
        float surfaceNormalDotWishSmallest = 1f;
        Vector3 surfaceNormalWishingInto = Vector3.Up;

        //Wallrunning surface (no default/nullable)
        bool isWallNormalAssigned = false;
        float wallNormalDot = 1f;
        Vector3? wallNormal = null;

        //Dynamic thrust force default (attenuated when tired-thrusting into a slope)
        float thrustForce = 1f;

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


        //RE-DIRECTION ALONG COLLIDER SURFACE TANGENT
        //Permutations:
        //- flat surface tangents [redirect wish tangent to surface]
        //- slope tangents (no up on slopes when tired) [if moving down, redirect wish tangent to surface; if moving up, redirect wish tangent to horizontal of surface]
        //- [maybe: can move away from slopes - or maybe wall jump away only, or maybe this is a non-issue]
        //- Default (!OnFlat && !onSlope): air movement [no redirection]

        //Allow climbing straight-up and inverted surfaces
        //if (isCurrentCheckAWishIntoASlope && SlopeMovementEnergy > 0f)
        //{
        //    wishDirection = wishDirectionRaw;
        //}

        //Prevent moving directly into a surface from slowing down movement speed OR from greatly increasing movement speed since we're considered not aligned and so speed cap doesn't apply!
        //Get direction along (tangent to) surface (if surface is flat, this is the last step. If in air, surfaceNormalWishingInto defaults to Vector3.Up)
        Vector3 thrustDirection = (wishDirection - (surfaceNormalWishingInto * wishDirection.Dot(surfaceNormalWishingInto))).Normalized();

        //Default
        ThrustTiredWishingIntoSlopeCoefficient = 1f;


        //SlopeMovementEnergy > 0f
        //thrustDirection != Vector3.Zero
        bool lookingTowardWall = wishDirectionRaw.Dot(WallNormal) < -0.5f;
        bool lookingAwayFromWall = wishDirectionRaw.Dot(WallNormal) > 0.5f;
        bool isWishingToThrustUp = thrustDirection.Dot(Vector3.Up) >= 0f; //SlopeDotUp //isWishingToClimb as long as this is into a surface
        bool isWishingToThrustDown = thrustDirection.Dot(Vector3.Up) <= 0f;
        ////Stick to wall
        //thrustDirection = (wishDirection - (WallNormal * wishDirection.Dot(WallNormal))).Normalized();
        //thrustDirection.Dot(Vector3.Up)
        //Statistic10.Text = $"IsClimbing: {IsClimbing}, IsWallRunning: {IsWallrunning}";

        if (contactCount > 0)
        {
            if (OnSlope)
            {
                //Get wall normal for wallrunning for:
                // - camera tilt, and;
                // - transforming the thrust direction to be [tangent to the wall] and [the horizontal component only]
                if (wallNormal.HasValue)
                {
                    WallNormal = (Vector3)wallNormal;
                }

                //Slope redirection
                if (
                    SlopeMovementEnergy <= 0f
                    && isWishingToThrustUp //&& thrustDirection.Dot(Vector3.Up) > 0f //wishing to thrust up
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
                    ThrustTiredWishingIntoSlopeCoefficient = thrustForce * 1f - Mathf.Max(0f, -dotWishToNormal);
                }
            }
            else if (
                //OnFlat by default from else
                isWishingToThrustDown //Not wishing to go uphill; wish is flat
            )
            {
                //OnFlat

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

                //Redirect along tangent of flat surface
                thrustDirection = (wishDirection - (normalOfFlattestCollider * wishDirection.Dot(normalOfFlattestCollider))).Normalized();
            }
        }

        return thrustDirection;
    }
}
