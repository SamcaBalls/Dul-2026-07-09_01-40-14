using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CRTController : MonoBehaviour
{
    private Volume volume;
    private CRTEffectVolume crtVolumeSettings;

    // Odkaz na tvùj URP Pipeline Asset, kde máš nastavený Renderer Feature
    public UniversalRenderPipelineAsset pipelineAsset;

    void Start()
    {
        volume = GetComponent<Volume>();
        if (volume == null) volume = FindFirstObjectByType<Volume>();

        if (volume != null && volume.profile != null)
        {
            volume.profile.TryGet(out crtVolumeSettings);
        }
    }

    void Update()
    {
        if (crtVolumeSettings == null || !crtVolumeSettings.IsActive()) return;

        // Najdeme náš CRT Render Feature v aktivním URP rendereru
        var rendererData = UniversalRenderPipeline.asset.GetRenderer(0); // nebo index tvého rendereru
        // (Pøípadnì mùžeme materiál vytáhnout pøímo, ale nejjednodušší je pøímá modifikace, pokud máš referenci)
    }
}