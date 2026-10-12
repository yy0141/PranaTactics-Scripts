using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.AI.Solo;
using PranaTactics.Board;
using PranaTactics.Board.Networking.Local;
using PranaTactics.Core;
using PranaTactics.Controls;
using PranaTactics.Match.Phases;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;
using PranaTactics.Unit.Networking.Local;

namespace PranaTactics.GameFlow.States
{
    /// <summary>
    /// 設定・訓練用ローカルシーンの読み込みと、
    /// ローカル環境におけるボード、ユニットおよび
    /// マッチフェイズの初期化を管理します。
    /// </summary>
    /// <remarks>
    /// <para>派生Stateが使用する専用シーンの名前です。</para>
    /// </remarks>
    public abstract class LocalSimulationState : GameState
    {
        protected abstract string SceneName
        {
            get;
        }

        /// <summary>
        /// ボードと兵装の初期化完了後に派生Stateへ制御を渡します。
        /// </summary>
        protected virtual void OnSimulationReady(UnitBehavior player, UnitBehavior target)
        {
        }

        protected virtual void OnSimulationFailed(Exception exception)
        {
        }

        /// <summary>
        /// 既定キャラクターを使用する場合は空文字列です。
        /// </summary>
        private const string k_LocalPlayerCharacterName = "";

        /// <summary>
        /// AIプレイヤーとして生成する
        /// キャラクターの名前です。
        /// </summary>
        private const string k_AIPlayerCharacterName = "ボット";

        /// <summary>
        /// ローカルプレイヤーの識別子です。
        /// </summary>
        private const int k_LocalPlayerId = 0;

        /// <summary>
        /// AIプレイヤーの識別子です。
        /// </summary>
        private const int k_AIPlayerId = 1;

        /// <summary>
        /// ローカルプレイヤーが所属する
        /// チームの識別子です。
        /// </summary>
        private const int k_LocalPlayerTeamId = 0;

        /// <summary>
        /// AIプレイヤーが所属するチームの識別子です。
        /// </summary>
        private const int k_AIPlayerTeamId = 1;

        /// <summary>
        /// AIプレイヤーを中央セルから
        /// Y方向へ離して配置する距離です。
        /// </summary>
        private const int k_AISpawnCoordinateYOffset = 10;

        /// <summary>
        /// AIプレイヤーによる移動を許可するかどうかを示します。
        /// </summary>
        private const bool k_IsAIMovementEnabled = false;

        /// <summary>
        /// AIプレイヤーによる武器の使用を許可するかどうかを示します。
        /// </summary>
        private const bool k_IsAIWeaponUsageEnabled = false;

        /// <summary>
        /// Pranam初期化完了を待機する最大時間です。
        /// </summary>
        private const float k_PranamInitializationTimeoutSeconds = 5.0f;

