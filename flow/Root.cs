using Godot;
using System;

public partial class Root : Node
{
    [Export] public Player Player;
    [Export] private SpinBox SpinBoxUpdateRate;
    [Export] private HSlider HSliderUpdateRate;
    private bool isChangeInternalUpdateRate = false;

    //HARDWARE
    private double FPSAverageSlowPrevious = -1.0;
    private double FPSAverageSlow = -1.0;
    private int FPSUserMax = 0;
    private const int MinUpdateRate = 1;

    public override void _Ready()
    {
        //Set default update rate to the screen refresh rate - this method must be called once to display the user's refresh rate
        SetUpdateRateManually(0);

        //Starting with a restart ensures the restart sate has parity with the starting state since they become the same thing
        RestartGame();
    }

    private void RestartGame()
    {
        Player.Respawn();
    }

    public override void _PhysicsProcess(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //Slow update
        //GD.Print($"Engine.GetPhysicsFrames(): {Engine.GetPhysicsFrames()}, Engine.PhysicsTicksPerSecond: {Engine.PhysicsTicksPerSecond}");
        if
        (
            FPSUserMax <= 0
            && Time.GetTicksMsec() > 6000f //wait until the physics engine is ready
            && (
                (int)Engine.GetPhysicsFrames() % Engine.PhysicsTicksPerSecond == 0 //check every 1 second
                || FPSAverageSlowPrevious < 0.0
                || FPSAverageSlow < 0.0
            )
        )
        {
            SetUpdateRateAutomatically();
        }
    }

    private void SetUpdateRateAutomatically()
    {
        //This method likely runs in a slow update

        //Get average fps
        if (FPSAverageSlowPrevious >= 0.0) //wait until initialized
        {
            FPSAverageSlow = (FPSAverageSlowPrevious + Engine.GetFramesPerSecond()) / 2.0;
        }
        FPSAverageSlowPrevious = Engine.GetFramesPerSecond();

        //Set max physics update rate
        if (FPSAverageSlow >= 0.0) //wait until initialized
        {
            Engine.PhysicsTicksPerSecond = Mathf.Min
            (
                (int)Mathf.Max(MinUpdateRate, FPSAverageSlow),
                (int)DisplayServer.ScreenGetRefreshRate()
            );
        }
    }

    private void SetUpdateRateManually(float val)
    {
        FPSUserMax = (int)val;

        //A value of 0 indicates to automatically set the update rate
        if (FPSUserMax > 0)
        {
            //Manual update rate
            Engine.MaxFps = Mathf.Max(MinUpdateRate, FPSUserMax);
            Engine.PhysicsTicksPerSecond = Mathf.Max(MinUpdateRate, FPSUserMax);

            SpinBoxUpdateRate.Suffix = "Hz";
        }
        else
        {
            //Automatic update rate
            Engine.MaxFps = (int)Mathf.Max(MinUpdateRate, DisplayServer.ScreenGetRefreshRate());
            SpinBoxUpdateRate.Suffix = $"(Auto: {Mathf.Max(MinUpdateRate, Engine.MaxFps)} Hz)";
        }
    }

    public void OnSpinBoxUpdateRateValueChanged(float val)
    {
        if (!isChangeInternalUpdateRate)
        {
            SetUpdateRateManually(val);
            isChangeInternalUpdateRate = true;
            HSliderUpdateRate.Value = FPSUserMax;
        }
        else
        {
            isChangeInternalUpdateRate = false;
        }
    }

    public void OnHSliderUpdateRateValueChanged(float val)
    {
        if (!isChangeInternalUpdateRate)
        {
            SetUpdateRateManually(val);
            isChangeInternalUpdateRate = true;
            SpinBoxUpdateRate.Value = FPSUserMax;
        }
        else
        {
            isChangeInternalUpdateRate = false;
        }
    }
}