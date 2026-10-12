using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 共通の対象選択ルールで選ばれた敵へ、
    /// 高速な狙撃用プラーナを1発発射します。
    /// </summary>
    public sealed class SniperBehavior : SniperBase
    {
        /// <summary>
        /// 所有ユニットの中心からプラーナを生成する位置までの距離です。
        /// </summary>
        private const float k_GeneratePositionOffset = 3.5f;

        /// <summary>
        /// 方向ベクトルを有効と判断する最小二乗距離です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude = 0.0001f;

        /// <summary>
        /// 現在狙撃可能な対象を取得します。
        /// </summary>
        protected override bool TryAcquireSniperTarget(
            out UnitBehavior targetUnit)
        {
            targetUnit =
                null;

            MicroUnitBehavior ownerMicroUnit =
                MicroUnitBehavior;

            if (ownerMicroUnit == null)
            {
                return false;
            }

            targetUnit =
                GetAttackTarget();

            return targetUnit != null;
        }

        protected override bool FireSniperShot(
            UnitBehavior targetUnit)
        {
            if (!CanFire())
            {
                return false;
            }

            MicroUnitBehavior ownerMicroUnit =
                MicroUnitBehavior;

            if (ownerMicroUnit == null)
            {
                return false;
            }

            if (targetUnit == null)
            {
                return false;
            }

            if (!TryGetTargetInformation(
                    targetUnit,
                    out int targetPlayerId,
                    out Vector3 targetPosition))
            {
                return false;
            }

            Vector3 generatePosition =
                CalculateGeneratePosition(
                    ownerMicroUnit.transform,
                    out Vector3 ownerForward);

            Quaternion generateRotation =
                CalculateGenerateRotation(
                    generatePosition,
                    targetPosition,
                    ownerForward);

            CreateAttackObject(
                _playerId,
                generatePosition,
                generateRotation,
                targetPlayerId);

            Vector3 soundForward =
                generateRotation *
                Vector3.forward;

            _pranamBehavior.RequestPranamSound(
                SniperData,
                PranamSoundEvent.Fire,
                generatePosition,
                soundForward);

            _pranamBehavior.ConsumeCosts(
                _playerId,
                SniperData.ActionPointCost,
                SniperData.PranaCost);

            _pranamBehavior.ApplyRecoveryLock(
                IsMain,
                Number,
                SniperData.RecoveryTime);

            _time =
                0.0f;

            return true;
        }

        /// <summary>
        /// 現在の状態で狙撃用プラーナを発射できるか判定します。
        /// </summary>
        /// <returns>
        /// 初期化が完了し、必要な行動力とプラーナが
        /// 残っている場合は<c>true</c>です。
        /// </returns>
        private bool CanFire()
        {
            if (!_isInitialized ||
                SniperData == null ||
                MicroUnitBehavior == null ||
                _pranamBehavior == null)
            {
                return false;
            }

            return _pranamBehavior.CanUsePranam(
                SniperData.ActionPointCost,
                SniperData.PranaCost);
        }

        /// <summary>
        /// 対象ユニットからプレイヤーIDと現在位置を取得します。
        /// </summary>
        /// <param name="targetUnit">
        /// 情報を取得する対象ユニットです。
        /// </param>
        /// <param name="targetPlayerId">
        /// 取得された対象プレイヤーIDです。
        /// </param>
        /// <param name="targetPosition">
        /// 取得された対象ユニットのワールド座標です。
        /// </param>
        /// <returns>
        /// 有効な対象情報を取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetTargetInformation(
            UnitBehavior targetUnit,
            out int targetPlayerId,
            out Vector3 targetPosition)
        {
            targetPlayerId =
                -1;

            targetPosition =
                Vector3.zero;

            if (targetUnit == null)
            {
                return false;
            }

            targetPlayerId =
                targetUnit.PlayerId;

            if (targetPlayerId < 0)
            {
                return false;
            }

            return TryGetTargetCenterPosition(
                targetUnit,
                out targetPosition);
        }

        /// <summary>
        /// ユニットの向きとメイン・サブの装備位置から、
        /// プラーナを生成するワールド座標を計算します。
        /// </summary>
        /// <param name="ownerTransform">
        /// プラーナムを所有するユニットのTransformです。
        /// </param>
        /// <param name="forward">
        /// 水平方向へ補正された所有ユニットの前方ベクトルです。
        /// </param>
        /// <returns>
        /// 計算されたプラーナの生成位置です。
        /// </returns>
        private Vector3 CalculateGeneratePosition(
            Transform ownerTransform,
            out Vector3 forward)
        {
            if (ownerTransform == null)
            {
                forward = Vector3.forward;

                return transform.position;
            }

            forward =
                Vector3.ProjectOnPlane(
                    ownerTransform.forward,
                    Vector3.up);

            if (forward.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                forward =
                    Vector3.ProjectOnPlane(
                        transform.forward,
                        Vector3.up);
            }

            if (forward.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                forward =
                    Vector3.forward;
            }

            forward.Normalize();

            Vector3 right =
                Vector3.Cross(
                    Vector3.up,
                    forward);

            if (!IsMain)
            {
                right = -right;
            }

            Vector3 diagonalDirection =
                forward + right;

            if (diagonalDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                diagonalDirection =
                    forward;
            }

            diagonalDirection.Normalize();

            return ownerTransform.position +
                diagonalDirection *
                k_GeneratePositionOffset;
        }

        /// <summary>
        /// 生成位置から対象へ向く回転を計算します。
        /// </summary>
        /// <param name="generatePosition">
        /// プラーナの生成位置です。
        /// </param>
        /// <param name="targetPosition">
        /// 攻撃対象の位置です。
        /// </param>
        /// <param name="fallbackForward">
        /// 対象方向を計算できない場合に使用する前方ベクトルです。
        /// </param>
        /// <returns>
        /// プラーナ生成時に使用する回転です。
        /// </returns>
        private static Quaternion CalculateGenerateRotation(
            Vector3 generatePosition,
            Vector3 targetPosition,
            Vector3 fallbackForward)
        {
            Vector3 direction =
                targetPosition -
                generatePosition;

            if (direction.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                direction =
                    fallbackForward;
            }

            if (direction.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
        }
    }
}
