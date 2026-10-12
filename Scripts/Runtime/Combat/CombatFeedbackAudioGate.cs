using System;

namespace PranaTactics.Combat
{
    /// <summary>
    /// 同時命中の優先順位と、確認音の最小再生間隔を管理します。
    /// </summary>
    public sealed class CombatFeedbackAudioGate
    {
        /// <summary>
        /// 次の演出更新で評価する結果です。
        /// </summary>
        private CombatFeedbackKind? _pendingKind;

        /// <summary>
        /// 通常音を次に開始できる時刻です。
        /// </summary>
        private double _nextHitTime = double.NegativeInfinity;

        /// <summary>
        /// 撃破音を次に開始できる時刻です。
        /// </summary>
        private double _nextEliminationTime = double.NegativeInfinity;

        /// <summary>
        /// 同一更新内の候補を、重要度の高い結果へ集約します。
        /// </summary>
        public void Submit(CombatFeedbackKind kind)
        {
            if (!Enum.IsDefined(typeof(CombatFeedbackKind), kind))
            {
                return;
            }

            if (!_pendingKind.HasValue || kind > _pendingKind.Value)
            {
                _pendingKind = kind;
            }
        }

        /// <summary>
        /// 今回の候補を消費し、現在再生できる結果を返します。
        /// </summary>
        /// <remarks>
        /// 省略した結果は後から再生せず、再生期限も延長しません。
        /// 再生に成功した場合だけ<see cref="Commit"/>を呼び出します。
        /// </remarks>
        public bool TryTake(double now, out CombatFeedbackKind kind)
        {
            kind = _pendingKind ?? CombatFeedbackKind.PranaHit;
            bool hasCandidate = _pendingKind.HasValue;
            _pendingKind = null;

            return hasCandidate && !double.IsNaN(now) && !double.IsInfinity(now) &&
                now >= (kind == CombatFeedbackKind.Elimination
                    ? _nextEliminationTime
                    : _nextHitTime);
        }

        /// <summary>
        /// 実際に開始した音から次の再生可能時刻を設定します。
        /// </summary>
        public void Commit(
            double now,
            CombatFeedbackKind kind,
            double hitInterval,
            double eliminationInterval)
        {
            if (kind == CombatFeedbackKind.Elimination)
            {
                _nextEliminationTime = now + Math.Max(0.0, eliminationInterval);
                _nextHitTime = _nextEliminationTime;
            }
            else
            {
                _nextHitTime = now + Math.Max(0.0, hitInterval);
            }
        }

        /// <summary>
        /// 操作対象や試合状態の変更時に一時状態を破棄します。
        /// </summary>
        public void Reset()
        {
            _pendingKind = null;
            _nextHitTime = double.NegativeInfinity;
            _nextEliminationTime = double.NegativeInfinity;
        }
    }
}
