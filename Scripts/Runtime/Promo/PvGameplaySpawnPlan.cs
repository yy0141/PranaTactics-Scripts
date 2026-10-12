#if UNITY_EDITOR || PV_CAPTURE
using System;
using System.Collections.Generic;

using PranaTactics.Board;
using PranaTactics.Core;
using PranaTactics.Unit;
using UnityEngine;

namespace PranaTactics.Promo
{
    /// <summary>
    /// 撮影設定を通常のプレイヤー情報とユニット生成へ変換します。
    /// </summary>
    public static class PvGameplaySpawnPlan
    {
        public static List<PlayerInfo> CreatePlayers(PvGameplayScenario scenario, BoardManager board)
        {
            var players = new List<PlayerInfo>();

            for (int index = 0; index < scenario.Actors.Length; index++)
            {
                PvGameplayActor actor = scenario.Actors[index];

                if (!board.ContainsHexCell(actor.X, actor.Y) ||
                    GameDataRepository.Instance.GetCharacterData(actor.Character) == null)
                {
                    throw new ArgumentException($"Actors[{index}]: キャラまたはセル({actor.X}, {actor.Y})が存在しません。");
                }

                players.Add(new PlayerInfo
                {
                    PlayerId = index,
                    TeamId = actor.Team,
                    CharacterName = actor.Character,
                    Coordinate = new Vector2Int(actor.X, actor.Y),
                    Rotation = new Vector3(0, actor.Yaw, 0)
                });
            }

            return players;
        }

        public static void CreateUnits(PvGameplayScenario scenario, UnitManager manager)
        {
            for (int index = 0; index < scenario.Actors.Length; index++)
            {
                PvGameplayActor actor = scenario.Actors[index];

                manager.CreateUnit(new UnitInitializeData
                {
                    OwnerId = index,
                    TeamId = actor.Team,
                    LocalTeamId = scenario.Actors[0].Team,
                    CharacterName = actor.Character,
                    StartX = actor.X,
                    StartY = actor.Y,
                    Rotation = new SerializableVector3(0, actor.Yaw, 0)
                });
            }
        }
    }
}
#endif
