using System;
using System.Collections.Generic;

namespace PranaTactics.Board
{
    /// <summary>
    /// 検知済みの罠について、対象を固定した発動待機時間を管理します。
    /// </summary>
    public sealed class BoardTrapActivationQueue
    {
        /// <summary>
        /// 罠IDに対応する最初の検知と残り時間です。
        /// </summary>
        private readonly Dictionary<string, (BoardTrapMessage Message, float Remaining)> _pending = new();

        /// <summary>
        /// 待機中のIDを変更せず走査するバッファです。
        /// </summary>
        private readonly List<string> _ids = new();

        /// <summary>
        /// 最初の検知だけを受理し、遅延と対象を固定します。
        /// </summary>
        public bool TrySchedule(BoardTrapMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.TrapId) || message.TargetId < 0 ||
                message.ActivationDelay < 0.0f || float.IsNaN(message.ActivationDelay) ||
                float.IsInfinity(message.ActivationDelay))
            {
                return false;
            }
            return _pending.TryAdd(message.TrapId, (message, message.ActivationDelay));
        }

        /// <summary>
        /// 有効なゲーム時間だけ待機を進め、裁定側には発動時刻を迎えた罠を一度返します。
        /// </summary>
        /// <param name="seconds">
        /// ミクロ中に経過した秒数です。他フェイズでは0を指定します。
        /// </param>
        /// <param name="canActivate">
        /// 裁定側の場合はtrueです。それ以外では残り時間だけを追従します。
        /// </param>
        public List<BoardTrapMessage> Advance(float seconds, bool canActivate)
        {
            if (seconds < 0.0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }
            var ready = new List<BoardTrapMessage>();
            _ids.Clear();
            _ids.AddRange(_pending.Keys);
            foreach (string id in _ids)
            {
                (BoardTrapMessage message, float remaining) = _pending[id];
                remaining = Math.Max(0.0f, remaining - seconds);
                if (canActivate && remaining <= 0.0f)
                {
                    _pending.Remove(id);
                    ready.Add(message);
                }
                else
                {
                    _pending[id] = (message, remaining);
                }
            }
            return ready;
        }

        /// <summary>
        /// 発動確定済みの罠を待機から除外します。
        /// </summary>
        public void Remove(string trapId)
        {
            _pending.Remove(trapId);
        }

        /// <summary>
        /// 盤面破棄時に待機を全て解除します。
        /// </summary>
        public void Clear()
        {
            _pending.Clear();
        }
    }
}
