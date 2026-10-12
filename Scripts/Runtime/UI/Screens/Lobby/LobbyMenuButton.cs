using UnityEngine;

using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// ロビーに配置されたメニューボタンです。
    /// メニュー表示中は自身を非表示にします。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyMenuButton :
        RepeatClickBehaviorBase,
        IClickableCondition
    {
        /// <summary>
        /// メニューを開けるかどうかを判定します。
        /// </summary>
        public bool CanClick()
        {
            SettingsUIManager settingsManager =
                SettingsUIManager.Instance;

            return settingsManager != null &&
                !settingsManager.IsOpen;
        }

        /// <summary>
        /// メニューボタンがクリックされたとき、
        /// SettingsUIを開きます。
        /// </summary>
        protected override void OnClickEnter()
        {
            SettingsUIManager settingsManager =
                SettingsUIManager.Instance;

            if (settingsManager == null)
            {
                Debug.LogWarning(
                    $"{nameof(LobbyMenuButton)}: " +
                    $"{nameof(SettingsUIManager)}が存在しません。",
                    this);

                return;
            }

            if (settingsManager.IsOpen)
            {
                return;
            }

            settingsManager.Open();
        }
    }
}
