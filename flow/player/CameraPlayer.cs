using Godot;
using System;

public partial class CameraPlayer : Camera3D
{
    [Export] public Node3D CameraGrandparent;
    [Export] public Node3D CameraParent;
}
