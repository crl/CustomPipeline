using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;

namespace UnityEngine.Experimental.Rendering.LWRP
{
    /// <summary>
    /// Render all opaque forward objects into the given color and depth target 
    ///
    /// You can use this pass to render objects that have a material and/or shader
    /// with the pass names LightweightForward or SRPDefaultUnlit. The pass only
    /// renders objects in the rendering queue range of Opaque objects.
    /// </summary>
    internal class RenderOpaqueForwardPass : ScriptableRenderPass
    {
        const string k_RenderOpaquesTag = "Render Opaques";
        FilteringSettings m_OpaqueFilterSettings;

        RenderTargetHandle colorAttachmentHandle { get; set; }
        RenderTargetHandle depthAttachmentHandle { get; set; }
        RenderTextureDescriptor descriptor { get; set; }
        ClearFlag clearFlag { get; set; }
        Color clearColor { get; set; }

        PerObjectData rendererConfiguration;

        public RenderOpaqueForwardPass()
        {
            RegisterShaderPassName("LightweightForward");
            RegisterShaderPassName("SRPDefaultUnlit");

            m_OpaqueFilterSettings = new FilteringSettings(RenderQueueRange.opaque);
        }

        /// <summary>
        /// Configure the pass before execution
        /// </summary>
        /// <param name="baseDescriptor">Current target descriptor</param>
        /// <param name="colorAttachmentHandle">Color attachment to render into</param>
        /// <param name="depthAttachmentHandle">Depth attachment to render into</param>
        /// <param name="clearFlag">Camera clear flag</param>
        /// <param name="clearColor">Camera clear color</param>
        /// <param name="configuration">Specific render configuration</param>
        public void Setup(
            RenderTextureDescriptor baseDescriptor,
            RenderTargetHandle colorAttachmentHandle,
            RenderTargetHandle depthAttachmentHandle,
            ClearFlag clearFlag,
            Color clearColor,
            PerObjectData configuration)
        {
            this.colorAttachmentHandle = colorAttachmentHandle;
            this.depthAttachmentHandle = depthAttachmentHandle;
            this.clearColor = CoreUtils.ConvertSRGBToActiveColorSpace(clearColor);
            this.clearFlag = clearFlag;
            descriptor = baseDescriptor;
            this.rendererConfiguration = configuration;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderer == null)
                throw new ArgumentNullException("renderer");
            
            CommandBuffer cmd = CommandBufferPool.Get(k_RenderOpaquesTag);
            using (new ProfilingSample(cmd, k_RenderOpaquesTag))
            {
                RenderBufferLoadAction loadOp = RenderBufferLoadAction.DontCare;
                RenderBufferStoreAction storeOp = RenderBufferStoreAction.Store;
                SetRenderTarget(cmd, colorAttachmentHandle.Identifier(), loadOp, storeOp,
                    depthAttachmentHandle.Identifier(), loadOp, storeOp, clearFlag, clearColor, descriptor.dimension);

                // TODO: We need a proper way to handle multiple camera/ camera stack. Issue is: multiple cameras can share a same RT
                // (e.g, split screen games). However devs have to be dilligent with it and know when to clear/preserve color.
                // For now we make it consistent by resolving viewport with a RT until we can have a proper camera management system
                //if (colorAttachmentHandle == -1 && !cameraData.isDefaultViewport)
                //    cmd.SetViewport(camera.pixelRect);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                Camera camera = renderingData.cameraData.camera;
                XRUtils.DrawOcclusionMesh(cmd, camera, renderingData.cameraData.isStereoEnabled);
                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(camera, sortFlags, rendererConfiguration, renderingData.supportsDynamicBatching, renderingData.lightData.mainLightIndex);
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_OpaqueFilterSettings);

                // Render objects that did not match any shader pass with error shader
                renderer.RenderObjectsWithError(context, ref renderingData.cullResults, camera, m_OpaqueFilterSettings, SortingCriteria.None);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
