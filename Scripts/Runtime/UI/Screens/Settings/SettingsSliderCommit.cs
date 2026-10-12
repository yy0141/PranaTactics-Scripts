using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// スライダー操作の終了時だけ保存を要求します。
    /// </summary>
    public sealed class SettingsSliderCommit : MonoBehaviour, IPointerUpHandler, IDeselectHandler, ISubmitHandler
    {
        public Action Commit
        {
            private get; set;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Commit?.Invoke();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Commit?.Invoke();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Commit?.Invoke();
        }
    }
}
