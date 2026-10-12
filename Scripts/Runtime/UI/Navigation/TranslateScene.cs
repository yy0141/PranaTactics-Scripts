using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.GameFlow;
using PranaTactics.GameFlow.States;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Navigation
{
    /// <summary>
    /// クリックされたときに、指定されたゲーム状態へ遷移します。
    /// </summary>
    /// <remarks>
    /// Inspectorで設定された<see cref="GameStateType"/>に対応する
    /// <see cref="GameState"/>を生成し、
    /// <see cref="GameFlowManager"/>へ状態変更を要求します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TranslateScene : ClickBehaviorBase
    {
        /// <summary>
        /// クリック時に遷移するゲーム状態の種類です。
        /// </summary>
        [FormerlySerializedAs("stateType")]
        [SerializeField]
        private GameStateType _stateType =
            GameStateType.Title;

        /// <summary>
        /// クリックされたときに、
        /// 指定されたゲーム状態へ遷移します。
        /// </summary>
        protected override void OnClickEnter()
        {
            Debug.Log(
                $"{nameof(TranslateScene)}: " +
                $"クリックされました。StateType={_stateType}",
                this);

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager == null)
            {
                Debug.LogError(
                    $"{nameof(TranslateScene)}: " +
                    $"{nameof(GameFlowManager)}が存在しないため、" +
                    "ゲーム状態を変更できません。",
                    this);

                return;
            }

            GameState nextState =
                CreateState(
                    _stateType);

            if (nextState == null)
            {
                Debug.LogError(
                    $"{nameof(TranslateScene)}: " +
                    "指定されたゲーム状態に対応する処理がありません。" +
                    $" StateType={_stateType}",
                    this);

                return;
            }

            Debug.Log(
                $"{nameof(TranslateScene)}: " +
                $"{nextState.GetType().Name}への変更を要求します。",
                this);

            gameFlowManager.ChangeState(
                nextState);
        }

        /// <summary>
        /// 指定されたゲーム状態の種類に対応する
        /// 状態オブジェクトを生成します。
        /// </summary>
        /// <param name="stateType">
        /// 生成するゲーム状態の種類です。
        /// </param>
        /// <returns>
        /// 生成されたゲーム状態です。
        /// 対応する状態が存在しない場合は<c>null</c>です。
        /// </returns>
        private static GameState CreateState(
            GameStateType stateType)
        {
            switch (stateType)
            {
                case GameStateType.Title:
                    return new TitleState();

                case GameStateType.Lobby:
                    return new LobbyState();

                case GameStateType.Settings:
                    return new SettingsState();

                case GameStateType.Tutorial:
                    return new TutorialState();

                case GameStateType.CharacterSelect:
                    return new CharacterSelectState();

                case GameStateType.Solo_CharacterSelect:
                    return GameFlowManager.Instance != null &&
                        GameFlowManager.Instance.GameModeId == PranaTactics.Match.Modes.GameModeRegistry.k_DefaultModeId
                        ? new SoloStageSelectState()
                        : new Solo_CharacterSelectState();

                case GameStateType.InGame:
                    return new GameplayState();

                case GameStateType.Result:
                    return new ResultState();

                default:
                    return null;
            }
        }
    }
}
