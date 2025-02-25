using Godot;
using System;

public partial class HUD : Node
{
    [Export] public Label LabelDead;

    [Export] public ColorRect RectDash;
    [Export] public ColorRect RectDashCooldown;
}
