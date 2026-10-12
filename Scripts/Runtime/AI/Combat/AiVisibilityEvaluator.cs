using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 所有MicroUnitのVisionSensorを使用して直接視認を判定します。
    /// 射程と方向はSoloAiAgent側でMapUnitから計算します。
    /// </summary>
    public static class AiVisibilityEvaluator
    {
        private const float k_DefaultSightHeight = 1.5f;

        private const float k_LineOfSightProbeRadius = 0.08f;

        private static readonly HashSet<int> s_missingSensorWarnings = new HashSet<int>();

        /// <summary>
        /// 視認センサーの検出範囲内で発見した敵だけを観測対象にします。
        /// </summary>
        /// <remarks>
        /// 攻撃時の射線確認と異なり、遠方の未発見の敵を観測済みにしません。
        /// </remarks>
        public static bool IsDetectedBySensor(UnitBehavior observer, UnitBehavior target)
        {
            MicroUnitBehavior observerMicro = observer != null ? observer.GetMicroUnitBehavior() : null;
            MicroUnitBehavior targetMicro = target != null ? target.GetMicroUnitBehavior() : null;

            if (observerMicro == null || targetMicro == null || !observerMicro.gameObject.activeInHierarchy ||
                !targetMicro.gameObject.activeInHierarchy)
            {
                return false;
            }

            VisionSensor sensor = observerMicro.GetComponentInChildren<VisionSensor>(true);

            return sensor != null && sensor.ContainsVisibleUnit(targetMicro) &&
                sensor.CanSeeUnitNow(
                targetMicro);
        }

        /// <summary>
        /// observerのMicroUnitからtargetのMicroUnitを現在直接視認できるか
        /// 判定します。
        /// </summary>
        /// <remarks>
        /// <para>通常弾・追尾弾・スナイパーの対象取得も同じAPIを使います。</para>
        /// <para>AI独自Raycastとの判定差により、選択済みなのに武器側で</para>
        /// <para>対象を取得できない状態を防止します。</para>
        /// </remarks>
        public static bool CanDirectlySee(UnitBehavior observer, UnitBehavior target)
        {
            if (observer == null || target == null)
            {
                return false;
            }

            MicroUnitBehavior observerMicro = observer.GetMicroUnitBehavior();
            MicroUnitBehavior targetMicro = target.GetMicroUnitBehavior();

            if (observerMicro == null || targetMicro == null || !observerMicro.gameObject.activeInHierarchy ||
                !targetMicro.gameObject.activeInHierarchy)
            {
                return false;
            }

            VisionSensor visionSensor = observerMicro.GetComponentInChildren<VisionSensor>(true);

            if (visionSensor == null)
            {
                if (s_missingSensorWarnings.Add(observer.PlayerId))
                {
                    Debug.LogWarning(
                        nameof(AiVisibilityEvaluator) + ": " + nameof(VisionSensor) +
                        "を取得できないため、直接視認不可として扱います。" +
                        " Observer=" +
                        observer.PlayerId,
                        observerMicro);
                }

                return false;
            }

            return visionSensor.CanSeeUnitNow(targetMicro);
        }

        /// <summary>
        /// Macro経路候補のMap座標から、対象Map座標へ移動した場合の
        /// Micro空間上の射線を予測します。
        /// </summary>
        /// <remarks>
        /// Cameraの画角内に入るかは判定せず、両者の間に遮蔽物が
        /// 存在するかだけを判定します。射程は呼び出し側でMapUnitを
        /// 基準に別途判定します。
        /// </remarks>
        public static bool CanDirectlySeeFromMapPosition(
            UnitBehavior observer,
            Vector3 observerMapWorldPosition,
            UnitBehavior target,
            Vector3 targetMapWorldPosition)
        {
            if (observer == null || target == null)
            {
                return false;
            }

            Vector3 origin = observerMapWorldPosition + Vector3.up * k_DefaultSightHeight;
            MicroUnitBehavior observerMicro = observer.GetMicroUnitBehavior();
            TacticalMapUnitBehavior observerMap = observer.GetTacticalMapUnitBehavior();

            if (observerMicro != null && observerMap != null)
            {
                Camera observerCamera = observerMicro.GetCamera();

                if (observerCamera != null)
                {
                    origin = observerMapWorldPosition + observerCamera.transform.position - observerMap.transform.position;
                }
            }

            Vector3 targetPosition = ResolvePredictedTargetPosition(target, targetMapWorldPosition);
            Vector3 direction = targetPosition - origin;
            float distance = direction.magnitude;

            if (distance <= Mathf.Epsilon)
            {
                return true;
            }

            direction /= distance;

            return !HasBlockingHit(origin, direction, distance, observer, target) &&
                !HasBlockingHit(
                targetPosition,
                -direction,
                distance,
                observer,
                target);
        }

        private static Vector3 ResolvePredictedTargetPosition(
            UnitBehavior target,
            Vector3 targetMapWorldPosition)
        {
            Vector3 fallback = targetMapWorldPosition + Vector3.up * k_DefaultSightHeight;

            if (target == null)
            {
                return fallback;
            }

            TacticalMapUnitBehavior targetMap = target.GetTacticalMapUnitBehavior();
            MicroUnitBehavior targetMicro = target.GetMicroUnitBehavior();
            GameObject targetModel = targetMicro != null ? targetMicro.GetModel() : null;

            if (targetMap == null || targetModel == null)
            {
                return fallback;
            }

            Renderer[] renderers = targetModel.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds();

            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null || !targetRenderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetRenderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return fallback;
            }

            return targetMapWorldPosition + bounds.center - targetMap.transform.position;
        }

        private static bool HasBlockingHit(
            Vector3 origin,
            Vector3 direction,
            float distance,
            UnitBehavior observer,
            UnitBehavior target)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                origin,
                k_LineOfSightProbeRadius,
                direction,
                distance,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            foreach (RaycastHit hit in hits)
            {
                Collider hitCollider = hit.collider;

                if (hitCollider == null)
                {
                    continue;
                }

                UnitBehavior hitUnit = hitCollider.GetComponentInParent<UnitBehavior>();

                if (hitUnit == observer || hitUnit == target)
                {
                    continue;
                }

                if (!hitCollider.isTrigger)
                {
                    return true;
                }

                if (hitUnit != null)
                {
                    continue;
                }

                Renderer obstacleRenderer = hitCollider.GetComponent<Renderer>();

                if (obstacleRenderer == null)
                {
                    obstacleRenderer = hitCollider.GetComponentInParent<Renderer>();
                }

                if (obstacleRenderer == null)
                {
                    obstacleRenderer = hitCollider.GetComponentInChildren<Renderer>(true);
                }

                if (obstacleRenderer != null && obstacleRenderer.enabled &&
                    obstacleRenderer.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
