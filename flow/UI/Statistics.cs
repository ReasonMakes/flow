using Godot;
using System;

public partial class Statistics : Control
{
    [Export] private Label FPS;
    [Export] public Label Statistic2;
    [Export] public Label Statistic3;
    [Export] public Label Statistic4;
    [Export] public Label Statistic5;
    [Export] public Label Statistic6;
    [Export] public Label Statistic7;
    [Export] public Label Statistic8;
    [Export] public Label Statistic9;
    [Export] public Label Statistic10;
    [Export] public Label Statistic11;
    [Export] public Label Statistic12;
    [Export] public Label Statistic13;
    [Export] public Label Statistic14;
    [Export] public Label Statistic15;

    public override void _Process(double delta)
    {
        FPS.Text = $"FPS: {Engine.GetFramesPerSecond()}";
    }
}