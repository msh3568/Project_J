using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class GlitchFullScreenFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;
        public bool forceDisableFeature = false;
    }

    [SerializeField] private Settings settings = new Settings();
    private GlitchPass pass;
    public static bool ForceDisable = false;

    public override void Create()
    {
        pass = new GlitchPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (ForceDisable || settings.forceDisableFeature)
            return;

        if (settings.material == null)
            return;

        if (renderingData.cameraData.cameraType == CameraType.SceneView)
            return;

        if (renderingData.cameraData.isPreviewCamera)
            return;

        float tvPower = Shader.GetGlobalFloat("_TVPower");
        float glitchStrength = Shader.GetGlobalFloat("_GlitchStrength");
        if (Mathf.Abs(tvPower - 1f) <= 0.0001f && glitchStrength <= 0.0001f)
            return;

        pass.Setup(renderer);
        renderer.EnqueuePass(pass);
    }

    private class GlitchPass : ScriptableRenderPass
    {
        private const string PassName = "GlitchFullScreen";
        private readonly Settings settings;
        private ScriptableRenderer renderer;
        private RTHandle tempTexture;

        public GlitchPass(Settings settings)
        {
            this.settings = settings;
            renderPassEvent = settings.passEvent;
        }

        public void Setup(ScriptableRenderer renderer)
        {
            this.renderer = renderer;
            requiresIntermediateTexture = true;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref tempTexture, descriptor, name: "_GlitchTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.material == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("GlitchFullScreen");
            RTHandle source = renderer.cameraColorTargetHandle;
            Blitter.BlitCameraTexture(cmd, source, tempTexture, settings.material, 0);
            Blitter.BlitCameraTexture(cmd, tempTexture, source);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (settings.material == null)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError($"Skipping render pass. {PassName} requires an intermediate ColorTexture.");
                return;
            }

            var source = resourceData.activeColorTexture;
            var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = $"{PassName}_Temp";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);
            RenderGraphUtils.BlitMaterialParameters blitParams = new(source, destination, settings.material, 0);
            renderGraph.AddBlitPass(blitParams, passName: PassName);
            resourceData.cameraColor = destination;
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (tempTexture != null)
                tempTexture.Release();
        }
    }
}
