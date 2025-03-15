using Godot;

[Tool]
public partial class TerrainGenerator : Node3D
{
    //Dynamic
    private Material _MeshMaterial;
    [Export] public Material MeshMaterial
    { get => _MeshMaterial; set { _MeshMaterial = value; OnGenerationSettingsChanged(); } }
    
    private FastNoiseLite _Noise;
    [Export] public FastNoiseLite Noise
    { get => _Noise; set { _Noise = value; OnGenerationSettingsChanged(); } }

    //Values
    private Vector2 _MeshSize = new(100f, 100f);
    [Export] public Vector2 MeshSize
    { get => _MeshSize; set { _MeshSize = value; OnGenerationSettingsChanged(); } }
    
    private float _MeshAmplitude = 10f;
    [Export] public float MeshAmplitude
    { get => _MeshAmplitude; set { _MeshAmplitude = value; OnGenerationSettingsChanged(); } }
    
    private int _MeshRes = 2;
    [Export] public int MeshRes
    { get => _MeshRes; set { _MeshRes = value; OnGenerationSettingsChanged(); } }
    
    //Static
    private MeshInstance3D Meshy = new();
    [Export] private Material MeshMaterialError;
    
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
        MeshMaterialError ??= new StandardMaterial3D() { AlbedoColor = new Color(1f, 0f, 0f) };
    
    
    
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
    
        //Slope detection
        for (int i = 0; i < meshData.GetFaceCount(); i++)
        {
            if (meshData.GetFaceNormal(i).Dot(Vector3.Up) < PlayerMovement.SlopeDotUp)
            {
                surface.SetMaterial(MeshMaterialError);
            }
        }

        //int probesCount = 0;
        //int probesCountMax = 20;
        //bool isProbesCountMaxedOut = false;
        //for (int i = 0; i < meshData.GetFaceCount(); i++)
        //{
        //    if (meshData.GetFaceNormal(i).Dot(Vector3.Up) < PlayerMovement.SlopeDotUp)
        //    {
        //        probesCount++;
        //
        //        //Set the entire terrain material to the error material
        //        //surface.SetMaterial(MeshMaterialError);
        //
        //        //Get the centre position of the face
        //        Vector3 pos1 = meshData.GetVertex(meshData.GetFaceVertex(i, 0));
        //        Vector3 pos2 = meshData.GetVertex(meshData.GetFaceVertex(i, 1));
        //        Vector3 pos3 = meshData.GetVertex(meshData.GetFaceVertex(i, 2));
        //        Vector3 faceCenter = (pos1 + pos2 + pos3) / 3.0f;
        //
        //        //Instantiate an error probe where the offending face was detected
        //        SphereMesh sphereMesh = new()
        //        {
        //            Radius = 1.0f,
        //            Height = 2.0f,
        //            RadialSegments = 32,
        //            Rings = 16,
        //            Material = MeshMaterialError
        //        };
        //        AddChild(new MeshInstance3D() { Mesh = sphereMesh, GlobalPosition = faceCenter });
        //
        //        //Stop checking after a certain point to avoid crashes
        //        if (probesCount >= probesCountMax)
        //        {
        //            MeshMaterial = MeshMaterialError;
        //            isProbesCountMaxedOut = true;
        //        }
        //
        //        if (isProbesCountMaxedOut) break;
        //    }
        //
        //    if (isProbesCountMaxedOut) break;
        //}
    
        //Mesh commitment #2
        Meshy.Mesh = surface.Commit();
    
        //Collider
        Meshy.CreateTrimeshCollision();
    
        //Hierachy
        AddChild(Meshy);
    }
}
