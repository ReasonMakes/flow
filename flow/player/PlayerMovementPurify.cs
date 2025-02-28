using Godot;

public partial class PlayerMovementPurify : CharacterBody3D
{
    [Export] public CameraPlayer CameraPlayer;
    [Export] private Player Player;

    [Export] private CsgBox3D TestVectorBox;
    [Export] private Label LabelSurfaceDot;
    [Export] private Label LabelCeilingNormalY;

    public float MouseSensitivity = 0.001f;

    //RUN
    private bool InputRunForward = false;
    private bool InputRunLeft = false;
    private bool InputRunRight = false;
    private bool InputRunBack = false;

    private const float PlayerMoveAcceleration = 0.1f;
    private const float PlayerMoveAccelerationAirCoefficient = 0.4f;

    private const float Drag = 20f; //Higher values are higher drag
    private const float DragAirCoefficient = 0.01f;


    public override void _Input(InputEvent @event)
    {
        //Run Direction
        InputRunForward = Input.IsActionPressed("move_run_dir_forward");
        InputRunLeft = Input.IsActionPressed("move_run_dir_left");
        InputRunRight = Input.IsActionPressed("move_run_dir_right");
        InputRunBack = Input.IsActionPressed("move_run_dir_back");

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
                CameraPlayer.Rotation.X - mouseMotion.Relative.Y * MouseSensitivity,
                //Mathf.Clamp(CameraPlayer.Rotation.X - mouseMotion.Relative.Y * MouseSensitivity,
                //    -0.25f * Mathf.Tau,
                //    0.25f * Mathf.Tau
                //),
                CameraPlayer.Rotation.Y,
                CameraPlayer.Rotation.Z
            );
        }
    }

    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //ACCELERATION/TIME
        //Velocity += ProcessGravityAndGetVector(delta);
        Velocity += ProcessMovementAndGetVector(delta);

        //Redirect when moving into a collider
        int collisionCount = GetSlideCollisionCount();
        if (collisionCount > 0)
        {
            //Get normal
            for (int i = 0; i < collisionCount; i++)
            {
                KinematicCollision3D collision = GetSlideCollision(i);
                Vector3 surfaceNormal = collision.GetNormal();

                float dot = Velocity.Dot(surfaceNormal);
                if (dot < 0f) //Negative dot means we're moving towards this surface
                {
                    //Redirect
                    Velocity -= surfaceNormal * dot;
                }

                //Test
                LabelSurfaceDot.Text = $"Surface dot product: {dot}";
                LabelCeilingNormalY.Text = $"collisionNormal.Y: {surfaceNormal.Y}";
                if (surfaceNormal == Vector3.Zero) GD.Print("Zero-normal surface!");
            }
        }
        
        //APPLY
        MoveAndSlide();
    }

    private Vector3 ProcessMovementAndGetVector(float delta)
    {
        return GetWishDirection(CameraPlayer.GlobalBasis) * PlayerMoveAcceleration;
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

    private Vector3 ProcessGravityAndGetVector(float delta)
    {
        bool isTesting = true;

        Vector3 gravityVector = GetGravity();

        //If we're on a floor or a wall, the gravity vector may be pointing into a collider
        if (IsOnFloor() || IsOnWall())
        {
            //Get the floor normal (may be slanted)
            Vector3 surfaceNormal = IsOnFloor() ? GetFloorNormal(): GetWallNormal();

            //Calculate direction along the floor
            gravityVector -= surfaceNormal * gravityVector.Dot(surfaceNormal);
        }

        //If testing, show the vector with a CSG Box
        if (isTesting) TestVectorBox.GlobalPosition = CameraPlayer.GlobalTransform.Origin + gravityVector.Normalized() * 2.0f;

        return gravityVector;
    }
}