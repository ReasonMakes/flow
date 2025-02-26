using Godot;
using System;

public partial class Menu : Control
{
    [Export] private Button ButtonQuit;
    [Export] private DirectionalLight3D sun;

    public override void _Ready()
    {
        Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;

        //Update default volume
        OnHSliderMainValueChanged(1f);
    }

    public override void _Input(InputEvent @event)
    {
        if (Input.IsActionJustPressed("escape"))
        {
            //Show/hide cursor
            if (Input.MouseMode == Input.MouseModeEnum.Visible)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
            else
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }

            //Toggle menu display
            Visible = !Visible;
        }

        
    }

    public void OnButtonQuitPressed()
    {
        GetTree().Quit();
    }

    public void OnHSliderMainValueChanged(float linearVal)
    {
        GD.Print($"Changed main: {linearVal}");
        int bus = AudioServer.GetBusIndex("Master");
        float dB = Mathf.LinearToDb(linearVal * 0.2f); //hardcoded multiplier
        AudioServer.SetBusVolumeDb(bus, dB);
    }

    public void OnHSliderFoleyValueChanged(float linearVal)
    {
        GD.Print($"Changed foley: {linearVal}");
        int bus = AudioServer.GetBusIndex("Foley");
        float dB = Mathf.LinearToDb(linearVal);
        AudioServer.SetBusVolumeDb(bus, dB);
    }

    public void OnHSliderMusicValueChanged(float linearVal)
    {
        GD.Print($"Changed music: {linearVal}");
        int bus = AudioServer.GetBusIndex("Music");
        float dB = Mathf.LinearToDb(linearVal);
        AudioServer.SetBusVolumeDb(bus, dB);
    }

    public void OnCheckBoxAntiAliasingToggled(bool isOn)
    {
        if (isOn)
        {
            GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        }
        else
        {
            GetViewport().Msaa3D = Viewport.Msaa.Disabled;
        }
    }

    //public void OnHSliderShadowSizeValueChanged(float valRoundedToInt)
    //{
    //    //TODO: this doesn't actually update the rendering even though the actual setting is being updated.
    //    //2^8 is the minimum, 2^12 is default, and max is 2^14
    //
    //    int size = (int)Mathf.Pow(2, valRoundedToInt);
    //    
    //    ProjectSettings.SetSetting("rendering/lights_and_shadows/directional_shadow/size", size);
    //    ProjectSettings.Save();
    //}
}