using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class RailwayBimOutlineFeature : ScriptableRendererFeature
{
    OutlinePass pass;

    public override void Create() => pass = new OutlinePass
    {
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents
    };

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (SceneQuickMenu.IsOpen) return;
        var camera = renderingData.cameraData.camera;
        foreach (var outline in RailwayBimOutline.Active)
        {
            if (!outline.RendersOn(camera) || !outline.HasVisibleSource(camera.cullingMask) ||
                outline.maskMaterial == null || outline.outlineMaterial == null) continue;
            pass.Selection = outline;
            // URP renders a non-default camera viewport into its own intermediate before the final viewport blit.
            pass.requiresIntermediateTexture = !renderingData.cameraData.isDefaultViewport;
            renderer.EnqueuePass(pass);
            return;
        }
    }

    sealed class OutlinePass : ScriptableRenderPass
    {
        internal RailwayBimOutline Selection;

        sealed class MaskData
        {
            public RailwayBimOutline selection;
            public int layers;
        }

        sealed class CompositeData
        {
            public TextureHandle mask;
            public Material material;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var camera = frameData.Get<UniversalCameraData>();
            var color = frameData.Get<UniversalResourceData>().activeColorTexture;
            var descriptor = camera.cameraTargetDescriptor;
            descriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.memoryless = RenderTextureMemoryless.None;
            // Rasterize the entire selection without scene depth. Dilating this mask outlines the union of its meshes.
            var mask = UniversalRenderer.CreateRenderGraphTexture(graph, descriptor, "BIM selection mask", true);

            using (var builder = graph.AddRasterRenderPass<MaskData>("BIM selection silhouette", out var data))
            {
                data.selection = Selection;
                data.layers = camera.camera.cullingMask;
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) =>
                {
                    foreach (var source in data.selection.Sources)
                    {
                        if (!RailwayBimOutline.IsRenderable(source) ||
                            (data.layers & (1 << source.gameObject.layer)) == 0) continue;
                        int submeshes = RailwayBimOutline.SubmeshCount(source);
                        for (int i = 0; i < submeshes; i++)
                            context.cmd.DrawRenderer(source, data.selection.maskMaterial, i, 0);
                    }
                });
            }

            using (var builder = graph.AddRasterRenderPass<CompositeData>("BIM selection outline", out var data))
            {
                data.mask = mask;
                data.material = Selection.outlineMaterial;
                builder.UseTexture(mask, AccessFlags.Read);
                builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, data.mask, new Vector4(1, 1, 0, 0), data.material, 1));
            }
        }
    }
}
