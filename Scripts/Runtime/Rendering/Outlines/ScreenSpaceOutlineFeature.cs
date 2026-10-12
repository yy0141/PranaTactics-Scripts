using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PranaTactics.Rendering.Outlines
{
    /// <summary>
    /// オブジェクト別マスクの境界から距離場を作り、画面上の一定幅で輪郭を合成します。
    /// </summary>
    public sealed class ScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField]
        private Shader _maskShader;
        [SerializeField]
        private Shader _compositeShader;
        private Material _compositeMaterial;
        private OutlinePass _pass;

        public override void Create()
        {
            CoreUtils.Destroy(_compositeMaterial);
            _pass = null;
            if (_maskShader != null && _compositeShader != null)
            {
                _compositeMaterial = CoreUtils.CreateEngineMaterial(_compositeShader);
                _pass = new OutlinePass(_maskShader, _compositeMaterial);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass != null && Outline.Active.Count > 0 && renderingData.cameraData.renderType == CameraRenderType.Base)
            {
                renderer.EnqueuePass(_pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_compositeMaterial);
            _pass = null;
        }

        /// <summary>
        /// カメラごとの対象と一時テクスチャを管理します。距離計算は描画先ピクセルを基準にします。
        /// </summary>
        private sealed class OutlinePass : ScriptableRenderPass
        {
            private readonly Shader _maskShader;
            private readonly Material _compositeMaterial;
            private static readonly int s_maskId = Shader.PropertyToID("_OutlineMask");
            private static readonly int s_sourceId = Shader.PropertyToID("_OutlineSeeds");
            private static readonly int s_sizeId = Shader.PropertyToID("_OutlineSize");
            private static readonly int s_jumpId = Shader.PropertyToID("_OutlineJump");
            private static readonly int s_depthId = Shader.PropertyToID("_OutlineSceneDepth");

            private struct DrawItem
            {
                public Renderer Renderer;
                public Material Material;
                public int SubmeshCount;
            }

            /// <summary>
            /// グラフの実行まで描画対象を保持します。
            /// </summary>
            private sealed class MaskData
            {
                public DrawItem[] Items;
                public TextureHandle SceneDepth;
                public Vector4 Size;
            }

            /// <summary>
            /// 画面処理一段分の入力を保持します。
            /// </summary>
            private sealed class ScreenData
            {
                public TextureHandle Mask;
                public TextureHandle Seeds;
                public Material Material;
                public int Pass;
                public int Jump;
                public Vector4 Size;
            }

            public OutlinePass(Shader maskShader, Material compositeMaterial)
            {
                _maskShader = maskShader;
                _compositeMaterial = compositeMaterial;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                Camera camera = cameraData.camera;
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
                var items = new List<DrawItem>();
                float maximumWidth = 0.0f;
                int identifier = 0;
                foreach (Outline outline in Outline.Active)
                {
                    if (outline == null || !outline.IsRenderingEnabled || outline.OutlineColor.a <= 0.0f)
                    {
                        continue;
                    }
                    Material material = null;
                    foreach (Renderer target in outline.Renderers)
                    {
                        if (target == null || !target.enabled || !target.gameObject.activeInHierarchy || target.forceRenderingOff ||
                            target.shadowCastingMode == ShadowCastingMode.ShadowsOnly ||
                            (camera.cullingMask & (1 << target.gameObject.layer)) == 0 || !GeometryUtility.TestPlanesAABB(planes, target.bounds))
                        {
                            continue;
                        }
                        Mesh mesh = target is SkinnedMeshRenderer skinned ? skinned.sharedMesh : target.GetComponent<MeshFilter>()?.sharedMesh;
                        if (mesh == null)
                        {
                            continue;
                        }
                        if (material == null)
                        {
                            if (++identifier > 65535)
                            {
                                break;
                            }
                            material = outline.PrepareMaterial(_maskShader, identifier);
                            maximumWidth = Mathf.Max(maximumWidth, outline.OutlineWidth);
                        }
                        items.Add(new DrawItem { Renderer = target, Material = material, SubmeshCount = mesh.subMeshCount });
                    }
                }
                if (items.Count == 0)
                {
                    return;
                }

                RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
                int width = descriptor.width;
                int height = descriptor.height;
                var size = new Vector4(width, height,
                    (float)width / Mathf.Max(1, camera.pixelWidth), (float)height / Mathf.Max(1, camera.pixelHeight));
                var texture = new TextureDesc(width, height)
                {
                    colorFormat = GraphicsFormat.R32G32_UInt,
                    filterMode = FilterMode.Point,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    name = "Outline object mask"
                };
                TextureHandle mask = graph.CreateTexture(texture);
                var depthDescriptor = new TextureDesc(width, height)
                {
                    depthBufferBits = DepthBits.Depth32,
                    clearBuffer = true,
                    name = "Outline own depth"
                };
                TextureHandle depth = graph.CreateTexture(depthDescriptor);
                using (var builder = graph.AddRasterRenderPass<MaskData>("Outline object mask", out MaskData data))
                {
                    data.Items = items.ToArray();
                    data.SceneDepth = resources.cameraDepthTexture;
                    data.Size = size;
                    builder.UseTexture(data.SceneDepth);
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((MaskData pass, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(s_depthId, pass.SceneDepth);
                        context.cmd.SetGlobalVector(s_sizeId, pass.Size);
                        foreach (DrawItem item in pass.Items)
                        {
                            for (int submesh = 0; submesh < item.SubmeshCount; submesh++)
                            {
                                context.cmd.DrawRenderer(item.Renderer, item.Material, submesh, 0);
                            }
                        }
                    });
                }

                texture.name = "Outline boundary seeds";
                TextureHandle seeds = graph.CreateTexture(texture);
                AddScreenPass(graph, mask, mask, seeds, size, 0, 0);
                int radius = Mathf.CeilToInt((maximumWidth * 0.5f + 1.0f) * Mathf.Max(size.z, size.w));
                int jump = Mathf.NextPowerOfTwo(Mathf.Max(1, radius));
                while (jump > 0)
                {
                    texture.name = "Outline distance propagation";
                    TextureHandle next = graph.CreateTexture(texture);
                    AddScreenPass(graph, mask, seeds, next, size, 1, jump);
                    seeds = next;
                    jump /= 2;
                }
                texture.name = "Outline distance refinement";
                TextureHandle refined = graph.CreateTexture(texture);
                AddScreenPass(graph, mask, seeds, refined, size, 1, 1);
                AddScreenPass(graph, mask, refined, resources.activeColorTexture, size, 2, 0);
            }

            private void AddScreenPass(RenderGraph graph, TextureHandle mask, TextureHandle seeds, TextureHandle target,
                Vector4 size, int shaderPass, int jump)
            {
                using (var builder = graph.AddRasterRenderPass<ScreenData>("Outline screen pass " + shaderPass, out ScreenData data))
                {
                    data.Mask = mask;
                    data.Seeds = seeds;
                    data.Material = _compositeMaterial;
                    data.Pass = shaderPass;
                    data.Jump = jump;
                    data.Size = size;
                    builder.UseTexture(mask);
                    if (!seeds.Equals(mask))
                    {
                        builder.UseTexture(seeds);
                    }
                    builder.SetRenderAttachment(target, 0, shaderPass == 2 ? AccessFlags.ReadWrite : AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((ScreenData pass, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(s_maskId, pass.Mask);
                        context.cmd.SetGlobalTexture(s_sourceId, pass.Seeds);
                        context.cmd.SetGlobalVector(s_sizeId, pass.Size);
                        context.cmd.SetGlobalInt(s_jumpId, pass.Jump);
                        context.cmd.DrawProcedural(Matrix4x4.identity, pass.Material, pass.Pass, MeshTopology.Triangles, 3);
                    });
                }
            }
        }
    }
}
