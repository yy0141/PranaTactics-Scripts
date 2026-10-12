using System.Collections.Generic;
using System.Linq;

using PranaTactics.Combat;

namespace PranaTactics.Match.Results
{
    /// <summary>
    /// 被攻撃側の累積記録から、全Unitの戦績を組み立てます。
    /// </summary>
    public sealed class MatchResultTracker
    {
        /// <summary>
        /// 撃破や退出後も保持する参加Unitの記録です。
        /// </summary>
        private readonly Dictionary<int, UnitBattleRecord> _records = new();

        public IEnumerable<int> PlayerIds => _records.Keys;

        public IEnumerable<UnitBattleRecord> Records => _records.Values;

        /// <summary>
        /// 同じUnitの再登録では既存の戦績を保持します。
        /// </summary>
        public UnitBattleRecord Register(int playerId, int teamId, string name, string role)
        {
            if (!_records.TryGetValue(playerId, out UnitBattleRecord record))
            {
                record = new UnitBattleRecord { PlayerId = playerId, TeamId = teamId, Name = name, Role = role };
                _records.Add(playerId, record);
            }

            return record;
        }

        /// <summary>
        /// 有効なHP減少量と、敵への致死攻撃者を記録します。
        /// </summary>
        /// <remarks>
        /// 自傷と味方への攻撃は被ダメージへ含めますが、撃破数へは含めません。
        /// 撃破スコアのチーム帰属は既存ルールに委ねます。
        /// </remarks>
        public UnitBattleRecord RecordDamage(int targetId, int attackerId, float damage, bool isFatal, bool isMain = false, int sourcePranamSlot = -1)
        {
            if (!_records.TryGetValue(targetId, out UnitBattleRecord target) || !target.IsAlive ||
                damage <= 0.0f || float.IsNaN(damage) || float.IsInfinity(damage))
            {
                return null;
            }

            DamageSourceRecord source = target.Sources.Find(item => item.PlayerId == attackerId);
            if (source == null)
            {
                source = new DamageSourceRecord { PlayerId = attackerId };
                target.Sources.Add(source);
            }

            source.Damage += damage;
            target.Revision++;
            if (isFatal)
            {
                target.FatalPranamIsMain = isMain;
                target.FatalPranamSlot = sourcePranamSlot;
                target.IsAlive = false;
                target.DefeatedBy = -1;
                if (_records.TryGetValue(attackerId, out UnitBattleRecord attacker) && attacker.TeamId != target.TeamId)
                {
                    target.DefeatedBy = attackerId;
                }
            }

            return target;
        }

        /// <summary>
        /// 撃破または退出を記録し、重複通知による変更を防ぎます。
        /// </summary>
        public UnitBattleRecord Eliminate(int playerId)
        {
            if (!_records.TryGetValue(playerId, out UnitBattleRecord record))
            {
                return null;
            }

            if (record.IsAlive)
            {
                record.IsAlive = false;
                record.Revision++;
            }

            return record;
        }

        /// <summary>
        /// 全攻撃者から最大貢献した一体を選び、その撃破ポイントと撃破数を確定します。
        /// </summary>
        public UnitBattleRecord RecordDefeatCredit(int targetId, int scoringTeamId, float maximumHealth)
        {
            if (maximumHealth <= 0.0f || float.IsNaN(maximumHealth) || float.IsInfinity(maximumHealth))
            {
                return null;
            }

            if (!_records.TryGetValue(targetId, out UnitBattleRecord target) || target.IsAlive ||
                target.HasDefeatCredit || scoringTeamId < 0 || scoringTeamId == target.TeamId ||
                !target.Sources.Any(source => source.Damage > 0.0f &&
                    _records.TryGetValue(source.PlayerId, out UnitBattleRecord attacker) && attacker.TeamId == scoringTeamId))
            {
                return null;
            }

            var contributions = new Dictionary<int, double>();
            foreach (DamageSourceRecord source in target.Sources)
            {
                if (source.Damage > 0.0f && _records.TryGetValue(source.PlayerId, out UnitBattleRecord attacker) &&
                    attacker.TeamId != target.TeamId)
                {
                    contributions[source.PlayerId] = DamageContributionTracker.CalculateContribution(
                        source.Damage, maximumHealth, source.PlayerId == target.DefeatedBy);
                }
            }

            int creditedPlayerId = DamageContributionTracker.SelectHighestContributor(contributions, target.DefeatedBy);
            if (!_records.TryGetValue(creditedPlayerId, out UnitBattleRecord winner) || winner.TeamId != scoringTeamId)
            {
                return null;
            }

            target.CreditedPlayerId = creditedPlayerId;
            target.HasDefeatCredit = true;
            target.ScoringTeamId = scoringTeamId;
            target.Revision++;
            return target;
        }

        /// <summary>
        /// 古い累積パケットを無視し、別クライアントの確定値を採用します。
        /// </summary>
        public void Merge(UnitBattleRecord record)
        {
            if (record == null || record.Sources == null || !_records.TryGetValue(record.PlayerId, out UnitBattleRecord current) ||
                record.TeamId != current.TeamId || record.Revision < current.Revision)
            {
                return;
            }

            if (record.Sources.Any(source => source == null || source.Damage < 0.0f || float.IsNaN(source.Damage) || float.IsInfinity(source.Damage)))
            {
                return;
            }

            if (record.Revision == current.Revision && record.Sources.Sum(source => source.Damage) <= current.Sources.Sum(source => source.Damage))
            {
                return;
            }

            _records[record.PlayerId] = record;
        }

        /// <summary>
        /// 被攻撃側の記録を集計して、重複加算のない一覧を返します。
        /// </summary>
        public IReadOnlyList<UnitResult> BuildUnits()
        {
            var units = new List<UnitResult>();
            foreach (UnitBattleRecord record in _records.Values)
            {
                float dealt = _records.Values.Sum(target => target.Sources.Where(source => source.PlayerId == record.PlayerId).Sum(source => source.Damage));
                int kills = _records.Values.Count(target =>
                    !target.IsAlive &&
                    target.TeamId != record.TeamId &&
                    target.HasDefeatCredit &&
                    target.ScoringTeamId == record.TeamId &&
                    target.CreditedPlayerId == record.PlayerId &&
                    target.Sources.Any(source => source.PlayerId == record.PlayerId && source.Damage > 0.0f));
                units.Add(new UnitResult(record.PlayerId, record.TeamId, record.Name, record.Role, kills, dealt,
                    record.Sources.Sum(source => source.Damage), record.IsAlive));
            }

            return units.AsReadOnly();
        }
    }
}
