using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Unit
{
    /// <summary>
    /// プレイヤーはカメラ画角、AIはセンサー範囲内の敵への視線から視認を判定します。
    /// </summary>
    /// <remarks>
    /// プレイヤーはRendererのBounds、AIは頭部と胴体への視線を使う幾何学的な近似です。
    /// 透明度や最終描画ピクセルは評価しません。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class VisionSensor : MonoBehaviour
    {
        private const float k_ViewportTolerance = 0.0001f;

        /// <summary>
        /// 視認判定を行うユニットです。
        /// </summary>
        [Header("Owner")]
        [FormerlySerializedAs("_actionUnitBehavior")]
        [SerializeField]
        private MicroUnitBehavior _ownerUnitBehavior;

        /// <summary>
        /// 視認状態の変更をConsoleへ出力するかどうかです。
        /// </summary>
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLogging;

        private Camera _sensorCamera;

        /// <summary>
        /// AIが全方向から敵を検出する範囲です。
        /// </summary>
        private SphereCollider _sensorCollider;

        /// <summary>
        /// AIがコライダー内の敵への視線を再確認する間隔です。
        /// </summary>
        [Min(0.02f)]
        [SerializeField]
        private float _aiVisibilityCheckInterval = 0.2f;

        /// <summary>
        /// 次回AI視認確認の時刻です。
        /// </summary>
        private float _nextAiVisibilityCheck;

        /// <summary>
        /// 直前の更新でAI向けの判定を使用していたかどうかです。
        /// </summary>
        private bool _wasAiObserver;

        private readonly Plane[] _frustumPlanes = new Plane[6];

        private readonly List<Renderer> _renderers = new List<Renderer>();

        private readonly List<Vector3> _samples = new List<Vector3>();

        private readonly HashSet<MicroUnitBehavior> _visibleUnits = new HashSet<MicroUnitBehavior>();

        private readonly List<MicroUnitBehavior> _candidateBuffer = new List<MicroUnitBehavior>();

        private readonly List<MicroUnitBehavior> _removalBuffer = new List<MicroUnitBehavior>();

        public float ViewDistance => IsAiObserver() ? GetSensorRadius() :
            _sensorCamera != null ? _sensorCamera.farClipPlane : 0.0f;

        public int VisibleUnitCount => _visibleUnits.Count;

        /// <summary>
        /// 直前の視認更新で対象を視認しているか確認します。
        /// </summary>
        public bool ContainsVisibleUnit(MicroUnitBehavior target)
        {
            return target != null && _visibleUnits.Contains(target);
        }

        /// <summary>
        /// AIには一定間隔で確認した視認結果、プレイヤーには現在の画角での視認結果を返します。
        /// </summary>
        /// <remarks>
        /// 武器の射程と向きを決める判断は呼び出し側で行います。
        /// AIの視認はカメラの向きや描画レイヤーに依存しません。
        /// </remarks>
        public bool CanSeeUnitNow(MicroUnitBehavior target)
        {
            CacheComponents();

            if (!CanEvaluateVisibility() || !IsEnemyUnit(target))
            {
                return false;
            }

            if (IsAiObserver())
            {
                return IsWithinSensor(target) && _visibleUnits.Contains(target);
            }

            GeometryUtility.CalculateFrustumPlanes(_sensorCamera, _frustumPlanes);

            return IsTargetVisible(target);
        }

        /// <summary>
        /// 所有者と論理カメラを取得します。
        /// </summary>
        private void Awake()
        {
            CacheComponents();
        }

        /// <summary>
        /// 有効化後の参照を確認します。
        /// </summary>
        private void OnEnable()
        {
            CacheComponents();
            _nextAiVisibilityCheck = 0.0f;
        }

        /// <summary>
        /// 無効化されたセンサーの視認情報を解除します。
        /// </summary>
        private void OnDisable()
        {
            ClearVisibleUnits();
        }

        /// <summary>
        /// AIは一定間隔でコライダー内、プレイヤーは画角内の視認状態を更新します。
        /// </summary>
        private void Update()
        {
            if (!CanEvaluateVisibility())
            {
                ClearVisibleUnits();

                return;
            }

            bool isAiObserver = IsAiObserver();
            if (_wasAiObserver != isAiObserver)
            {
                ClearVisibleUnits();
                _nextAiVisibilityCheck = 0.0f;
                _wasAiObserver = isAiObserver;
            }

            if (isAiObserver && Time.time < _nextAiVisibilityCheck)
            {
                return;
            }

            _candidateBuffer.Clear();

            if (isAiObserver)
            {
                _nextAiVisibilityCheck = Time.time + Mathf.Max(0.02f, _aiVisibilityCheckInterval);
                CollectSensorCandidates();
            }
            else
            {
                GeometryUtility.CalculateFrustumPlanes(_sensorCamera, _frustumPlanes);
                foreach (UnitBehavior unit in UnitManager.Instance.GetAllUnitBehaviors())
                {
                    MicroUnitBehavior target = unit != null ? unit.GetMicroUnitBehavior() : null;
                    if (IsEnemyUnit(target))
                    {
                        _candidateBuffer.Add(target);
                    }
                }
            }

            _removalBuffer.Clear();

            foreach (MicroUnitBehavior target in _visibleUnits)
            {
                if (!_candidateBuffer.Contains(target))
                {
                    _removalBuffer.Add(target);
                }
            }

            foreach (MicroUnitBehavior target in _removalBuffer)
            {
                RemoveVisibleTarget(target);
            }

            foreach (MicroUnitBehavior target in _candidateBuffer)
            {
                if (isAiObserver ? IsTargetVisibleToAi(target) : IsTargetVisible(target))
                {
                    if (_visibleUnits.Add(target))
                    {
                        _ownerUnitBehavior.AddVisiblePlayer(target);
                        LogVisibility(target, true);
                    }
                    else
                    {
                        _ownerUnitBehavior.RefreshVisiblePlayer(target);
                    }
                }
                else
                {
                    RemoveVisibleTarget(target);
                }
            }
        }

        /// <summary>
        /// 所有ユニットがAI制御中か確認します。
        /// </summary>
        private bool IsAiObserver()
        {
            return _ownerUnitBehavior != null && UnitManager.Instance != null &&
                UnitManager.Instance.IsAIControlledUnit(_ownerUnitBehavior.PlayerId());
        }

        /// <summary>
        /// SphereColliderの拡大率を含めた検出半径を取得します。
        /// </summary>
        private float GetSensorRadius()
        {
            if (_sensorCollider == null)
            {
                return 0.0f;
            }

            Vector3 scale = _sensorCollider.transform.lossyScale;
            return _sensorCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        /// <summary>
        /// 敵の実際の戦闘位置が有効なセンサー範囲内か確認します。
        /// </summary>
        private bool IsWithinSensor(MicroUnitBehavior target)
        {
            if (_sensorCollider == null || !_sensorCollider.enabled || !IsEnemyUnit(target))
            {
                return false;
            }

            Vector3 center = _sensorCollider.transform.TransformPoint(_sensorCollider.center);
            float radius = GetSensorRadius();
            return radius > 0.0f && (target.transform.position - center).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// センサー球内のColliderから、重複を除いた生存敵を収集します。
        /// </summary>
        private void CollectSensorCandidates()
        {
            if (_sensorCollider == null || !_sensorCollider.enabled)
            {
                return;
            }

            Vector3 center = _sensorCollider.transform.TransformPoint(_sensorCollider.center);
            foreach (Collider collider in Physics.OverlapSphere(center, GetSensorRadius(), Physics.AllLayers, QueryTriggerInteraction.Collide))
            {
                MicroUnitBehavior target = collider.GetComponentInParent<MicroUnitBehavior>();
                if (IsWithinSensor(target) && !_candidateBuffer.Contains(target))
                {
                    _candidateBuffer.Add(target);
                }
            }
        }

        /// <summary>
        /// カメラの画角と描画状態を使わず、敵の頭部と胴体への遮蔽を確認します。
        /// </summary>
        private bool IsTargetVisibleToAi(MicroUnitBehavior target)
        {
            if (!IsWithinSensor(target))
            {
                return false;
            }

            Vector3 origin = _sensorCamera.transform.position;
            Camera targetCamera = target.GetCamera();
            Vector3 head = targetCamera != null ? targetCamera.transform.position : target.transform.position + Vector3.up * 1.5f;
            Vector3 torso = target.transform.position + Vector3.up;
            return HasUnblockedAiSight(origin, head, target) || HasUnblockedAiSight(origin, torso, target);
        }

        /// <summary>
        /// 検出対象の身体上の点まで遮蔽物がないか確認します。
        /// </summary>
        private bool HasUnblockedAiSight(Vector3 origin, Vector3 point, MicroUnitBehavior target)
        {
            Vector3 offset = point - origin;
            float distance = offset.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            Vector3 direction = offset / distance;
            return !HasBlockingHit(origin, direction, distance, target) &&
                !HasBlockingHit(point, -direction, distance, target);
        }

        /// <summary>
        /// 所有者が生存し、ユニットのロジックを実行できるか確認します。
        /// </summary>
        private bool CanEvaluateVisibility()
        {
            return isActiveAndEnabled && _sensorCamera != null && _ownerUnitBehavior != null &&
                _ownerUnitBehavior.GetUnitStatus() != null && _ownerUnitBehavior.GetUnitStatus().IsAlive &&
                UnitManager.Instance != null && UnitManager.Instance.CanExecuteUnitLogic(
                    _ownerUnitBehavior.PlayerId());
        }

        /// <summary>
        /// 有効かつ生存している敵ユニットだけを視認候補にします。
        /// </summary>
        private bool IsEnemyUnit(MicroUnitBehavior target)
        {
            UnitStatus ownerStatus = _ownerUnitBehavior != null ? _ownerUnitBehavior.GetUnitStatus() : null;
            UnitStatus targetStatus = target != null ? target.GetUnitStatus() : null;

            return target != null && target.gameObject.activeInHierarchy && ownerStatus != null &&
                targetStatus != null && targetStatus.IsAlive && ownerStatus.TeamId != targetStatus.TeamId;
        }

        /// <summary>
        /// モデルを構成する各Rendererの画角内部分から、遮られていない点を探します。
        /// </summary>
        private bool IsTargetVisible(MicroUnitBehavior target)
        {
            GameObject model = target.GetModel();

            if (model == null || !model.activeInHierarchy)
            {
                return false;
            }

            model.GetComponentsInChildren(false, _renderers);

            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff ||
                    (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) ||
                    (_sensorCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0 ||
                    !GeometryUtility.TestPlanesAABB(_frustumPlanes, renderer.bounds))
                {
                    continue;
                }

                CameraVisibilitySamples.Collect(renderer.bounds, _frustumPlanes, _samples);

                foreach (Vector3 sample in _samples)
                {
                    if (IsVisibilitySampleValid(target, sample))
                    {
                        return true;
                    }
                }

                if (HasVisibleViewportIntersection(target, renderer.bounds))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 画面全体を横切る大きなBoundsなど、辺が画角外にある場合も検査します。
        /// </summary>
        private bool HasVisibleViewportIntersection(MicroUnitBehavior target, Bounds bounds)
        {
            for (int x = 0; x <= 2; x++)
            {
                for (int y = 0; y <= 2; y++)
                {
                    Ray ray = _sensorCamera.ViewportPointToRay(new Vector3(x * 0.5f, y * 0.5f, 0.0f));

                    if (bounds.IntersectRay(ray, out float distance) &&
                        IsVisibilitySampleValid(target, ray.GetPoint(Mathf.Max(0.0f, distance))))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 画角内の点へNearクリップ面から視線を通します。
        /// </summary>
        /// <remarks>
        /// 細い露出部分をSphereCastの半径で隠さないようRaycastを使用します。
        /// 正射影カメラでも、各画面位置に対応する視線の始点を使用します。
        /// </remarks>
        private bool IsVisibilitySampleValid(MicroUnitBehavior target, Vector3 point)
        {
            Vector3 viewport = _sensorCamera.WorldToViewportPoint(point);

            if (viewport.z < _sensorCamera.nearClipPlane - k_ViewportTolerance ||
                viewport.z > _sensorCamera.farClipPlane + k_ViewportTolerance ||
                viewport.x < -k_ViewportTolerance || viewport.x > 1.0f + k_ViewportTolerance ||
                viewport.y < -k_ViewportTolerance || viewport.y > 1.0f + k_ViewportTolerance)
            {
                return false;
            }

            Ray ray = _sensorCamera.ViewportPointToRay(viewport);
            Vector3 offset = point - ray.origin;
            float distance = offset.magnitude;

            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            Vector3 direction = offset / distance;

            return !HasBlockingHit(ray.origin, direction, distance, target) &&
                !HasBlockingHit(point, -direction, distance, target);
        }

        /// <summary>
        /// 所有者と対象自身を除き、視線上に遮蔽物が存在するか判定します。
        /// </summary>
        /// <remarks>
        /// 非Trigger Colliderは遮蔽物として扱います。Unitの検出用Triggerは無視し、
        /// その他のTriggerは有効なRendererを持つ場合だけ遮蔽物として扱います。
        /// </remarks>
        private bool HasBlockingHit(
            Vector3 origin,
            Vector3 direction,
            float distance,
            MicroUnitBehavior target)
        {
            UnitBehavior ownerUnit = _ownerUnitBehavior.GetUnitBehavior();
            UnitBehavior targetUnit = target.GetUnitBehavior();
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                distance,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;

                if (collider == null)
                {
                    continue;
                }

                UnitBehavior hitUnit = collider.GetComponentInParent<UnitBehavior>();

                if (hitUnit != null && (hitUnit == ownerUnit || hitUnit == targetUnit))
                {
                    continue;
                }

                if (!collider.isTrigger)
                {
                    return true;
                }

                if (hitUnit != null)
                {
                    continue;
                }

                Renderer obstacle = collider.GetComponent<Renderer>() ?? collider.GetComponentInParent<Renderer>() ??
                    collider.GetComponentInChildren<Renderer>(true);

                if (obstacle != null && obstacle.enabled && obstacle.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 視認を失った対象を解除します。
        /// </summary>
        private void RemoveVisibleTarget(MicroUnitBehavior target)
        {
            if (!_visibleUnits.Remove(target))
            {
                return;
            }

            if (target != null)
            {
                _ownerUnitBehavior?.RemoveVisiblePlayer(target);
                LogVisibility(target, false);
            }
        }

        /// <summary>
        /// センサーが保持している視認状態をすべて解除します。
        /// </summary>
        private void ClearVisibleUnits()
        {
            _removalBuffer.Clear();
            _removalBuffer.AddRange(_visibleUnits);

            foreach (MicroUnitBehavior target in _removalBuffer)
            {
                RemoveVisibleTarget(target);
            }

            _removalBuffer.Clear();
        }

        /// <summary>
        /// シリアライズされた所有者参照を維持し、未設定の参照を補完します。
        /// </summary>
        private void CacheComponents()
        {
            if (_ownerUnitBehavior == null)
            {
                _ownerUnitBehavior = GetComponentInParent<MicroUnitBehavior>(true);
            }

            if (_sensorCamera == null)
            {
                _sensorCamera = GetComponent<Camera>();
            }

            if (_sensorCollider == null)
            {
                _sensorCollider = GetComponent<SphereCollider>();
            }
        }

        /// <summary>
        /// 視認状態が変化した場合の診断情報を出力します。
        /// </summary>
        private void LogVisibility(MicroUnitBehavior target, bool isVisible)
        {
            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(VisionSensor)}: Viewer={_ownerUnitBehavior.PlayerId()}, Target={target.PlayerId()}, IsVisible={isVisible}",
                    this);
            }
        }
    }
}
