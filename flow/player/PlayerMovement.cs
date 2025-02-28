using Godot;

public partial class PlayerMovement : RigidBody3D
{
    //Testing
    [Export] private Label Statistic1;
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
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float smallestDot = 1f;
        Vector3 relativeUp = Vector3.Up; //default up is global up

        //WASD - This is not normalized!
        Vector3 wishDirection = Vector3.Zero;
        if (InputRunForward) wishDirection -= CameraPlayer.GlobalBasis.Z;
        if (InputRunLeft) wishDirection -= CameraPlayer.GlobalBasis.X;
        if (InputRunRight) wishDirection += CameraPlayer.GlobalBasis.X;
        if (InputRunBack) wishDirection += CameraPlayer.GlobalBasis.Z;
        
        // Loop over each contact
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            Vector3 surfaceNormal = state.GetContactLocalNormal(i);
            Statistic1.Text = $"surfaceNormal: {surfaceNormal}";

            //Update movement frame to be the relative to whatever of the colliders we're moving into that looking at the most (whatever has the smallest dot product)\
            //1. Are we moving into this collider?
            //wishDirection = wishDirection.Normalized();
            float wishMoveToSurfaceDot = wishDirection.Dot(surfaceNormal);
            Statistic2.Text = $"wishMoveToSurfaceDot: {wishMoveToSurfaceDot}";
            if (wishMoveToSurfaceDot < 0f)
            {
                Statistic2.Text += " (moving into this collider)";

                //2. Are we looking at this collider the most?
                float lookToSurfaceDot = -CameraPlayer.GlobalBasis.Z.Dot(surfaceNormal);
                Statistic3.Text = $"lookToSurfaceDot: {lookToSurfaceDot}";

                if (lookToSurfaceDot < smallestDot)
                {
                    Statistic3.Text += " (looking at this collider the most)";
                    smallestDot = lookToSurfaceDot;
                    relativeUp = surfaceNormal;
                }
            }
        }

        Vector3 moveDirection = wishDirection;

        //Move along surface
        //If this surface is inclined enough, then we're climbing/wallrunning, and can only do this if we have wallEnergy. Otherwise just move along Up?
        moveDirection -= relativeUp * wishDirection.Dot(relativeUp);

        moveDirection = moveDirection.Normalized(); //is this necessary?

        Statistic4.Text = $"moveDirection: {moveDirection}";
        TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + moveDirection * 2.0f;

        ApplyForce(moveDirection * MoveForce);
    }
}
