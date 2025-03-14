using Godot;

[Tool]
public partial class TerrainGenerator : Node3D
{
    //Complex resources
    private Material _MeshMaterial;
    [Export] public Material MeshMaterial
    { get => _MeshMaterial; set { _MeshMaterial = value; OnGenerationSettingsChanged(); } }

    private FastNoiseLite _Noise;
    [Export] public FastNoiseLite Noise
    { get => _Noise; set { _Noise = value; OnGenerationSettingsChanged(); } }

    //Values
    private Vector2 _MeshSize;
    [Export] public Vector2 MeshSize
    { get => _MeshSize; set { _MeshSize = value; OnGenerationSettingsChanged(); } }

    private float _MeshAmplitude;
    [Export] public float MeshAmplitude
    { get => _MeshAmplitude; set { _MeshAmplitude = value; OnGenerationSettingsChanged(); } }

    private int _MeshRes;
    [Export] public int MeshRes
    { get => _MeshRes; set { _MeshRes = value; OnGenerationSettingsChanged(); } }

    //Instance
    public MeshInstance3D Meshy = new();

    private void OnGenerationSettingsChanged()
    {
        GD.Print("Generation settings changed");
        Generate();
    }

    private void Generate()
    {
        //Remove old mesh
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }
        Meshy = new();

        //Protect against null/problem values
        MeshMaterial ??= new Material();
        Noise ??= new FastNoiseLite();
        if (MeshSize == Vector2.Zero){ MeshSize = Vector2.One * 2f; }
        if (MeshAmplitude == 0f) { MeshAmplitude = 1f; }
        if (MeshRes == 0) { MeshRes = 1; }



        //Mesh definfition
        PlaneMesh meshPlane = new() {
            Size = MeshSize,
            SubdivideWidth = (int)MeshSize.X,
            SubdivideDepth = (int)MeshSize.Y,
            Material = MeshMaterial,
        };

        //Mesh commitment #1
        SurfaceTool surface = new();
        MeshDataTool meshData = new();
        surface.CreateFrom(meshPlane, 0);
        ArrayMesh meshArray = surface.Commit();
        
        //Noise offset
        meshData.CreateFromSurface(meshArray, 0);

        for (int i = 0; i < meshData.GetVertexCount(); i++)
        {
            Vector3 vert = meshData.GetVertex(i);
            vert.Y = Noise.GetNoise2D(vert.X, vert.Z) * MeshAmplitude;
            //vert.Y = new RandomNumberGenerator().RandfRange(0f, 10f);
            meshData.SetVertex(i, vert);
        }

        meshArray.ClearSurfaces();
        meshData.CommitToSurface(meshArray);
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.CreateFrom(meshArray, 0);
        surface.GenerateNormals();

        //Mesh commitment #2
        Meshy.Mesh = surface.Commit();

        //Collider
        Meshy.CreateTrimeshCollision();

        //Hierachy
        AddChild(Meshy);
    }
}
