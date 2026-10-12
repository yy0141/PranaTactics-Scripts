using System;

namespace PranaTactics.Match.Solo
{
    /// <summary>
    /// ステージで指定したUnitステータスを保持し、省略値をキャラクター設定へ委ねます。
    /// </summary>
    public sealed class SoloUnitStats
    {
        public float? MoveSpeed { get; }
        public float? PranaCapacity { get; }
        public int? ActionPoint { get; }

        /// <summary>
        /// 移動速度と行動力は0以上、プラーナ量は正の有限値で検証します。
        /// </summary>
        /// <remarks>
        /// プラーナ量は初期HPでもあるため、0以下の値でUnitを生成できません。
        /// </remarks>
        /// <param name="moveSpeed">
        /// 基本移動速度です。未指定の場合はキャラクター設定を使います。
        /// </param>
        /// <param name="pranaCapacity">
        /// 最大・初期プラーナ量です。未指定の場合はキャラクター設定を使います。
        /// </param>
        /// <param name="actionPoint">
        /// 初期行動力と各Macro Phaseの回復量です。未指定の場合はキャラクター設定を使います。
        /// </param>
        public SoloUnitStats(float? moveSpeed, float? pranaCapacity, int? actionPoint)
        {
            if (moveSpeed.HasValue && (moveSpeed.Value < 0.0f || float.IsNaN(moveSpeed.Value) || float.IsInfinity(moveSpeed.Value)))
            {
                throw new ArgumentException("stats.moveSpeedは0以上の有限値で指定してください。", nameof(moveSpeed));
            }

            if (pranaCapacity.HasValue && (pranaCapacity.Value <= 0.0f || float.IsNaN(pranaCapacity.Value) || float.IsInfinity(pranaCapacity.Value)))
            {
                throw new ArgumentException("stats.pranaCapacityは0より大きい有限値で指定してください。", nameof(pranaCapacity));
            }

            if (actionPoint.HasValue && actionPoint.Value < 0)
            {
                throw new ArgumentException("stats.actionPointは0以上の整数で指定してください。", nameof(actionPoint));
            }

            MoveSpeed = moveSpeed;
            PranaCapacity = pranaCapacity;
            ActionPoint = actionPoint;
        }
    }
}
