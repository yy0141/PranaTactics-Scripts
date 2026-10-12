namespace PranaTactics.Unit
{
    /// <summary>
    /// 右クリックと右ドラッグを移動量で区別し、遮断された操作を取り消します。
    /// </summary>
    public sealed class MacroCameraDragGesture
    {
        /// <summary>
        /// 小さな手ぶれをクリックとして扱う画面ピクセル距離です。
        /// </summary>
        private const float k_DragThresholdPixels = 6.0f;

        private float _startX;
        private float _startY;

        public bool IsPressed { get; private set; }
        public bool IsDragging { get; private set; }
        public bool WasDragReleased { get; private set; }
        public bool WasClickReleased { get; private set; }

        /// <summary>
        /// 一フレームの入力を受理し、クリックとドラッグの状態を更新します。
        /// </summary>
        public void Update(bool pressed, bool held, bool released, float x, float y, bool canInteract)
        {
            WasClickReleased = false;
            WasDragReleased = false;
            if (!canInteract)
            {
                Cancel();
                return;
            }

            if (pressed)
            {
                IsPressed = true;
                IsDragging = false;
                _startX = x;
                _startY = y;
            }

            if (!IsPressed)
            {
                return;
            }

            float deltaX = x - _startX;
            float deltaY = y - _startY;
            IsDragging |= deltaX * deltaX + deltaY * deltaY >= k_DragThresholdPixels * k_DragThresholdPixels;
            if (released)
            {
                WasClickReleased = !IsDragging;
                WasDragReleased = IsDragging;
                IsPressed = false;
                IsDragging = false;
            }
            else if (!held)
            {
                Cancel();
            }
        }

        /// <summary>
        /// フェーズ切り替え、リセット、UIへの移動時に保留中のクリックを破棄します。
        /// </summary>
        public void Cancel()
        {
            IsPressed = false;
            IsDragging = false;
            WasClickReleased = false;
            WasDragReleased = false;
        }
    }
}
