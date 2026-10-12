using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 同じチーム・同じ標的へ割り当てられた複数AIの射撃準備を待ち、
    /// 短い同時発射ウィンドウを開きます。
    /// </summary>
    public sealed class AiCoordinatedStrikeController
    {
        private const float k_MinimumWindowSeconds = 0.06f;

        private const float k_MaximumWindowSeconds = 0.20f;

        private const float k_RearmDelaySeconds = 0.20f;

        private struct StrikeKey : IEquatable<StrikeKey>
        {
            public int TeamId;

            public int TargetPlayerId;

            public bool Equals(StrikeKey other)
            {
                return TeamId == other.TeamId && TargetPlayerId == other.TargetPlayerId;
            }

            public override bool Equals(object obj)
            {
                return obj is StrikeKey && Equals((StrikeKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (TeamId * 397) ^ TargetPlayerId;
                }
            }
        }

        private sealed class StrikeState
        {
            public float FireWindowRemaining;

            public float RearmRemaining;

            public float ReadyWaitSeconds;
        }

        private readonly Dictionary<StrikeKey, StrikeState> _states = new Dictionary<StrikeKey, StrikeState>();

        /// <summary>
        /// 標的ごとの連携射撃を更新し、指定された場合はチーム別の許容時間を使用します。
        /// </summary>
        /// <param name="getTeamTolerance">
        /// チームIDから許容時間を取得します。未指定の場合は共通の値を使用します。
        /// </param>
        public void Update(IEnumerable<SoloAiAgent> agents, float deltaTime, float coordinationTolerance,
            Func<int, float> getTeamTolerance = null)
        {
            if (agents == null)
            {
                return;
            }

            float elapsed = Mathf.Max(0.0f, deltaTime);
            Dictionary<StrikeKey, List<SoloAiAgent>> groups = new Dictionary<StrikeKey, List<SoloAiAgent>>();

            foreach (SoloAiAgent agent in agents)
            {
                if (agent == null)
                {
                    continue;
                }

                agent.SetCoordinatedFireAuthorization(false);

                if (agent.Unit == null || agent.Target == null ||
                    agent.TeamTaskId != AiTeamTaskIds.CoordinatedStrike)
                {
                    continue;
                }

                StrikeKey key = new StrikeKey
                {
                    TeamId = agent.Unit.GetTeamId(),
                    TargetPlayerId = agent.Target.PlayerId
                };
                List<SoloAiAgent> group;

                if (!groups.TryGetValue(key, out group))
                {
                    group = new List<SoloAiAgent>();
                    groups[key] = group;
                }

                group.Add(agent);
            }

            List<StrikeKey> staleKeys = new List<StrikeKey>(_states.Keys);

            foreach (KeyValuePair<StrikeKey, List<SoloAiAgent>> pair in groups)
            {
                staleKeys.Remove(pair.Key);
                StrikeState state;

                if (!_states.TryGetValue(pair.Key, out state))
                {
                    state = new StrikeState();
                    _states[pair.Key] = state;
                }

                state.FireWindowRemaining -= elapsed;
                state.RearmRemaining -= elapsed;
                int required = 2;
                int readyCount = 0;

                foreach (SoloAiAgent agent in pair.Value)
                {
                    required = Mathf.Max(required, agent.MinimumCoordinatedParticipants);

                    if (agent.IsReadyForCoordinatedStrike)
                    {
                        readyCount++;
                    }
                }

                required = Mathf.Min(required, pair.Value.Count);

                state.ReadyWaitSeconds = readyCount > 0 && state.RearmRemaining <= 0.0f
                    ? state.ReadyWaitSeconds + elapsed
                    : 0.0f;

                if (state.FireWindowRemaining <= 0.0f && state.RearmRemaining <= 0.0f &&
                    AiEngagementPolicy.CanOpenFireWindow(pair.Value.Count, readyCount, required, state.ReadyWaitSeconds))
                {
                    state.FireWindowRemaining = Mathf.Clamp(
                        getTeamTolerance != null ? getTeamTolerance(pair.Key.TeamId) : coordinationTolerance,
                        k_MinimumWindowSeconds,
                        k_MaximumWindowSeconds);
                    state.RearmRemaining = state.FireWindowRemaining + k_RearmDelaySeconds;
                    state.ReadyWaitSeconds = 0.0f;
                }

                if (state.FireWindowRemaining > 0.0f)
                {
                    foreach (SoloAiAgent agent in pair.Value)
                    {
                        agent.SetCoordinatedFireAuthorization(true);
                    }
                }
            }

            foreach (StrikeKey staleKey in staleKeys)
            {
                _states.Remove(staleKey);
            }
        }

        public void Reset()
        {
            _states.Clear();
        }
    }
}
