using Godot;

[Tool]
[GlobalClass]
public partial class TerrainGeneratorResource : Resource
{
    [Export] public Material MeshMaterial;
    [Export] public FastNoiseLite Noise;

    public MeshInstance3D Meshy = new();
    [Export] public Vector2 MeshSize;
    [Export] public float MeshAmplitude;
    [Export] public int MeshRes;
}