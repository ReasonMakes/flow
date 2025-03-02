using Godot;

public partial class PlayerMovement : RigidBody3D
{
    //Testing
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

    public float ThrustForce = 10000f; //public variable because slider attached
    //private const float ThrustAcceleration = 0.2f;
    private const float ThrustSpeedMax = 12f;
    //TODO: add twitch/jerk
    //TODO: add greatly reduced friction unless thrusting along flat surface
    private const float ThrustTwitchSpeedMin = 4f; //speed we must have already developed in order to have the agility to twitch
    private const float ThrustTwitchSpeedMax = 8f; //if above ThrustTwitchSpeedMin and below this speed when wishing to thrust,
    
    //Wall/ceiling thrusting
    private const float SlopeDotUp = 0.75f; //What angle (as a dot product of the surface normal to global up)
                                            //constitutes a slope vs a flat surface and engages climbing/wallrunning
                                            //-1 is down, 0 is toward the horizon, 1 is up
    private bool OnFlat = false;
    private bool OnSlope = false;
    private bool IsWishingIntoSlope = false;
    private bool IsWallRunning = false;
    private float ClimbEnergy = 1f;
    private const float ClimbRestRate = 4f; //multiplied with delta
    private const float ClimbTireRate = 1f; //multiplied with delta

    //Crouching/sliding
    private bool InputCrouch = false;
    private bool IsSliding = false;

    //Old variables
    private const float DragOnFlat = 20f;
    private const float DragInAirCoefficient = 0.01f;
    private const float DragSlidingCoefficient = 0.05f;
    private const float DragWallrunningCoefficient = 1f;
    
    private bool IsClimbing = false;

    private const float ThrustAcceleration = 250f;
    private const float ThrustAccelerationInAirCoefficient = 0.4f;
    private const float ThrustAccelerationSlidingCoefficient = 0.075f;
    private const float ThrustMaxSpeedOnFlat = 10f;
    private const float ThrustMaxSpeedInAir = 5f;
    
    private float Jerk = 0f;
    private const float JerkMagnitude = 200f;
    private const float JerkPeriod = 2f;
    private const float JerkRate = 2f;
    private const float JerkDecayRate = 16f;
    private const float JerkDecayRateInAir = 4f;
    
    //Jump
    private bool InputJump = false;
    private const float JumpForce = 500f;

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
            CameraPlayer.CameraGrandparent.Rotation = new Vector3(
                CameraPlayer.CameraGrandparent.Rotation.X,
                CameraPlayer.CameraGrandparent.Rotation.Y - mouseMotion.Relative.X * MouseSensitivity,
                CameraPlayer.CameraGrandparent.Rotation.Z
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

    public void UpdateThrustForce(float val)
    {
        ThrustForce = val;
    }

    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //Unfreeze once game started
        if (Time.GetTicksMsec() > 2000f)
        {
            Freeze = false;
        }

        //Process climb energy
        if (OnFlat)
        {
            ClimbEnergy = Mathf.Min(1f, ClimbEnergy + (delta * ClimbRestRate));
        }
        else if (IsWishingIntoSlope)
        {
            ClimbEnergy = Mathf.Max(0f, ClimbEnergy - (delta * ClimbTireRate));
        }


        string testSpeed = LinearVelocity.Length() != 0f && LinearVelocity.Length() < 0.1f ? "<0.1" : $"{LinearVelocity.Length():F2}";
        Statistic2.Text = $"Speed: {testSpeed}";
        string testHSpeed = new Vector3(LinearVelocity.X, 0f, LinearVelocity.Z).Length() != 0f && new Vector3(LinearVelocity.X, 0f, LinearVelocity.Z).Length() < 0.1f ? "<0.1" : $"{new Vector3(LinearVelocity.X, 0f, LinearVelocity.Z).Length():F2}";
        Statistic3.Text = $"HSpeed: {testHSpeed}";
        string testVSpeed = LinearVelocity.Y != 0f && Mathf.Abs(LinearVelocity.Y) < 0.1f ? "<+/-0.1" : $"{LinearVelocity.Y:F2}";
        Statistic4.Text = $"VSpeed: {testVSpeed}";
        Statistic5.Text = $"Angular velocity: {AngularVelocity}";

        Statistic7.Text = $"IsWishingIntoSlope: {IsWishingIntoSlope}";

        Statistic8.Text = $"Climb energy: {ClimbEnergy}";
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float delta = state.Step;

        Vector3 thrustDirection = GetDirection(state);

        //MAGNITUDE
        ApplyAccelerationAndDragOverTime(ProcessMovementAndGetVector(thrustDirection, delta), delta);

        //JUMP
        if (InputJump && OnFlat)
        {
            ApplyImpulse(Vector3.Up * JumpForce);
        }

        //TEST
        TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + thrustDirection * 2.0f;
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

        float dragComponent = GetDrag();

        //Apply drag friction with an exponential decay expression to account for users with throttled physics update rates
        float decayFactor = Mathf.Exp(-dragComponent * delta);
        LinearVelocity = acceleration / dragComponent * (1f - decayFactor) + (LinearVelocity * decayFactor);
    }

