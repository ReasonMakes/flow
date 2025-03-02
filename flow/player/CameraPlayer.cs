using Godot;
using System;

public partial class CameraPlayer : Camera3D
{
    [Export] public Node3D CameraGrandparent; //Used for wallrun tilt
    [Export] public Node3D CameraParent; //Used for look
}
