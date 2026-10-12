using PranaTactics.Match.Phases;
using PranaTactics.Unit;

namespace PranaTactics.Combat
{
    /// <summary>
    /// ローカル操作対象に公開できる攻撃結果を選別します。
    /// </summary>
    public static class CombatFeedbackVisibility
    {
        /// <summary>
        /// 自分が確定させた敵Unitへの命中・撃破音は視認範囲に関係なく許可します。
        /// </summary>
        public static bool CanHear(CombatFeedbackResult result)
        {
            if (result.Kind != CombatFeedbackKind.UnitHit && result.Kind != CombatFeedbackKind.Elimination)
            {
                return CanPresent(result);
            }

            UnitManager manager = UnitManager.Instance;
            MatchPhaseManager phases = MatchPhaseManager.Instance;
            if (manager == null || phases == null || phases.CurrentPhase != MatchPhase.Micro ||
                result.AttackerId != manager.ControlledUnitId)
            {
                return false;
            }

            int attackerTeamId = ResolveTeamId(manager, result.AttackerId);
            int targetTeamId = ResolveTeamId(manager, result.TargetId);
            return attackerTeamId >= 0 && targetTeamId >= 0 && attackerTeamId != targetTeamId;
        }

        /// <summary>
        /// 撃破済みUnitが削除された後もプレイヤー情報から所属チームを取得します。
        /// </summary>
        private static int ResolveTeamId(UnitManager manager, int playerId)
        {
            if (playerId < 0)
            {
                return -1;
            }

            int teamId = manager.GetTeamId(playerId);
            if (teamId >= 0)
            {
                return teamId;
            }

            return manager.TryGetUnit(playerId, out UnitBehavior unit) ? unit.GetTeamId() : -1;
        }

        /// <summary>
        /// Micro視点で視認情報が許可する敵への攻撃結果か確認します。
        /// </summary>
        public static bool CanPresent(CombatFeedbackResult result)
        {
            if (!TryGetEnemyResult(result, out int teamId, out UnitBehavior target))
            {
                return false;
            }

            return !target.IsStealthHiddenFromTeam(teamId) &&
                UnitManager.Instance.IsUnitVisibleToTeam(teamId, result.TargetId);
        }

        /// <summary>
        /// 現在の操作Unitから敵への確定結果か確認します。
        /// </summary>
        private static bool TryGetEnemyResult(
            CombatFeedbackResult result,
            out int teamId,
            out UnitBehavior target)
        {
            teamId = -1;
            target = null;
            UnitManager manager = UnitManager.Instance;
            MatchPhaseManager phases = MatchPhaseManager.Instance;
            if (manager == null || phases == null || phases.CurrentPhase != MatchPhase.Micro ||
                result.AttackerId != manager.ControlledUnitId ||
                !manager.TryGetUnit(result.AttackerId, out UnitBehavior attacker) ||
                !manager.TryGetUnit(result.TargetId, out target))
            {
                return false;
            }

            teamId = attacker.GetTeamId();
            return teamId >= 0 && target.GetTeamId() >= 0 && teamId != target.GetTeamId();
        }
    }
}
