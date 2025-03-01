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
    private bool InputRunForward = false;
    private bool InputRunLeft = false;
    private bool InputRunRight = false;
    private bool InputRunBack = false;

    public float ThrustForce = 10000f;
    //TODO: add twitch/jerk
    //TODO: add greatly reduced friction unless thrusting along flat surface

    //Jump
    private bool InputJump = false;

    //Wall/ceiling thrusting
    private bool OnFlat = false;
    private bool IsWishingIntoSlope = false;
    private float ClimbEnergy = 1f;
    private const float ClimbRestRate = 4f; //multiplied with delta
    private const float ClimbTireRate = 1f; //multiplied with delta

    public override void _Input(InputEvent @event)
    {
        //Run Direction
        InputRunForward = Input.IsActionPressed("thrust_run_dir_forward");
        InputRunLeft = Input.IsActionPressed("thrust_run_dir_left");
        InputRunRight = Input.IsActionPressed("thrust_run_dir_right");
        InputRunBack = Input.IsActionPressed("thrust_run_dir_back");
        InputJump = Input.IsActionPressed("thrust_jump");

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

        
        Statistic2.Text = $"Speed: {LinearVelocity.Length()}";
        Statistic3.Text = $"HSpeed: {new Vector3(LinearVelocity.X, 0f, LinearVelocity.Z).Length()}";
        Statistic4.Text = $"VSpeed: {LinearVelocity.Y}";
        Statistic5.Text = $"Angular velocity: {AngularVelocity}";

        Statistic6.Text = $"IsOnFlatSurface: {OnFlat}";
        Statistic7.Text = $"IsTryingToThrustIntoWall: {IsWishingIntoSlope}";

        Statistic13.Text = $"Climb energy: {ClimbEnergy}";
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        //GET WISH DIRECTION
        Vector3 wishDirectionRaw = Vector3.Zero;
        if (InputRunForward) wishDirectionRaw -= CameraPlayer.GlobalBasis.Z;
        if (InputRunLeft) wishDirectionRaw -= CameraPlayer.GlobalBasis.X;
        if (InputRunRight) wishDirectionRaw += CameraPlayer.GlobalBasis.X;
        if (InputRunBack) wishDirectionRaw += CameraPlayer.GlobalBasis.Z;

        //Convert camera look to flat plane
        //this step MUST be skipped if we want to be able to climb straight-up or inverted inclines
        //yet, this step is ABSOLUTELY NECESSARY if we want to prevent staying on walls even when tired
        Vector3 wishDirection = (wishDirectionRaw - (Vector3.Up * wishDirectionRaw.Dot(Vector3.Up))).Normalized();

        //COLLISION DETECTION
        //Types of colliders:
        //- air          [no collider]
        //- slope        [Vector3.Up.Dot(collider) < 0.75f]
        //- flat         [Vector3.Up.Dot(collider) >= 0.75f]

        //Types of collisions:
        //- wishDirection   Wish vector into a collider         [we only use this one in this implementation]
        //- LinearVelocity  Velocity vector into a collider
        //- GetGravity()    Gravity vector into a collider

        OnFlat = false;
        bool onSlope = false;
        
        float wishIntoDotSmallest = 1f;
        Vector3 wishIntoNormal = Vector3.Up;

        bool isWishIntoASlope = false;
        IsWishingIntoSlope = false;

        float thrustForce = ThrustForce;

        Statistic8.Text = "";
        Statistic9.Text = "";
        Statistic10.Text = "";
        Statistic11.Text = "";
        Statistic12.Text = "";

        int contactCount = state.GetContactCount();
        if (contactCount > 0)
        {
            for (int i = 0; i < contactCount; i++)
            {
                Vector3 surfaceNormal = state.GetContactLocalNormal(i);
                Statistic13.Text = $"Vector3.Up.Dot(surfaceNormal): {Vector3.Up.Dot(surfaceNormal)}";

                if (Vector3.Up.Dot(surfaceNormal) < 0.75f)
                {
                    //Wishing into slope
                    onSlope = true;

                    //Ensure this is the collider we're wishing into the most
                    float wishIntoDot = wishDirection.Dot(surfaceNormal);
                    if (wishIntoDot < 0f && wishIntoDot < wishIntoDotSmallest)
                    {
                        wishIntoDotSmallest = wishIntoDot;
                        wishIntoNormal = surfaceNormal;

                        isWishIntoASlope = true;
                    }
                }
                else
                {
                    //Wishing into flat
                    OnFlat = true;

                    //Ensure this is the collider we're wishing into the most
                    float wishIntoDot = wishDirection.Dot(surfaceNormal);
                    if (wishIntoDot < 0f && wishIntoDot < wishIntoDotSmallest)
                    {
                        wishIntoDotSmallest = wishIntoDot;
                        wishIntoNormal = surfaceNormal;

                        isWishIntoASlope = false;
                    }
                }
            }

            //The collider that we're wishing to move into the most (not just at least one collider) is a slope
            if (isWishIntoASlope)
            {
                IsWishingIntoSlope = true;
            }
        }

        //RE-DIRECTION ALONG COLLIDER SURFACE TANGENT
        //Permutations:
        //- flat surface tangents [redirect wish tangent to surface]
        //- slope tangents (no up on slopes when tired) [if moving down, redirect wish tangent to surface; if moving up, redirect wish tangent to horizontal of surface]
        //- [maybe: can move away from slopes - or maybe wall jump away only, or maybe this is a non-issue]
        //- Default (!OnFlat && !onSlope): air movement [no redirection]

        //Allow climbing straight-up and inverted surfaces
        if (isWishIntoASlope && ClimbEnergy > 0f)
        {
            wishDirection = wishDirectionRaw;
        }

        //Get direction along (tangent to) surface (if surface is flat, this is the last step. If in air, wishIntoNormal defaults to Vector3.Up)
        Vector3 thrustDirection = (wishDirection - (wishIntoNormal * wishDirection.Dot(wishIntoNormal))).Normalized();

        if (contactCount > 0)
        {
            Statistic12.Text = $"thrustDirection.Dot(Vector3.Up): {thrustDirection.Dot(Vector3.Up)}; onSlope: {onSlope}";
            if (onSlope)
            {
                if (ClimbEnergy <= 0f && thrustDirection.Dot(Vector3.Up) > 0f) //wishing to thrust up
                {
                    //Limit if tired-wishing on slope
                    //If wishing to thrust up, redirect wish to be tangent to the horizontal component of the surface

                    //Direction
                    //1. Get [the direction on the slope that points upward but is still tangent to it] by removing [the wall's normal] component from [global up].
                    Vector3 globalUpTangentToSurface = (Vector3.Up - (Vector3.Up.Dot(wishIntoNormal) * wishIntoNormal)).Normalized();
                    //2. Remove the globalUpTangentToSurface component from thrustDirection to get a purely horizontal direction
                    Vector3 horizontalAlongSlope = thrustDirection - globalUpTangentToSurface * thrustDirection.Dot(globalUpTangentToSurface);
                    thrustDirection = horizontalAlongSlope.Normalized();

                    //Force
                    float dotWishToNormal = wishDirection.Dot(wishIntoNormal);
                    float forceMultiplier = 1f - Mathf.Max(0f, -dotWishToNormal);
                    thrustForce *= 1f - Mathf.Max(0f, -dotWishToNormal);

                    Statistic8.Text = "Tired-wishing on slope";
                    Statistic9.Text = $"dotWishToNormal: {dotWishToNormal}";
                    Statistic10.Text = $"forceMultiplier: {forceMultiplier}";
                    Statistic11.Text = $"thrustForce: {thrustForce}";
                    Statistic12.Text = $"thrustDirection: {thrustDirection}";
                }
            }
            else if ( //wishing to thrust down
                -CameraPlayer.GlobalBasis.Z.Dot(Vector3.Up) < 0f //Looking generally-down
                && thrustDirection.Dot(Vector3.Up) <= 0f         //Not wishing to go uphill; wish is flat (will be > 0f if going uphill) or downward (I don't think it will ever be downward)
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
                        normalOfFlattestCollider = normalChecking;
                    }
                }

                //Redirect along tangent
                thrustDirection = (wishDirection - (normalOfFlattestCollider * wishDirection.Dot(normalOfFlattestCollider))).Normalized();
            }
        }

        Statistic6.Text = $"onFlat: {OnFlat}, onSlope: {onSlope}";

        thrustDirection = thrustDirection.Normalized();
        ApplyForce(thrustDirection * thrustForce);

        TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + thrustDirection * 2.0f;

        if (InputJump && OnFlat)
        {
            ApplyImpulse(Vector3.Up * 500f);
        }
    }
}
