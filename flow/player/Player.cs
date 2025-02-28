using Godot;
using System;

public partial class Player : Node3D
{
    public bool IsAlive = true;

    [Export] public PlayerMovement PlayerMovement;
    [Export] public Statistics Statistics;
    [Export] public HUD HUD;
    [Export] public ScreenEffects ScreenEffects;
    [Export] public Foley Foley;
    [Export] private Label LabelTimer;
    private float TimeAtLifeStart;

    public override void _Process(double deltaDouble)
    {
        float delta = (float)deltaDouble;

        //Alive Timer
        float timeDeltaMsTotal = (Time.GetTicksMsec() - TimeAtLifeStart);
        //float timeDeltaMs = timeDeltaMsTotal % 1000f;
        float timeDeltaCentiSeconds = (timeDeltaMsTotal / 10f) % 100f;
        float timeDeltaSeconds = (timeDeltaMsTotal / 1000f) % 60f;
        float timeDeltaMinutes = (timeDeltaMsTotal / 60000f) % 60f;

        int timeDeltaCentiSecondsInt = (int)timeDeltaCentiSeconds;
        int timeDeltaSecondsInt = (int)timeDeltaSeconds;
        int timeDeltaMinutesInt = (int)timeDeltaMinutes;

        LabelTimer.Text = $"{timeDeltaMinutesInt:D2}:{timeDeltaSecondsInt:D2}.{timeDeltaCentiSecondsInt:D2}";
    }

    public void Respawn()
    {
        //PlayerMovement.Velocity = Vector3.Zero;
        PlayerMovement.GlobalPosition = GlobalPosition;

        HUD.LabelDead.Visible = false;

        IsAlive = true;

        TimeAtLifeStart = Time.GetTicksMsec();
    }

    public void Kill(string cause)
    {
        if (IsAlive)
        {
            //SLIDING WHEN DEAD IS HARD-CODED IN.

            //Message
            HUD.LabelDead.Visible = true;
            HUD.LabelDead.Text = $"You were killed {cause}\nPress [{GetKeybindText("restart", "Enter")}] to restart";

            IsAlive = false;
        }
    }

    private string GetKeybindText(string keybindCode, string keybindDefault)
    {
        //This doesn't work :) Always returns default keybind

        string keybind = keybindDefault;
        if (InputMap.ActionGetEvents(keybindCode).Count >= 2)
        {
            keybind = "" + InputMap.ActionGetEvents(keybindCode)[1];
        }
        else
        {
            //GD.Print($"Error: couldn't get the keybind... Defaulting to [{keybind}]");
        }

        return keybind;
    }
}