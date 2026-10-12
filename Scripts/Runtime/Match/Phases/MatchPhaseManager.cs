using UnityEngine;

using PranaTactics.UI.Screens;

namespace PranaTactics.Match.Phases
{
    /// <summary>
    /// 現在の試合フェイズを管理し、
    /// フェイズに応じた共通設定を適用します。
    /// </summary>
    public sealed class MatchPhaseManager : MonoBehaviour
    {
        /// <summary>
        /// 全体マップカメラの高度からFPS視点へ移行する秒数です。0の場合は即座に開始します。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _microTransitionDuration = 1.5f;

        public float MicroTransitionDuration => Mathf.Max(0.0f, _microTransitionDuration);

        /// <summary>
        /// FPS視点から全体マップカメラの高度へ上昇する秒数です。0の場合は即座に開始します。
        /// </summary>
        [SerializeField, Min(0.0f)]
        private float _macroTransitionDuration = 1.5f;

        public float MacroTransitionDuration => Mathf.Max(0.0f, _macroTransitionDuration);

        public static MatchPhaseManager Instance
        {
            get; private set;
        }

        public MatchPhase CurrentPhase
        {
            get; private set;
        }

        public MacroPlanningSession MacroPlanning
        {
            get; internal set;
        }

        /// <summary>
        /// シングルトンインスタンスを設定します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;
        }

        /// <summary>
        /// このインスタンスが破棄されたとき、
        /// シングルトンに保持されている参照を解除します。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Instance = null;
            }
        }

        /// <summary>
        /// 現在の試合フェイズを変更し、
        /// フェイズに対応するカーソル状態を適用します。
        /// </summary>
        /// <param name="newPhase">
        /// 新しく適用する試合フェイズです。
        /// </param>
        public void ChangePhase(MatchPhase newPhase)
        {
            CurrentPhase = newPhase;

            RefreshCursorState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (Instance != this)
            {
                return;
            }

            if (hasFocus)
            {
                RefreshCursorState();
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// フォーカス復帰や設定終了時に、現在のフェーズのカーソル状態を復元します。
        /// </summary>
        public void RefreshCursorState()
        {
            if (!Application.isFocused ||
                (SettingsUIManager.Instance != null && SettingsUIManager.Instance.IsOpen))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                return;
            }

            ApplyCursorVisibility(CurrentPhase);
        }

        /// <summary>
        /// 指定された試合フェイズに応じて、
        /// カーソルの表示状態を変更します。
        /// </summary>
        /// <param name="phase">
        /// カーソル表示を設定する試合フェイズです。
        /// </param>
        private static void ApplyCursorVisibility(
            MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Macro:
                    Cursor.lockState = CursorLockMode.Confined;
                    Cursor.visible = true;
                    break;

                case MatchPhase.Micro:
                case MatchPhase.MicroTransition:
                case MatchPhase.MacroTransition:
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                    break;

                default:
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }
        }
    }
}
