using UnityEngine;

using PranaTactics.Controls;
using PranaTactics.Settings;

namespace PranaTactics.Tutorial
{
    public struct TutorialInputState
    {
        public bool ConfirmPressed;

        public bool ConfirmHeld;

        public bool MenuPressed;

        public bool DocumentPressed;
    }

    public interface ITutorialInput
    {
        TutorialInputState Read();

        bool IsKeyPressed(KeyCode key);
    }

    /// <summary>
    /// Unityの物理入力を論理操作に変換します。別デバイスやテスト入力へ交換可能です。
    /// </summary>
    public sealed class TutorialPlayerInput : ITutorialInput
    {
        private readonly TutorialInputBindings _bindings;

        public TutorialPlayerInput(TutorialInputBindings bindings)
        {
            _bindings = bindings;
        }

        public bool IsKeyPressed(KeyCode key)
        {
            if (key == KeyCode.LeftShift || key == KeyCode.RightShift)
            {
                return KeyboardBindingInput.IsPressed(SettingsManager.Current.Controls.WalkKey);
            }

            if (key == KeyCode.LeftControl || key == KeyCode.RightControl)
            {
                return KeyboardBindingInput.IsPressed(SettingsManager.Current.Controls.StopKey);
            }

            return Input.GetKeyDown(key);
        }

        public TutorialInputState Read() => new TutorialInputState
        {
            ConfirmPressed = Input.GetKeyDown(_bindings.Confirm.Key),
            ConfirmHeld = Input.GetKey(_bindings.Confirm.Key),
            MenuPressed = Input.GetKeyDown(_bindings.Menu.Key),
            DocumentPressed = Input.GetKeyDown(_bindings.Document.Key)
        };
    }
}
