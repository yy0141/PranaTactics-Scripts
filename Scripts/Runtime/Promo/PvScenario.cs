#if UNITY_EDITOR || PV_CAPTURE
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PranaTactics.Promo
{
    [Serializable]
    public sealed class PvScenario
    {
        /// <summary>
        /// floatの開始時刻と長さを加算する際、実行環境ごとに残る丸め誤差を吸収します。
        /// </summary>
        private const double k_EndTimeToleranceSeconds = 0.00001;

        public float Duration = 9;

        public bool ShowUi;

        public PvDuelDefinition Duel;

        [JsonProperty(Required = Required.Always)]
        public PvActorDefinition[] Actors;

        [JsonProperty(Required = Required.Always)]
        public PvCue[] Cues;

        public static PvScenario Parse(string json)
        {
            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error
            };

            settings.Converters.Add(new StringEnumConverter { AllowIntegerValues = false });
            PvScenario scenario = JsonConvert.DeserializeObject<PvScenario>(json, settings);

            if (scenario == null || !IsFinite(scenario.Duration) || scenario.Duration <= 0 ||
                scenario.Actors == null || scenario.Actors.Length == 0 || scenario.Cues == null)
            {
                throw new ArgumentException("Duration > 0 と Actors、Cues が必要です。");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (PvActorDefinition actor in scenario.Actors)
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.Id) || !ids.Add(actor.Id) ||
                    string.IsNullOrWhiteSpace(actor.Character) || actor.Team < 0 || !IsFinite(actor.Speed) || actor.Speed < 0)
                {
                    throw new ArgumentException("Actors: Idの重複、Character、Team、Speedを確認してください。");
                }
            }

            scenario.Duel?.Validate(ids);
            float previousTime = -1;

            for (int index = 0; index < scenario.Cues.Length; index++)
            {
                PvCue cue = scenario.Cues[index];

                ValidateCueTiming(cue, index, previousTime, scenario.Duration);

                if (!IsFinite(cue.Value) || !IsFinite(cue.Yaw) || !IsFinite(cue.Pitch) ||
                    !IsFinite(cue.Position.X) || !IsFinite(cue.Position.Y) || !IsFinite(cue.Position.Z))
                {
                    throw new ArgumentException(
                        $"Cues[{index}] {cue.Action} ({cue.Time:R}s): Value、Yaw、Pitch、Positionには有限の数値が必要です。");
                }

                previousTime = cue.Time;

                if ((cue.Action <= PvAction.Fire || cue.Action == PvAction.SelectWeapon ||
                    cue.Action == PvAction.CheckDamage) &&
                    !ids.Contains(cue.Actor ?? ""))
                {
                    throw new ArgumentException($"Cues: 未定義のActor '{cue.Actor}'。");
                }

                if (!string.IsNullOrEmpty(cue.Target) && !ids.Contains(cue.Target))
                {
                    throw new ArgumentException($"Cues: 未定義のTarget '{cue.Target}'。");
                }

                if (cue.Targets != null)
                {
                    var cameraTargets = new HashSet<string>(StringComparer.Ordinal);

                    foreach (string target in cue.Targets)
                    {
                        if (target == null || !ids.Contains(target) || !cameraTargets.Add(target))
                        {
                            throw new ArgumentException("CameraのTargetsに未定義または重複したActorがあります。");
                        }
                    }

                    if (cue.Action != PvAction.Camera || cue.Targets.Length == 0 ||
                        !string.IsNullOrEmpty(
                        cue.Target))
                    {
                        throw new ArgumentException("TargetsはCamera専用です。Targetと併用せず、1体以上指定してください。");
                    }
                }

                if (cue.Action == PvAction.Move && (cue.Path == null || cue.Path.Length == 0) ||
                    cue.Action == PvAction.Speed && cue.Value < 0 ||
                    cue.Action == PvAction.Fire && (cue.Duration <= 0 || cue.Slot < 0) ||
                    cue.Action == PvAction.SelectWeapon && cue.Slot < 0 ||
                    cue.Action == PvAction.Camera &&
                        (cue.FieldOfView < 1 || cue.FieldOfView > 179 ||
                        !IsFinite(
                        cue.FieldOfView)) ||
                    !IsFinite(cue.ImpactShake) || cue.ImpactShake < 0 || cue.ImpactShake > 2 ||
                    cue.Targets != null && cue.Position.X == 0 && cue.Position.Y == 0 && cue.Position.Z == 0 ||
                    cue.Action == PvAction.Audio && (cue.Value < 0 || cue.Value > 1) ||
                    cue.Action == PvAction.TimeScale && (cue.Value <= 0 || cue.Value > 4))
                {
                    throw new ArgumentException($"Cues: {cue.Time}s {cue.Action} の値が不正です。");
                }
            }

            return scenario;
        }

        /// <summary>
        /// 不正な時刻を個別に報告し、終端の加算に限って微小な丸め誤差を許容します。
        /// </summary>
        private static void ValidateCueTiming(PvCue cue, int index, float previousTime, float duration)
        {
            if (cue == null)
            {
                throw new ArgumentException($"Cues[{index}]: nullは指定できません。");
            }

            string label = $"Cues[{index}] {cue.Action} (Time={cue.Time:R}, Duration={cue.Duration:R})";

            if (!IsFinite(cue.Time) || !IsFinite(cue.Duration))
            {
                throw new ArgumentException($"{label}: TimeとDurationには有限の数値が必要です。");
            }

            if (cue.Time < 0 || cue.Time > duration || cue.Duration < 0)
            {
                throw new ArgumentException($"{label}: Timeは0〜{duration:R}秒、Durationは0以上にしてください。");
            }

            if (cue.Time < previousTime)
            {
                throw new ArgumentException($"{label}: 直前のTime={previousTime:R}秒より前です。Time順に並べてください。");
            }

            double endTime = (double)cue.Time + cue.Duration;

            if (endTime - duration > k_EndTimeToleranceSeconds)
            {
                throw new ArgumentException($"{label}: 終了時刻={endTime:R}秒がシナリオのDuration={duration:R}秒を超えています。");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    public sealed class PvDuelDefinition
    {
        public string Shooter = "A";

        public string Defender = "B";

        public string Flanker = "C";

        public int OpeningSlot;

        public int ShieldSlot = 3;

        public int FlankSlot;

        public float FireDelay = 0.8f;

        public float TravelCells = 2.5f;

        public float OrbitSeconds = 3;

        public float SlowScale = 0.12f;

        public float TurnSeconds = 0.65f;

        public float DefenseSeconds = 0.9f;

        public float DefenderViewHeight = 1.5f;

        public float OverheadHeightRatio = 1;

        public float TimeoutSeconds = 30;

        public bool IsolateOrbit;

        public void Validate(HashSet<string> ids)
        {
            if (!ids.Contains(Shooter ?? "") || !ids.Contains(Defender ?? "") || !ids.Contains(Flanker ?? "") ||
                Shooter == Defender || Shooter == Flanker || Defender == Flanker ||
                OpeningSlot < 0 || ShieldSlot < 0 || FlankSlot < 0 ||
                !IsPositive(FireDelay) || !IsPositive(TravelCells) || !IsPositive(OrbitSeconds) ||
                !IsPositive(TurnSeconds) || !IsPositive(DefenseSeconds) ||
                float.IsNaN(DefenderViewHeight) || float.IsInfinity(DefenderViewHeight) ||
                !IsPositive(OverheadHeightRatio) || OverheadHeightRatio > 1 ||
                !IsPositive(TimeoutSeconds) || !IsPositive(SlowScale) || SlowScale > 1 ||
                TimeoutSeconds <= FireDelay + OrbitSeconds + DefenseSeconds + TurnSeconds)
            {
                throw new ArgumentException("Duel: 異なる3人の演者、非負のSlot、正の有限時間・距離、0 < SlowScale <= 1を指定してください。");
            }
        }

        public static int GetCellDistance(PvActorDefinition first, PvActorDefinition second)
        {
            int deltaX = Math.Abs(first.X - second.X);
            int deltaY = Math.Abs(first.Y - second.Y);

            return Math.Max(deltaX, (deltaX + deltaY) / 2);
        }

        private static bool IsPositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        }
    }

    [Serializable]
    public sealed class PvActorDefinition
    {
        public string Id;

        public string Character;

        public int Team;

        public int X;

        public int Y;

        public float Speed = 20;
    }

    public enum PvAction
    {
        Move,
        Speed,
        Aim,
        Fire,
        Camera,
        Ui,
        Audio,
        TimeScale,
        SelectWeapon,
        CheckDamage
    }

    [Serializable]
    public sealed class PvCue
    {
        public float Time;

        [JsonProperty(Required = Required.Always)]
        public PvAction Action;

        public string Actor;

        public string Target;

        public string[] Targets;

        public string Weapon;

        public float Duration;

        public float Value;

        public bool Enabled;

        public bool IsMain = true;

        public int Slot;

        public PvCell[] Path;

        public PvPosition Position;

        public float Yaw;

        public float Pitch;

        public float FieldOfView = 60;

        public float ImpactShake;
    }

    [Serializable]
    public struct PvCell
    {
        public int X;

        public int Y;
    }

    [Serializable]
    public struct PvPosition
    {
        public float X;

        public float Y;

        public float Z;
    }
}
#endif
