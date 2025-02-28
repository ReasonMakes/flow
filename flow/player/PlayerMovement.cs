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
    [Export] private CsgBox3D TestBox;

    //Look
    [Export] public CameraPlayer CameraPlayer;
    public float MouseSensitivity = 0.001f;

    //Move
    private bool InputRunForward = false;
    private bool InputRunLeft = false;
    private bool InputRunRight = false;
    private bool InputRunBack = false;

    private const float MoveForce = 100f;

    //Wall/ceiling movement
    private bool IsOnFlatSurface = false;
    private bool IsTryingToMoveOnWall = false;
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
        else if (IsTryingToMoveOnWall)
        {
            ClimbEnergy = Mathf.Max(0f, ClimbEnergy - (delta * ClimbTireRate));
        }
        Statistic4.Text = $"IsOnFlatSurface: {IsOnFlatSurface}";
        Statistic5.Text = $"IsTryingToMoveOnWall: {IsTryingToMoveOnWall}";
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        //Desired behaviours:
        //Know if we're on any flat surface
        //Know if we're trying to climb
        //Walk along a flat surface - any collider with a Vector3.Up.Dot(surfaceNormal) >= 0.75f
        //Climb a slanted surface - if there's a collider with a Vector3.Up.Dot(surfaceNormal) < 0.75f, and it is the collider that we're looking at the most, and we have climb energy - takes priority over walking along flat surface

        //WASD - This is not normalized!
        Vector3 wishDirection = Vector3.Zero;
        if (InputRunForward) wishDirection -= CameraPlayer.GlobalBasis.Z;
        if (InputRunLeft) wishDirection -= CameraPlayer.GlobalBasis.X;
        if (InputRunRight) wishDirection += CameraPlayer.GlobalBasis.X;
        if (InputRunBack) wishDirection += CameraPlayer.GlobalBasis.Z;

        // Loop over each contact
        float smallestDot = 1f;
        Vector3 normalOfColliderLookingAt = Vector3.Up;
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            Vector3 surfaceNormal = state.GetContactLocalNormal(i);
            float lookToSurfaceDot = -CameraPlayer.GlobalBasis.Z.Dot(surfaceNormal);

            //Find the collider we're looking at the most
            if (lookToSurfaceDot < smallestDot)
            {
                smallestDot = lookToSurfaceDot;
                normalOfColliderLookingAt = surfaceNormal;
            }
        }
        Statistic2.Text = $"normalOfColliderLookingAt: {normalOfColliderLookingAt}";

        //Adjust movement based on if moving into a slanted surface
        float moveForce = MoveForce;
        IsTryingToMoveOnWall = false;
        IsOnFlatSurface = false;
        Vector3 relativeUp = normalOfColliderLookingAt;
        bool isMovingIntoSurface = false;
        float dotWishToCollider = 0f;
        if (state.GetContactCount() == 0)
        {
            //In the air
            Statistic3.Text = $"In the air";
        }
        else
        {
            float surfaceSlant = Vector3.Up.Dot(normalOfColliderLookingAt);
            dotWishToCollider = wishDirection.Dot(normalOfColliderLookingAt);
            isMovingIntoSurface = dotWishToCollider < 0f;
            if (surfaceSlant < 0.75f)
            {
                if (isMovingIntoSurface)
                {
                    //Slanted surface that we're too tired to move into
                    IsTryingToMoveOnWall = true;
                    Statistic3.Text = $"Colliding and looking at a slanted surface; ClimbEnergy={ClimbEnergy}";

                    if (ClimbEnergy <= 0f)
                    {
                        //Allow moving only by the amount not moving into the wall
                        //Vector3 lookDirectionAlongWall = wishDirection - (wishDirection.Dot(normalOfColliderLookingAt) * normalOfColliderLookingAt);
                        //Vector3 horizontalDirection = lookDirectionAlongWall - (lookDirectionAlongWall.Dot(Vector3.Up) * Vector3.Up);


                        //float clampedDotToColliderComplement = 1f - Mathf.Max(0f, -dotToCollider);
                        //moveForce *= clampedDotToColliderComplement;
                        //
                        //Statistic3.Text += "; tired";
                        //Statistic6.Text = $"dotToCollider: {clampedDotToColliderComplement}";
                        //Statistic7.Text = $"moveForce: {moveForce}";
                    }
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
                IsOnFlatSurface = true;
                Statistic3.Text = $"On a flat-ish surface";
            }
        }

        //Get direction along surface
        Vector3 moveDirection = (wishDirection - (relativeUp * wishDirection.Dot(relativeUp))).Normalized();

        //Don't allow moving up a slanted surface when tired - when trying to, instead move horizontally along it
        float dotMoveToUp = moveDirection.Dot(Vector3.Up);
        Statistic7.Text = $"dotMoveToUp: {dotMoveToUp}";
        if (IsTryingToMoveOnWall && ClimbEnergy <= 0f && isMovingIntoSurface && dotMoveToUp >= 0f)
        {
            //Direction
            Vector3 lookDirectionAlongWall = wishDirection - (wishDirection.Dot(normalOfColliderLookingAt) * normalOfColliderLookingAt);
            //Horizontal
            moveDirection = lookDirectionAlongWall - (lookDirectionAlongWall.Dot(Vector3.Up) * Vector3.Up);

            //This allows for a little curved area near the player
            //Vector3 moveDirection = moveDirection - (moveDirection.Dot(Vector3.Up) * Vector3.Up);

            //Force
            moveForce *= 1f - Mathf.Max(0f, -dotWishToCollider);
        }
        
        //Apply
        ApplyForce(moveDirection * moveForce);

        TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + moveDirection * 2.0f;
    }

    //public void Old_IntegrateForces(PhysicsDirectBodyState3D state)
    //{
    //    //Default values
    //    float smallestDot = 1f;
    //    Vector3 relativeUp = Vector3.Up;
    //    Vector3 surfaceNormal = Vector3.Up;
    //    IsOnFlatSurface = false;
    //    IsTryingToMoveOnWall = false;
    //    float moveForce = MoveForce;
    //
    //    //WASD - This is not normalized!
    //    Vector3 wishDirection = Vector3.Zero;
    //    if (InputRunForward) wishDirection -= CameraPlayer.GlobalBasis.Z;
    //    if (InputRunLeft) wishDirection -= CameraPlayer.GlobalBasis.X;
    //    if (InputRunRight) wishDirection += CameraPlayer.GlobalBasis.X;
    //    if (InputRunBack) wishDirection += CameraPlayer.GlobalBasis.Z;
    //    
    //    // Loop over each contact
    //    for (int i = 0; i < state.GetContactCount(); i++)
    //    {
    //        surfaceNormal = state.GetContactLocalNormal(i);
    //        Statistic1.Text = $"surfaceNormal: {surfaceNormal}";
    //
    //        //ONE of these colliders is a flat surface
    //        float surfaceAnySlant = Vector3.Up.Dot(surfaceNormal);
    //        Statistic7.Text = $"IsOnFlatSurface: {IsOnFlatSurface} (surfaceAnySlant: {surfaceAnySlant})";
    //        if (surfaceAnySlant >= 0.75f)
    //        {
    //            IsOnFlatSurface = true;
    //        }
    //
    //        //Update movement frame to be the relative to whatever of the colliders we're moving into that looking at the most (whatever has the smallest dot product)\
    //        //1. Are we moving into this collider?
    //        bool isMovingIntoSurface = wishDirection.Dot(surfaceNormal) < 0f;
    //        Statistic2.Text = $"isMovingIntoSurface: {isMovingIntoSurface}";
    //        if (isMovingIntoSurface)
    //        {
    //            Statistic2.Text += " (moving into this collider)";
    //
    //            //2. Are we looking at this collider the most?
    //            float lookToSurfaceDot = -CameraPlayer.GlobalBasis.Z.Dot(surfaceNormal);
    //            Statistic3.Text = $"lookToSurfaceDot: {lookToSurfaceDot}";
    //
    //            if (lookToSurfaceDot < smallestDot)
    //            {
    //                Statistic3.Text += " (looking at this collider the most)";
    //                smallestDot = lookToSurfaceDot;
    //                relativeUp = surfaceNormal;
    //
    //                //CLIMBING/WALLRUNNING
    //                //If THIS PARTICULAR surface is inclined enough, then we're climbing/wallrunning, and can only do this if we have wallEnergy
    //                float surfaceMovingIntoSlant = Vector3.Up.Dot(surfaceNormal);
    //                Statistic4.Text = $"surfaceMovingIntoSlant: {surfaceMovingIntoSlant}";
    //                if (surfaceMovingIntoSlant < 0.75f) //45 degrees
    //                {
    //                    //Slanted surface
    //                    IsTryingToMoveOnWall = true;
    //
    //                    //Out of climb energy
    //                    if (ClimbEnergy <= 0f)
    //                    {
    //                        //Reduce movement force
    //                        moveForce = 0f;
    //                    }
    //                }
    //            }
    //        }
    //    }
    //
    //    Vector3 moveDirection = wishDirection;
    //
    //
    //    
    //    //TODO: allow for running down hills without running into the air
    //
    //
    //    //Move along surface
    //    moveDirection -= relativeUp * wishDirection.Dot(relativeUp);
    //    moveDirection = moveDirection.Normalized();
    //
    //    Statistic5.Text = $"moveDirection: {moveDirection}";
    //    TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + moveDirection * 2.0f;
    //
    //    Statistic6.Text = $"moveForce: {moveForce}";
    //
    //    ApplyForce(moveDirection * moveForce);
    //}
}