        /// <summary>
        /// この状態で実行する非同期処理を
        /// 中断するために使用します。
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 設定状態へ遷移したとき、
        /// 設定シーンの非同期読み込みを開始します。
        /// </summary>
        public override void Enter()
        {
            CancelPendingOperations();
            _cancellationTokenSource = new CancellationTokenSource();
            _ = LoadAndSetUpAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// 設定状態を終了し、
        /// ボードのイベント購読と実行中の
        /// 非同期処理を停止します。
        /// </summary>
        public override void Exit()
        {
            UnsubscribeBoardCreatedEvent();
            CancelPendingOperations();
        }

        /// <summary>
        /// 設定シーンを非同期で読み込み、
        /// シーン内のゲーム要素を初期化します。
        /// </summary>
        /// <param name="cancellationToken">
        /// 処理を中断するためのトークンです。
        /// </param>
        /// <remarks>
        /// <para>初回起動ではAddressablesがまだ読み込み中です。</para>
        /// <para>シーン側のAwake/Startもデータを利用するため、遷移前に待ちます。</para>
        /// <para>シーン読み込み完了直後のAwakeやStartが</para>
        /// <para>実行されるのを1フレーム待ちます。</para>
        /// <para>状態終了によるキャンセルは</para>
        /// <para>正常な終了として扱います。</para>
        /// </remarks>
        private async Awaitable LoadAndSetUpAsync(CancellationToken cancellationToken)
        {
            try
            {
                await WaitForGameDataAsync(cancellationToken);
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

                if (loadOperation == null)
                {
                    Debug.LogError($"{nameof(LocalSimulationState)}: " + $"{SceneName}の読み込みを" + "開始できませんでした。");

                    return;
                }

                while (!loadOperation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Awaitable.NextFrameAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(cancellationToken);
                await GameplayUiSceneLoader.EnsureLoadedAsync(cancellationToken);
                SetLoadingPanelVisible(true);
                await SetUpAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(LocalSimulationState)}: " + "設定シーンの読み込み中に" + "エラーが発生しました。");
                Debug.LogException(exception);
                OnSimulationFailed(exception);
            }
        }

        /// <summary>
        /// データの準備完了まで、状態終了時にキャンセルできる形で待ちます。
        /// </summary>
        /// <param name="cancellationToken">
        /// 初期化処理を中断するためのトークンです。
        /// </param>
        private static async Awaitable WaitForGameDataAsync(CancellationToken cancellationToken)
        {
            float deadline = Time.realtimeSinceStartup + 45.0f;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameDataRepository repository = GameDataRepository.Instance;

                if (repository == null)
                {
                    throw new InvalidOperationException("ゲームデータ管理がありません。TitleSceneから起動してください。");
                }

                if (repository.InitializationError != null)
                {
                    throw new InvalidOperationException("ゲームデータの読み込みに失敗しました。", repository.InitializationError);
                }

                if (repository.IsLoaded)
                {
                    return;
                }

                if (Time.realtimeSinceStartup >= deadline)
                {
                    throw new TimeoutException("ゲームデータの準備が45秒以内に完了しませんでした。");
                }

                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 入力、兵装UIおよびボードを初期化します。
        /// </summary>
        /// <remarks>
        /// <para>UIとBoardManagerの初期化処理が</para>
        /// <para>同一フレームへ集中しないようにします。</para>
        /// <para>状態終了によるキャンセルは</para>
        /// <para>正常な終了として扱います。</para>
        /// </remarks>
        private async Awaitable SetUpAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!TryGetRequiredManagers(
                    out PlayerInputContext inputContext,
                    out BoardManager boardManager))
                {
                    SetLoadingPanelVisible(false);

                    return;
                }

                PranamUIManager.Instance.Initialize(
                    new BattlePranamSelector(
                    inputContext,
                    () => PranaTactics.Settings.SettingsManager.Current.Controls));
                LocalBoardNetwork boardNetwork = GetOrAddLocalBoardNetwork(boardManager);

                boardManager.Initialize(boardNetwork);
                boardManager.BoardCreated -= HandleBoardCreated;
                boardManager.BoardCreated += HandleBoardCreated;
                cancellationToken.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(cancellationToken);
                boardManager.RequestBoardCreation();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SetLoadingPanelVisible(false);
                Debug.LogError($"{nameof(LocalSimulationState)}: " + "設定シーンの初期化中に" + "エラーが発生しました。");
                Debug.LogException(exception);
                OnSimulationFailed(exception);
            }
        }

