using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProfilingScope = UnityEngine.Rendering.ProfilingScope;

namespace VicotSoft.OutlineFeature
{

public class OutlineFeature : ScriptableRendererFeature
{
    private class CustomRenderPass : ScriptableRenderPass
    {
        private static readonly int MainTexId       = Shader.PropertyToID("_MainTex");
        private static readonly int MaskTexId       = Shader.PropertyToID("_MaskTex");
        private static readonly int TempTexId       = Shader.PropertyToID("_TempTex");
        private static readonly int Temp2TexId      = Shader.PropertyToID("_Temp2Tex");
        private static readonly int HiddenMaskTexId = Shader.PropertyToID("_HiddenMaskTex");
        private static readonly int HiddenTexId     = Shader.PropertyToID("_HiddenTex");

        private static readonly int DepthTexId = Shader.PropertyToID("_CameraDepthTexture");

        private static readonly int OutlineSizeId = Shader.PropertyToID("_SizeId");
        private static readonly int ColorId       = Shader.PropertyToID("_OutlineColor");
        private static readonly int AlphaCutoffId = Shader.PropertyToID("_Cutoff");

        private const string ProfilerTagLayer        = "Outline Pass - Layers";
        private const string ProfilerTagObjects      = "Outline Pass - per Object";
        private const string ProfilerTagRenderObject = "Render object";
        private const string ProfilerTagMask         = "Mask objects";
        private const string ProfilerTagMaskHidden   = "Mask hidden";
        private const string ProfilerTagOutline      = "Apply outline";

        private readonly PassSettings _settings;
        private readonly Material     _outlineMaterial;
        private readonly Material     _maskMaterial;

        private RenderTargetIdentifier _colorBuffer;
        private RenderTargetIdentifier _depthBuffer;
        private RenderTargetIdentifier _maskBuffer;
        private RenderTargetIdentifier _tempBuffer;
        private RenderTargetIdentifier _temp2Buffer;
        private RenderTargetIdentifier _hiddenMaskBuffer;
        private RenderTargetIdentifier _hiddenBuffer;

        private readonly List<ShaderTagId> _shaderTagIdList = new();

