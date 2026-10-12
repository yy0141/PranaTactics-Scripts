using System;

namespace PranaTactics.Unit
{
    /// <summary>
    /// 通信とUnityシリアライズで共有する、Unit生成時のステータス上書きDTOです。
    /// </summary>
    /// <remarks>
    /// NullableはJsonUtilityで保存できないため、指定有無と値を別々に保持します。
    /// </remarks>
    [Serializable]
    public sealed class UnitInitialStats
    {
        /// <summary>
        /// 移動速度を上書きするかどうかです。
        /// </summary>
        public bool HasMoveSpeed;

        /// <summary>
        /// 指定された移動速度です。
        /// </summary>
        public float MoveSpeed;

        /// <summary>
        /// 最大・初期プラーナ量を上書きするかどうかです。
        /// </summary>
        public bool HasPranaCapacity;

        /// <summary>
        /// 指定された最大・初期プラーナ量です。
        /// </summary>
        public float PranaCapacity;

        /// <summary>
        /// 各Macro Phaseの行動力を上書きするかどうかです。
        /// </summary>
        public bool HasActionPoint;

        /// <summary>
        /// 指定された行動力です。
        /// </summary>
        public int ActionPoint;

        /// <summary>
        /// 生成情報の指定値が有効かどうかを確認します。
        /// </summary>
        public bool IsValid()
        {
            return (!HasMoveSpeed || (MoveSpeed >= 0.0f && !float.IsNaN(MoveSpeed) && !float.IsInfinity(MoveSpeed))) &&
                (!HasPranaCapacity || (PranaCapacity > 0.0f && !float.IsNaN(PranaCapacity) && !float.IsInfinity(PranaCapacity))) &&
                (!HasActionPoint || ActionPoint >= 0);
        }
    }
}
