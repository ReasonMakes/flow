using Godot;
using System;
using System.Globalization;

public partial class Menu : Control
{
    [Export] private Button ButtonQuit;

    [Export] PlayerMovement PlayerMovement;
    [Export] SpinBox SpinBoxSensitivity;
    [Export] HSlider HSliderSensitivity;
    private bool isChangeInternalSensitivity = false;

    [Export] private SpinBox SpinBoxMainVolume;
    [Export] private HSlider HSliderMainVolume;
    private bool isChangeInternalMainVolume = false;

    [Export] private SpinBox SpinBoxFoleyVolume;
    [Export] private HSlider HSliderFoleyVolume;
    private bool isChangeInternalFoleyVolume = false;

    [Export] private SpinBox SpinBoxMusicVolume;
    [Export] private HSlider HSliderMusicVolume;
    private bool isChangeInternalMusicVolume = false;

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

    //Main
    public void OnHSliderMainValueChanged(float linearVal)
    {
        if (!isChangeInternalMainVolume)
        {
            SetAudioBusVolume("Master", linearVal * 0.2f); //hardcoded multiplier because otherwise volume is way too loud
            isChangeInternalMainVolume = true;
            SpinBoxMainVolume.Value = linearVal;
        }
        else
        {
            isChangeInternalMainVolume = false;
        }
    }

    public void OnSpinboxMainValueChanged(float linearVal)
    {
        if (!isChangeInternalMainVolume)
        {
            SetAudioBusVolume("Master", linearVal * 0.2f); //hardcoded multiplier because otherwise volume is way too loud
            isChangeInternalMainVolume = true;
            HSliderMainVolume.Value = linearVal;
        }
        else
        {
            isChangeInternalMainVolume = false;
        }
    }

    //Foley
    public void OnHSliderFoleyValueChanged(float linearVal)
    {
        if (!isChangeInternalFoleyVolume)
        {
            SetAudioBusVolume("Foley", linearVal);
            isChangeInternalFoleyVolume = true;
            SpinBoxFoleyVolume.Value = linearVal;
        }
        else
        {
            isChangeInternalFoleyVolume = false;
        }
    }

    public void OnSpinboxFoleyValueChanged(float linearVal)
    {
        if (!isChangeInternalFoleyVolume)
        {
            SetAudioBusVolume("Foley", linearVal);
            isChangeInternalFoleyVolume = true;
            HSliderFoleyVolume.Value = linearVal;
        }
        else
        {
            isChangeInternalFoleyVolume = false;
        }
    }

    //Music
    public void OnHSliderMusicValueChanged(float linearVal)
    {
        if (!isChangeInternalMusicVolume)
        {
            SetAudioBusVolume("Music", linearVal);
            isChangeInternalMusicVolume = true;
            SpinBoxMusicVolume.Value = linearVal;
        }
        else
        {
            isChangeInternalMusicVolume = false;
        }
    }

    public void OnSpinboxMusicValueChanged(float linearVal)
    {
        if (!isChangeInternalMusicVolume)
        {
            SetAudioBusVolume("Music", linearVal);
            isChangeInternalMusicVolume = true;
            HSliderMusicVolume.Value = linearVal;
        }
        else
        {
            isChangeInternalMusicVolume = false;
        }
    }

    private void SetAudioBusVolume(string busName, float busVolumeLinear)
    {
        GD.Print($"Changed {busName}: {busVolumeLinear}");
        int busIndex = AudioServer.GetBusIndex(busName);
        float busVolumeDb = Mathf.LinearToDb(busVolumeLinear);
        AudioServer.SetBusVolumeDb(busIndex, busVolumeDb);
    }

    //Anti-aliasing
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

    //Look sensitivity
    public void OnSpinBoxSensitivityValueChanged(float val)
    {
        if (!isChangeInternalSensitivity)
        {
            PlayerMovement.MouseSensitivity = val / 1000f;
            isChangeInternalSensitivity = true;
            HSliderSensitivity.Value = PlayerMovement.MouseSensitivity * 1000f;
        }
        else
        {
            isChangeInternalSensitivity = false;
        }
    }

    public void OnHSliderSensitivityValueChanged(float val)
    {
        if (!isChangeInternalSensitivity)
        {
            PlayerMovement.MouseSensitivity = val / 1000f;
            isChangeInternalSensitivity = true;
            SpinBoxSensitivity.Value = PlayerMovement.MouseSensitivity * 1000f;
        }
        else
        {
            isChangeInternalSensitivity = false;
        }
    }
}