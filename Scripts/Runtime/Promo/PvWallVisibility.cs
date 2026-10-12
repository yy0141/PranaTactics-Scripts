#if UNITY_EDITOR || PV_CAPTURE
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Promo
{
    /// <summary>
    /// 撮影カメラへのめり込みと、演者への視線を遮るWallの描画対象を収集します。
    /// </summary>
    public sealed class PvWallVisibility
    {
        private readonly int _wallMask = LayerMask.GetMask("Wall");

        private readonly List<Renderer> _renderers = new List<Renderer>();

        /// <summary>
        /// トリガー形式の建物も検出し、衝突を変えずに完全透過する対象へ追加します。
        /// </summary>
        public void Collect(Camera camera, PvActor first, PvActor second, HashSet<Renderer> hidden)
        {
            float near = camera.nearClipPlane;
            float halfHeight = camera.orthographic ? camera.orthographicSize : near * Mathf.Tan(
                camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float halfWidth = halfHeight * camera.aspect;
            float radius = Mathf.Sqrt(near * near + halfHeight * halfHeight + halfWidth * halfWidth) + 0.1f;
            Collider[] walls = Physics.OverlapSphere(
                camera.transform.position,
                radius,
                _wallMask,
                QueryTriggerInteraction.Collide);

            foreach (Collider wall in walls)
            {
                CollectWall(wall, hidden);
            }

            CollectObstructions(camera, first, hidden);
            CollectObstructions(camera, second, hidden);
        }

        /// <summary>
        /// 演者の中心と輪郭へ複数の視線を通し、手前から奥まで重なった遮蔽物を収集します。
        /// </summary>
        private void CollectObstructions(Camera camera, PvActor actor, HashSet<Renderer> hidden)
        {
            GameObject model = actor.Unit.MicroUnitBehavior.GetModel();

            model.GetComponentsInChildren(false, _renderers);
            var bounds = new Bounds(actor.Position + Vector3.up * 1.5f, Vector3.one);
            bool hasBounds = false;

            foreach (Renderer renderer in _renderers)
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                if (hasBounds)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }

            Vector3 right = camera.transform.right;
            float halfWidth = Mathf.Abs(right.x) * bounds.extents.x +
                Mathf.Abs(right.y) * bounds.extents.y + Mathf.Abs(right.z) * bounds.extents.z;

            for (int horizontal = -1; horizontal <= 1; horizontal++)
            {
                for (int vertical = -1; vertical <= 1; vertical++)
                {
                    Vector3 target = bounds.center + right * (horizontal * halfWidth * 0.9f) +
                        Vector3.up * (vertical * bounds.extents.y * 0.9f);
                    Vector3 direction = target - camera.transform.position;
                    float distance = direction.magnitude;

                    if (distance <= 0.001f)
                    {
                        continue;
                    }

                    foreach (RaycastHit hit in Physics.SphereCastAll(camera.transform.position, 0.15f,
                        direction / distance, distance, _wallMask, QueryTriggerInteraction.Collide))
                    {
                        CollectWall(hit.collider, hidden);
                    }
                }
            }
        }

        private void CollectWall(Collider wall, HashSet<Renderer> hidden)
        {
            wall.GetComponentsInChildren(false, _renderers);

            if (_renderers.Count == 0)
            {
                Renderer parentRenderer = wall.GetComponentInParent<Renderer>();

                if (parentRenderer != null && (_wallMask & (1 << parentRenderer.gameObject.layer)) != 0)
                {
                    hidden.Add(parentRenderer);
                }
            }

            foreach (Renderer renderer in _renderers)
            {
                hidden.Add(renderer);
            }
        }
    }
}
#endif
