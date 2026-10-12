namespace PranaTactics.Tutorial
{
    /// <summary>
    /// 達成前の押しっぱなし・達成と同フレームの入力で次へ進まないための確認待ち。
    /// </summary>
    public sealed class TutorialConfirmation
    {
        private int _armedFrame;

        private bool _isReleased;

        /// <summary>
        /// 指定フレームから、入力を離した後の再押下を待ち始めます。
        /// </summary>
        /// <param name="frame">
        /// 待機を開始するフレーム番号です。
        /// </param>
        public void Arm(int frame)
        {
            _armedFrame = frame;
            _isReleased = false;
        }

        /// <summary>
        /// 待機開始より後のフレームで、入力を一度離してから押したか判定します。
        /// </summary>
        /// <returns>
        /// 入力が制限されておらず、新しい確認操作として受け付ける場合はtrueです。
        /// </returns>
        public bool Accept(int frame, bool pressed, bool held, bool blocked)
        {
            if (blocked || frame <= _armedFrame)
            {
                return false;
            }

            if (!held)
            {
                _isReleased = true;
            }

            return _isReleased && pressed;
        }
    }
}
