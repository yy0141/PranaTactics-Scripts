using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

using PranaTactics.Board;
using PranaTactics.Board.Networking;
using PranaTactics.Board.Networking.EOS;
using PranaTactics.Board.Networking.Local;
using PranaTactics.Board.Networking.Photon;
using PranaTactics.Controls;
using PranaTactics.GameFlow;
using PranaTactics.Networking;
using PranaTactics.Networking.EOS;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;
using PranaTactics.Unit.Networking;
using PranaTactics.Unit.Networking.EOS;
using PranaTactics.Unit.Networking.Photon;

namespace PranaTactics.Match.Phases
{
    /// <summary>
    /// ゲームプレイ開始時のボード、プレイヤー、ユニットおよび
    /// UIの初期化処理を管理します。
    /// </summary>
    /// <remarks>
    /// Photonでは既存のネットワーク生成処理を使用し、
    /// EOSではホストが生成したユニット初期化情報を
    /// ルームプロパティを介して各クライアントへ共有します。
    /// </remarks>
    public sealed class StartPhaseState : MatchPhaseState
    {
        /// <summary>
        /// プレイヤーがゲームプレイシーンの準備を完了したことを示す
        /// プレイヤープロパティのキーです。
        /// </summary>
        private const string k_InGameSceneReadyPropertyKey =
            "inGameSceneReady";

        /// <summary>
        /// EOSでユニット生成情報を保存する
        /// ルームプロパティのキーです。
        /// </summary>
        private const string k_EosUnitSpawnsPropertyKey =
            "battleUnitSpawns";

        /// <summary>
        /// オンラインサービスがEOS実装か判定するときに使用する文字列です。
        /// </summary>
        private const string k_EosServiceTypeName = "Eos";

        /// <summary>
        /// オンライン処理の待機間隔です。
        /// </summary>
        private const int k_OnlinePollingIntervalMilliseconds = 100;

        /// <summary>
        /// ローカルユニット生成の待機間隔です。
        /// </summary>
        private const int k_LocalUnitPollingIntervalMilliseconds = 50;

        /// <summary>
        /// 全プレイヤーのゲームシーン準備を待機する最大時間です。
        /// </summary>
        private const float k_PlayerReadyTimeoutSeconds = 10.0f;

        /// <summary>
        /// EOSのユニット生成情報を待機する最大時間です。
        /// </summary>
        private const float k_EosUnitSpawnTimeoutSeconds = 10.0f;

        /// <summary>
        /// ローカルユニットの生成を待機する最大時間です。
        /// </summary>
        private const float k_LocalUnitTimeoutSeconds = 10.0f;

        /// <summary>
        /// プレイヤーのTeamId同期を待機する最大時間です。
        /// </summary>
        private const float k_TeamIdTimeoutSeconds = 5.0f;

        /// <summary>
        /// 待機状態の詳細ログを出力する間隔です。
        /// </summary>
        private const float k_WaitLogIntervalSeconds = 1.0f;

        /// <summary>
        /// ソロプレイ時に使用する開始フェイズです。
        /// </summary>
        private SoloStartPhaseState _soloState;

        /// <summary>
        /// 現在使用しているオンラインサービスです。
        /// </summary>
        private IOnlineService _onlineService;

        /// <summary>
        /// この状態で実行する非同期処理を中断するために使用します。
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// ゲーム開始に必要な初期化処理が完了しているかどうかを示します。
        /// </summary>
        private bool _isSetupComplete;

        /// <summary>
        /// EOS用のユニット生成情報を適用済みかどうかを示します。
        /// </summary>
        private bool _areEosUnitSpawnsApplied;

        /// <summary>
        /// UnitManagerの初期化が完了しているかどうかを示します。
        /// </summary>
        private bool _isUnitManagerInitialized;

        /// <summary>
        /// ルームプロパティ変更イベントを購読済みかどうかを示します。
        /// </summary>
        private bool _isRoomPropertyEventRegistered;

        /// <summary>
        /// Macro Phaseへの遷移要求を送信済みかどうかを示します。
        /// </summary>
        private bool _hasRequestedMacroPhase;

        /// <summary>
        /// UnitManagerの初期化前に受信した
        /// EOS用ユニット生成情報です。
        /// </summary>
        private string _pendingEosUnitSpawnsJson;

        /// <summary>
        /// 次に待機状態の詳細ログを出力できる時刻です。
        /// </summary>
        private float _nextWaitLogTime;

        /// <summary>
        /// Start Phaseへ遷移したとき、
        /// ゲーム開始に必要な初期化処理を開始します。
        /// </summary>
        public override void Enter()
        {
            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager != null &&
                gameFlowManager.IsSoloPlay)
            {
                Debug.Log(
                    $"{nameof(StartPhaseState)}: " +
                    "ソロプレイのため、" +
                    $"{nameof(SoloStartPhaseState)}を開始します。");

                _soloState =
                    new SoloStartPhaseState();

                _soloState.Enter();

                return;
            }

            _soloState = null;

            ResetState();
            CreateCancellationTokenSource();

            _onlineService =
                NetworkServiceLocator.Online;

            RegisterRoomPropertyEvent();

            _ = SetUpAsync(
                _cancellationTokenSource.Token);
        }

