using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace PranaTactics.Settings
{
    /// <summary>
    /// チームと味方経路の配色を、登録順を維持して保存します。
    /// </summary>
    public sealed class AppearanceSettings
    {
        public string ConfirmedTargetColor { get; set; } = "#FFB814";
        public string AllyTeamColor { get; set; } = "#1980FF";
        public int EnemyTeamColorCount { get; set; } = 6;
        public int AdditionalAllyRouteColorCount { get; set; } = 4;
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> EnemyTeamColors { get; set; } = new List<string>
        {
            "#FF334D", "#FF9933", "#CC66FF", "#FF66AA", "#FFD633", "#AA6633"
        };
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<RouteColorSettings> AllyRouteColors { get; set; } = new List<RouteColorSettings>
        {
            new RouteColorSettings("#33CCFF", "#FFFFFF"),
            new RouteColorSettings("#66EE66", "#FFFFFF"),
            new RouteColorSettings("#FFD633", "#FFFFFF"),
            new RouteColorSettings("#CC66FF", "#FFFFFF"),
            new RouteColorSettings("#FF9933", "#FFFFFF")
        };

        /// <summary>
        /// ローカルチーム以外をID順に割り当て、登録色を左から使用します。
        /// </summary>
        /// <remarks>
        /// 登録色を超えるチームでは先頭へ戻ります。チームIDの欠番と味方IDを除外します。
        /// </remarks>
        public string GetTeamColor(int teamId, int localTeamId, IReadOnlyCollection<int> teamIds)
        {
            if (teamId == localTeamId)
            {
                return AllyTeamColor;
            }

            var previous = new HashSet<int>();
            foreach (int id in teamIds)
            {
                if (id >= 0 && id != localTeamId && id < teamId)
                {
                    previous.Add(id);
                }
            }

            return EnemyTeamColors[previous.Count % EnemyTeamColorCount];
        }

        /// <summary>
        /// 死亡中の味方も含めた登録IDから、変化しない配色順を計算します。
        /// </summary>
        public static int GetAllyIndex(int playerId, int localPlayerId, IReadOnlyCollection<int> allyPlayerIds)
        {
            if (playerId == localPlayerId)
            {
                return 0;
            }

            var previous = new HashSet<int>();
            foreach (int id in allyPlayerIds)
            {
                if (id >= 0 && id != localPlayerId && id < playerId)
                {
                    previous.Add(id);
                }
            }

            return previous.Count + 1;
        }

        /// <summary>
        /// プレイヤーを先頭とした味方の順番から、線と矢じりの配色を取得します。
        /// </summary>
        public RouteColorSettings GetRouteColors(int allyIndex)
        {
            int index = allyIndex <= 0 || AdditionalAllyRouteColorCount == 0
                ? 0 : 1 + (allyIndex - 1) % AdditionalAllyRouteColorCount;
            return AllyRouteColors[index];
        }

        /// <summary>
        /// 欠落した色と不正値を補正し、非表示の登録枠も保持します。
        /// </summary>
        public void Validate()
        {
            EnemyTeamColorCount = Math.Max(1, Math.Min(6, EnemyTeamColorCount));
            AdditionalAllyRouteColorCount = Math.Max(0, Math.Min(4, AdditionalAllyRouteColorCount));
            AllyTeamColor = Normalize(AllyTeamColor, "#1980FF");
            ConfirmedTargetColor = Normalize(ConfirmedTargetColor, "#FFB814");
            EnemyTeamColors ??= new List<string>();
            AllyRouteColors ??= new List<RouteColorSettings>();
            string[] defaults = { "#FF334D", "#FF9933", "#CC66FF", "#FF66AA", "#FFD633", "#AA6633" };
            for (int index = 0; index < 6; index++)
            {
                if (EnemyTeamColors.Count <= index)
                {
                    EnemyTeamColors.Add(defaults[index]);
                }

                EnemyTeamColors[index] = Normalize(EnemyTeamColors[index], defaults[index]);
            }

            string[] routes = { "#33CCFF", "#66EE66", "#FFD633", "#CC66FF", "#FF9933" };
            for (int index = 0; index < 5; index++)
            {
                if (AllyRouteColors.Count <= index)
                {
                    AllyRouteColors.Add(new RouteColorSettings(routes[index], "#FFFFFF"));
                }

                AllyRouteColors[index] ??= new RouteColorSettings(routes[index], "#FFFFFF");
                AllyRouteColors[index].Line = Normalize(AllyRouteColors[index].Line, routes[index]);
                AllyRouteColors[index].Arrow = Normalize(AllyRouteColors[index].Arrow, "#FFFFFF");
            }
        }

        /// <summary>
        /// 不透明なRGB色を検証し、大文字の保存形式へ統一します。
        /// </summary>
        public static bool TryNormalizeColor(string value, out string normalized)
        {
            normalized = null;
            if (value == null || value.Length != 7 || value[0] != '#' ||
                !uint.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }

            normalized = value.ToUpperInvariant();
            return true;
        }

        /// <summary>
        /// 不正なRGB色を既定値で置き換えます。
        /// </summary>
        private static string Normalize(string value, string fallback)
        {
            return TryNormalizeColor(value, out string normalized) ? normalized : fallback;
        }
    }

    /// <summary>
    /// 一体分の線と矢じりの保存色です。
    /// </summary>
    public sealed class RouteColorSettings
    {
        public string Line { get; set; } = "#33CCFF";
        public string Arrow { get; set; } = "#FFFFFF";

        /// <summary>
        /// JSONから復元する配色を生成します。
        /// </summary>
        public RouteColorSettings()
        {
        }

        /// <summary>
        /// 指定された線と矢じりの配色を生成します。
        /// </summary>
        public RouteColorSettings(string line, string arrow)
        {
            Line = line;
            Arrow = arrow;
        }
    }
}
