using System.Collections.Generic;

namespace PranaTactics.Unit
{
    /// <summary>
    /// 発見したチームごとに、隠密ユニットのマップ公開期限を保持します。
    /// </summary>
    internal sealed class StealthDetectionMemory
    {
        /// <summary>
        /// 最後の直接視認からマップ表示を維持する秒数です。
        /// </summary>
        private const float k_RevealSeconds = 3.0f;

        /// <summary>
        /// 観測チームごとの公開期限です。
        /// </summary>
        private readonly Dictionary<int, float> _revealedUntilByTeam = new();

        /// <summary>
        /// 期限切れのチームを列挙する再利用バッファです。
        /// </summary>
        private readonly List<int> _expiredTeams = new();

        /// <summary>
        /// 視認したチームの公開期限を更新します。
        /// </summary>
        public void Record(int observerTeamId, float now)
        {
            if (observerTeamId < 0 || float.IsNaN(now) || float.IsInfinity(now))
            {
                return;
            }

            float deadline = now + k_RevealSeconds;
            if (!_revealedUntilByTeam.TryGetValue(observerTeamId, out float previous) || deadline > previous)
            {
                _revealedUntilByTeam[observerTeamId] = deadline;
            }
        }

        /// <summary>
        /// 指定チームへのマップ公開期間中か確認します。
        /// </summary>
        public bool IsRevealed(int observerTeamId, float now)
        {
            return _revealedUntilByTeam.TryGetValue(observerTeamId, out float deadline) && now < deadline;
        }

        /// <summary>
        /// 期限切れの情報を削除し、表示更新が必要か返します。
        /// </summary>
        public bool Expire(float now)
        {
            _expiredTeams.Clear();
            foreach (KeyValuePair<int, float> pair in _revealedUntilByTeam)
            {
                if (now >= pair.Value)
                {
                    _expiredTeams.Add(pair.Key);
                }
            }

            foreach (int teamId in _expiredTeams)
            {
                _revealedUntilByTeam.Remove(teamId);
            }

            return _expiredTeams.Count > 0;
        }

        /// <summary>
        /// 再生成時に前のユニットの公開状態を破棄します。
        /// </summary>
        public void Clear()
        {
            _revealedUntilByTeam.Clear();
            _expiredTeams.Clear();
        }
    }
}