        /// <summary>
        /// 初期化完了後、ホストからMacro Phaseへの遷移を要求します。
        /// </summary>
        public override void UpdateState()
        {
            if (_soloState != null)
            {
                _soloState.UpdateState();

                return;
            }

            if (!_isSetupComplete ||
                _hasRequestedMacroPhase)
            {
                return;
            }

            if (!IsHostOrOffline())
            {
                return;
            }

            if (MatchManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    $"{nameof(MatchManager)}が存在しません。");

                return;
            }

            _hasRequestedMacroPhase = true;

            MatchManager.Instance.ChangeState(
                new MacroPhaseState());
        }

        /// <summary>
        /// Start Phaseを終了し、イベント購読と
        /// 実行中の非同期処理を停止します。
        /// </summary>
        public override void Exit()
        {
            if (_soloState != null)
            {
                _soloState.Exit();
                _soloState = null;

                return;
            }

            UnregisterRoomPropertyEvent();
            UnregisterBoardCreatedEvent();
            CancelPendingOperations();
        }

        /// <summary>
        /// Start Phaseで使用する状態値を初期化します。
        /// </summary>
        private void ResetState()
        {
            _isSetupComplete = false;
            _areEosUnitSpawnsApplied = false;
            _isUnitManagerInitialized = false;
            _isRoomPropertyEventRegistered = false;
            _hasRequestedMacroPhase = false;
            _pendingEosUnitSpawnsJson = null;
            _nextWaitLogTime = 0.0f;
        }

        /// <summary>
        /// 非同期処理を制御するCancellationTokenSourceを生成します。
        /// </summary>
        private void CreateCancellationTokenSource()
        {
            CancelPendingOperations();

            _cancellationTokenSource =
                new CancellationTokenSource();
        }

        /// <summary>
        /// 現在実行されている非同期処理を中断します。
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

        /// <summary>
        /// オンラインサービスのルームプロパティ変更イベントを購読します。
        /// </summary>
        private void RegisterRoomPropertyEvent()
        {
            if (_isRoomPropertyEventRegistered)
            {
                return;
            }

            if (_onlineService == null)
            {
                _onlineService =
                    NetworkServiceLocator.Online;
            }

            if (_onlineService == null)
            {
                return;
            }

            _onlineService.RoomPropertiesChanged +=
                HandleRoomPropertiesChanged;

            _isRoomPropertyEventRegistered = true;
        }

        /// <summary>
        /// オンラインサービスのルームプロパティ変更イベントを解除します。
        /// </summary>
        private void UnregisterRoomPropertyEvent()
        {
            if (!_isRoomPropertyEventRegistered)
            {
                return;
            }

            if (_onlineService != null)
            {
                _onlineService.RoomPropertiesChanged -=
                    HandleRoomPropertiesChanged;
            }

            _isRoomPropertyEventRegistered = false;
        }

        /// <summary>
        /// ボード生成完了イベントの購読を解除します。
        /// </summary>
        private void UnregisterBoardCreatedEvent()
        {
            if (BoardManager.Instance == null)
            {
                return;
            }

            BoardManager.Instance.BoardCreated -=
                HandleBoardCreated;
        }

        /// <summary>
        /// ローカルプレイヤーのTeamIdを補完し、
        /// ゲームプレイシーンの準備完了状態を設定します。
        /// </summary>
        private void SetLocalInGameReady()
        {
            IOnlineService onlineService =
                GetOnlineService();

            if (onlineService == null)
            {
                Debug.Log(
                    $"{nameof(StartPhaseState)}: " +
                    "オンラインサービスが存在しないため、" +
                    "オフラインとして初期化を続行します。");

                return;
            }

            if (!onlineService.IsInRoom)
            {
                return;
            }

            OnlinePlayer localPlayer =
                GetLocalOnlinePlayer(onlineService);

            if (MatchTeamAssignment.TryGet(onlineService.CurrentRoom, onlineService.LocalPlayerId, out int assignedTeam))
            {
                onlineService.SetLocalPlayerProperty(NetworkPropertyKeys.TeamId, assignedTeam);
                if (localPlayer != null)
                {
                    localPlayer.Properties[NetworkPropertyKeys.TeamId] = assignedTeam;
                }
            }

            bool hasTeamId =
                localPlayer != null &&
                localPlayer.TryGetProperty(
                    NetworkPropertyKeys.TeamId,
                    out int _);

            if (!hasTeamId)
            {
                int resolvedTeamId =
                    ResolveLocalTeamId(onlineService);

                onlineService.SetLocalPlayerProperty(
                    NetworkPropertyKeys.TeamId,
                    resolvedTeamId);

                Debug.Log(
                    $"{nameof(StartPhaseState)}: " +
                    "ローカルプレイヤーのTeamIdを決定しました。" +
                    $"PlayerId={onlineService.LocalPlayerId}, " +
                    $"TeamId={resolvedTeamId}");
            }

            onlineService.SetLocalPlayerProperty(
                k_InGameSceneReadyPropertyKey,
                true);
        }

        /// <summary>
        /// ローカルプレイヤーの所属チームIDを決定します。
        /// </summary>
        /// <param name="onlineService">
        /// プレイヤー情報を取得するオンラインサービスです。
        /// </param>
        /// <returns>
        /// 決定されたチームIDです。
        /// </returns>
        private static int ResolveLocalTeamId(
            IOnlineService onlineService)
        {
            if (onlineService == null)
            {
                return 0;
            }

            int maximumTeamCount = 1;

            if (GameFlowManager.Instance != null)
            {
                maximumTeamCount = Mathf.Max(
                    1,
                    GameFlowManager.Instance.MaximumTeamCount);
            }

            OnlinePlayer localPlayer =
                GetLocalOnlinePlayer(onlineService);

            string localPartyId = string.Empty;

            if (localPlayer != null)
            {
                localPlayer.TryGetProperty(
                    NetworkPropertyKeys.PartyId,
                    out localPartyId);
            }

            if (!string.IsNullOrEmpty(localPartyId))
            {
                int partyTeamId =
                    ResolveTeamIdFromPartyId(
                        onlineService,
                        localPartyId,
                        maximumTeamCount);

                if (partyTeamId >= 0)
                {
                    return partyTeamId;
                }
            }

            return ResolveTeamIdFromPlayerId(
                onlineService,
                maximumTeamCount);
        }

        /// <summary>
        /// パーティIDの並び順からローカルプレイヤーのチームIDを決定します。
        /// </summary>
        /// <param name="onlineService">
        /// プレイヤー情報を取得するオンラインサービスです。
        /// </param>
        /// <param name="localPartyId">
        /// ローカルプレイヤーのパーティIDです。
        /// </param>
        /// <param name="maximumTeamCount">
        /// 試合に参加できる最大チーム数です。
        /// </param>
        /// <returns>
        /// 決定できたチームIDです。
        /// 決定できない場合は<c>-1</c>です。
        /// </returns>
        private static int ResolveTeamIdFromPartyId(
            IOnlineService onlineService,
            string localPartyId,
            int maximumTeamCount)
        {
            List<string> partyIds = new();

            foreach (OnlinePlayer player in onlineService.Players)
            {
                if (player == null)
                {
                    continue;
                }

                if (!player.TryGetProperty(
                        NetworkPropertyKeys.PartyId,
                        out string partyId) ||
                    string.IsNullOrEmpty(partyId))
                {
                    continue;
                }

                if (!partyIds.Contains(partyId))
                {
                    partyIds.Add(partyId);
                }
            }

            partyIds.Sort(StringComparer.Ordinal);

            int partyIndex =
                partyIds.IndexOf(localPartyId);

            if (partyIndex < 0)
            {
                return -1;
            }

            return partyIndex % maximumTeamCount;
        }

        /// <summary>
        /// プレイヤーIDの並び順からローカルプレイヤーのチームIDを決定します。
        /// </summary>
        /// <param name="onlineService">
        /// プレイヤー情報を取得するオンラインサービスです。
        /// </param>
        /// <param name="maximumTeamCount">
        /// 試合に参加できる最大チーム数です。
        /// </param>
        /// <returns>
        /// 決定されたチームIDです。
        /// </returns>
        private static int ResolveTeamIdFromPlayerId(
            IOnlineService onlineService,
            int maximumTeamCount)
        {
            List<int> playerIds = new();

            foreach (OnlinePlayer player in onlineService.Players)
            {
                if (player != null)
                {
                    playerIds.Add(player.PlayerId);
                }
            }

            playerIds.Sort();

            int playerIndex =
                playerIds.IndexOf(
                    onlineService.LocalPlayerId);

            if (playerIndex >= 0)
            {
                return playerIndex % maximumTeamCount;
            }

            return Mathf.Abs(
                onlineService.LocalPlayerId) %
                maximumTeamCount;
        }

        /// <summary>
        /// 入力、兵装UI、ボードネットワークおよび
        /// ボード生成イベントを初期化します。
        /// </summary>
        /// <param name="cancellationToken">
        /// 初期化処理を中断するためのトークンです。
        /// </param>
        /// <returns>
        /// 初期化処理の完了を表すタスクです。
        /// </returns>
        /// <remarks>
        /// 状態終了によるキャンセルは正常な終了として扱います。
        /// </remarks>
        private async Task SetUpAsync(
                    CancellationToken cancellationToken)
        {
            try
            {
                await GameplayUiSceneLoader.EnsureLoadedAsync(cancellationToken);
                RegisterRoomPropertyEvent();
                SetLocalInGameReady();

                if (!TryGetRequiredManagers(
                        out PlayerInputContext inputContext,
                        out BoardManager boardManager))
                {
                    return;
                }

                PranamUIManager.Instance.Initialize(
                    new BattlePranamSelector(
                        inputContext,
                        () => PranaTactics.Settings.SettingsManager.Current.Controls));

                IBoardNetwork boardNetwork =
                    CreateBoardNetwork(boardManager);

                if (boardNetwork == null)
                {
                    Debug.LogError(
                        $"{nameof(StartPhaseState)}: " +
                        "ボードネットワークを生成できませんでした。");

                    return;
                }

                boardManager.Initialize(boardNetwork);

                boardManager.BoardCreated -=
                    HandleBoardCreated;

                boardManager.BoardCreated +=
                    HandleBoardCreated;

                if (!IsHostOrOffline())
                {
                    return;
                }

                await WaitUntilAllPlayersReadyForInGameAsync(
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                boardManager.RequestBoardCreation();
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    "ゲーム開始処理中にエラーが発生しました。");

                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// 初期化に必要なManagerと入力コンテキストを取得します。
        /// </summary>
        /// <param name="inputContext">
        /// 取得された入力コンテキストです。
        /// </param>
        /// <param name="boardManager">
        /// 取得されたBoardManagerです。
        /// </param>
        /// <returns>
        /// 必要なオブジェクトをすべて取得できた場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool TryGetRequiredManagers(
            out PlayerInputContext inputContext,
            out BoardManager boardManager)
        {
            inputContext = null;
            boardManager = null;

            if (InputManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    $"{nameof(InputManager)}が存在しません。");

                return false;
            }

            inputContext =
                InputManager.Instance.InputContext;

            if (inputContext == null)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    "プレイヤー入力コンテキストが初期化されていません。");

                return false;
            }

            if (PranamUIManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    $"{nameof(PranamUIManager)}が存在しません。");

                return false;
            }

            boardManager = BoardManager.Instance;

            if (boardManager == null)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    $"{nameof(BoardManager)}が存在しません。");

                return false;
            }

            return true;
        }

        /// <summary>
        /// 現在のオンライン環境に対応する
        /// ボードネットワーク実装を取得します。
        /// </summary>
        /// <param name="boardManager">
        /// ネットワークコンポーネントを保持するBoardManagerです。
        /// </param>
        /// <returns>
        /// 選択されたボードネットワーク実装です。
        /// </returns>
        private IBoardNetwork CreateBoardNetwork(
            BoardManager boardManager)
        {
            if (IsEosOnline())
            {
                Debug.Log(
                    $"{nameof(StartPhaseState)}: " +
                    $"{nameof(EosBoardNetwork)}を使用します。");

                return GetOrAddComponent<EosBoardNetwork>(
                    boardManager.gameObject);
            }

            IOnlineService onlineService =
                GetOnlineService();

            if (onlineService == null ||
                !onlineService.IsInRoom)
            {
                Debug.Log(
                    $"{nameof(StartPhaseState)}: " +
                    $"{nameof(LocalBoardNetwork)}を使用します。");

                return GetOrAddComponent<LocalBoardNetwork>(
                    boardManager.gameObject);
            }

            Debug.Log(
                $"{nameof(StartPhaseState)}: " +
                $"{nameof(PhotonBoardNetwork)}を使用します。");

            return GetOrAddComponent<PhotonBoardNetwork>(
                boardManager.gameObject);
        }

        /// <summary>
        /// 指定されたGameObjectからコンポーネントを取得し、
        /// 存在しない場合は新しく追加します。
        /// </summary>
        /// <typeparam name="T">
        /// 取得または追加するコンポーネントの型です。
        /// </typeparam>
        /// <param name="target">
        /// コンポーネントを保持するGameObjectです。
        /// </param>
        /// <returns>
        /// 取得または追加されたコンポーネントです。
        /// </returns>
        private static T GetOrAddComponent<T>(
            GameObject target)
            where T : Component
        {
            if (target.TryGetComponent(
                    out T component))
            {
                return component;
            }

            return target.AddComponent<T>();
        }

        /// <summary>
        /// ボードの生成が完了したとき、
        /// UnitManagerとユニット生成処理を初期化します。
        /// </summary>
        private void HandleBoardCreated()
        {
            UnregisterBoardCreatedEvent();

            if (_cancellationTokenSource == null ||
                _cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            _ = HandleBoardCreatedAsync(
                _cancellationTokenSource.Token);
        }

        /// <summary>
        /// UnitManagerを初期化し、
        /// 使用中のオンラインサービスに対応するユニットを生成します。
        /// </summary>
        /// <param name="cancellationToken">
        /// 初期化処理を中断するためのトークンです。
        /// </param>
        /// <returns>
        /// 初期化処理の完了を表すタスクです。
        /// </returns>
        /// <remarks>
        /// 状態終了によるキャンセルは正常な終了として扱います。
        /// </remarks>
        private async Task HandleBoardCreatedAsync(
                    CancellationToken cancellationToken)
        {
            try
            {
                if (UnitManager.Instance == null)
                {
                    Debug.LogError(
                        $"{nameof(StartPhaseState)}: " +
                        $"{nameof(UnitManager)}が存在しません。");

                    return;
                }

                IPlayerService playerService =
                    CreatePlayerService();

                IUnitFactory unitFactory =
                    CreateUnitFactory();

                if (playerService == null ||
                    unitFactory == null)
                {
                    Debug.LogError(
                        $"{nameof(StartPhaseState)}: " +
                        "プレイヤーまたはユニットのネットワーク実装を生成できません。");

                    return;
                }

                UnitManager.Instance.Initialize(
                    playerService,
                    unitFactory);

                _isUnitManagerInitialized = true;

                if (IsEosOnline())
                {
                    await SetUpEosUnitsAsync(
                        cancellationToken);

                    return;
                }

                await WaitUntilTeamIdsReadyAsync(
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                if (MatchManager.Instance == null)
                {
                    Debug.LogError(
                        $"{nameof(StartPhaseState)}: " +
                        $"{nameof(MatchManager)}が存在しません。");

                    return;
                }

                MatchManager.Instance.CachePlayerTeamIds();

                UnitManager.Instance.CreateLocalUnit();

                bool isLocalUnitReady =
                    await WaitUntilLocalUnitReadyAsync(
                        cancellationToken);

                if (!isLocalUnitReady)
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                CompleteInitialSetup();
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    "ユニット初期化中にエラーが発生しました。");

                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// 現在のオンライン環境に対応する
        /// プレイヤーサービスを生成します。
        /// </summary>
        /// <returns>
        /// 生成されたプレイヤーサービスです。
        /// </returns>
        private IPlayerService CreatePlayerService()
        {
            if (IsEosOnline())
            {
                Debug.Log(
                    $"{nameof(StartPhaseState)}: " +
                    $"{nameof(EosPlayerService)}を使用します。");

                return new EosPlayerService();
            }

            Debug.Log(
                $"{nameof(StartPhaseState)}: " +
                $"{nameof(PhotonPlayerService)}を使用します。");

            return new PhotonPlayerService();
        }

        /// <summary>
        /// 現在のオンライン環境に対応する
        /// ユニット生成処理を生成します。
        /// </summary>
        /// <returns>
        /// 生成されたユニットFactoryです。
        /// </returns>
        private IUnitFactory CreateUnitFactory()
        {
            if (IsEosOnline())
            {
                Debug.Log(
                    $"{nameof(StartPhaseState)}: " +
                    $"{nameof(EosUnitFactory)}を使用します。");

                return new EosUnitFactory();
            }

            Debug.Log(
                $"{nameof(StartPhaseState)}: " +
                $"{nameof(PhotonUnitFactory)}を使用します。");

            return new PhotonUnitFactory();
        }

        /// <summary>
        /// EOS環境で使用するユニット生成情報を共有し、
        /// ローカルユニットの生成完了を待機します。
        /// </summary>
        /// <param name="cancellationToken">
        /// 初期化処理を中断するためのトークンです。
        /// </param>
        /// <returns>
        /// EOSユニット初期化処理の完了を表すタスクです。
        /// </returns>
        private async Task SetUpEosUnitsAsync(
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(
                    _pendingEosUnitSpawnsJson))
            {
                ApplyEosUnitSpawnsJson(
                    _pendingEosUnitSpawnsJson,
                    "PendingBeforeUnitManagerInitialization");
            }

            TryApplyEosUnitSpawnsFromRoom(
                "InitialRoomSnapshot");

            if (IsHostOrOffline())
            {
                await CreateAndShareEosUnitSpawnsAsync(
                    cancellationToken);
            }

            bool areSpawnsApplied =
                await WaitUntilEosUnitSpawnsAppliedAsync(
                    cancellationToken);

            if (!areSpawnsApplied)
            {
                return;
            }

            bool isLocalUnitReady =
                await WaitUntilLocalUnitReadyAsync(
                    cancellationToken);

            if (!isLocalUnitReady)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    "EOSローカルユニットを取得できないため、" +
                    "フェイズ遷移を開始できません。");

                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            CompleteInitialSetup();
        }

        /// <summary>
        /// EOSホストとしてユニット生成情報を作成し、
        /// ルームプロパティへ保存します。
        /// </summary>
        /// <param name="cancellationToken">
        /// 処理を中断するためのトークンです。
        /// </param>
        /// <returns>
        /// ユニット生成情報の共有完了を表すタスクです。
        /// </returns>
        private Task CreateAndShareEosUnitSpawnsAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IOnlineService onlineService =
                GetOnlineService();

            if (onlineService == null)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    "EOSオンラインサービスを取得できません。");

                return Task.CompletedTask;
            }

            List<UnitInitializeData> spawnData =
                UnitManager.Instance
                    .BuildOnlinePlayerSpawnData();

            string json =
                JsonConvert.SerializeObject(spawnData);

            Debug.Log(
                $"{nameof(StartPhaseState)}: " +
                "EOSユニット生成情報をルームプロパティへ保存します。" +
                $"Count={spawnData.Count}");

            onlineService.SetRoomProperty(
                k_EosUnitSpawnsPropertyKey,
                json);

            ApplyEosUnitSpawnsJson(
                json,
                "HostCreate");

            return Task.CompletedTask;
        }

        /// <summary>
        /// ルームプロパティの変更時、
        /// EOS用ユニット生成情報を取得して適用します。
        /// </summary>
        /// <param name="changedProperties">
        /// 変更されたルームプロパティです。
        /// </param>
        private void HandleRoomPropertiesChanged(
            Dictionary<string, object> changedProperties)
        {
            if (!IsEosOnline() ||
                changedProperties == null)
            {
                return;
            }

            if (!TryGetPropertyIgnoreCase(
                    changedProperties,
                    k_EosUnitSpawnsPropertyKey,
                    out object rawSpawns))
            {
                return;
            }

            ApplyEosUnitSpawnsJson(
                rawSpawns?.ToString(),
                "RoomPropertiesChanged");
        }

        /// <summary>
        /// 現在のルームプロパティから
        /// EOS用ユニット生成情報を取得して適用します。
        /// </summary>
        /// <param name="reason">
        /// 適用処理を実行した理由です。
        /// </param>
        private void TryApplyEosUnitSpawnsFromRoom(
            string reason)
        {
            IOnlineService onlineService =
                GetOnlineService();

            if (onlineService == null ||
                onlineService.CurrentRoom == null)
            {
                return;
            }

            if (!TryGetPropertyIgnoreCase(
                    onlineService.CurrentRoom.Properties,
                    k_EosUnitSpawnsPropertyKey,
                    out object rawSpawns))
            {
                return;
            }

            ApplyEosUnitSpawnsJson(
                rawSpawns?.ToString(),
                reason);
        }

        /// <summary>
        /// JSON形式のEOSユニット生成情報を解析して適用します。
        /// </summary>
        /// <param name="json">
        /// ユニット生成情報を表すJSON文字列です。
        /// </param>
        /// <param name="reason">
        /// 適用処理を実行した理由です。
        /// </param>
        private void ApplyEosUnitSpawnsJson(
            string json,
            string reason)
        {
            if (_areEosUnitSpawnsApplied ||
                string.IsNullOrEmpty(json))
            {
                return;
            }

            if (!_isUnitManagerInitialized)
            {
                _pendingEosUnitSpawnsJson = json;

                Debug.Log(
                    $"{nameof(StartPhaseState)}: " +
                    "UnitManager初期化前のため、" +
                    "EOSユニット生成情報を保留します。" +
                    $"Reason={reason}");

                return;
            }

            List<UnitInitializeData> spawnData;

            try
            {
                spawnData =
                    JsonConvert.DeserializeObject<
                        List<UnitInitializeData>>(json);
            }
            catch (JsonException exception)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    "EOSユニット生成情報の解析に失敗しました。" +
                    $"Reason={reason}, Error={exception.Message}");

                return;
            }

            if (spawnData == null ||
                spawnData.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(StartPhaseState)}: " +
                    "EOSユニット生成情報が空です。" +
                    $"Reason={reason}");

                return;
            }

            if (MatchManager.Instance == null ||
                UnitManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    "ユニット生成に必要なManagerが存在しません。");

                return;
            }

            Debug.Log(
                $"{nameof(StartPhaseState)}: " +
                "EOSユニット生成情報を適用します。" +
                $"Reason={reason}, Count={spawnData.Count}");

            MatchManager.Instance
                .CachePlayerTeamIdsFromSpawns(
                    spawnData);

            UnitManager.Instance
                .CreateUnits(
                    spawnData);

            _pendingEosUnitSpawnsJson = null;
            _areEosUnitSpawnsApplied = true;
        }

        /// <summary>
        /// ローカルユニットが生成されるまで待機します。
        /// </summary>
        /// <param name="cancellationToken">
        /// 待機処理を中断するためのトークンです。
        /// </param>
        /// <returns>
        /// ローカルユニットが生成された場合は<c>true</c>、
        /// タイムアウトした場合は<c>false</c>です。
        /// </returns>
        private static async Task<bool>
            WaitUntilLocalUnitReadyAsync(
                CancellationToken cancellationToken)
        {
            float elapsedTime = 0.0f;

            while (UnitManager.Instance != null &&
                UnitManager.Instance.LocalUnit == null)
            {
                await Task.Delay(
                    k_LocalUnitPollingIntervalMilliseconds,
                    cancellationToken);

                elapsedTime +=
                    k_LocalUnitPollingIntervalMilliseconds /
                    1000.0f;

                if (elapsedTime <
                    k_LocalUnitTimeoutSeconds)
                {
                    continue;
                }

                Debug.LogWarning(
                    $"{nameof(StartPhaseState)}: " +
                    "ローカルユニットの生成待機がタイムアウトしました。");

                return false;
            }

            return UnitManager.Instance != null &&
                UnitManager.Instance.LocalUnit != null;
        }

        /// <summary>
        /// EOS用ユニット生成情報が適用されるまで待機します。
        /// </summary>
        /// <param name="cancellationToken">
        /// 待機処理を中断するためのトークンです。
        /// </param>
        /// <returns>
        /// 生成情報が適用された場合は<c>true</c>、
        /// タイムアウトした場合は<c>false</c>です。
        /// </returns>
        private async Task<bool>
            WaitUntilEosUnitSpawnsAppliedAsync(
                CancellationToken cancellationToken)
        {
            float elapsedTime = 0.0f;

            while (!_areEosUnitSpawnsApplied)
            {
                TryApplyEosUnitSpawnsFromRoom(
                    "WaitPoll");

                if (_areEosUnitSpawnsApplied)
                {
                    return true;
                }

                await Task.Delay(
                    k_OnlinePollingIntervalMilliseconds,
                    cancellationToken);

                elapsedTime +=
                    k_OnlinePollingIntervalMilliseconds /
                    1000.0f;

                if (elapsedTime <
                    k_EosUnitSpawnTimeoutSeconds)
                {
                    continue;
                }

                Debug.LogWarning(
                    $"{nameof(StartPhaseState)}: " +
                    "EOSユニット生成情報の待機がタイムアウトしました。");

                return false;
            }

            return true;
        }

        /// <summary>
        /// 全プレイヤーがゲームプレイシーンの準備を完了するまで待機します。
        /// </summary>
        /// <param name="cancellationToken">
        /// 待機処理を中断するためのトークンです。
        /// </param>
        /// <returns>
        /// 準備待機の完了を表すタスクです。
        /// </returns>
        private async Task
            WaitUntilAllPlayersReadyForInGameAsync(
                CancellationToken cancellationToken)
        {
            float elapsedTime = 0.0f;

            while (!AreAllPlayersReadyForInGame(
                    ShouldLogWaitDetails()))
            {
                await Task.Delay(
                    k_OnlinePollingIntervalMilliseconds,
                    cancellationToken);

                elapsedTime +=
                    k_OnlinePollingIntervalMilliseconds /
                    1000.0f;

                if (elapsedTime <
                    k_PlayerReadyTimeoutSeconds)
                {
                    continue;
                }

                IOnlineService onlineService =
                    GetOnlineService();

                int currentPlayerCount =
                    onlineService?.Players?.Count ?? 0;

                Debug.LogWarning(
                    $"{nameof(StartPhaseState)}: " +
                    "全プレイヤーのゲームシーン準備待機が" +
                    "タイムアウトしました。" +
                    $"CurrentPlayerCount={currentPlayerCount}");

                return;
            }
        }

        /// <summary>
        /// 全プレイヤーがゲームプレイシーンの準備を
        /// 完了しているかどうかを確認します。
        /// </summary>
        /// <param name="shouldLogWaitDetails">
        /// 準備未完了の理由をログへ出力するかどうかを指定します。
        /// </param>
        /// <returns>
        /// 全プレイヤーの準備が完了している場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool AreAllPlayersReadyForInGame(
            bool shouldLogWaitDetails)
        {
            IOnlineService onlineService =
                GetOnlineService();

            if (onlineService == null ||
                !onlineService.IsInRoom)
            {
                return true;
            }

            if (onlineService.CurrentRoom == null)
            {
                return false;
            }

            int expectedPlayerCount =
                onlineService.CurrentRoom.MaxPlayers;

            if (onlineService.Players.Count <
                expectedPlayerCount)
            {
                if (shouldLogWaitDetails)
                {
                    Debug.Log(
                        $"{nameof(StartPhaseState)}: " +
                        "プレイヤーの参加を待機しています。" +
                        $"Current={onlineService.Players.Count}, " +
                        $"Expected={expectedPlayerCount}");
                }

                return false;
            }

            foreach (OnlinePlayer player in onlineService.Players)
            {
                if (player == null)
                {
                    return false;
                }

                if (!player.TryGetProperty(
                        NetworkPropertyKeys.TeamId,
                        out int _))
                {
                    if (shouldLogWaitDetails)
                    {
                        Debug.Log(
                            $"{nameof(StartPhaseState)}: " +
                            "TeamIdの同期を待機しています。" +
                            $"PlayerId={player.PlayerId}");
                    }

                    return false;
                }

                if (!TryGetInGameSceneReady(
                        player,
                        out bool isReady))
                {
                    if (shouldLogWaitDetails)
                    {
                        Debug.Log(
                            $"{nameof(StartPhaseState)}: " +
                            "ゲームシーン準備状態の同期を待機しています。" +
                            $"PlayerId={player.PlayerId}");
                    }

                    return false;
                }

                if (!isReady)
                {
                    if (shouldLogWaitDetails)
                    {
                        Debug.Log(
                            $"{nameof(StartPhaseState)}: " +
                            "ゲームシーンの準備が完了していません。" +
                            $"PlayerId={player.PlayerId}");
                    }

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// プレイヤーからゲームプレイシーンの準備状態を取得します。
        /// </summary>
        /// <param name="player">
        /// 準備状態を確認するプレイヤーです。
        /// </param>
        /// <param name="isReady">
        /// 取得された準備状態です。
        /// </param>
        /// <returns>
        /// 準備状態を取得できた場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool TryGetInGameSceneReady(
            OnlinePlayer player,
            out bool isReady)
        {
            isReady = false;

            if (player == null)
            {
                return false;
            }

            if (player.TryGetProperty(
                    k_InGameSceneReadyPropertyKey,
                    out isReady))
            {
                return true;
            }

            if (player.TryGetProperty(
                    k_InGameSceneReadyPropertyKey
                        .ToUpperInvariant(),
                    out isReady))
            {
                return true;
            }

            return player.TryGetProperty(
                k_InGameSceneReadyPropertyKey
                    .ToLowerInvariant(),
                out isReady);
        }

        /// <summary>
        /// 全プレイヤーのTeamIdが同期されるまで待機します。
        /// </summary>
        /// <param name="cancellationToken">
        /// 待機処理を中断するためのトークンです。
        /// </param>
        /// <returns>
        /// TeamIdの同期待機完了を表すタスクです。
        /// </returns>
        private async Task WaitUntilTeamIdsReadyAsync(
            CancellationToken cancellationToken)
        {
            float elapsedTime = 0.0f;

            while (!AreAllTeamIdsReady(
                    ShouldLogWaitDetails()))
            {
                await Task.Delay(
                    k_OnlinePollingIntervalMilliseconds,
                    cancellationToken);

                elapsedTime +=
                    k_OnlinePollingIntervalMilliseconds /
                    1000.0f;

                if (elapsedTime <
                    k_TeamIdTimeoutSeconds)
                {
                    continue;
                }

                Debug.LogWarning(
                    $"{nameof(StartPhaseState)}: " +
                    "TeamIdの同期待機がタイムアウトしました。");

                return;
            }
        }

        /// <summary>
        /// 全プレイヤーのTeamIdが同期されているか確認します。
        /// </summary>
        /// <param name="shouldLogWaitDetails">
        /// 未同期のプレイヤーをログへ出力するかどうかを指定します。
        /// </param>
        /// <returns>
        /// 全プレイヤーのTeamIdが同期済みの場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool AreAllTeamIdsReady(
            bool shouldLogWaitDetails)
        {
            IOnlineService onlineService =
                GetOnlineService();

            if (onlineService == null ||
                !onlineService.IsInRoom)
            {
                return true;
            }

            foreach (OnlinePlayer player in onlineService.Players)
            {
                if (player == null)
                {
                    return false;
                }

                if (player.TryGetProperty(
                        NetworkPropertyKeys.TeamId,
                        out int _))
                {
                    continue;
                }

                if (shouldLogWaitDetails)
                {
                    Debug.Log(
                        $"{nameof(StartPhaseState)}: " +
                        "TeamIdの同期を待機しています。" +
                        $"PlayerId={player.PlayerId}");
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// 指定された辞書から、大文字と小文字を区別せずに
        /// プロパティを取得します。
        /// </summary>
        /// <param name="properties">
        /// 検索するプロパティ一覧です。
        /// </param>
        /// <param name="key">
        /// 検索するキーです。
        /// </param>
        /// <param name="value">
        /// 取得された値です。
        /// </param>
        /// <returns>
        /// プロパティが存在する場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool TryGetPropertyIgnoreCase(
            IReadOnlyDictionary<string, object> properties,
            string key,
            out object value)
        {
            value = null;

            if (properties == null ||
                string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (properties.TryGetValue(
                    key,
                    out value))
            {
                return true;
            }

            foreach (
                KeyValuePair<string, object> property
                in properties)
            {
                if (!string.Equals(
                        property.Key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = property.Value;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 現在のオンラインサービスを取得します。
        /// </summary>
        /// <returns>
        /// 登録されているオンラインサービスです。
        /// 未登録の場合は<c>null</c>です。
        /// </returns>
        private IOnlineService GetOnlineService()
        {
            if (_onlineService == null)
            {
                _onlineService =
                    NetworkServiceLocator.Online;
            }

            return _onlineService;
        }

        /// <summary>
        /// 現在の環境でローカル側が初期生成を担当するか確認します。
        /// </summary>
        /// <returns>
        /// オフラインまたはホストの場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool IsHostOrOffline()
        {
            IOnlineService onlineService =
                GetOnlineService();

            if (onlineService == null ||
                !onlineService.IsInRoom)
            {
                return true;
            }

            return onlineService.IsHost;
        }

        /// <summary>
        /// 現在のオンラインサービスがEOS実装かどうかを確認します。
        /// </summary>
        /// <returns>
        /// EOS実装の場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool IsEosOnline()
        {
            IOnlineService onlineService =
                GetOnlineService();

            if (onlineService == null)
            {
                return false;
            }

            return onlineService
                    .GetType()
                    .Name
                    .IndexOf(
                        k_EosServiceTypeName,
                        StringComparison.OrdinalIgnoreCase) >=
                0;
        }

        /// <summary>
        /// オンラインサービスからローカルプレイヤーを取得します。
        /// </summary>
        /// <param name="onlineService">
        /// プレイヤー情報を取得するオンラインサービスです。
        /// </param>
        /// <returns>
        /// ローカルプレイヤーです。
        /// 見つからない場合は<c>null</c>です。
        /// </returns>
        private static OnlinePlayer GetLocalOnlinePlayer(
            IOnlineService onlineService)
        {
            if (onlineService == null)
            {
                return null;
            }

            foreach (OnlinePlayer player in onlineService.Players)
            {
                if (player != null &&
                    player.PlayerId ==
                    onlineService.LocalPlayerId)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>
        /// 待機状態の詳細ログを出力するタイミングかどうかを判定します。
        /// </summary>
        /// <returns>
        /// ログを出力する場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool ShouldLogWaitDetails()
        {
            if (Time.realtimeSinceStartup <
                _nextWaitLogTime)
            {
                return false;
            }

            _nextWaitLogTime =
                Time.realtimeSinceStartup +
                k_WaitLogIntervalSeconds;

            return true;
        }

        /// <summary>
        /// ローカル表示を更新し、
        /// MatchManagerへゲーム開始初期化の完了を通知します。
        /// </summary>
        private void CompleteInitialSetup()
        {
            if (UnitManager.Instance == null ||
                MatchManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(StartPhaseState)}: " +
                    "初期化完了処理に必要なManagerが存在しません。");

                return;
            }

            UnitManager.Instance.RefreshLocalPresentation();
            MatchManager.Instance.NotifyInitialSetupComplete();

            _isSetupComplete = true;
        }
    }
}
