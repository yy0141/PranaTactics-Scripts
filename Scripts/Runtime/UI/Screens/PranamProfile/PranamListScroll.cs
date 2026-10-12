using UnityEngine;

using PranaTactics.Settings;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens.PranamProfile
{
    /// <summary>
    /// 既存クリック状態機械を使い、兵装一覧の縦スライダーとホイールを処理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PranamListScroll : RepeatClickBehaviorBase, IClickableCondition
    {
        [SerializeField]
        private PranamProfileView _view;

        [SerializeField]
        private RectTransform _viewport;

        [SerializeField]
        private RectTransform _content;

        [SerializeField]
        private RectTransform _handle;

        [Min(1.0f)]
        [SerializeField]
        private float _wheelSpeed = 96.0f;

        private float _offset;

        private float _grabOffset;

        private bool _isDragging;

        private RectTransform Track => (RectTransform)transform;

        private float MaximumOffset => Mathf.Max(0.0f, _content.rect.height - _viewport.rect.height);

        private Camera EventCamera
        {
            get
            {
                Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
                return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }
        }

        /// <summary>
        /// 超過分があり、画面が操作可能な場合だけドラッグを許可します。
        /// </summary>
        public bool CanClick()
        {
            return _view != null && _view.CanInteract && MaximumOffset > 0.01f;
        }

        /// <summary>
        /// 一覧内にポインターがある場合だけホイール移動を反映します。
        /// </summary>
        private void Update()
        {
            if (!CanClick())
            {
                _isDragging = false;
                return;
            }

            float wheel = SettingsManager.Current.Controls.ApplyScrollDirection(Input.mouseScrollDelta.y);

            if (!_isDragging && Mathf.Abs(wheel) > 0.001f &&
                (RectTransformUtility.RectangleContainsScreenPoint(_viewport, Input.mousePosition, EventCamera) ||
                 RectTransformUtility.RectangleContainsScreenPoint(Track, Input.mousePosition, EventCamera)))
            {
                SetOffset(_offset - wheel * _wheelSpeed);
            }
        }

        /// <summary>
        /// つまみ内の掴んだ位置を保持し、トラックのクリックではその場所へ移動します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (!CanClick() || !TryGetPointerPosition(out float position))
            {
                return;
            }

            float top = -_handle.anchoredPosition.y;
            float height = _handle.rect.height;
            _grabOffset = position >= top && position <= top + height ? position - top : height * 0.5f;
            _isDragging = true;
            MoveHandle(position);
        }

        /// <summary>
        /// クリック保持中は、トラック外へ移動してもドラッグを継続します。
        /// </summary>
        protected override void OnClickUpdate()
        {
            if (!Input.GetMouseButton(0))
            {
                _isDragging = false;
            }

            if (_isDragging && TryGetPointerPosition(out float position))
            {
                MoveHandle(position);
            }
        }

        /// <summary>
        /// 別のUI操作や無効化でクリック状態が終了した場合、ドラッグを停止します。
        /// </summary>
        protected override void OnClickExit()
        {
            _isDragging = false;
        }

        /// <summary>
        /// 兵装数に応じてスクロール領域を設定し、位置を先頭へ戻します。
        /// </summary>
        public void SetContentHeight(float height)
        {
            _isDragging = false;
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, _viewport.rect.height));
            SetOffset(0.0f);
        }

        /// <summary>
        /// 装備アイコンから開いた場合など、選択された行が表示枠へ収まるよう移動します。
        /// </summary>
        public void EnsureVisible(RectTransform row)
        {
            float top = -row.anchoredPosition.y;
            float bottom = top + row.rect.height;

            if (top < _offset)
            {
                SetOffset(top);
            }
            else if (bottom > _offset + _viewport.rect.height)
            {
                SetOffset(bottom - _viewport.rect.height);
            }
        }

        /// <summary>
        /// マウス座標をトラック上端からの距離へ変換します。
        /// </summary>
        private bool TryGetPointerPosition(out float position)
        {
            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Track, Input.mousePosition, EventCamera, out Vector2 local);
            position = Track.rect.yMax - local.y;
            return success;
        }

        /// <summary>
        /// つまみ位置をコンテンツの移動量へ変換します。
        /// </summary>
        private void MoveHandle(float position)
        {
            float travel = Mathf.Max(0.01f, Track.rect.height - _handle.rect.height);
            SetOffset(Mathf.Clamp01((position - _grabOffset) / travel) * MaximumOffset);
        }

        /// <summary>
        /// 移動量を上下端へ制限し、つまみの大きさと位置を更新します。
        /// </summary>
        private void SetOffset(float offset)
        {
            float maximum = MaximumOffset;
            _offset = Mathf.Clamp(offset, 0.0f, maximum);
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, _offset);
            float ratio = Mathf.Clamp01(_viewport.rect.height / Mathf.Max(1.0f, _content.rect.height));
            float height = Mathf.Min(Track.rect.height, Mathf.Max(32.0f, Track.rect.height * ratio));
            _handle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            float normalized = maximum > 0.0f ? _offset / maximum : 0.0f;
            _handle.anchoredPosition = new Vector2(_handle.anchoredPosition.x, -normalized * (Track.rect.height - height));
        }
    }
}
