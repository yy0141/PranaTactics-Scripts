using System;

using PranaTactics.Networking;

namespace PranaTactics.Match.Phases
{
    /// <summary>
    /// 1回のMacro Phaseの決定状態を管理し、オンラインでは今回の識別子に対する全員の応答を待ちます。
    /// </summary>
    public sealed class MacroPlanningSession
    {
        private readonly IOnlineService _online;

        private readonly int _turn;

        private readonly string _hostRound;

        private string _publishedReady;

        public int Turn => _turn;

        public bool IsLocalConfirmed
        {
            get; private set;
        }

        public bool IsPlanningConfirmationOpen
        {
            get; set;
        }

        public bool CanAdvance => _online == null || _online.IsHost;

        /// <summary>
        /// 今回の計画を開始し、ホストから識別子を公開します。
        /// </summary>
        public MacroPlanningSession(IOnlineService online, int turn)
        {
            _online = online;
            _turn = turn;

            if (_online != null && _online.IsHost)
            {
                _hostRound = turn + ":" + Guid.NewGuid().ToString("N");
                _online.SetRoomProperty(NetworkPropertyKeys.MacroPlanningRound, _hostRound);
            }
        }

        /// <summary>
        /// ローカルプレイヤーの計画を決定します。
        /// </summary>
        public void Confirm()
        {
            IsLocalConfirmed = true;
            RefreshConfirmation();
        }

        /// <summary>
        /// 今回の識別子を受信できた場合に決定状態を送信します。
        /// </summary>
        public void RefreshConfirmation()
        {
            if (!IsLocalConfirmed || _online == null || !TryGetRound(out string round) ||
                round == _publishedReady)
            {
                return;
            }

            _online.SetLocalPlayerProperty(NetworkPropertyKeys.MacroPlanningReady, round);
            _publishedReady = round;
        }

        /// <summary>
        /// AIの計画と操作可能な全プレイヤーの今回の決定が揃ったか確認します。
        /// </summary>
        public bool AreAllConfirmed(bool isAiPlanningComplete, Func<int, bool> isPlayerInactive)
        {
            if (!isAiPlanningComplete || isPlayerInactive == null)
            {
                return false;
            }

            if (_online == null)
            {
                return IsLocalConfirmed;
            }

            if (!TryGetRound(out string round) || _online.Players.Count == 0)
            {
                return false;
            }

            foreach (OnlinePlayer player in _online.Players)
            {
                if (player == null)
                {
                    return false;
                }

                if (isPlayerInactive(player.PlayerId))
                {
                    continue;
                }

                if (!player.TryGetProperty(NetworkPropertyKeys.MacroPlanningReady, out string ready) || ready != round)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 現在のターンに対応する計画識別子を取得します。
        /// </summary>
        private bool TryGetRound(out string round)
        {
            round = null;

            return _online.CurrentRoom != null &&
                _online.CurrentRoom.TryGetProperty(NetworkPropertyKeys.MacroPlanningRound, out round) &&
                round != null && round.StartsWith(_turn + ":", StringComparison.Ordinal) &&
                (_hostRound == null || round == _hostRound);
        }
    }
}
