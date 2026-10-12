namespace PranaTactics.Controls
{
    /// <summary>
    /// 歩行と停止の独立した入力方式を処理し、同時入力では停止を優先します。
    /// 別の速度を選ぶと切替状態を解除し、長押し状態は現在の押下状態に従います。
    /// </summary>
    public sealed class MovementSpeedInputState
    {
        private bool _isWalking;

        private bool _isStopping;

        public bool IsWalking => _isWalking && !_isStopping;

        public bool IsStopping => _isStopping;

        public void Reset()
        {
            _isWalking = false;
            _isStopping = false;
        }

        public void Suspend(bool holdWalk, bool holdStop)
        {
            if (holdWalk)
            {
                _isWalking = false;
            }

            if (holdStop)
            {
                _isStopping = false;
            }
        }

        public void Update(
            bool holdWalk,
            bool holdStop,
            bool walkHeld,
            bool walkPressed,
            bool stopHeld,
            bool stopPressed)
        {
            if (holdWalk)
            {
                _isWalking = walkHeld;
            }

            if (holdStop)
            {
                _isStopping = stopHeld;
            }

            if (stopPressed)
            {
                if (!holdStop)
                {
                    _isStopping = !_isStopping;
                }

                _isWalking = false;
            }
            else if (walkPressed && !(holdStop && stopHeld))
            {
                if (!holdWalk)
                {
                    _isWalking = !_isWalking;
                }

                _isStopping = false;
            }
        }
    }
}