        public CustomRenderPass(PassSettings passSettings)
        {
            _settings       = passSettings;
            renderPassEvent = _settings.renderPassEvent;

            _outlineMaterial = CoreUtils.CreateEngineMaterial(_settings.outlineShader);
            _maskMaterial    = CoreUtils.CreateEngineMaterial(_settings.maskingShader);

            _shaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIdList.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;

            descriptor.shadowSamplingMode = ShadowSamplingMode.None;
            descriptor.colorFormat        = RenderTextureFormat.ARGB32;
            descriptor.msaaSamples        = 1;
            descriptor.depthBufferBits    = 0;

            cmd.GetTemporaryRT(TempTexId, descriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(Temp2TexId, descriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(MaskTexId, descriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(HiddenMaskTexId, descriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(HiddenTexId, descriptor, FilterMode.Bilinear);

            _colorBuffer      = renderingData.cameraData.renderer.cameraColorTarget;
            _tempBuffer       = new RenderTargetIdentifier(TempTexId);
            _temp2Buffer      = new RenderTargetIdentifier(Temp2TexId);
            _maskBuffer       = new RenderTargetIdentifier(MaskTexId);
            _depthBuffer      = new RenderTargetIdentifier(DepthTexId);
            _hiddenMaskBuffer = new RenderTargetIdentifier(HiddenMaskTexId);
            _hiddenBuffer     = new RenderTargetIdentifier(HiddenTexId);
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();

            if (_settings.layerMask != 0) RenderLayers(cmd, context, ref renderingData);

            var objects = OutlineManager.GetObjects();
            if (objects.Length > 0) RenderObjects(cmd, context, objects);

            context.Submit();
            CommandBufferPool.Release(cmd);
        }

        private OutlineSettings GetSettings(OutlineEffect effect = null)
        {
            return effect == null || effect.OutlineSettings.useGlobalSettings ? _settings.outline : effect.OutlineSettings;
        }

        private MaterialPropertyBlock GetProps(OutlineEffect effect = null)
        {
            var props = new MaterialPropertyBlock();

            var settings = GetSettings(effect);

            props.SetColor(ColorId, settings.color);
            props.SetInt(OutlineSizeId, settings.outlineSize);

            return props;
        }

        private void RenderLayers(CommandBuffer cmd, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTagLayer)))
            {
                var props    = GetProps();
                var settings = GetSettings();

                if (settings.FlagAlpha) cmd.SetGlobalFloat(AlphaCutoffId, settings.alphaCutoff);
                else cmd.SetGlobalFloat(AlphaCutoffId, 0);

                var depthBuffer = settings.FlagDepth ? _depthBuffer : (RenderTargetIdentifier?) null;
                SetTargetAndClear(cmd, _temp2Buffer, care: false);
                if (settings.FlagPrecise)
                {
                    SetTargetAndClear(cmd, _maskBuffer, care: false);
                    SetTargetAndClear(cmd, _tempBuffer, depthBuffer);
                }
                else
                {
                    // masking directly on first render
                    SetTargetAndClear(cmd, _tempBuffer, care: false);
                    SetTargetAndClear(cmd, _maskBuffer, depthBuffer);
                }

                Execute(cmd, context);

                var drawingSettings = CreateDrawingSettings(_shaderTagIdList, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
                drawingSettings.enableDynamicBatching = true;

                if (!settings.FlagPrecise)
                {
                    drawingSettings.overrideMaterial          = _maskMaterial;
                    drawingSettings.overrideMaterialPassIndex = settings.FlagAlpha ? 1 : 0;
                }

                var filteringSettings = new FilteringSettings(RenderQueueRange.all, _settings.layerMask);

                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);

                MaskBufferFromTemp(cmd, context);

                Execute(cmd, context);

                if (settings.FlagHidden)
                {
                    if (settings.FlagDepth)
                    {
                        //depth buffer enabled, render extra without depth
                        SetTargetAndClear(cmd, _hiddenMaskBuffer, null);
                    }
                    else
                    {
                        //depth buffer disabled, render extra with depth
                        SetTargetAndClear(cmd, _hiddenMaskBuffer, _depthBuffer);
                    }

                    Execute(cmd, context);

                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);

                    SetTargetAndClear(cmd, _hiddenBuffer, null);
                    Execute(cmd, context);

                    var drawingSettings2 = CreateDrawingSettings(_shaderTagIdList, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
                    drawingSettings2.enableDynamicBatching = true;
                    drawingSettings2.overrideMaterial      = _settings.hiddenMaterial;
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings2, ref filteringSettings);

                    using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTagMaskHidden)))
                    {
                        cmd.SetGlobalTexture(HiddenMaskTexId, _hiddenMaskBuffer);
                        cmd.SetGlobalTexture(HiddenTexId, _hiddenBuffer);
                        pBlit(cmd, _maskBuffer, _temp2Buffer, 3, _maskMaterial, null);
                    }

                    Execute(cmd, context);
                }

                pBlit(cmd, _temp2Buffer, _colorBuffer, 4, _maskMaterial, null);
                ApplyOutline(cmd, context, props);
            }

