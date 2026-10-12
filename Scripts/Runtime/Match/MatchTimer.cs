using System;

namespace PranaTactics.Match
{
    /// <summary>
    /// 指定された秒数から0までカウントダウンするタイマーです。
    /// </summary>
    /// <remarks>
    /// <see cref="Tick"/>を定期的に呼び出して経過時間を渡します。
    /// 残り時間が0になったとき、<see cref="TimeEnded"/>が一度だけ発生します。
    /// </remarks>
    public sealed class MatchTimer
    {
        /// <summary>
        /// 残り時間を減少させる時間単位です。
        /// </summary>
        private const float k_SecondsPerCount = 1.0f;

        /// <summary>
        /// 1秒未満の経過時間を保持します。
        /// </summary>
        private float _elapsedTime;

        public int RemainingTime
        {
            get; private set;
        }

        public float RemainingSeconds =>
    Math.Max(0.0f, RemainingTime - _elapsedTime);

        public bool IsCompleted
        {
            get
            {
                return RemainingTime <= 0;
            }
        }

        public event Action TimeEnded;

        public MatchTimer(int durationSeconds)
        {
            if (durationSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds),
                    durationSeconds,
                    "タイマーの時間には0以上の値を指定してください。");
            }

            RemainingTime = durationSeconds;
            _elapsedTime = 0.0f;
        }

        /// <summary>
        /// 指定された経過時間に基づいて、
        /// タイマーの残り時間を更新します。
        /// </summary>
        /// <param name="deltaTime">
        /// 前回の更新から経過した時間です。
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="deltaTime"/>が0未満の場合に発生します。
        /// </exception>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaTime),
                    deltaTime,
                    "経過時間には0以上の値を指定してください。");
            }

            if (IsCompleted ||
                deltaTime == 0.0f)
            {
                return;
            }

            _elapsedTime += deltaTime;

            while (_elapsedTime >= k_SecondsPerCount &&
                RemainingTime > 0)
            {
                _elapsedTime -= k_SecondsPerCount;
                RemainingTime--;
            }

            if (!IsCompleted)
            {
                return;
            }

            _elapsedTime = 0.0f;

            TimeEnded?.Invoke();
        }
    }
}
