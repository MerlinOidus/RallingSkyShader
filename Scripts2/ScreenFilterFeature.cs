using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace RTXOn
{
    /// <summary>
    /// URP Renderer Feature that draws the screen filter triggers' effects (<see cref="ScreenFilters"/>) over the
    /// picture after post-processing. It does nothing while every effect is off. "RTX ON > Hook Up Screen Filters"
    /// adds it to the project's URP renderers (done by itself when the scripts load); by hand: select the renderer
    /// asset (e.g. PC_Renderer) > Add Renderer Feature > Screen Filter Feature.
    /// </summary>
    public class ScreenFilterFeature : ScriptableRendererFeature
    {
        [Tooltip("RTXOn/ScreenFilters. Found by itself when empty.")]
        public Shader shader;

        Material material;
        FilterPass pass;

        public override void Create()
        {
            if (shader == null) shader = Shader.Find(ScreenFilters.ShaderName);
            pass = new FilterPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!ScreenFilters.Active) return;
            if (material == null)
            {
                if (shader == null) shader = Shader.Find(ScreenFilters.ShaderName);
                if (shader == null) return;
                material = CoreUtils.CreateEngineMaterial(shader);
            }
            pass.material = material;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        /// <summary>Copies the camera picture through the filter shader and makes the copy the camera's picture.</summary>
        class FilterPass : ScriptableRenderPass
        {
            public Material material;

            public FilterPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true; // the picture must be a texture the pass can read
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType != CameraType.Game) return; // not the Scene view or previews
                if (ScreenFilters.Target != null && cameraData.camera != ScreenFilters.Target) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || material == null) return;

                var source = resources.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "RTX Screen Filters";
                desc.clearBuffer = false;
                var destination = renderGraph.CreateTexture(desc);
                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), "RTX Screen Filters");
                resources.cameraColor = destination;
            }
        }
    }
}