        /// <summary>
        /// 初期化に必要な入力コンテキストと
        /// BoardManagerを取得できるか確認します。
        /// </summary>
        /// <param name="inputContext">
        /// 取得されたプレイヤー入力コンテキストです。
        /// </param>
        /// <param name="boardManager">
        /// 取得されたBoardManagerです。
        /// </param>
        /// <returns>
        /// 必要な管理クラスをすべて取得できた場合は
        /// <c>true</c>、それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool TryGetRequiredManagers(
            out PlayerInputContext inputContext,
            out BoardManager boardManager)
        {
            inputContext = null;
            boardManager = null;

            if (InputManager.Instance == null)
            {
                Debug.LogError($"{nameof(LocalSimulationState)}: " + $"{nameof(InputManager)}が存在しません。");

                return false;
            }

            inputContext = InputManager.Instance.InputContext;

            if (inputContext == null)
            {
                Debug.LogError($"{nameof(LocalSimulationState)}: " + "入力コンテキストが" + "初期化されていません。");

                return false;
            }

            if (PranamUIManager.Instance == null)
            {
                Debug.LogError($"{nameof(LocalSimulationState)}: " + $"{nameof(PranamUIManager)}が存在しません。");

                return false;
            }

            boardManager = BoardManager.Instance;

            if (boardManager == null)
            {
                Debug.LogError($"{nameof(LocalSimulationState)}: " + $"{nameof(BoardManager)}が存在しません。");

                return false;
            }

            return true;
        }

        /// <summary>
        /// BoardManagerと同じGameObjectに設定されている
        /// LocalBoardNetworkを取得します。
        /// 存在しない場合は新しく追加します。
        /// </summary>
        /// <param name="boardManager">
        /// LocalBoardNetworkを設定するBoardManagerです。
        /// </param>
        /// <returns>
        /// 取得または追加されたLocalBoardNetworkです。
        /// </returns>
        private static LocalBoardNetwork GetOrAddLocalBoardNetwork(BoardManager boardManager)
        {
            if (boardManager.TryGetComponent(out LocalBoardNetwork boardNetwork))
            {
                return boardNetwork;
            }

            return boardManager.gameObject.AddComponent<LocalBoardNetwork>();
        }

        /// <summary>
        /// ボードの生成が完了したとき、
        /// ローカルプレイヤーとAIプレイヤーの
        /// 初期化を開始します。
        /// </summary>
        private void HandleBoardCreated()
        {
            UnsubscribeBoardCreatedEvent();

            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            _ = InitializeBattleAsync(_cancellationTokenSource.Token);
        }

        /// <summary>
        /// 指定されたユニットのPranamと扇形範囲の
        /// 初期化完了を待機します。
        /// </summary>
        private static async Awaitable WaitForPranamInitializationAsync(
            UnitBehavior unitBehavior,
            CancellationToken cancellationToken)
        {
            TacticalMapUnitBehavior tacticalMapUnitBehavior = unitBehavior?.GetTacticalMapUnitBehavior();

            if (tacticalMapUnitBehavior == null)
            {
                throw new InvalidOperationException("TacticalMapUnitBehaviorを取得できません。");
            }

            float timeoutAt = Time.realtimeSinceStartup + k_PranamInitializationTimeoutSeconds;

            while (!tacticalMapUnitBehavior.IsPranamInitialized)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (tacticalMapUnitBehavior.HasPranamInitializationFailed)
                {
                    throw new InvalidOperationException(
                        "Pranamまたは扇形範囲の初期化に失敗しました。" +
                        $" PlayerId={unitBehavior.PlayerId}");
                }

                if (Time.realtimeSinceStartup >= timeoutAt)
                {
                    throw new TimeoutException(
                        "Pranamまたは扇形範囲の初期化が" + "制限時間内に完了しませんでした。" +
                        $" PlayerId={unitBehavior.PlayerId}," +
                        $" Timeout={k_PranamInitializationTimeoutSeconds:0.0}s");
                }

                await Awaitable.NextFrameAsync(cancellationToken);
            }
        }

        /// <summary>
        /// ローカルプレイヤー、AIプレイヤーおよび
        /// マッチフェイズを初期化します。
        /// </summary>
        /// <param name="cancellationToken">
        /// 初期化処理を中断するためのトークンです。
        /// </param>
        /// <remarks>
        /// <para>状態終了によるキャンセルは</para>
        /// <para>正常な終了として扱います。</para>
        /// </remarks>
        private async Awaitable InitializeBattleAsync(CancellationToken cancellationToken)
        {
            try
            {
                BoardManager boardManager = BoardManager.Instance;
                UnitManager unitManager = UnitManager.Instance;
                MatchPhaseManager matchPhaseManager = MatchPhaseManager.Instance;

                if (boardManager == null)
                {
                    Debug.LogError($"{nameof(LocalSimulationState)}: " + $"{nameof(BoardManager)}が存在しません。");
                    SetLoadingPanelVisible(false);

                    return;
                }

                if (unitManager == null)
                {
                    Debug.LogError($"{nameof(LocalSimulationState)}: " + $"{nameof(UnitManager)}が存在しません。");
                    SetLoadingPanelVisible(false);

                    return;
                }

                if (matchPhaseManager == null)
                {
                    Debug.LogError(
                        $"{nameof(LocalSimulationState)}: " + $"{nameof(MatchPhaseManager)}が" +
                        "存在しません。");
                    SetLoadingPanelVisible(false);

                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                Vector2Int centerCoordinate = boardManager.GetCenterCoordinate();
                List<PlayerInfo> players = CreatePlayerInformation(boardManager, centerCoordinate);
                LocalPlayerService playerService = new LocalPlayerService();

                playerService.Initialize(k_LocalPlayerId, players, false);
                unitManager.Initialize(playerService, new LocalUnitFactory());
                unitManager.CreateUnits(players);

                if (!unitManager.TryGetUnit(k_LocalPlayerId, out UnitBehavior localUnit))
                {
                    Debug.LogError($"{nameof(LocalSimulationState)}: " + "生成したローカルプレイヤーを取得できません。");

                    return;
                }

                if (!unitManager.TryGetUnit(k_AIPlayerId, out UnitBehavior aiUnit))
                {
                    Debug.LogError($"{nameof(LocalSimulationState)}: " + "生成したボットを取得できません。");

                    return;
                }

                await WaitForPranamInitializationAsync(localUnit, cancellationToken);
                await WaitForPranamInitializationAsync(aiUnit, cancellationToken);
                bool aiEnabled = unitManager.EnableAI(
                    k_AIPlayerId,
                    k_IsAIMovementEnabled,
                    k_IsAIWeaponUsageEnabled);

                if (!aiEnabled)
                {
                    Debug.LogError($"{nameof(LocalSimulationState)}: " + "ボットのAI設定に失敗しました。");

                    return;
                }

                SoloAiDirector aiDirector = SoloAiDirector.EnsureExists();

                matchPhaseManager.ChangePhase(MatchPhase.Macro);
                aiDirector.PlanMacroPhaseImmediately();
                aiUnit.GetMacroUnitBehavior()?.CancelAiMacroMovement();
                aiUnit.GetMicroUnitBehavior()?.PrepareForMicroPhase();
                matchPhaseManager.ChangePhase(MatchPhase.Micro);
                aiDirector.InitializeMicroPhaseImmediately();
                await Awaitable.NextFrameAsync(cancellationToken);
                SetLoadingPanelVisible(false);
                OnSimulationReady(localUnit, aiUnit);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                SetLoadingPanelVisible(false);
                Debug.LogError($"{nameof(LocalSimulationState)}: " + "ローカルバトルの初期化中に" + "エラーが発生しました。");
                Debug.LogException(exception);
                OnSimulationFailed(exception);
            }
        }

        /// <summary>
        /// ローカルプレイヤーとAIプレイヤーの
        /// 情報を生成します。
        /// </summary>
        /// <param name="centerCoordinate">
        /// ローカルプレイヤーを配置する
        /// ボード中央の座標です。
        /// </param>
        /// <returns>
        /// ローカルバトルに参加する
        /// プレイヤー情報の一覧です。
        /// </returns>
        private static List<PlayerInfo> CreatePlayerInformation(
            BoardManager boardManager,
            Vector2Int centerCoordinate)
        {
            Vector2Int aiCoordinate = new Vector2Int(
                centerCoordinate.x,
                centerCoordinate.y +
                k_AISpawnCoordinateYOffset);
            Vector3 aiRotation = CalculateFacingRotation(boardManager, aiCoordinate, centerCoordinate);
            string initializedCharacterName = k_LocalPlayerCharacterName;

            if (k_LocalPlayerCharacterName == "")
            {
                List<UnitParams> characters = GameDataRepository.Instance.GetPlayerCharactersData();

                if (characters.Count == 0)
                {
                    throw new InvalidOperationException(
                        "プレイヤー用キャラクターデータがありません。CharacterのAddressables登録を確認してください。");
                }

                initializedCharacterName = characters[0].UnitName;
            }

            return new List<PlayerInfo>
            {
                new PlayerInfo
                {
                    PlayerId = k_LocalPlayerId,
                    TeamId = k_LocalPlayerTeamId,
                    CharacterName = initializedCharacterName,
                    Coordinate = centerCoordinate,
                    Rotation = Vector3.zero
                },
                new PlayerInfo
                {
                    PlayerId = k_AIPlayerId,
                    TeamId = k_AIPlayerTeamId,
                    CharacterName = k_AIPlayerCharacterName,
                    Coordinate = aiCoordinate,
                    Rotation = aiRotation
                }
            };
        }

        /// <summary>
        /// 指定された座標から対象座標を向く初期回転を計算します。
        /// </summary>
        /// <param name="boardManager">
        /// 座標からワールド位置を取得するBoardManagerです。
        /// </param>
        /// <param name="fromCoordinate">
        /// 回転させるユニットの座標です。
        /// </param>
        /// <param name="targetCoordinate">
        /// 向く対象の座標です。
        /// </param>
        /// <returns>
        /// モデルの正面が対象を向くY軸回転です。
        /// </returns>
        private static Vector3 CalculateFacingRotation(
            BoardManager boardManager,
            Vector2Int fromCoordinate,
            Vector2Int targetCoordinate)
        {
            if (boardManager == null)
            {
                return Vector3.zero;
            }

            Vector3 fromPosition = boardManager.GetMapHexCellPosition(fromCoordinate.x, fromCoordinate.y);
            Vector3 targetPosition = boardManager.GetMapHexCellPosition(
                targetCoordinate.x,
                targetCoordinate.y);
            Vector3 direction = targetPosition - fromPosition;

            direction.y = 0.0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            return new Vector3(0.0f, rotation.eulerAngles.y, 0.0f);
        }

        /// <summary>
        /// ローディング用パネルの表示状態を変更します。
        /// </summary>
        /// <param name="isVisible">
        /// パネルを表示する場合は<c>true</c>、
        /// 非表示にする場合は<c>false</c>です。
        /// </param>
        private static void SetLoadingPanelVisible(bool isVisible)
        {
            if (HUDManager.Instance == null)
            {
                if (isVisible)
                {
                    Debug.LogWarning($"{nameof(LocalSimulationState)}: " + $"{nameof(HUDManager)}が存在しません。");
                }

                return;
            }

            HUDManager.Instance.EnablePanel(isVisible);
        }

        /// <summary>
        /// BoardManagerのボード生成完了イベントから
        /// この状態のイベントハンドラーを解除します。
        /// </summary>
        private void UnsubscribeBoardCreatedEvent()
        {
            if (BoardManager.Instance == null)
            {
                return;
            }

            BoardManager.Instance.BoardCreated -= HandleBoardCreated;
        }

        /// <summary>
        /// この状態で実行されている非同期処理を中断し、
        /// キャンセルトークンソースを破棄します。
        /// </summary>
        private void CancelPendingOperations()
        {
            if (_cancellationTokenSource == null)
            {
                return;
            }

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }
}
