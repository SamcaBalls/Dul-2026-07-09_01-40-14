using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenuForRenderPipeline("Custom/Simple CRT", typeof(UniversalRenderPipeline))]
public class CRTEffectVolume : VolumeComponent, IPostProcessComponent
{
    public BoolParameter isActive = new BoolParameter(false);

    [Header("Glitch & Distortion")]
    public ClampedFloatParameter chromaticAberration = new ClampedFloatParameter(0f, 0f, 0.05f);
    public ClampedFloatParameter screenJump = new ClampedFloatParameter(0f, 0f, 1f);
    public ClampedFloatParameter flickering = new ClampedFloatParameter(0f, 0f, 0.1f);

    [Header("Effects Toggles")]
    public BoolParameter scanlines = new BoolParameter(true);
    public BoolParameter whiteNoise = new BoolParameter(false);
    public BoolParameter monochrome = new BoolParameter(false);
    public BoolParameter filmDirt = new BoolParameter(false);
    public BoolParameter multipleGhost = new BoolParameter(false);

    public bool IsActive() => isActive.value && (chromaticAberration.value > 0 || screenJump.value > 0 || flickering.value > 0 || scanlines.value || whiteNoise.value || monochrome.value || filmDirt.value || multipleGhost.value);
    public bool IsTileCompatible() => false;
}