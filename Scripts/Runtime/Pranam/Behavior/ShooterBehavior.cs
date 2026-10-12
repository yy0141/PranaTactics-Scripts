using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// ハイライトされている複数の敵へ、
    /// 分割したプラーナを均等に割り当てて発射します。
    /// </summary>
    public sealed class ShooterBehavior : ShooterBase
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
        /// ハイライトされている全ての対象へ、
        /// 分割したプラーナを発射します。
        /// </summary>
        public override void UsePranam()
        {
            if (CanUseShooter())
            {
                TryGenerateShot(CollectTargetPlayerIds());
            }
        }

#if UNITY_EDITOR || PV_CAPTURE
        /// <summary>
        /// 撮影で指定された演者へ発射し、画面上のハイライト状態に依存せず生成成否を返します。
        /// </summary>
        public bool TryFireForCapture(UnitBehavior target)
        {
            if (target == null || !target.Status.IsAlive || !isActiveAndEnabled)
            {
                return false;
            }

            return TryGenerateShot(new List<int> { target.PlayerId });
        }

#endif

        /// <summary>
        /// 指定対象への実弾生成、消費、効果音、硬直を通常戦闘と撮影で共有します。
        /// </summary>
        private bool TryGenerateShot(List<int> targetPlayerIds)
        {
            if (!CanUseShooter())
            {
                return false;
            }

            MicroUnitBehavior ownerMicroUnit =
                _unitBehavior.GetMicroUnitBehavior();

            if (ownerMicroUnit == null)
            {
                Debug.LogWarning(
                    $"{nameof(ShooterBehavior)}: " +
                    $"{nameof(MicroUnitBehavior)}を取得できないため、" +
                    "プラーナを生成できません。",
                    this);

                return false;
            }

            if (targetPlayerIds.Count == 0)
            {
                return false;
            }

            Vector3 generatePosition =
                CalculateGeneratePosition(
                    ownerMicroUnit.transform,
                    out Vector3 forward);

            bool hasGeneratedPrana =
                GenerateDistributedPranaCube(
                    generatePosition,
                    targetPlayerIds);

            if (!hasGeneratedPrana)
            {
                return false;
            }

            _pranamBehavior.RequestPranamSound(
                ShooterData,
                PranamSoundEvent.Generate,
                generatePosition,
                forward);

            _pranamBehavior.ConsumeCosts(
                _playerId,
                ShooterData.ActionPointCost,
                ShooterData.PranaCost);

            _pranamBehavior.ApplyRecoveryLock(
                IsMain,
                Number,
                ShooterData.RecoveryTime);

            _time = 0.0f;

            return true;
        }

        /// <summary>
        /// 現在の状態で射出型プラーナムを使用できるか判定します。
        /// </summary>
        /// <returns>
        /// 初期化が完了し、必要な行動力とプラーナが
        /// 残っている場合は<c>true</c>です。
        /// </returns>
        private bool CanUseShooter()
        {
            if (!_isInitialized ||
                ShooterData == null ||
                _pranamBehavior == null ||
                _unitBehavior == null)
            {
                return false;
            }

            return _pranamBehavior.CanUsePranam(
                ShooterData.ActionPointCost,
                ShooterData.PranaCost);
        }

        /// <summary>
        /// 現在ハイライトされている対象から、
        /// 有効なプレイヤーIDの一覧を生成します。
        /// </summary>
        /// <returns>
        /// 重複を除外した対象プレイヤーID一覧です。
        /// </returns>
        /// <remarks>
        /// ハイライトのキャッシュが同一フレーム内に残っていても，
        /// 発射直前のMicroUnit視認判定に失敗した対象は除外します。
        /// </remarks>
        private List<int> CollectTargetPlayerIds()
        {
            List<int> targetPlayerIds = new();

            HashSet<int> registeredPlayerIds = new();

            foreach (UnitBehavior targetUnit in
                    AttackTargets)
            {

                if (!CanSeeTarget(targetUnit))
                {
                    continue;
                }

                if (!TryGetTargetPlayerId(
                        targetUnit,
                        out int targetPlayerId))
                {
                    continue;
                }

                if (!registeredPlayerIds.Add(
                        targetPlayerId))
                {
                    continue;
                }

                targetPlayerIds.Add(
                    targetPlayerId);
            }

            return targetPlayerIds;
        }

        /// <summary>
        /// 対象ユニットから有効なプレイヤーIDを取得します。
        /// </summary>
        /// <param name="targetUnit">
        /// プレイヤーIDを取得する対象ユニットです。
        /// </param>
        /// <param name="targetPlayerId">
        /// 取得されたプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 有効なプレイヤーIDを取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetTargetPlayerId(
            UnitBehavior targetUnit,
            out int targetPlayerId)
        {
            targetPlayerId = -1;

            if (targetUnit == null)
            {
                return false;
            }

            MicroUnitBehavior targetMicroUnit =
                targetUnit.GetMicroUnitBehavior();

            if (targetMicroUnit == null)
            {
                return false;
            }

            targetPlayerId =
                targetUnit.PlayerId;

            return targetPlayerId >= 0;
        }

        /// <summary>
        /// ユニットの向きとメイン・サブの装備位置から、
        /// プラーナを生成するワールド座標を計算します。
        /// </summary>
        /// <param name="ownerTransform">
        /// プラーナムを所有するユニットのTransformです。
        /// </param>
        /// <param name="forward">
        /// 水平方向へ補正されたユニットの前方ベクトルです。
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
                forward = Vector3.forward;
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
    }
}
