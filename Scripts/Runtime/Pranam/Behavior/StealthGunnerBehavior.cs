using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// MapUnit基準の扇形範囲内に存在し、
    /// マップ上で追跡できる最も近い敵へ、
    /// 1回の入力につき1発のプラーナを発射します。
    /// </summary>
    /// <remarks>
    /// 攻撃範囲と発射対象はMapUnitから判定します。
    /// ハイライトは通常のPranamと同様に、所有MicroUnitの
    /// VisionSensorによる現在の視認結果だけを使用します。
    /// </remarks>
    public sealed class StealthGunnerBehavior : GunnerBase
    {
        /// <summary>
        /// 所有ユニットの中心からプラーナを生成する位置までの距離です。
        /// </summary>
        private const float k_GeneratePositionOffset =
            3.5f;

        /// <summary>
        /// 方向ベクトルを有効と判断する最小二乗距離です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude =
            0.0001f;

        /// <summary>
        /// MapUnit基準の扇形範囲内に存在する、
        /// マップ上で追跡できる最も近い敵へ1発発射します。
        /// </summary>
        /// <remarks>
        /// 発射入力を受けたフレームのMapUnit状態から、
        /// 扇形範囲内の対象一覧を再構築します。
        /// </remarks>
        public override void UsePranam()
        {
            if (!CanFireStealthGunner())
            {
                return;
            }

            MicroUnitBehavior ownerMicroUnit =
                _unitBehavior.GetMicroUnitBehavior();

            if (ownerMicroUnit == null)
            {
                Debug.LogWarning(
                    $"{nameof(StealthGunnerBehavior)}: " +
                    $"{nameof(MicroUnitBehavior)}を取得できないため、" +
                    "プラーナを発射できません。",
                    this);

                return;
            }

            RefreshTargetsImmediately();

            UnitBehavior targetUnit =
                GetAttackTarget();

            if (!TryGetTargetPlayerId(
                    targetUnit,
                    out int targetPlayerId))
            {
                return;
            }

            Vector3 generatePosition =
                CalculateGeneratePosition(
                    ownerMicroUnit.transform,
                    out Vector3 forward);

            CreateAttackObject(
                _playerId,
                generatePosition,
                targetPlayerId);

            _pranamBehavior.RequestPranamSound(
                GunnerData,
                PranamSoundEvent.Fire,
                generatePosition,
                forward);

            _pranamBehavior.ConsumeCosts(
                _playerId,
                GunnerData.ActionPointCost,
                GunnerData.PranaCost);

            _pranamBehavior.ApplyRecoveryLock(
                IsMain,
                Number,
                GunnerData.RecoveryTime);

            _time = 0.0f;
        }

        /// <summary>
        /// 現在の状態で単発射撃を実行できるか判定します。
        /// </summary>
        /// <returns>
        /// 初期化、射撃間隔、行動力およびプラーナ量の条件を
        /// 満たしている場合は<c>true</c>です。
        /// </returns>
        private bool CanFireStealthGunner()
        {
            if (!_isInitialized ||
                GunnerData == null ||
                _pranamBehavior == null ||
                _unitBehavior == null)
            {
                return false;
            }

            if (_time <
                GunnerData.FireInterval)
            {
                return false;
            }

            return _pranamBehavior.CanUsePranam(
                GunnerData.ActionPointCost,
                GunnerData.PranaCost);
        }

        /// <summary>
        /// マップ上で追跡できる候補を攻撃対象として選択します。
        /// </summary>
        protected override bool CanSelectAttackTarget(UnitBehavior targetUnit)
        {
            return CanTargetMapUnit(targetUnit);
        }

        /// <summary>
        /// マップ上の水平距離を共通の対象選択ルールへ提供します。
        /// </summary>
        protected override bool TryGetTargetSquaredDistance(UnitBehavior targetUnit, out float squaredDistance)
        {
            return TryGetMapTargetSquaredDistance(targetUnit, out squaredDistance);
        }

        /// <summary>
        /// 対象ユニットから有効なプレイヤーIDを取得します。
        /// </summary>
        /// <param name="targetUnit">
        /// プレイヤーIDを取得する対象です。
        /// </param>
        /// <param name="targetPlayerId">
        /// 取得されたプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 有効なプレイヤーIDを取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetTargetPlayerId(
            UnitBehavior targetUnit,
            out int targetPlayerId)
        {
            targetPlayerId = -1;

            if (!CanTargetMapUnit(
                    targetUnit))
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
        /// プラーナムを所有するMicroUnitのTransformです。
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
                forward +
                right;

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
