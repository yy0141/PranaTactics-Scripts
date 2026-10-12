using System.Collections.Generic;
using UnityEngine;

using PranaTactics.GameFlow;
using PranaTactics.Settings;
using PranaTactics.Unit;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 保存済みの配色をローカルプレイヤーから見たチームと味方経路へ対応付けます。
    /// </summary>
    public static class GameplayColorSettings
    {
        /// <summary>
        /// 味方色、またはチームID順に割り当てた敵色を取得します。
        /// </summary>
        public static Color GetTeamColor(int teamId)
        {
            UnitManager units = UnitManager.Instance;
            int localTeamId = units != null ? units.GetTeamId(units.LocalPlayerId) : -1;
            var teamIds = new HashSet<int>();
            GameFlowManager flow = GameFlowManager.Instance;
            if (flow != null && flow.IsSoloPlay && flow.SoloBattleConfiguration != null)
            {
                foreach (var team in flow.SoloBattleConfiguration.Teams)
                {
                    teamIds.Add(team.TeamId);
                }
            }

            if (units != null)
            {
                foreach (UnitBehavior unit in units.GetAllUnitBehaviors())
                {
                    if (unit != null)
                    {
                        teamIds.Add(unit.GetTeamId());
                    }
                }
            }

            string hex = SettingsManager.Current.Appearance.GetTeamColor(teamId, localTeamId, teamIds);
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        /// <summary>
        /// プレイヤーを先頭にした味方順序で線と矢じりの登録色を取得します。
        /// </summary>
        public static void GetRouteColors(int playerId, out Color line, out Color arrow)
        {
            UnitManager units = UnitManager.Instance;
            int localPlayerId = units != null ? units.LocalPlayerId : -1;
            int localTeamId = units != null ? units.GetTeamId(localPlayerId) : -1;
            var allyIds = new HashSet<int>();
            if (units != null)
            {
                foreach (UnitBehavior unit in units.GetAllUnitBehaviors())
                {
                    if (unit != null && unit.GetTeamId() == localTeamId)
                    {
                        allyIds.Add(unit.PlayerId);
                    }
                }
            }

            int allyIndex = AppearanceSettings.GetAllyIndex(playerId, localPlayerId, allyIds);
            RouteColorSettings colors = SettingsManager.Current.Appearance.GetRouteColors(allyIndex);
            ColorUtility.TryParseHtmlString(colors.Line, out line);
            ColorUtility.TryParseHtmlString(colors.Arrow, out arrow);
        }
    }
}
