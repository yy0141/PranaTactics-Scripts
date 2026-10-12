using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 共通の対象選択ルールで選ばれた敵へ、
    /// 複数のプラーナを同時に発射する散弾型プラーナムです。
    /// </summary>
    public sealed class ShotgunGunnerBehavior : GunnerBase
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
        /// ハイライトされている最も近い対象へ、
        /// 設定された弾数のプラーナを発射します。
        /// </summary>
        public override void UsePranam()
        {
            if (!CanFire())
            {
                return;
            }

            MicroUnitBehavior ownerMicroUnit =
                _unitBehavior.GetMicroUnitBehavior();

            if (ownerMicroUnit == null)
            {
                Debug.LogWarning(
                    $"{nameof(ShotgunGunnerBehavior)}: " +
                    $"{nameof(MicroUnitBehavior)}を取得できないため、" +
                    "プラーナを発射できません。",
                    this);

                return;
            }

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

            int projectileCount =
                Mathf.Max(
                    0,
                    GunnerData.BulletCount);

            if (projectileCount == 0)
            {
                Debug.LogWarning(
                    $"{nameof(ShotgunGunnerBehavior)}: " +
                    "発射数が0に設定されています。" +
                    $" Pranam={GunnerData.DisplayName}",
                    GunnerData);

                return;
            }

            for (int projectileIndex = 0;
                projectileIndex < projectileCount;
                projectileIndex++)
            {
                CreateAttackObject(
                    _playerId,
                    generatePosition,
                    targetPlayerId);
            }

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
        /// 現在の状態で散弾を発射できるか判定します。
        /// </summary>
        /// <returns>
        /// 初期化、発射間隔、行動力およびプラーナ量の
        /// 条件を満たしている場合は<c>true</c>です。
        /// </returns>
        private bool CanFire()
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

            if (GunnerData.BulletCount <= 0)
            {
                return false;
            }

            return _pranamBehavior.CanUsePranam(
                GunnerData.ActionPointCost,
                GunnerData.PranaCost);
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
