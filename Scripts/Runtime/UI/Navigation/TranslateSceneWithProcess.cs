using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.GameFlow;
using PranaTactics.GameFlow.States;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Navigation
{
    /// <summary>
    /// 指定された前処理が完了した後、
    /// 設定されたゲーム状態へ遷移します。
    /// </summary>
    /// <remarks>
    /// セーブ、通信切断、フェードアウトなど、
    /// シーン遷移前に完了させる必要がある処理を実行できます。
    ///
    /// 前処理を設定していない場合は、
    /// クリック後すぐにゲーム状態を変更します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TranslateSceneWithProcess :
    ClickBehaviorBase
    {
        /// <summary>
        /// 前処理完了後に遷移するゲーム状態です。
        /// </summary>
        [FormerlySerializedAs("stateType")]
        [SerializeField]
        private GameStateType _stateType =
            GameStateType.Title;

        /// <summary>
        /// シーン遷移前処理を実装したコンポーネントです。
        /// </summary>
        [SerializeField]
        private MonoBehaviour _beforeProcessObject;

        /// <summary>
        /// シーン遷移前処理として使用するインターフェースです。
        /// </summary>
        private IBeforeSceneTransitionProcess _beforeProcess;

        /// <summary>
        /// 現在、前処理または状態遷移を実行中かどうかを示します。
        /// </summary>
        private bool _isTransitioning;

        /// <summary>
        /// シーン遷移前処理を取得し、
        /// 設定内容を検証します。
        /// </summary>
        private void Awake()
        {
            ResolveBeforeProcess();
        }

        /// <summary>
        /// クリックされたとき、
        /// 前処理を実行してからゲーム状態を変更します。
        /// </summary>
        protected override void OnClickEnter()
        {
            if (_isTransitioning)
            {
                return;
            }

            if (GameFlowManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(TranslateSceneWithProcess)}: " +
                    $"{nameof(GameFlowManager)}が存在しないため、" +
                    "遷移前処理を開始できません。",
                    this);

                return;
            }

            _isTransitioning =
                true;

            if (_beforeProcess == null &&
                _beforeProcessObject != null)
            {
                ResolveBeforeProcess();
            }

            if (_beforeProcess == null)
            {
                Translate();

                return;
            }

            _beforeProcess.Execute(
                HandleBeforeProcessCompleted);
        }

        /// <summary>
        /// シーン遷移前処理の完了通知を受け取り、
        /// ゲーム状態を変更します。
        /// </summary>
        private void HandleBeforeProcessCompleted()
        {
            if (!_isTransitioning ||
                !isActiveAndEnabled)
            {
                return;
            }

            Translate();
        }

        /// <summary>
        /// 設定されたゲーム状態へ遷移します。
        /// </summary>
        private void Translate()
        {
            if (!isActiveAndEnabled)
            {
                _isTransitioning =
                    false;

                return;
            }

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager == null)
            {
                Debug.LogError(
                    $"{nameof(TranslateSceneWithProcess)}: " +
                    $"{nameof(GameFlowManager)}が存在しないため、" +
                    "ゲーム状態を変更できません。",
                    this);

                _isTransitioning =
                    false;

                return;
            }

            GameState nextState =
                CreateState(
                    _stateType);

            if (nextState == null)
            {
                Debug.LogError(
                    $"{nameof(TranslateSceneWithProcess)}: " +
                    "指定されたゲーム状態に対応する処理がありません。" +
                    $" StateType={_stateType}",
                    this);

                _isTransitioning =
                    false;

                return;
            }

            gameFlowManager.ChangeState(
                nextState);
        }

        /// <summary>
        /// 設定されたMonoBehaviourから、
        /// シーン遷移前処理を取得します。
        /// </summary>
        private void ResolveBeforeProcess()
        {
            _beforeProcess =
                null;

            if (_beforeProcessObject == null)
            {
                return;
            }

            _beforeProcess =
                _beforeProcessObject as
                    IBeforeSceneTransitionProcess;

            if (_beforeProcess != null)
            {
                return;
            }

            Debug.LogError(
                $"{nameof(TranslateSceneWithProcess)}: " +
                $"{_beforeProcessObject.name}に設定された" +
                $"{_beforeProcessObject.GetType().Name}は、" +
                $"{nameof(IBeforeSceneTransitionProcess)}を" +
                "実装していません。",
                _beforeProcessObject);
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
                    if (GameFlowManager.Instance != null &&
                        GameFlowManager.Instance.HasSelectedSoloStage &&
                        GameFlowManager.Instance.CurrentState is ResultState)
                    {
                        return new SoloStageSelectState();
                    }

                    return new LobbyState();

                case GameStateType.Settings:
                    return new SettingsState();

                case GameStateType.Tutorial:
                    return new TutorialState();

                case GameStateType.CharacterSelect:
                    return new CharacterSelectState();

                case GameStateType.InGame:
                    return new GameplayState();

                case GameStateType.Result:
                    return new ResultState();

                default:
                    return null;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定された前処理コンポーネントを検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_beforeProcessObject == null)
            {
                return;
            }

            if (_beforeProcessObject is
                IBeforeSceneTransitionProcess)
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(TranslateSceneWithProcess)}: " +
                $"{_beforeProcessObject.name}は" +
                $"{nameof(IBeforeSceneTransitionProcess)}を" +
                "実装していません。",
                this);
        }
#endif
    }
}
