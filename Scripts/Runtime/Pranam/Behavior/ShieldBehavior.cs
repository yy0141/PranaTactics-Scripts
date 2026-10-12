using UnityEngine;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// プレイヤーの入力に応じて所有ユニットの位置へ
    /// シールドを生成する防御型プラーナムです。
    /// </summary>
    public sealed class ShieldBehavior : ShieldBase
    {
        /// <summary>
        /// 所有ユニットの位置へシールドを生成し、
        /// 行動力とプラーナを消費します。
        /// </summary>
        public override void UsePranam()
        {
            if (!CanDeployShield())
            {
                return;
            }

            Vector3 generatePosition =
                MicroUnitBehavior.transform.position;

            CreateShieldObject(
                _playerId,
                IsMain,
                true,
                generatePosition,
                Quaternion.identity);

            _pranamBehavior.ConsumeCosts(
                _playerId,
                ShieldData.ActionPointCost,
                ShieldData.PranaCost);

            _pranamBehavior.SetRecoveryTime(
                IsMain,
                Number,
                ShieldData.RecoveryTime);

            _time = 0.0f;
        }

        /// <summary>
        /// 現在の状態でシールドを生成できるか判定します。
        /// </summary>
        /// <returns>
        /// 必要な参照が存在し、行動力とプラーナが
        /// 十分に残っている場合は<c>true</c>です。
        /// </returns>
        private bool CanDeployShield()
        {
            if (!_isInitialized ||
                ShieldData == null ||
                MicroUnitBehavior == null ||
                _pranamBehavior == null)
            {
                return false;
            }

            return _pranamBehavior.CanUsePranam(
                ShieldData.ActionPointCost,
                ShieldData.PranaCost);
        }
    }
}
