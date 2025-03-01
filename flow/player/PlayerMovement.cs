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

        Statistic4.Text = $"IsOnFlatSurface: {OnFlat}";
        Statistic5.Text = $"IsTryingToThrustIntoWall: {IsWishingIntoSlope}";

        Statistic13.Text = $"Climb energy: {ClimbEnergy}";
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        //WASD
        Vector3 wishDirection = Vector3.Zero;
        if (InputRunForward) wishDirection -= CameraPlayer.GlobalBasis.Z;
        if (InputRunLeft) wishDirection -= CameraPlayer.GlobalBasis.X;
        if (InputRunRight) wishDirection += CameraPlayer.GlobalBasis.X;
        if (InputRunBack) wishDirection += CameraPlayer.GlobalBasis.Z;
        Vector3 thrustDirection = wishDirection;

        //Collision detection
        //Types of colliders:
        //- air          [no collider]
        //- slope        [Vector3.Up.Dot(collider) < 0.75f]
        //- flat         [Vector3.Up.Dot(collider) >= 0.75f]

        //Types of collisions:
        //- wishDirection   Wish vector into a collider         [we only use this one in this implementation]
        //- LinearVelocity  Velocity vector into a collider
        //- GetGravity()    Gravity vector into a collider

        wishDirection = (wishDirection - (Vector3.Up * wishDirection.Dot(Vector3.Up))).Normalized();

        OnFlat = false;
        bool onSlope = false;

        bool isWishIntoACollider = false;
        
        float wishIntoDotSmallest = 1f;
        Vector3 wishIntoNormal = Vector3.Up;

        bool isWishIntoASlope = false;
        IsWishingIntoSlope = false;

        float thrustForce = ThrustForce;

        Statistic7.Text = "";
        Statistic8.Text = "";
        Statistic9.Text = "";
        Statistic10.Text = "";
        Statistic11.Text = "";

        int contactCount = state.GetContactCount();
        if (contactCount > 0 && wishDirection != Vector3.Zero)
        {
            for (int i = 0; i < contactCount; i++)
            {
                Vector3 surfaceNormal = state.GetContactLocalNormal(i);

                if (Vector3.Up.Dot(surfaceNormal) < 0.75f)
                {
                    onSlope = true;

                    //Ensure this is the collider we're wishing into the most
                    float wishIntoDot = wishDirection.Dot(surfaceNormal);
                    if (wishIntoDot < 0f && wishIntoDot < wishIntoDotSmallest)
                    {
                        wishIntoDotSmallest = wishIntoDot;
                        wishIntoNormal = surfaceNormal;

                        isWishIntoASlope = true;

                        isWishIntoACollider = true;
                    }
                }
                else
                {
                    OnFlat = true;

                    //Ensure this is the collider we're wishing into the most
                    float wishIntoDot = wishDirection.Dot(surfaceNormal);
                    if (wishIntoDot < 0f && wishIntoDot < wishIntoDotSmallest)
                    {
                        wishIntoDotSmallest = wishIntoDot;
                        wishIntoNormal = surfaceNormal;

                        isWishIntoASlope = false;

                        isWishIntoACollider = true;
                    }
                }
            }

            //The collider that we're wishing to move into the most (not just at least one collider) is a slope
            if (isWishIntoASlope)
            {
                IsWishingIntoSlope = true;
            }
        }

        //Redirection
        //Permutations:
        //- flat surface tangents [redirect wish tangent to surface]
        //- slope tangents (no up on slopes when tired) [if moving down, redirect wish tangent to surface; if moving up, redirect wish tangent to horizontal of surface]
        //- [maybe: can move away from slopes - or maybe wall jump away only, or maybe this is a non-issue]
        //- Default (!OnFlat && !onSlope): air movement [no redirection]

        //Get direction along (tangent to) surface (if surface is flat, this is the last step. If in air, wishIntoNormal defaults to Vector3.Up)
        thrustDirection = (wishDirection - (wishIntoNormal * wishDirection.Dot(wishIntoNormal))).Normalized();

        if (contactCount > 0 && wishDirection != Vector3.Zero)
        {
            //TODO: Add tangent thrusting when on flat ground (that is very slightly sloped.
            //So, we need to check if we're on the ground and ignore whether we're thrusting into it or not)

            //Limit if tired-wishing on slope
            Statistic12.Text = $"thrustDirection.Dot(Vector3.Up): {thrustDirection.Dot(Vector3.Up)}";
            if (onSlope && thrustDirection.Dot(Vector3.Up) >= 0f && ClimbEnergy <= 0f)
            {
                //If wishing to thrust up, redirect wish to be tangent to the horizontal component of the surface
                //1. Get [the direction on the slope that points upward but is still tangent to it] by removing [the wall's normal] component from [global up].
                Vector3 globalUpTangentToSurface = (Vector3.Up - (Vector3.Up.Dot(wishIntoNormal) * wishIntoNormal)).Normalized();

                //2. Remove the globalUpTangentToSurface component from thrustDirection to get a purely horizontal direction
                Vector3 horizontalAlongSlope = thrustDirection - globalUpTangentToSurface * thrustDirection.Dot(globalUpTangentToSurface);
                thrustDirection = horizontalAlongSlope.Normalized();

                //Force
                float dotWishToNormal = wishDirection.Dot(wishIntoNormal);
                float forceMultiplier = 1f - Mathf.Max(0f, -dotWishToNormal);
                thrustForce *= 1f - Mathf.Max(0f, -dotWishToNormal);

                Statistic7.Text = "Tired-wishing on slope";
                Statistic8.Text = $"dotWishToNormal: {dotWishToNormal}";
                Statistic9.Text = $"forceMultiplier: {forceMultiplier}";
                Statistic10.Text = $"thrustForce: {thrustForce}";
                Statistic11.Text = $"thrustDirection: {thrustDirection}";
            }
        }

        Statistic6.Text = $"onFlat: {OnFlat}, onSlope: {onSlope}";

        thrustDirection = thrustDirection.Normalized();
        ApplyForce(thrustDirection * thrustForce);

        TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + thrustDirection * 2.0f;
    }

    public void Old_IntegrateForces(PhysicsDirectBodyState3D state)
    {
        //Behaviours:
        //Know if we're on any flat surface
        //Know if we're trying to climb
        //Walk along a flat surface - any collider with a Vector3.Up.Dot(surfaceNormal) >= 0.75f
        //Climb a slanted surface - if there's a collider with a Vector3.Up.Dot(surfaceNormal) < 0.75f,
        //                          and it is the collider that we're looking at the most,
        //                          and we have climb energy - takes priority over walking along flat surface

        //WASD - This is not normalized!
        Vector3 wishDirection = Vector3.Zero;
        if (InputRunForward) wishDirection -= CameraPlayer.GlobalBasis.Z;
        if (InputRunLeft) wishDirection -= CameraPlayer.GlobalBasis.X;
        if (InputRunRight) wishDirection += CameraPlayer.GlobalBasis.X;
        if (InputRunBack) wishDirection += CameraPlayer.GlobalBasis.Z;

        // Loop over each contact
        float smallestLookDot = 1f;
        Vector3 normalOfColliderLookingAt = Vector3.Up;
        float smallestThrustIntoDot = 1f;
        Vector3 normalOfColliderThrustingInto = Vector3.Up;
        float smallestGravityThrustIntoDot = 1f;
        Vector3 normalOfColliderGravityThrustingInto = Vector3.Zero;
        float smallestMoveIntoDot = 1f;
        Vector3 normalOfColliderMovingInto = Vector3.Zero;
        bool isMovingIntoAnyCollider = false;
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            Vector3 surfaceNormal = state.GetContactLocalNormal(i);
            float lookToSurfaceDot = -CameraPlayer.GlobalBasis.Z.Dot(surfaceNormal);

            //Find the collider we're looking at the most
            if (lookToSurfaceDot < smallestLookDot)
            {
                smallestLookDot = lookToSurfaceDot;
                normalOfColliderLookingAt = surfaceNormal;
            }

            //Find the collider we're thrusting into
            float thrustIntoDot = wishDirection.Dot(surfaceNormal);
            if (thrustIntoDot < 0f && thrustIntoDot < smallestThrustIntoDot)
            {
                smallestThrustIntoDot = thrustIntoDot;
                normalOfColliderThrustingInto = surfaceNormal;
            }

            //Find the collider that gravity is thrusting us into
            float gravityThrustIntoDot = GetGravity().Dot(surfaceNormal);
            if (gravityThrustIntoDot < 0f && gravityThrustIntoDot < smallestGravityThrustIntoDot)
            {
                smallestGravityThrustIntoDot = gravityThrustIntoDot;
                normalOfColliderGravityThrustingInto = surfaceNormal;
            }

            //Find the collider we're moving (falling?) into
            float moveIntoDot = LinearVelocity.Dot(surfaceNormal);
            if (moveIntoDot < 0f && moveIntoDot < smallestMoveIntoDot)
            {
                isMovingIntoAnyCollider = true;
                smallestMoveIntoDot = moveIntoDot;
                normalOfColliderMovingInto = surfaceNormal;
            }
        }
        Statistic2.Text = $"normalOfColliderLookingAt: {normalOfColliderLookingAt}";

        //Get surface information

        //Permutations:
        //In the air [no colliders]
        //On a slope and wishing to thrust into it (decrement climb) [collider(s), sloped, thrusting]
        //On a slope and not thrusting into it [collider(s), sloped, not thrusting]
        //On the ground (regenerate climb) [collider(s) but none sloped]

        //Info needed:
        //Collider count
        //Check all colliders for any sloped
        //Check for if thrusting into a collider
        //Check for if thrusting into a collider, get its slope

        OnFlat = false;
        IsWishingIntoSlope = false;
        Vector3 relativeUp = Vector3.Up;
        if (state.GetContactCount() == 0)
        {
            //In the air
            Statistic3.Text = $"In the air";
        }
        else
        {
            if (
                normalOfColliderThrustingInto != Vector3.Up                //Thrusting into a collider
                && Vector3.Up.Dot(normalOfColliderThrustingInto) < 0.75f   //Collider is sloped
            )
            {
                //On a slope and thrusting into it
                Statistic3.Text = $"On a slope and wishing to thrust into it";
                IsWishingIntoSlope = true;
                relativeUp = normalOfColliderThrustingInto;
            }
            else
            {
                //Moving (falling) into a collider without thrusting
                float dotCollider = Vector3.Up.Dot(normalOfColliderMovingInto);
                if (
                    isMovingIntoAnyCollider
                    && Vector3.Up.Dot(normalOfColliderMovingInto) < 0.75f //Collider is sloped
                )
                {
                    //On a slope and not thrusting into it [collider(s), sloped, not thrusting]
                    //This fails every other tick
                    Statistic3.Text = $"isMovingIntoAnyCollider: {isMovingIntoAnyCollider}, dot: {dotCollider}, normal: {normalOfColliderMovingInto}. On a slope and not thrusting into it";
                }
                else if (normalOfColliderGravityThrustingInto != Vector3.Zero) //Gravity is thrusting us into a collider
                {
                    if (Vector3.Up.Dot(normalOfColliderGravityThrustingInto) >= 0.75f) //Collider is flat-ish
                    {
                        //On a slope, gravity goes into it
                        Statistic3.Text = $"On a slope, gravity goes into it";
                    }
                    else
                    {
                        //On a flat-ish surface, gravity goes into it
                        Statistic3.Text = $"On a flat-ish surface, gravity goes into it";
                        OnFlat = true;
                    }
                }
                else
                {
                    //SHOULD be impossible
                    //On a flat-ish surface, linear velocity moving into it
                    //This condition is met every other tick when on a slope
                    //This condition was met every other tick when on flat ground with previous implementation, since sometimes there will be no linear velocity, and therefore we are not moving into anything
                    Statistic3.Text = $"isMovingIntoAnyCollider: {isMovingIntoAnyCollider}, dot: {dotCollider}, normal: {normalOfColliderMovingInto}. On a flat-ish surface, linear velocity moving into it";
                    OnFlat = true;
                }
            }
        }


        ////Adjust thrusting based on if thrusting into a slanted surface
        //float thrustForce = ThrustForce;
        //IsTryingToThrustIntoWall = false;
        //IsOnFlatSurface = false;
        //Vector3 relativeUp = normalOfColliderThrustingInto;
        //bool isThrustingIntoSurface = false;
        //float dotWishToThrustIntoCollider = 0f;
        //if (state.GetContactCount() == 0)
        //{
        //    //In the air
        //    Statistic3.Text = $"In the air";
        //}
        //else
        //{
        //    float slantOfSurfaceThrustingInto = Vector3.Up.Dot(normalOfColliderThrustingInto);
        //    dotWishToThrustIntoCollider = wishDirection.Dot(normalOfColliderThrustingInto);
        //    isThrustingIntoSurface = dotWishToThrustIntoCollider < 0f;
        //
        //
        //
        //    //float slantOfSurfaceFallingInto = Vector3.Up.Dot(normalOfColliderVelocityInto);
        //    if (slantOfSurfaceThrustingInto < 0.75f)
        //    {
        //        //Thrusting into a surface
        //        if (isThrustingIntoSurface)
        //        {
        //            //Slanted surface that we're too tired to thrust into
        //            IsTryingToThrustIntoWall = true;
        //            Statistic3.Text = $"Thrusting into a slanted surface";
        //        }
        //    }
        //    //else if (slantOfSurfaceMovingInto >= 0.75f)
        //    //{
        //    //    //Moving (falling?) into a surface
        //    //    Statistic3.Text = $"Moving (falling?) into a flat-ish surface";
        //
        //            // Allow for thrusting off the wall
        //            //relativeUp = Vector3.Up;
        //    //}
        //    else
        //            {
        //        //Flat-ish surface
        //        //TODO: allow this to check for if we're landing/standing on a surface, not just thrusting into it
        //        IsOnFlatSurface = true;
        //        Statistic3.Text = $"On a flat-ish surface";
        //    }
        //}

        //Get direction along surface
        Vector3 thrustDirection = (wishDirection - (relativeUp * wishDirection.Dot(relativeUp))).Normalized();

        //Don't allow thrusting up a slanted surface when tired - when trying to, instead thrust horizontally along it
        float thrustForce = ThrustForce;
        bool isThrustingUpAlongSurface = thrustDirection.Dot(Vector3.Up) >= 0f;
        Statistic6.Text = $"isThrustingUpAlongSurface: {isThrustingUpAlongSurface}";
        if (IsWishingIntoSlope && ClimbEnergy <= 0f && isThrustingUpAlongSurface)
        {
            //1. Get [the direction on the slope that points upward but is still tangent to it] by removing [the wall's normal] component from [global up].
            Vector3 globalUpTangentToSurface = (Vector3.Up - (Vector3.Up.Dot(relativeUp) * relativeUp)).Normalized();

            //2. Remove the globalUpTangentToSurface component from thrustDirection to get a purely horizontal direction
            Vector3 horizontalAlongSlope = thrustDirection - globalUpTangentToSurface * thrustDirection.Dot(globalUpTangentToSurface);
            thrustDirection = horizontalAlongSlope.Normalized();

            //Force
            float dotWishToThrustingIntoCollider = wishDirection.Dot(normalOfColliderThrustingInto);
            float forceMultiplier = 1f - Mathf.Max(0f, -dotWishToThrustingIntoCollider);
            thrustForce *= 1f - Mathf.Max(0f, -dotWishToThrustingIntoCollider);

            Statistic12.Text = $"dotWishToThrustingIntoCollider: {dotWishToThrustingIntoCollider}";
            Statistic13.Text = $"forceMultiplier: {forceMultiplier}";
            Statistic14.Text = $"thrustForce: {thrustForce}";
            Statistic15.Text = $"thrustDirection: {thrustDirection}";
        }

        //Force proportional to friction
        //float friction = PhysicsMaterialOverride != null ? PhysicsMaterialOverride.Friction : 1.0f; // Default is 1.0 if no override exists

        //Jump
        if (InputJump)
        {
            ApplyImpulse(relativeUp * 10f);
        }

        //Apply
        ApplyForce(thrustDirection * thrustForce);

        TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + thrustDirection * 2.0f;
    }
}