            Execute(cmd, context);
        }

        private void RenderObjects(CommandBuffer cmd, ScriptableRenderContext context, OutlineEffect[] effects)
        {
            using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTagObjects)))
            {
                foreach (var effect in effects)
                {
                    var props    = GetProps(effect);
                    var settings = GetSettings(effect);

                    if (settings.FlagAlpha) cmd.SetGlobalFloat(AlphaCutoffId, settings.alphaCutoff);
                    else cmd.SetGlobalFloat(AlphaCutoffId, 0);

                    SetTargetAndClear(cmd, _temp2Buffer, care: false);

                    var depthBuffer = settings.FlagDepth ? _depthBuffer : (RenderTargetIdentifier?) null;
                    if (settings.FlagPrecise)
                    {
                        SetTargetAndClear(cmd, _maskBuffer, care: false);
                        SetTargetAndClear(cmd, _tempBuffer, depthBuffer);
                    }
                    else
                    {
                        // masking directly on first render
                        SetTargetAndClear(cmd, _tempBuffer, care: false);
                        SetTargetAndClear(cmd, _maskBuffer, depthBuffer);
                    }

                    Execute(cmd, context);

                    List<Material> materials = new();
                    effect.Renderer.GetSharedMaterials(materials);
                    var customPasses = settings.shaderPasses.Any();

                    using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTagRenderObject)))
                    {
                        for (var index = 0; index < materials.Count; index++)
                        {
                            var mat = settings.FlagPrecise ? materials[index] : _maskMaterial;
                            if (settings.FlagPrecise)
                            {
                                if (customPasses && settings.shaderPasses.Count > index)
                                    for (var pass = 0; pass < settings.shaderPasses[index].Count; pass++)
                                    {
                                        var passes = settings.shaderPasses[index];
                                        cmd.DrawRenderer(effect.Renderer, mat, index, passes[pass]);
                                    }
                                else
                                    cmd.DrawRenderer(effect.Renderer, mat, index, 0);
                            }
                            else
                            {
                                cmd.DrawRenderer(effect.Renderer, mat, index, settings.FlagAlpha ? 1 : 0);
                            }
                        }
                    }

                    if (settings.FlagPrecise) MaskBufferFromTemp(cmd, context, settings);

                    if (settings.FlagHidden)
                    {
                        if (settings.FlagDepth)
                        {
                            //depth buffer enabled, render extra without depth
                            SetTargetAndClear(cmd, _hiddenMaskBuffer, null);
                        }
                        else
                        {
                            //depth buffer disabled, render extra with depth
                            SetTargetAndClear(cmd, _hiddenMaskBuffer, _depthBuffer);
                        }

                        Execute(cmd, context);

                        cmd.DrawRenderer(effect.Renderer, _maskMaterial, 0, settings.FlagAlpha ? 1 : 0);

                        SetTargetAndClear(cmd, _hiddenBuffer, null);
                        Execute(cmd, context);

                        var hiddenMat = settings.OverrideHiddenMaterial ? (settings.HiddenMaterial ? settings.HiddenMaterial : _settings.hiddenMaterial) : _settings.hiddenMaterial;
                        cmd.DrawRenderer(effect.Renderer, hiddenMat, 0, -1);

                        using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTagMaskHidden)))
                        {
                            cmd.SetGlobalTexture(HiddenMaskTexId, _hiddenMaskBuffer);
                            cmd.SetGlobalTexture(HiddenTexId, _hiddenBuffer);
                            pBlit(cmd, _maskBuffer, _temp2Buffer, 3, _maskMaterial, null);
                        }

                        Execute(cmd, context);
                    }

                    pBlit(cmd, _temp2Buffer, _colorBuffer, 4, _maskMaterial, null);
                    ApplyOutline(cmd, context, props);
                }
            }

            Execute(cmd, context);
        }


        private static void Execute(CommandBuffer cmd, ScriptableRenderContext context)
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private void SetTargetAndClear(CommandBuffer cmd, RenderTargetIdentifier target, RenderTargetIdentifier? depth = null, bool care = true)
        {
            if (depth.HasValue)
                cmd.SetRenderTarget(target, care ? RenderBufferLoadAction.Load : RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    depth.Value, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            else
                cmd.SetRenderTarget(target, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);

            cmd.ClearRenderTarget(false, true, Color.clear);
        }

        private void MaskBufferFromTemp(CommandBuffer cmd, ScriptableRenderContext context, OutlineSettings settings = null)
        {
            using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTagMask)))
            {
                pBlit(cmd, _tempBuffer, _maskBuffer, 2, _maskMaterial, null);
            }

            Execute(cmd, context);
        }

        private void ApplyOutline(CommandBuffer cmd, ScriptableRenderContext context, MaterialPropertyBlock props)
        {
            using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTagOutline)))
            {
                pBlit(cmd, _maskBuffer, _tempBuffer, 0, _outlineMaterial, props);
                pBlit(cmd, _tempBuffer, _colorBuffer, 1, _outlineMaterial, props);
            }

            Execute(cmd, context);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(MaskTexId);
            cmd.ReleaseTemporaryRT(TempTexId);
            cmd.ReleaseTemporaryRT(Temp2TexId);
            cmd.ReleaseTemporaryRT(HiddenMaskTexId);
            cmd.ReleaseTemporaryRT(HiddenTexId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void pBlit(CommandBuffer cmd, RenderTargetIdentifier src, RenderTargetIdentifier dest, int shaderPass, Material mat, MaterialPropertyBlock props)
        {
            cmd.SetRenderTarget(dest, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            cmd.SetGlobalTexture(MainTexId, src);

            cmd.DrawProcedural(Matrix4x4.identity, mat, shaderPass, MeshTopology.Triangles, 3, 1, props);
        }
    }

    [Serializable]
    public class PassSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Shader          outlineShader;
        public Shader          maskingShader;
        public Material        hiddenMaterial;
        public LayerMask       layerMask;
        public OutlineSettings outline = new() {useGlobalSettings = true, theGlobalOne = true};
    }

    [Serializable]
    public class OutlineSettings
    {
        [Tooltip("Color of the outline")]
        [ColorUsage(true, true)] public Color color = Color.white;

        [Tooltip("Size of the outline")]
        [Range(0, 2)] public int outlineSize = 2;

        [Tooltip(
            nameof(OutlineFlags.UseDepth) + " \t- outline only the visible part of the object\n\n" +
            // TODO: alpha test broken // nameof(OutlineFlags.UseAlpha) + " \t- ignore transparent part of the object\n\n" +
            nameof(OutlineFlags.ShowHidden) + " \t- display part of the object hidden behind other objects\n\n" +
            nameof(OutlineFlags.Precise) + " \t\t- use original materials when discovering object's silhouette"
        )]
        public OutlineFlags flags = OutlineFlags.None;

        [Tooltip("Use global settings from render feature")]
        public bool useGlobalSettings = false;

        [Tooltip("Pixels with alpha value below this threshold will be ignored in silhouette")]
        [Range(0, 1.0f)] public float alphaCutoff = 0.5f;

        [Tooltip("Use specific shader passes for each submesh, will use pass 0 for each by default")]
        public List<ShaderPassList> shaderPasses = new();

        [HideInInspector]
        public bool theGlobalOne = false;

        [Tooltip("Use custom hidden material instead of global settings")]
        public bool OverrideHiddenMaterial;

        [Tooltip("Material to use for hidden part of the object")]
        public Material HiddenMaterial;

        [DoNotSerialize] public bool FlagDepth   => (flags & OutlineFlags.UseDepth) != 0;
        [DoNotSerialize] public bool FlagAlpha   => false; //TODO: broken // (flags & OutlineFlags.UseAlpha) != 0;
        [DoNotSerialize] public bool FlagHidden  => (flags & OutlineFlags.ShowHidden) != 0;
        [DoNotSerialize] public bool FlagPrecise => (flags & OutlineFlags.Precise) != 0;
    }

    [Serializable]
    public class ShaderPassList
    {
        public List<int> Passes;

        public bool Any   => Passes.Any();
        public int  Count => Passes.Count;
        public int this[int key] => Passes[key];
    }

    [Flags]
    public enum OutlineFlags
    {
        None       = 0b_0,
        UseDepth   = 0b_1,
       // UseAlpha   = 0b_10,
        ShowHidden = 0b_100,
        Precise    = 0b_1000
    }

    [SerializeField] private PassSettings settings = new();

    private CustomRenderPass _renderPass;

    /// <inheritdoc/>
    public override void Create()
    {
        if (settings.outlineShader == null || settings.maskingShader == null)
        {
            Debug.LogWarning($"Missing shader reference in {name} render feature");
            return;
        }

        _renderPass = new CustomRenderPass(settings);
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_renderPass != null) renderer.EnqueuePass(_renderPass);
    }
}

}