using Godot;

public partial class PlayerMovement : RigidBody3D
{
    //Testing
    [Export] private Label LabelSurfaceDot;
    [Export] private Label LabelColliderNormalY;
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
            CameraPlayer.Yaw.Rotation = new Vector3(
                CameraPlayer.Yaw.Rotation.X,
                CameraPlayer.Yaw.Rotation.Y - mouseMotion.Relative.X * MouseSensitivity,
                CameraPlayer.Yaw.Rotation.Z
            );

            //Pitch, clamp to straight up or down
            CameraPlayer.Pitch.Rotation = new Vector3(
                Mathf.Clamp(CameraPlayer.Pitch.Rotation.X - mouseMotion.Relative.Y * MouseSensitivity,
                    -0.24f * Mathf.Tau,
                    0.24f * Mathf.Tau
                ),
                CameraPlayer.Pitch.Rotation.Y,
                CameraPlayer.Pitch.Rotation.Z
            );
        }
    }

    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //void _integrate_forces(state: PhysicsDirectBodyState3D) virtual
        //void add_constant_central_force(force: Vector3)
        //void add_constant_force(force: Vector3, position: Vector3 = Vector3(0, 0, 0))
        //void add_constant_torque(torque: Vector3)
        //void apply_central_force(force: Vector3)
        //void apply_central_impulse(impulse: Vector3)
        //void apply_force(force: Vector3, position: Vector3 = Vector3(0, 0, 0))
        //void apply_impulse(impulse: Vector3, position: Vector3 = Vector3(0, 0, 0))
        //void apply_torque(torque: Vector3)
        //void apply_torque_impulse(impulse: Vector3)
        //Array[Node3D] get_colliding_bodies() const
        //int get_contact_count() const
        //Basis get_inverse_inertia_tensor() const
        //void set_axis_velocity(axis_velocity: Vector3)

        //Unfreeze once game started
        if (Time.GetTicksMsec() > 2000f)
        {
            Freeze = false;
        }




        //WASD Movement - relative to whichever collider we're looking at the most
        //Vector3 wishDirection = Vector3.Zero; //Can't wish for any direction unless colliding with something

        //if (GetContactCount() > 0)
        //{
        //    Godot.Collections.Array<Node3D> collidingBodies = GetCollidingBodies();
        //    for (int i = 0; i < collidingBodies.Count; i++)
        //    {
        //        //Vector3 surfaceNormal = collidingBodies[i].GetNormal();
        //
        //        Vector3 cameraVector = CameraPlayer.Yaw.GlobalRotation + CameraPlayer.Pitch.GlobalRotation;
        //        GD.Print($"Colliding with {state.}");
        //        //Colliding with 2
        //        //Contact #0: Normal=(0.7750174, 0.4058608, 0.48438105), Position=(1.4622355, -4.917383E-06, -23.537766)
        //        //Contact #1: Normal=(-0, 1, -0), Position=(1.4551114, -0.00010642409, -23.530642)
        //        //cameraVector.Dot(surfaceNormal);
        //    }
        //}

        //wishDirection = GetWishDirection(CameraPlayer.GlobalBasis);


        //ApplyForce(wishDirection * MoveForce);

        //Gravity

    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float smallestDot = 1f;
        Vector3 movementFrame = Vector3.Up;

        Vector3 lookDirection = -CameraPlayer.GlobalBasis.Z;

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
            //Vector3 surfacePosition = state.GetContactLocalPosition(i);

            //Update movement frame to be the relative to whatever of our colliders we're looking at the most
            float surfaceDot = lookDirection.Dot(surfaceNormal);
            if (surfaceDot < smallestDot)
            {
                smallestDot = surfaceDot;
                movementFrame = surfaceNormal;
            }

            //Test
            LabelSurfaceDot.Text = $"lookDirection={lookDirection}, surfaceNormal={surfaceNormal}, dot={surfaceDot}";
        }

        wishDirection -= movementFrame * wishDirection.Dot(movementFrame);
        wishDirection = wishDirection.Normalized();

        LabelColliderNormalY.Text = $"wishDirection: {wishDirection}";
        TestBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + wishDirection * 2.0f;

        ApplyForce(wishDirection * MoveForce);
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
}
