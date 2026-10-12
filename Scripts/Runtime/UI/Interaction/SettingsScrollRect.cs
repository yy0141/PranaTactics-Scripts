using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using PranaTactics.Settings;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// ドラッグの向きを維持し、ホイール操作へユーザーのスクロール方向設定を反映します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsScrollRect : ScrollRect
    {
        /// <summary>
        /// 縦横のホイール入力を設定に合わせて処理し、共有イベントの値を復元します。
        /// </summary>
        public override void OnScroll(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            Vector2 originalDelta = eventData.scrollDelta;
            ControlsSettings controls = SettingsManager.Current.Controls;
            try
            {
                eventData.scrollDelta = new Vector2(
                    controls.ApplyScrollDirection(originalDelta.x),
                    controls.ApplyScrollDirection(originalDelta.y));
                base.OnScroll(eventData);
            }
            finally
            {
                eventData.scrollDelta = originalDelta;
            }
        }
    }
}
