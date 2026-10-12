using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PranaTactics.Unit
{
    /// <summary>
    /// Unitのカメラに埋まった壁とユニットを、そのカメラの描画中だけ完全透過にします。
    /// </summary>
    internal static class CameraPenetrationVisibility
    {
        private static readonly Dictionary<Renderer, bool> s_original = new();

        private static readonly HashSet<Renderer> s_hidden = new();

        private static readonly List<Renderer> s_renderers = new();

        private static Collider[] s_colliders = new Collider[32];

        private static int s_mask;

        private static int s_unitLayer;

        /// <summary>
        /// Domain Reloadを無効にした再生でも購読を重複させず、通常ビルドへ適用します。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Restore();
            s_mask = LayerMask.GetMask("Wall", "Unit");
            s_unitLayer = LayerMask.NameToLayer("Unit");
            RenderPipelineManager.beginCameraRendering -= BeginRendering;
            RenderPipelineManager.endCameraRendering -= EndRendering;
            RenderPipelineManager.endFrameRendering -= EndFrame;
            Application.quitting -= Restore;
            RenderPipelineManager.beginCameraRendering += BeginRendering;
            RenderPipelineManager.endCameraRendering += EndRendering;
            RenderPipelineManager.endFrameRendering += EndFrame;
            Application.quitting += Restore;
        }

        /// <summary>
        /// カメラ位置から近クリップ面までの領域と接触している描画対象を収集します。
        /// </summary>
        private static void BeginRendering(ScriptableRenderContext context, Camera camera)
        {
            Restore();

            if (camera == null || camera.cameraType != CameraType.Game ||
                camera.GetComponentInParent<UnitBehaviorBase>() == null)
            {
                return;
            }

            float near = Mathf.Max(0.01f, camera.nearClipPlane);
            float halfHeight = camera.orthographic ? 0.1f : near * Mathf.Tan(
                camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            var halfExtents = new Vector3(
                halfHeight * camera.aspect + 0.05f,
                halfHeight + 0.05f,
                near * 0.5f +
                0.05f);
            Vector3 center = camera.transform.position + camera.transform.forward * (near * 0.5f);
            int count;

            while (true)
            {
                count = Physics.OverlapBoxNonAlloc(center, halfExtents, s_colliders,
                    camera.transform.rotation, s_mask, QueryTriggerInteraction.Collide);

                if (count < s_colliders.Length)
                {
                    break;
                }

                s_colliders = new Collider[s_colliders.Length * 2];
            }

            s_hidden.Clear();

            for (int index = 0; index < count; index++)
            {
                Collider collider = s_colliders[index];

                Collect(collider, center, halfExtents.magnitude);
                s_colliders[index] = null;
            }

            foreach (Renderer renderer in s_hidden)
            {
                if (renderer != null)
                {
                    s_original.Add(renderer, renderer.forceRenderingOff);
                    renderer.forceRenderingOff = true;
                }
            }

            s_hidden.Clear();
        }

        /// <summary>
        /// Unitの検知用トリガーだけに触れた場合は透過せず、身体付近への接触を確認します。
        /// </summary>
        private static void Collect(Collider collider, Vector3 center, float radius)
        {
            if (collider.gameObject.layer == s_unitLayer)
            {
                UnitBehavior unit = collider.GetComponentInParent<UnitBehavior>();
                GameObject model = unit != null ? unit.MicroUnitBehavior?.GetModel() : null;

                if (model != null)
                {
                    model.GetComponentsInChildren(false, s_renderers);
                    bool intersectsBody = !collider.isTrigger;

                    foreach (Renderer renderer in s_renderers)
                    {
                        if (renderer.bounds.SqrDistance(center) <= radius * radius)
                        {
                            intersectsBody = true;
                            break;
                        }
                    }

                    if (intersectsBody)
                    {
                        s_hidden.UnionWith(s_renderers);
                    }

                    return;
                }

                if (collider.isTrigger)
                {
                    return;
                }
            }

            collider.GetComponentsInChildren(false, s_renderers);
            s_hidden.UnionWith(s_renderers);

            if (s_renderers.Count == 0)
            {
                Renderer renderer = collider.GetComponentInParent<Renderer>();

                if (renderer != null && (s_mask & (1 << renderer.gameObject.layer)) != 0)
                {
                    s_hidden.Add(renderer);
                }
            }
        }

        /// <summary>
        /// 描画後は視認判定や次のカメラへ非表示状態を残しません。
        /// </summary>
        private static void EndRendering(ScriptableRenderContext context, Camera camera)
        {
            Restore();
        }

        /// <summary>
        /// フレーム終了時にも描画状態を復元します。
        /// </summary>
        private static void EndFrame(ScriptableRenderContext context, Camera[] cameras)
        {
            Restore();
        }

        /// <summary>
        /// 元から非表示だったRendererを含め、描画直前の状態へ復元します。
        /// </summary>
        private static void Restore()
        {
            foreach (KeyValuePair<Renderer, bool> entry in s_original)
            {
                if (entry.Key != null)
                {
                    entry.Key.forceRenderingOff = entry.Value;
                }
            }

            s_original.Clear();
        }
    }
}
