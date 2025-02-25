using Godot;
using System;

public partial class Statistics : Control
{
    [Export] private Label FPS;
    [Export] public Label LabelHSpeed;
    [Export] public Label LabelClimb;
    [Export] public Label LabelJerk;
    [Export] public Label LabelDash;
    [Export] public Label LabelJumpFatigueRecency;
    [Export] public Label LabelJumpFatigueOnGround;
    
    public override void _Process(double delta)
    {
        FPS.Text = $"FPS: {Engine.GetFramesPerSecond()}";
    }
}