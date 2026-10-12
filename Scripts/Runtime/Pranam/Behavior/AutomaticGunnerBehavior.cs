using UnityEngine;

using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 使用ボタンを押している間、一定間隔で射撃を続ける
    /// 連射型ガンナープラーナムです。
    /// </summary>
    public class AutomaticGunnerBehavior : GunnerBase
    {
        /// <summary>
        /// 弾を生成するユニット位置からの距離です。
        /// </summary>
        private const float k_GeneratePositionOffset = 3.5f;

        /// <summary>
        /// 方向ベクトルを有効と判断する最小二乗距離です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude = 0.0001f;

        /// <summary>
        /// 有効な対象が存在しないことを表すプレイヤーIDです。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// 現在連射中かどうかを示します。
        /// </summary>
        private bool _isFiring;

        public override bool IsContinuousUseActive => _isFiring;

        /// <summary>
        /// 武器を非表示にしたとき、連射状態を解除します。
        /// </summary>
        protected override void OnDisableInternal()
        {
            StopFiring();
            base.OnDisableInternal();
        }

        public override bool CanUseHold
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// プラーナムの使用ボタンが離されたとき、
        /// 現在の連射状態を終了します。
        /// </summary>
        public override void ReleasePranam()
        {
            StopFiring();
        }

        /// <summary>
        /// 現在の対象選択規則で取得した対象へ弾を発射します。
        /// </summary>
        /// <remarks>
        /// 行動力は連射開始時の1発目だけ消費し、
        /// プラーナは発射するたびに消費します。
        /// </remarks>
        public override void UsePranam()
        {
            if (!CanAttemptShot())
            {
                return;
            }

            MicroUnitBehavior ownerMicroUnit =
                _unitBehavior.GetMicroUnitBehavior();

            if (ownerMicroUnit == null)
            {
                StopFiring();

                return;
            }

            UnitBehavior targetUnit =
                GetAttackTarget();

            if (!TryGetTargetPlayerId(
                    targetUnit,
                    out int targetPlayerId))
            {
                StopFiring();

                return;
            }

            bool isFirstShot =
                !_isFiring;

            int actionPointCost =
                isFirstShot
                    ? GunnerData.ActionPointCost
                    : 0;

            int pranaCost =
                GunnerData.PranaCost;

            if (!_pranamBehavior.CanUsePranam(
                    actionPointCost,
                    pranaCost))
            {
                StopFiring();

                return;
            }

            Vector3 generatePosition =
                CalculateGeneratePosition(
                    ownerMicroUnit.transform);

            CreateAutomaticAttackObject(
                _playerId,
                generatePosition,
                targetPlayerId,
                GunnerData.FireInterval,
                actionPointCost,
                pranaCost,
                isFirstShot);

            _isFiring = true;
        }

        /// <summary>
        /// 現在の状態で弾の発射処理を開始できるか判定します。
        /// </summary>
        /// <returns>
        /// 初期化が完了しており、発射間隔を満たしている場合は
        /// <c>true</c>です。
        /// </returns>
        private bool CanAttemptShot()
        {
            if (!_isInitialized ||
                GunnerData == null ||
                _pranamBehavior == null ||
                _unitBehavior == null)
            {
                return false;
            }

            return _time >=
                GunnerData.FireInterval;
        }

        /// <summary>
        /// 対象ユニットから有効なプレイヤーIDを取得します。
        /// </summary>
        /// <param name="targetUnit">
        /// IDを取得する対象ユニットです。
        /// </param>
        /// <param name="targetPlayerId">
        /// 取得されたプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 有効な対象プレイヤーIDを取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetTargetPlayerId(
            UnitBehavior targetUnit,
            out int targetPlayerId)
        {
            targetPlayerId =
                k_InvalidPlayerId;

            if (targetUnit == null ||
                targetUnit.GetMicroUnitBehavior() == null)
            {
                return false;
            }

            targetPlayerId =
                targetUnit.PlayerId;

            return targetPlayerId >= 0;
        }

        /// <summary>
        /// ユニットの向きとメイン・サブの装備位置から、
        /// 弾を生成するワールド座標を計算します。
        /// </summary>
        /// <param name="ownerTransform">
        /// プラーナムを所有するユニットのTransformです。
        /// </param>
        /// <returns>
        /// 計算された弾の生成位置です。
        /// </returns>
        private Vector3 CalculateGeneratePosition(
            Transform ownerTransform)
        {
            if (ownerTransform == null)
            {
                return transform.position;
            }

            Vector3 forward =
                Vector3.ProjectOnPlane(
                    ownerTransform.forward,
                    Vector3.up);

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

            if (!_isMain)
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
        /// 現在の連射状態を終了します。
        /// </summary>
        private void StopFiring()
        {
            _isFiring = false;
        }
    }
}
