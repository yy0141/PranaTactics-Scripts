using UnityEngine;

namespace PranaTactics.AI.Solo
{
    public enum AiDifficulty
    {
        Easy,
        Normal,
        Hard,
        Expert,
        Disaster
    }

    /// <summary>
    /// 実行中に参照する難易度パラメータです。
    /// </summary>
    /// <remarks>
    /// <para>ソロAIの難易度です。
    /// 判断規則は共通のまま、反応速度・照準誤差・危険許容度を変更します。</para>
    /// </remarks>
    public sealed class AiDifficultyProfile
    {
        public AiDifficulty Difficulty
        {
            get; private set;
        }

        public float ReactionTime
        {
            get; private set;
        }

        public float AimErrorDegrees
        {
            get; private set;
        }

        public float AimToleranceDegrees
        {
            get; private set;
        }

        public float TargetLeadRatio
        {
            get; private set;
        }

        public float DecisionInterval
        {
            get; private set;
        }

        public float TargetLockSeconds
        {
            get; private set;
        }

        public float FireIntervalMultiplier
        {
            get; private set;
        }

        public float MacroDangerMultiplier
        {
            get; private set;
        }

        public float UtilityNoise
        {
            get; private set;
        }

        public float CoordinationTolerance
        {
            get; private set;
        }

        public static AiDifficultyProfile Create(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy:
                    return new AiDifficultyProfile
                    {
                        Difficulty = difficulty,
                        ReactionTime = 0.60f,
                        AimErrorDegrees = 10.0f,
                        AimToleranceDegrees = 14.0f,
                        TargetLeadRatio = 0.45f,
                        DecisionInterval = 0.40f,
                        TargetLockSeconds = 1.5f,
                        FireIntervalMultiplier = 1.10f,
                        MacroDangerMultiplier = 0.85f,
                        UtilityNoise = 15.0f,
                        CoordinationTolerance = 0.45f
                    };
                case AiDifficulty.Hard:
                    return new AiDifficultyProfile
                    {
                        Difficulty = difficulty,
                        ReactionTime = 0.25f,
                        AimErrorDegrees = 4.0f,
                        AimToleranceDegrees = 10.0f,
                        TargetLeadRatio = 0.75f,
                        DecisionInterval = 0.22f,
                        TargetLockSeconds = 2.25f,
                        FireIntervalMultiplier = 0.80f,
                        MacroDangerMultiplier = 1.10f,
                        UtilityNoise = 6.0f,
                        CoordinationTolerance = 0.20f
                    };
                case AiDifficulty.Expert:
                    return new AiDifficultyProfile
                    {
                        Difficulty = difficulty,
                        ReactionTime = 0.15f,
                        AimErrorDegrees = 2.0f,
                        AimToleranceDegrees = 9.0f,
                        TargetLeadRatio = 0.90f,
                        DecisionInterval = 0.16f,
                        TargetLockSeconds = 2.50f,
                        FireIntervalMultiplier = 0.68f,
                        MacroDangerMultiplier = 1.15f,
                        UtilityNoise = 3.0f,
                        CoordinationTolerance = 0.12f
                    };
                case AiDifficulty.Disaster:
                    return new AiDifficultyProfile
                    {
                        Difficulty = difficulty,
                        ReactionTime = 0.08f,
                        AimErrorDegrees = 0.75f,
                        AimToleranceDegrees = 8.0f,
                        TargetLeadRatio = 1.0f,
                        DecisionInterval = 0.12f,
                        TargetLockSeconds = 2.75f,
                        FireIntervalMultiplier = 0.55f,
                        MacroDangerMultiplier = 1.20f,
                        UtilityNoise = 0.0f,
                        CoordinationTolerance = 0.06f
                    };
                default:
                    return new AiDifficultyProfile
                    {
                        Difficulty = AiDifficulty.Normal,
                        ReactionTime = 0.40f,
                        AimErrorDegrees = 7.0f,
                        AimToleranceDegrees = 11.0f,
                        TargetLeadRatio = 0.60f,
                        DecisionInterval = 0.30f,
                        TargetLockSeconds = 2.0f,
                        FireIntervalMultiplier = 0.95f,
                        MacroDangerMultiplier = 1.0f,
                        UtilityNoise = 10.0f,
                        CoordinationTolerance = 0.30f
                    };
            }
        }

        /// <summary>
        /// プリセットへステージ固有の指定値だけを反映し、独立した実行時設定を生成します。
        /// </summary>
        public static AiDifficultyProfile Create(AiDifficulty fallback, AiDifficultyOverrides overrides)
        {
            overrides?.Validate();
            AiDifficultyProfile profile = Create(overrides?.Difficulty ?? fallback);
            if (overrides == null)
            {
                return profile;
            }

            profile.ReactionTime = overrides.ReactionTime ?? profile.ReactionTime;
            profile.AimErrorDegrees = overrides.AimErrorDegrees ?? profile.AimErrorDegrees;
            profile.AimToleranceDegrees = overrides.AimToleranceDegrees ?? profile.AimToleranceDegrees;
            profile.DecisionInterval = overrides.DecisionInterval ?? profile.DecisionInterval;
            profile.FireIntervalMultiplier = overrides.FireIntervalMultiplier ?? profile.FireIntervalMultiplier;
            profile.MacroDangerMultiplier = overrides.MacroDangerMultiplier ?? profile.MacroDangerMultiplier;
            profile.CoordinationTolerance = overrides.CoordinationTolerance ?? profile.CoordinationTolerance;
            return profile;
        }

        public float ApplyMacroDanger(float danger)
        {
            return Mathf.Max(0.0f, danger) * MacroDangerMultiplier;
        }
    }
}
