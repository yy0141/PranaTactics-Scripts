using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// ステージJSONから指定するAI難易度の差分を保持します。
    /// </summary>
    /// <remarks>
    /// 省略値は指定された難易度プリセットを使用します。秒、角度、倍率は有限値で指定します。
    /// </remarks>
    public sealed class AiDifficultyOverrides
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AiDifficulty? Difficulty { get; set; }
        public float? ReactionTime { get; set; }
        public float? AimErrorDegrees { get; set; }
        public float? AimToleranceDegrees { get; set; }
        public float? DecisionInterval { get; set; }
        public float? FireIntervalMultiplier { get; set; }
        public float? MacroDangerMultiplier { get; set; }
        public float? CoordinationTolerance { get; set; }

        /// <summary>
        /// 非有限値、範囲外の値と未知の難易度を生成前に拒否します。
        /// </summary>
        public void Validate()
        {
            if (Difficulty.HasValue && !Enum.IsDefined(typeof(AiDifficulty), Difficulty.Value))
            {
                throw new ArgumentException("enemyAi.difficultyが不正です。");
            }
            ValidateRange(ReactionTime, 0.01f, 5.00f, "ReactionTime");
            ValidateRange(AimErrorDegrees, 0.00f, 45.00f, "AimErrorDegrees");
            ValidateRange(AimToleranceDegrees, 0.10f, 45.00f, "AimToleranceDegrees");
            ValidateRange(DecisionInterval, 0.05f, 2.00f, "DecisionInterval");
            ValidateRange(FireIntervalMultiplier, 0.10f, 3.00f, "FireIntervalMultiplier");
            ValidateRange(MacroDangerMultiplier, 0.10f, 3.00f, "MacroDangerMultiplier");
            ValidateRange(CoordinationTolerance, 0.06f, 1.00f, "CoordinationTolerance");
        }

        /// <summary>
        /// 指定された値だけを検証し、省略値を維持します。
        /// </summary>
        private static void ValidateRange(float? value, float minimum, float maximum, string name)
        {
            if (value.HasValue && (float.IsNaN(value.Value) || float.IsInfinity(value.Value) ||
                value.Value < minimum || value.Value > maximum))
            {
                throw new ArgumentException($"enemyAi.{name}は{minimum}～{maximum}の有限値で指定してください。");
            }
        }
    }
}
