using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using PranaTactics.Networking;

namespace PranaTactics.Match.Results
{
    /// <summary>
    /// 試合シーンを破棄した後も結果とオンラインの累積記録を保持します。
    /// </summary>
    public static class MatchResultSession
    {
        /// <summary>
        /// 戦績同期に使用するメンバープロパティ接頭辞です。
        /// </summary>
        public const string k_RecordPrefix = "Result.Unit.";

        /// <summary>
        /// 前回の試合の累積記録を除外する識別子です。
        /// </summary>
        public const string k_RoundKey = "Result.Round";

        /// <summary>
        /// 結果画面表示中に到着する最終パケットの反映先です。
        /// </summary>
        private static MatchResultTracker s_tracker;

        public static MatchResultSnapshot Current { get; private set; }

        /// <summary>
        /// 新しい試合で前回の結果を破棄します。
        /// </summary>
        public static void Clear()
        {
            Current = null;
            s_tracker = null;
        }

        /// <summary>
        /// UIシーンへ渡す確定順位と戦績を保持します。
        /// </summary>
        public static void Save(int localPlayerId, int localTeamId, IEnumerable<TeamResult> teams, MatchResultTracker tracker)
        {
            s_tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            Current = new MatchResultSnapshot(localPlayerId, localTeamId, teams, tracker.BuildUnits());
        }

        /// <summary>
        /// 権限を持つ被攻撃側のメンバープロパティから現在の試合の記録を読み込みます。
        /// </summary>
        public static void MergeOnline(MatchResultTracker tracker, IOnlineService online)
        {
            if (online?.CurrentRoom == null)
            {
                return;
            }

            if (!online.CurrentRoom.TryGetProperty(k_RoundKey, out string roundId))
            {
                return;
            }

            foreach (int playerId in tracker.PlayerIds.ToArray())
            {
                foreach (OnlinePlayer owner in online.Players)
                {
                    if (!owner.TryGetProperty(k_RecordPrefix + playerId, out string json))
                    {
                        continue;
                    }

                    try
                    {
                        UnitBattleRecord record = JsonUtility.FromJson<UnitBattleRecord>(json);
                        if (record != null && record.RoundId == roundId)
                        {
                            tracker.Merge(record);
                        }
                    }
                    catch (ArgumentException exception)
                    {
                        Debug.LogWarning($"Unit戦績を読み込めませんでした: {exception.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// シーン遷移後に届いた戦績を表示用のスナップショットへ反映します。
        /// </summary>
        public static void RefreshOnline(IOnlineService online)
        {
            if (Current == null || s_tracker == null)
            {
                return;
            }

            MergeOnline(s_tracker, online);
            Current = new MatchResultSnapshot(Current.LocalPlayerId, Current.LocalTeamId, Current.Teams, s_tracker.BuildUnits());
        }
    }
}
