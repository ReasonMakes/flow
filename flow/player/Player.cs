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

    public void Respawn()
    {
        PlayerMovement.Velocity = Vector3.Zero;
        PlayerMovement.GlobalPosition = GlobalPosition;

        HUD.LabelDead.Visible = false;

        IsAlive = true;
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