    private float GetDrag()
    {
        float dragComponent;

        if (OnFlat)
        {
            //Ground
            if (IsSliding)
            {
                dragComponent = DragOnFlat * DragSlidingCoefficient;
            }
            else
            {
                dragComponent = DragOnFlat;
            }
        }
        else if (IsWallRunning)
        {
            //Wall-running
            dragComponent = DragOnFlat * DragWallrunningCoefficient;
        }
        else
        {
            //Air or climbing
            dragComponent = DragOnFlat * DragInAirCoefficient;
        }

        return dragComponent;
    }

    private Vector3 ProcessMovementAndGetVector(Vector3 wishDirection, float delta)
    {
        //Default values
        Vector3 finalMoveOnWallVector = Vector3.Zero;

        //Do we WANT to do movement?
        if (
            InputForward || InputLeft || InputRight || InputBack
            || InputJump
        )
        {
            //Running on the ground
            finalMoveOnWallVector = Run(wishDirection, delta);
        }

        ////Wall-running camera roll
        //if (IsWallRunning)
        //{
        //    if (IsWishingIntoSlope)
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

        return finalMoveOnWallVector;
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

        //TWITCH ACCELERATION
        if (InputCrouch)
        {
            //Different acceleration when crouched/sliding
            runDynamicAccelerationTwitch *= ThrustAccelerationSlidingCoefficient;
        }
        else if (!OnFlat && !OnSlope)
        {
            //Ground vs Air Acceleration
            runDynamicAccelerationTwitch *= ThrustAccelerationInAirCoefficient;
        }

        //JERK
        //Develop
        //Value from 0.5 to 1 depending on how aligned our running is with our current hVelocity
        float jerkAlignment = Mathf.Clamp(runAlignment / (runDynamicMaxSpeed / 2f), 0f, 1f);
        if (!IsSliding && wishDirection.Normalized().Length() == 1)
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


    //public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    //{
    //    Vector3 thrustDirection = GetDirection(state);
    //
    //    //MAGNITUDE
    //    //PhysicsMaterialOverride.Friction = 100000000f;
    //    //ApplyForce(thrustDirection * thrustForce);
    //
    //    float speed = LinearVelocity.Length();
    //    if (
    //        thrustDirection != Vector3.Zero
    //        && OnFlat
    //        && speed >= ThrustTwitchSpeedMin
    //        && speed <= ThrustTwitchSpeedMax
    //        && LinearVelocity.Dot(thrustDirection) < speed //velocity component in thrust direction is less than speed
    //    )
    //    {
    //        LinearVelocity = thrustDirection * speed;
    //    }
    //    else if (LinearVelocity.Dot(thrustDirection) < ThrustSpeedMax) //below max speed
    //    {
    //        //TODO: this allows for really extreme air strafing!
    //        LinearVelocity += thrustDirection * ThrustAcceleration;
    //    }
    //
    //    //JUMP
    //    if (InputJump && OnFlat)
    //    {
    //        ApplyImpulse(Vector3.Up * JumpForce);
    //    }
    //
    //    //TEST
    //    TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + thrustDirection * 2.0f;
    //    if (TestPrint) TestPrint = false;
    //}

    private Vector3 GetDirection(PhysicsDirectBodyState3D state)
    {
        //GET WISH DIRECTION
        Vector3 wishDirectionRaw = Vector3.Zero;
        if (InputForward) wishDirectionRaw -= CameraPlayer.GlobalBasis.Z;
        if (InputLeft) wishDirectionRaw -= CameraPlayer.GlobalBasis.X;
        if (InputRight) wishDirectionRaw += CameraPlayer.GlobalBasis.X;
        if (InputBack) wishDirectionRaw += CameraPlayer.GlobalBasis.Z;

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

        OnFlat = false;
        bool onSlope = false;
        
        float surfaceNormalDotWishSmallest = 1f;
        Vector3 surfaceNormalWishingInto = Vector3.Up;

        bool isCurrentCheckAWishIntoASlope = false;
        IsWishingIntoSlope = false;

        float thrustForce = ThrustForce;

        int contactCount = state.GetContactCount();
        if (contactCount > 0)
        {
            for (int i = 0; i < contactCount; i++)
            {
                Vector3 surfaceNormal = state.GetContactLocalNormal(i);

                if (Vector3.Up.Dot(surfaceNormal) < SlopeDotUp)
                {
                    //Wishing into slope
                    onSlope = true;

                    //Ensure this is the collider we're wishing into the most
                    float wishIntoDot = wishDirection.Dot(surfaceNormal);
                    if (wishIntoDot < 0f && wishIntoDot < surfaceNormalDotWishSmallest)
                    {
                        surfaceNormalDotWishSmallest = wishIntoDot;
                        surfaceNormalWishingInto = surfaceNormal;

                        isCurrentCheckAWishIntoASlope = true;
                    }
                }
                else
                {
                    //Wishing into flat
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
        Statistic6.Text = $"onFlat: {OnFlat}, onSlope: {onSlope}";


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
        Statistic10.Text = "Not wallrunning";
        IsWallRunning = false;

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
                    Statistic9.Text = $"camera-surface dot: {-CameraPlayer.GlobalBasis.Z.Dot(surfaceNormalWishingInto)}";
                    bool isLookingAtSurface = -CameraPlayer.GlobalBasis.Z.Dot(surfaceNormalWishingInto) < -0.5f;
                    if (!isLookingAtSurface)
                    {
                        //Wall-running
                        IsWallRunning = true;

                        //TODO: change this from look direction to thrust direction so that we can wallrun while looking elsewhere
                        //TODO: add sticking (can EITHER be not looking at surface or be already stuck. We unstick once we're no longer on a slope)

                        thrustDirection = new Vector3(thrustDirection.X, 0f, thrustDirection.Z).Normalized();

                        //ApplyImpulse(Vector3.Up * JumpForce);
                        ApplyForce(Mass * -GetGravity()); //F = ma TODO: make this start to weaken only right before climb energy runs out
                        //LinearVelocity = new Vector3(LinearVelocity.X, 0f, LinearVelocity.Z);

                        Statistic10.Text = $"Wallrunning, thrustDirection.Dot(Vector3.Up): {thrustDirection.Dot(Vector3.Up)}";
                    }
                }
                else if (
                    ClimbEnergy <= 0f
                    && thrustDirection.Dot(Vector3.Up) > 0f //wishing to thrust up
                )
                {
                    //Limit if tired-wishing on slope
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
                    thrustForce *= 1f - Mathf.Max(0f, -dotWishToNormal);
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
