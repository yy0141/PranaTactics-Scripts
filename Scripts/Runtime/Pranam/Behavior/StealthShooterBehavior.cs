using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// MapUnit基準の扇形範囲内に存在し，
    /// マップ上で追跡できる複数の対象へ
    /// 分割したプラーナを均等に発射します。
    /// </summary>
    public sealed class StealthShooterBehavior : ShooterBase
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
        /// MapUnit基準の扇形範囲内に存在し，
        /// マップ上で追跡できる全ての対象へ，
        /// 分割したプラーナを均等に発射します。
        /// </summary>
        /// <remarks>
        /// 発射入力の同一フレームで候補を再構築し，
        /// ハイライトの有無とは独立したMapUnit基準の
        /// 共通の攻撃対象選択結果を発射対象の正とします。
        /// </remarks>
        public override void UsePranam()
        {
            if (!CanUseStealthShooter())
            {
                return;
            }

            MicroUnitBehavior ownerMicroUnit =
                _unitBehavior.GetMicroUnitBehavior();

            if (ownerMicroUnit == null)
            {
                Debug.LogWarning(
                    $"{nameof(StealthShooterBehavior)}: " +
                    $"{nameof(MicroUnitBehavior)}を取得できないため、" +
                    "プラーナを生成できません。",
                    this);

                return;
            }

            RefreshTargetsImmediately();

            List<int> targetPlayerIds =
                CollectTargetPlayerIds();

            if (targetPlayerIds.Count == 0)
            {
                return;
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
                return;
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
        }

        /// <summary>
        /// 現在の状態でステルス射出型プラーナムを
        /// 使用できるか判定します。
        /// </summary>
        /// <returns>
        /// 初期化が完了し、必要な行動力とプラーナが
        /// 残っている場合は<c>true</c>です。
        /// </returns>
        private bool CanUseStealthShooter()
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
        /// マップ上で追跡できる候補を複数対象の攻撃選択へ渡します。
        /// </summary>
        protected override bool CanSelectAttackTarget(UnitBehavior targetUnit)
        {
            return CanTargetMapUnit(targetUnit);
        }

        /// <summary>
        /// マップ上の距離を対象選択へ提供します。
        /// </summary>
        protected override bool TryGetTargetSquaredDistance(UnitBehavior targetUnit, out float squaredDistance)
        {
            return TryGetMapTargetSquaredDistance(targetUnit, out squaredDistance);
        }

        /// <summary>
        /// MapUnit基準の扇形範囲内に存在する対象から，
        /// 非ステルスまたは所有チームに発見されたプレイヤーIDの一覧を生成します。
        /// </summary>
        /// <returns>
        /// 重複を除外した対象プレイヤーID一覧です。
        /// </returns>
        private List<int> CollectTargetPlayerIds()
        {
            List<int> targetPlayerIds = new();

            HashSet<int> registeredPlayerIds = new();

            foreach (UnitBehavior targetUnit in
                    AttackTargets)
            {
                if (!CanTargetMapUnit(targetUnit))
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
        /// 水平方向へ補正された前方ベクトルです。
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
