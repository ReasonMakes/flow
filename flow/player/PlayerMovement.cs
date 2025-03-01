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

    //Move
    private bool InputRunForward = false;
    private bool InputRunLeft = false;
    private bool InputRunRight = false;
    private bool InputRunBack = false;

    public float MoveForce = 10000f;
    //TODO: add twitch/jerk

    //Jump
    private bool InputJump = false;

    //Wall/ceiling movement
    private bool IsOnFlatSurface = false;
    private bool IsTryingToMoveIntoWall = false;
    private float ClimbEnergy = 1f;
    private const float ClimbRestRate = 4f; //multiplied with delta
    private const float ClimbTireRate = 1f; //multiplied with delta

    public override void _Input(InputEvent @event)
    {
        //Run Direction
        InputRunForward = Input.IsActionPressed("move_run_dir_forward");
        InputRunLeft = Input.IsActionPressed("move_run_dir_left");
        InputRunRight = Input.IsActionPressed("move_run_dir_right");
        InputRunBack = Input.IsActionPressed("move_run_dir_back");
        InputJump = Input.IsActionPressed("move_jump");

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

    public void UpdateMoveForce(float val)
    {
        MoveForce = val;
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
        if (IsOnFlatSurface)
        {
            ClimbEnergy = Mathf.Min(1f, ClimbEnergy + (delta * ClimbRestRate));
        }
        else if (IsTryingToMoveIntoWall)
        {
            ClimbEnergy = Mathf.Max(0f, ClimbEnergy - (delta * ClimbTireRate));
        }
        Statistic4.Text = $"IsOnFlatSurface: {IsOnFlatSurface}";
        Statistic5.Text = $"IsTryingToMoveOnWall: {IsTryingToMoveIntoWall}";
        Statistic7.Text = $"Speed: {LinearVelocity.Length()}";
        Statistic8.Text = $"HSpeed: {new Vector3(LinearVelocity.X, 0f, LinearVelocity.Z).Length()}";
        Statistic9.Text = $"VSpeed: {LinearVelocity.Y}";
        Statistic10.Text = $"Angular velocity: {AngularVelocity}";
        Statistic11.Text = $"Climb energy: {ClimbEnergy}";
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
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
        float smallestMoveIntoDot = 1f;
        Vector3 normalOfColliderMovingInto = Vector3.Up;
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

            //Find the collider we're moving into
            float moveIntoDot = wishDirection.Dot(surfaceNormal);
            if (moveIntoDot < 0f && moveIntoDot < smallestMoveIntoDot)
            {
                smallestMoveIntoDot = moveIntoDot;
                normalOfColliderMovingInto = surfaceNormal;
            }
        }
        Statistic2.Text = $"normalOfColliderLookingAt: {normalOfColliderLookingAt}";

        //Adjust movement based on if moving into a slanted surface
        float moveForce = MoveForce;
        IsTryingToMoveIntoWall = false;
        IsOnFlatSurface = false;
        Vector3 relativeUp = normalOfColliderMovingInto;
        bool isMovingIntoSurface = false;
        float dotWishToMoveIntoCollider = 0f;
        if (state.GetContactCount() == 0)
        {
            //In the air
            Statistic3.Text = $"In the air";
        }
        else
        {
            float surfaceMovingIntoSlant = Vector3.Up.Dot(normalOfColliderMovingInto);
            dotWishToMoveIntoCollider = wishDirection.Dot(normalOfColliderMovingInto);
            isMovingIntoSurface = dotWishToMoveIntoCollider < 0f;
            if (surfaceMovingIntoSlant < 0.75f)
            {
                Statistic3.Text = $"On a slanted surface";
                if (isMovingIntoSurface)
                {
                    //Slanted surface that we're too tired to move into
                    IsTryingToMoveIntoWall = true;
                    Statistic3.Text = $"Moving into a slanted surface";
                }
                else
                {
                    //Allow for moving off the wall
                    relativeUp = Vector3.Up;
                }
            }
            else
            {
                //Flat-ish surface
                //TODO: allow this to check for if we're landing/standing on a surface, not just moving into it willingly
                IsOnFlatSurface = true;
                Statistic3.Text = $"On a flat-ish surface";
            }
        }

        //Get direction along surface
        Vector3 moveDirection = (wishDirection - (relativeUp * wishDirection.Dot(relativeUp))).Normalized();

        //Don't allow moving up a slanted surface when tired - when trying to, instead move horizontally along it
        float dotMoveToUp = moveDirection.Dot(Vector3.Up);
        Statistic6.Text = $"dotMoveToUp: {dotMoveToUp}";
        if (IsTryingToMoveIntoWall && ClimbEnergy <= 0f && dotMoveToUp >= 0f)
        {
            // 1) Calculate the slope's "up" direction by removing the wall's normal component from global up.
            Vector3 globalUpTangentToSurface = (Vector3.Up - (Vector3.Up.Dot(relativeUp) * relativeUp)).Normalized();

            // 2) Remove the slopeUp component from moveDirection to get purely horizontal movement on the slope.
            Vector3 horizontalAlongSlope = moveDirection - globalUpTangentToSurface * moveDirection.Dot(globalUpTangentToSurface);
            moveDirection = horizontalAlongSlope.Normalized();

            //Force
            float dotWishToMovingIntoCollider = wishDirection.Dot(normalOfColliderMovingInto);
            float forceMultiplier = 1f - Mathf.Max(0f, -dotWishToMovingIntoCollider);
            moveForce *= 1f - Mathf.Max(0f, -dotWishToMovingIntoCollider);

            Statistic12.Text = $"dotWishToMovingIntoCollider: {dotWishToMovingIntoCollider}";
            Statistic13.Text = $"forceMultiplier: {forceMultiplier}";
            Statistic14.Text = $"moveForce: {moveForce}";
            Statistic15.Text = $"move direction: {moveDirection}";
        }

        //Force proportional to friction
        //float friction = PhysicsMaterialOverride != null ? PhysicsMaterialOverride.Friction : 1.0f; // Default is 1.0 if no override exists

        //Jump
        if (InputJump)
        {
            ApplyImpulse(relativeUp * 10f);
        }

        //Apply
        ApplyForce(moveDirection * moveForce);

        TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + moveDirection * 2.0f;
    }
}
