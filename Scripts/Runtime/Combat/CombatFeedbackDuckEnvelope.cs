using System;

namespace PranaTactics.Combat
{
    /// <summary>
    /// 減衰要求を加算せず、音量を滑らかに復元します。
    /// </summary>
    public sealed class CombatFeedbackDuckEnvelope
    {
        /// <summary>
        /// 現在の減衰開始時刻です。
        /// </summary>
        private double _started;

        /// <summary>
        /// 減衰音量を保持する期限です。
        /// </summary>
        private double _holdUntil = double.NegativeInfinity;

        /// <summary>
        /// 元の音量へ復帰する期限です。
        /// </summary>
        private double _releaseUntil = double.NegativeInfinity;

        /// <summary>
        /// 開始時点の音量比率です。
        /// </summary>
        private double _fromGain = 1.0;

        /// <summary>
        /// 保持する音量比率です。
        /// </summary>
        private double _targetGain = 1.0;

        /// <summary>
        /// 現在時刻に必要な音量比率を取得します。
        /// </summary>
        public double GetGain(double now)
        {
            if (now >= _releaseUntil)
            {
                return 1.0;
            }

            if (now < _started + 0.005)
            {
                double progress = Math.Max(0.0, Math.Min(1.0, (now - _started) / 0.005));
                return _fromGain + (_targetGain - _fromGain) * progress;
            }

            if (now <= _holdUntil)
            {
                return _targetGain;
            }

            double recovery = (now - _holdUntil) / (_releaseUntil - _holdUntil);
            return _targetGain + (1.0 - _targetGain) * recovery;
        }

        /// <summary>
        /// 現在の音量から新しい減衰要求へ移行します。
        /// </summary>
        public void Request(double now, double gain, double hold, double release)
        {
            _fromGain = GetGain(now);
            bool isHolding = now < _holdUntil;
            _targetGain = Math.Max(0.0, Math.Min(isHolding ? _targetGain : _fromGain, gain));
            _started = now;
            double requestedHold = now + Math.Max(0.005, hold);
            _holdUntil = isHolding ? Math.Max(_holdUntil, requestedHold) : requestedHold;
            double requestedRelease = _holdUntil + Math.Max(0.001, release);
            _releaseUntil = isHolding ? Math.Max(_releaseUntil, requestedRelease) : requestedRelease;
        }

        /// <summary>
        /// 減衰要求を解除します。
        /// </summary>
        public void Reset()
        {
            _holdUntil = double.NegativeInfinity;
            _releaseUntil = double.NegativeInfinity;
            _fromGain = 1.0;
            _targetGain = 1.0;
        }
    }
}
