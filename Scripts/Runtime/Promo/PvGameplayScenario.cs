#if UNITY_EDITOR || PV_CAPTURE
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PranaTactics.Promo
{
    /// <summary>
    /// 通常操作による個人・複数人撮影の生成設定を読み込みます。先頭の演者を操作します。
    /// </summary>
    public sealed class PvGameplayScenario
    {
        public bool AllowMatchEnd;

        public bool MacroAssistEnabled = true;

        [JsonProperty(Required = Required.Always)]
        public PvGameplayActor[] Actors;

        public static PvGameplayScenario Parse(string json)
        {
            var scenario = JsonConvert.DeserializeObject<PvGameplayScenario>(json,
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });

            if (scenario?.Actors == null || scenario.Actors.Length == 0)
            {
                throw new ArgumentException("Actorsには1人以上を指定してください。");
            }

            var coordinates = new HashSet<(int, int)>();

            foreach (PvGameplayActor actor in scenario.Actors)
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.Character) || actor.Team < 0 ||
                    actor.Team > 1 ||
                    float.IsNaN(actor.Yaw) || float.IsInfinity(actor.Yaw) ||
                    !coordinates.Add((actor.X, actor.Y)))
                {
                    throw new ArgumentException("演者のキャラ・チーム・向きと座標の重複を確認してください。");
                }
            }

            if (scenario.Actors[0].UseAi)
            {
                throw new ArgumentException("先頭の演者は操作対象のためUseAiをfalseにしてください。");
            }

            return scenario;
        }
    }

    /// <summary>
    /// 撮影する演者のキャラクター、ボード座標、向き、AIの有無を指定します。
    /// </summary>
    public sealed class PvGameplayActor
    {
        [JsonProperty(Required = Required.Always)]
        public string Character;

        public int Team;

        [JsonProperty(Required = Required.Always)]
        public int X;

        [JsonProperty(Required = Required.Always)]
        public int Y;

        public float Yaw;

        public bool UseAi;
    }
}
#endif
