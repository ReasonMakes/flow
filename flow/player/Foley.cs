using Godot;
using System;

public partial class Foley : Node
{
    [Export] public AudioStreamPlayer AudioFootstep;
    public float RunAudioTimer = 0f; //no touchy :)
    [Export(PropertyHint.Range, "0,10,")] public float RunAudioTimerPeriod = 0.25f; //time in seconds before another footstep sound can be played

    [Export] public AudioStreamPlayer AudioWallrun;
    [Export] public float AudioWallrunVolume = -10f;
    [Export] public float AudioWallrunVolumeFadeOutRate = 100f; //0 to +inf. Larger values mean fade out faster. Decibels subtracted/second
    [Export] public AudioStreamPlayer AudioClimb;

    [Export] public AudioStreamPlayer AudioJump;
    [Export] public AudioStreamPlayer AudioLand;

    [Export] public AudioStreamPlayer AudioSlide;
    [Export] public float AudioSlideVolume = -16f;
    [Export] public float AudioSlideVolumeFadeOutRate = 100f; //0 to +inf. Larger values mean fade out faster. Decibels subtracted/second

    [Export] public AudioStreamPlayer AudioDash;
}