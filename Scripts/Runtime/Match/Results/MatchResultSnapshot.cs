using System;
using System.Collections.Generic;
using System.Linq;

namespace PranaTactics.Match.Results
{
    public enum MatchOutcome
    {
        Victory,
        Defeat,
        Draw
    }

    /// <summary>
    /// シーンの寿命に依存しない、確定した試合結果です。
    /// </summary>
    public sealed class MatchResultSnapshot
    {
        public int LocalPlayerId { get; }
        public int LocalTeamId { get; }
        public MatchOutcome Outcome { get; }
        public IReadOnlyList<TeamResult> Teams { get; }
        public IReadOnlyList<UnitResult> Units { get; }

        /// <summary>
        /// チーム順位と撃破数順位を確定し、同率首位を引き分けとして扱います。
        /// </summary>
        public MatchResultSnapshot(int localPlayerId, int localTeamId, IEnumerable<TeamResult> teams, IEnumerable<UnitResult> units)
        {
            LocalPlayerId = localPlayerId;
            LocalTeamId = localTeamId;
            List<TeamResult> orderedTeams = teams.OrderBy(team => team.Rank).ThenBy(team => team.TeamId).ToList();
            Teams = orderedTeams.AsReadOnly();
            Units = units.OrderByDescending(unit => unit.Kills).ThenBy(unit => unit.TeamId).ThenBy(unit => unit.PlayerId).ToList().AsReadOnly();
            TeamResult localTeam = orderedTeams.Find(team => team.TeamId == localTeamId);
            Outcome = localTeam == null || localTeam.Rank > 1
                ? MatchOutcome.Defeat
                : orderedTeams.Count(team => team.Rank == 1) > 1 ? MatchOutcome.Draw : MatchOutcome.Victory;
        }
    }

    /// <summary>
    /// 得点の内訳と生存人数を保持します。
    /// </summary>
    public sealed class TeamResult
    {
        public int TeamId { get; }
        public int Rank { get; }
        public int DefeatScore { get; }
        public int SurvivalBonus { get; }
        public int Total => DefeatScore + SurvivalBonus;
        public int Survivors { get; }

        /// <summary>
        /// 試合ルールが確定した順位と得点内訳を保存します。
        /// </summary>
        public TeamResult(int teamId, int rank, int defeatScore, int survivalBonus, int survivors)
        {
            TeamId = teamId;
            Rank = rank;
            DefeatScore = defeatScore;
            SurvivalBonus = survivalBonus;
            Survivors = survivors;
        }
    }

    /// <summary>
    /// 一体のUnitの最終戦績を保持します。
    /// </summary>
    public sealed class UnitResult
    {
        public int PlayerId { get; }
        public int TeamId { get; }
        public string Name { get; }
        public string Role { get; }
        public int Kills { get; }
        public float DamageDealt { get; }
        public float DamageReceived { get; }
        public bool IsAlive { get; }

        /// <summary>
        /// シーン内オブジェクトを参照せずに表示値を保存します。
        /// </summary>
        /// <param name="kills">
        /// 全攻撃者の中で最大貢献者になった敵の数です。
        /// </param>
        public UnitResult(int playerId, int teamId, string name, string role, int kills, float damageDealt, float damageReceived, bool isAlive)
        {
            PlayerId = playerId;
            TeamId = teamId;
            Name = name ?? string.Empty;
            Role = role ?? string.Empty;
            Kills = kills;
            DamageDealt = damageDealt;
            DamageReceived = damageReceived;
            IsAlive = isAlive;
        }
    }

    /// <summary>
    /// 被攻撃側の所有者が同期する累積戦績DTOです。
    /// </summary>
    [Serializable]
    public sealed class UnitBattleRecord
    {
        public int PlayerId;
        public int TeamId;
        public string Name;
        public string Role;
        public bool IsAlive = true;
        public int Revision;
        public string RoundId;
        public int DefeatedBy = -1;
        /// <summary>
        /// 致死攻撃の兵装がメインかどうかです。
        /// </summary>
        public bool FatalPranamIsMain;
        /// <summary>
        /// 致死攻撃を生成した兵装スロットです。
        /// </summary>
        public int FatalPranamSlot = -1;
        /// <summary>
        /// 撃破ポイントの獲得チームが確定しているかどうかです。
        /// </summary>
        public bool HasDefeatCredit;
        /// <summary>
        /// 最大貢献ユニットの所属チームです。
        /// </summary>
        public int ScoringTeamId = -1;
        /// <summary>
        /// 全攻撃者の中で最大の撃破貢献を行ったユニットです。
        /// </summary>
        public int CreditedPlayerId = -1;
        public List<DamageSourceRecord> Sources = new();
    }

    /// <summary>
    /// 攻撃者ごとの実際のHP減少量を同期します。
    /// </summary>
    [Serializable]
    public sealed class DamageSourceRecord
    {
        public int PlayerId;
        public float Damage;
    }
}
