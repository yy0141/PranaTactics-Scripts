using UnityEngine;
using UnityEngine.UI;

using UiToggle = UnityEngine.UI.Toggle;

namespace PranaTactics.UI
{
    /// <summary>
    /// トグルの表示を切り替えます。
    /// </summary>
    public class Toggle : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _handle;

        [SerializeField]
        private UiToggle _toggle;

        [SerializeField]
        private Image _backgroundImage;

        [SerializeField]
        private Color _backgroundOnColor;

        [SerializeField]
        private Color _backgroundOffColor;

        /// <summary>
        /// トグルの状態に合わせて表示を更新します。
        /// </summary>
        public void ToggleChanged()
        {
            _handle.anchoredPosition *= -1.0f;

            if (_backgroundImage == null)
            {
                return;
            }

            _backgroundImage.color =
                _toggle.isOn
                    ? _backgroundOnColor
                    : _backgroundOffColor;
        }
    }
}
