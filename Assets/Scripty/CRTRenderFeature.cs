using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CRTRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader crtShader;

        [Header("Textures")]
        public Texture2D decalTex;
        public Texture2D filmDirtTex;

        [Header("CRT Toggles")]
        public bool scanlineOnOff = true;
        public bool chromaticAberrationOnOff = true;
        public bool whiteNoiseOnOff = false;
        public bool monochormeOnOff = false;
        public bool filmDirtOnOff = false;
        public bool multipleGhostOnOff = false;
        public bool slippageOnOff = false;
        public bool letterBoxOnOff = false;

        [Header("Settings")]
        [Range(0, 0.05f)] public float chromaticAberrationStrength = 0.005f;
        public float flickeringStrength = 0.005f;
        public float flickeringCycle = 10f;
        public float slippageStrength = 0.05f;
        public float slippageInterval = 5f;
        public float slippageScrollSpeed = 2f;
        [Range(0, 1)] public float slippageNoiseOnOff = 1f;
        public float slippageSize = 5f;
        public float multipleGhostStrength = 0.02f;
        public float screenJumpLevel = 0f;
        [Range(0, 1)] public int letterBoxType = 0;
    }

    public Settings settings = new Settings();
    private CRTRenderPass crtPass;

    public override void Create()
    {
        crtPass = new CRTRenderPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings == null || settings.crtShader == null)
            return;

        crtPass.Setup(settings);
        renderer.EnqueuePass(crtPass);
    }

    protected override void Dispose(bool disposing)
    {
        crtPass?.Dispose();
    }

    class CRTRenderPass : ScriptableRenderPass
    {
        private Material crtMaterial;
        private Settings currentSettings;

        public CRTRenderPass()
        {
            this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public void Setup(Settings settings)
        {
            currentSettings = settings;

            if (settings.crtShader != null)
            {
                if (crtMaterial == null || crtMaterial.shader != settings.crtShader)
                {
                    CoreUtils.Destroy(crtMaterial);
                    crtMaterial = CoreUtils.CreateEngineMaterial(settings.crtShader);
                }
            }
            else
            {
                CoreUtils.Destroy(crtMaterial);
                crtMaterial = null;
            }
        }

        private class PassData
        {
            public Material material;
            public TextureHandle sourceTexture;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (crtMaterial == null || currentSettings == null) return;

            // --- PROPOJENÍ VŠECH HODNOT ZE SETTINGS DO MATERIALU ---
            if (currentSettings.decalTex != null) crtMaterial.SetTexture("_DecalTex", currentSettings.decalTex);
            if (currentSettings.filmDirtTex != null) crtMaterial.SetTexture("_FilmDirtTex", currentSettings.filmDirtTex);

            crtMaterial.SetInt("_ScanlineOnOff", currentSettings.scanlineOnOff ? 1 : 0);
            crtMaterial.SetInt("_ChromaticAberrationOnOff", currentSettings.chromaticAberrationOnOff ? 1 : 0);
            crtMaterial.SetInt("_WhiteNoiseOnOff", currentSettings.whiteNoiseOnOff ? 1 : 0);
            crtMaterial.SetInt("_MonochormeOnOff", currentSettings.monochormeOnOff ? 1 : 0);
            crtMaterial.SetInt("_FilmDirtOnOff", currentSettings.filmDirtOnOff ? 1 : 0);
            crtMaterial.SetInt("_MultipleGhostOnOff", currentSettings.multipleGhostOnOff ? 1 : 0);
            crtMaterial.SetInt("_SlippageOnOff", currentSettings.slippageOnOff ? 1 : 0);
            crtMaterial.SetInt("_LetterBoxOnOff", currentSettings.letterBoxOnOff ? 1 : 0);

            crtMaterial.SetFloat("_ChromaticAberrationStrength", currentSettings.chromaticAberrationStrength);
            crtMaterial.SetFloat("_FlickeringStrength", currentSettings.flickeringStrength);
            crtMaterial.SetFloat("_FlickeringCycle", currentSettings.flickeringCycle);
            crtMaterial.SetFloat("_SlippageStrength", currentSettings.slippageStrength);
            crtMaterial.SetFloat("_SlippageInterval", currentSettings.slippageInterval);
            crtMaterial.SetFloat("_SlippageScrollSpeed", currentSettings.slippageScrollSpeed);
            crtMaterial.SetFloat("_SlippageNoiseOnOff", currentSettings.slippageNoiseOnOff);
            crtMaterial.SetFloat("_SlippageSize", currentSettings.slippageSize);
            crtMaterial.SetFloat("_MultipleGhostStrength", currentSettings.multipleGhostStrength);
            crtMaterial.SetFloat("_ScreenJumpLevel", currentSettings.screenJumpLevel);
            crtMaterial.SetInt("_LetterBoxType", currentSettings.letterBoxType);
            // -----------------------------------------------------

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
                return;

            TextureHandle activeColor = resourceData.activeColorTexture;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "CRTTempTexture", false);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CRT Effect Pass", out var passData))
            {
                passData.material = crtMaterial;
                passData.sourceTexture = activeColor;

                builder.UseTexture(passData.sourceTexture, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.sourceTexture, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("CRT Copy Back Pass", out var passData))
            {
                passData.sourceTexture = tempTexture;

                builder.UseTexture(passData.sourceTexture, AccessFlags.Read);
                builder.SetRenderAttachment(activeColor, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.sourceTexture, new Vector4(1, 1, 0, 0), 0.0f, false);
                });
            }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(crtMaterial);
        }
    }
}