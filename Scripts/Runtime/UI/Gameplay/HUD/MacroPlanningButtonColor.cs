using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Controls;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 操作可否、マウスオーバー、クリック直後の色をInspector設定から適用します。
    /// </summary>
    public sealed class MacroPlanningButtonColor : MouseOverBehaviorBase
    {
        [SerializeField]
        private Color _normalColor = new(0.12f, 0.36f, 0.52f, 1f);

        [SerializeField]
        private Color _disabledColor = new(0.20f, 0.23f, 0.28f, 1f);

        [SerializeField]
        private Color _hoverColor = new(0.20f, 0.62f, 0.80f, 1f);

        [SerializeField]
        private Color _pressedColor = new(0.12f, 0.82f, 0.65f, 1f);

        private Image _image;

        private MacroPlanningButton _button;

        private float _pressedUntil;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _button = GetComponent<MacroPlanningButton>();
        }

        public void ShowPressed()
        {
            _pressedUntil = Time.unscaledTime + 0.16f;
        }

        /// <summary>
        /// クリック選択が残る既存UIでも、再びポインターを重ねたときの色を更新します。
        /// </summary>
        private void LateUpdate()
        {
            bool canClick = _button.RefreshClickAvailability();
            bool isHovered = canClick && InputRaycastUtility.GetUIRaycastResults().Contains(gameObject);

            _image.color = Time.unscaledTime < _pressedUntil ? _pressedColor :
                !canClick ? _disabledColor : isHovered ? _hoverColor : _button.IsSelected ? _pressedColor : _normalColor;
        }
    }
}
