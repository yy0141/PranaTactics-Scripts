using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using PranaTactics.AI.Solo;
using PranaTactics.Board;
using PranaTactics.Board.Networking.Local;
using PranaTactics.Controls;
using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.Match.Solo;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;
using PranaTactics.Unit.Networking.Local;

namespace PranaTactics.Match.Phases
{
    /// <summary>
    /// ソロプレイ開始時に、
    /// JSONで指定された各チームのUnitを生成し、
    /// ローカル環境の対戦を初期化します。
    /// </summary>
    /// <remarks>
    /// PhotonおよびEOSは使用しません。
    /// ボードには<see cref="LocalBoardNetwork"/>、
    /// ユニット生成には<see cref="LocalUnitFactory"/>を使用します。
    /// </remarks>
    public sealed class SoloStartPhaseState :
        MatchPhaseState
    {
        /// <summary>
        /// ローカルプレイヤーのIDです。
        /// </summary>
        private const int k_LocalPlayerId = 0;

        /// <summary>
        /// AIの移動を有効にします。
        /// </summary>
        private const bool k_IsAiMovementEnabled =
            true;

        /// <summary>
        /// AIの兵装使用を有効にします。
        /// </summary>
        private const bool k_IsAiWeaponUsageEnabled =
            true;

        /// <summary>
        /// Pranam初期化待機の最大時間です。
        /// </summary>
        private const float
            k_PranamInitializationTimeoutSeconds =
                5.0f;

        /// <summary>
        /// 初期化処理のキャンセルに使用します。
        /// </summary>
        private CancellationTokenSource
            _cancellationTokenSource;

        /// <summary>
        /// ソロ対戦の初期化が完了したかどうかです。
        /// </summary>
        private bool _isSetupComplete;

        /// <summary>
        /// Macro Phaseへの遷移要求済みかどうかです。
        /// </summary>
        private bool _hasRequestedMacroPhase;

        /// <summary>
        /// ソロ対戦の初期化を開始します。
        /// </summary>
        public override void Enter()
        {
            _isSetupComplete =
                false;

            _hasRequestedMacroPhase =
                false;

            CancelPendingOperations();

            _cancellationTokenSource =
                new CancellationTokenSource();

            _ = SetUpAsync(
                _cancellationTokenSource.Token);
        }

        /// <summary>
        /// 初期化完了後に通常のMacro Phaseへ遷移します。
        /// </summary>
        public override void UpdateState()
        {
            if (!_isSetupComplete ||
                _hasRequestedMacroPhase)
            {
                return;
            }

            MatchManager matchManager =
                MatchManager.Instance;

            if (matchManager == null)
            {
                Debug.LogError(
                    $"{nameof(SoloStartPhaseState)}: " +
                    $"{nameof(MatchManager)}が存在しません。");

                return;
            }

            _hasRequestedMacroPhase =
                true;

            matchManager.ChangeState(
                new MacroPhaseState());
        }

        /// <summary>
        /// イベント購読と非同期処理を終了します。
        /// </summary>
        public override void Exit()
        {
            UnsubscribeBoardCreatedEvent();
            CancelPendingOperations();
        }

        /// <summary>
        /// プレイヤー入力UIとローカルボードを初期化します。
        /// </summary>
        private async Awaitable SetUpAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                await GameplayUiSceneLoader.EnsureLoadedAsync(cancellationToken);

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

                LocalBoardNetwork boardNetwork =
                    GetOrAddLocalBoardNetwork(
                        boardManager);

                boardManager.Initialize(
                    boardNetwork);

                boardManager.BoardCreated -=
                    HandleBoardCreated;

                boardManager.BoardCreated +=
                    HandleBoardCreated;

                await Awaitable.NextFrameAsync(
                    cancellationToken);

                cancellationToken
                    .ThrowIfCancellationRequested();

                boardManager.RequestBoardCreation();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(SoloStartPhaseState)}: " +
                    "ソロ用ボードの初期化中に" +
                    "エラーが発生しました。");

                Debug.LogException(
                    exception);
            }
        }

        /// <summary>
        /// ボード生成後に設定人数分のUnitを生成します。
        /// </summary>
        private void HandleBoardCreated()
        {
            UnsubscribeBoardCreatedEvent();

            if (_cancellationTokenSource == null ||
                _cancellationTokenSource
                    .IsCancellationRequested)
            {
                return;
            }

            _ = InitializeUnitsAsync(
                _cancellationTokenSource.Token);
        }

        /// <summary>
        /// JSONで指定された各チームのUnitを生成し、
        /// ローカルプレイヤー以外のAI制御を有効化します。
        /// </summary>
        private async Awaitable InitializeUnitsAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                BoardManager boardManager =
                    BoardManager.Instance;

                UnitManager unitManager =
                    UnitManager.Instance;

                MatchManager matchManager =
                    MatchManager.Instance;

                if (boardManager == null ||
                    unitManager == null ||
                    matchManager == null)
                {
                    Debug.LogError(
                        $"{nameof(SoloStartPhaseState)}: " +
                        "ソロ対戦初期化に必要なManagerが" +
                        "存在しません。");

                    return;
                }

                string localCharacterName =
                    ResolveLocalCharacterName();

                if (string.IsNullOrWhiteSpace(
                        localCharacterName))
                {
                    Debug.LogError(
                        $"{nameof(SoloStartPhaseState)}: " +
                        "ソロプレイ用キャラクターが" +
                        "選択されていません。");

                    return;
                }

                List<PlayerInfo> players;
#if UNITY_EDITOR || PV_CAPTURE
                PranaTactics.Promo.PvGameplayScenario capture = matchManager.CaptureScenario;

                if (capture != null)
                {
                    players = PranaTactics.Promo.PvGameplaySpawnPlan.CreatePlayers(capture, boardManager);
                }
                else
#endif
                {
                    players = CreatePlayerInformation(localCharacterName);
                }

                LocalPlayerService playerService =
                    new LocalPlayerService();

                playerService.Initialize(
                    k_LocalPlayerId,
                    players,
                    false);

                unitManager.Initialize(
                    playerService,
                    new LocalUnitFactory());

#if UNITY_EDITOR || PV_CAPTURE
                if (capture != null)
                {
                    PranaTactics.Promo.PvGameplaySpawnPlan.CreateUnits(capture, unitManager);
                }
                else
#endif
                {
                    unitManager.CreateUnits(players);
                }

                foreach (PlayerInfo player in
                    players)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    if (!unitManager.TryGetUnit(
                            player.PlayerId,
                            out UnitBehavior unitBehavior))
                    {
                        Debug.LogError(
                            $"{nameof(SoloStartPhaseState)}: " +
                            "生成したUnitを取得できません。" +
                            $" PlayerId={player.PlayerId}");

                        return;
                    }

                    await WaitForPranamInitializationAsync(
                        unitBehavior,
                        cancellationToken);
                }

                foreach (PlayerInfo player in
                    players)
                {
                    if (player == null ||
                        player.PlayerId ==
                        k_LocalPlayerId)
                    {
                        continue;
                    }

#if UNITY_EDITOR || PV_CAPTURE
                    if (capture != null && !capture.Actors[player.PlayerId].UseAi)
                    {
                        continue;
                    }

#endif
                    bool enabled =
                        unitManager.EnableAI(
                            player.PlayerId,
                            k_IsAiMovementEnabled,
                            k_IsAiWeaponUsageEnabled);

                    if (!enabled)
                    {
                        Debug.LogError(
                            $"{nameof(SoloStartPhaseState)}: " +
                            "AIの有効化に失敗しました。" +
                            $" PlayerId={player.PlayerId}");

                        return;
                    }
                }

                SoloAiDirector.EnsureExists();

                unitManager
                    .RefreshLocalPresentation();

                matchManager
                    .NotifyInitialSetupComplete();

                _isSetupComplete =
                    true;

                Debug.Log(
                    $"{nameof(SoloStartPhaseState)}: " +
                    $"{players.Count}人の" +
                    "ソロ対戦初期化が完了しました。");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(SoloStartPhaseState)}: " +
                    "ソロUnit初期化中に" +
                    "エラーが発生しました。");

                Debug.LogException(
                    exception);
            }
        }

        /// <summary>
        /// SoloCharacterSelectUIManagerが保存した
        /// キャラクター名を取得します。
        /// </summary>
        private static string ResolveLocalCharacterName()
        {
            if (!SoloCharacterSelectionContext
                    .HasSelection)
            {
                return string.Empty;
            }

            string characterName =
                SoloCharacterSelectionContext
                    .CharacterName;

            if (string.IsNullOrWhiteSpace(
                    characterName))
            {
                return string.Empty;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null ||
                repository.GetCharacterData(
                    characterName) == null)
            {
                return string.Empty;
            }

            return characterName.Trim();
        }

        /// <summary>
        /// JSONのチーム編成を検証し、プレイヤーと各チームのAI情報を生成します。
        /// </summary>
        private static List<PlayerInfo> CreatePlayerInformation(string localCharacterName)
        {
            SoloBattleConfiguration configuration =
                GameFlowManager.Instance.SoloBattleConfiguration;
            GameDataRepository repository = GameDataRepository.Instance;
            if (repository == null)
            {
                throw new InvalidOperationException("キャラクターのデータを取得できません。");
            }

            configuration.ValidateCharacters(name => repository.GetCharacterData(name) != null);
            var players = new List<PlayerInfo>(configuration.TotalPlayerCount);
            foreach (SoloBattleParticipant participant in configuration.CreateParticipants(localCharacterName))
            {
                players.Add(new PlayerInfo
                {
                    PlayerId = participant.PlayerId,
                    TeamId = participant.TeamId,
                    CharacterName = participant.CharacterName,
                    InitialStats = new UnitInitialStats
                    {
                        HasMoveSpeed = participant.Stats.MoveSpeed.HasValue,
                        MoveSpeed = participant.Stats.MoveSpeed.GetValueOrDefault(),
                        HasPranaCapacity = participant.Stats.PranaCapacity.HasValue,
                        PranaCapacity = participant.Stats.PranaCapacity.GetValueOrDefault(),
                        HasActionPoint = participant.Stats.ActionPoint.HasValue,
                        ActionPoint = participant.Stats.ActionPoint.GetValueOrDefault()
                    },
                    Coordinate = Vector2Int.zero,
                    Rotation = Vector3.zero
                });
            }

            return players;
        }

        /// <summary>
        /// Pranamと扇形Colliderの初期化完了を待ちます。
        /// </summary>
        private static async Awaitable
            WaitForPranamInitializationAsync(
                UnitBehavior unitBehavior,
                CancellationToken cancellationToken)
        {
            TacticalMapUnitBehavior tacticalMapUnit =
                unitBehavior?
                    .GetTacticalMapUnitBehavior();

            if (tacticalMapUnit == null)
            {
                throw new InvalidOperationException(
                    "TacticalMapUnitBehaviorを" +
                    "取得できません。");
            }

            float timeoutAt =
                Time.realtimeSinceStartup +
                k_PranamInitializationTimeoutSeconds;

            while (!tacticalMapUnit
                .IsPranamInitialized)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                if (tacticalMapUnit
                    .HasPranamInitializationFailed)
                {
                    throw new InvalidOperationException(
                        "Pranam初期化に失敗しました。" +
                        $" PlayerId={unitBehavior.PlayerId}");
                }

                if (Time.realtimeSinceStartup >=
                    timeoutAt)
                {
                    throw new TimeoutException(
                        "Pranam初期化待機が" +
                        "タイムアウトしました。" +
                        $" PlayerId={unitBehavior.PlayerId}");
                }

                await Awaitable.NextFrameAsync(
                    cancellationToken);
            }
        }

        /// <summary>
        /// 初期化に必要なManagerを取得します。
        /// </summary>
        private static bool TryGetRequiredManagers(
            out PlayerInputContext inputContext,
            out BoardManager boardManager)
        {
            inputContext =
                null;

            boardManager =
                null;

            if (InputManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(SoloStartPhaseState)}: " +
                    $"{nameof(InputManager)}が存在しません。");

                return false;
            }

            inputContext =
                InputManager.Instance.InputContext;

            if (inputContext == null)
            {
                Debug.LogError(
                    $"{nameof(SoloStartPhaseState)}: " +
                    "PlayerInputContextが" +
                    "初期化されていません。");

                return false;
            }

            if (PranamUIManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(SoloStartPhaseState)}: " +
                    $"{nameof(PranamUIManager)}が存在しません。");

                return false;
            }

            boardManager =
                BoardManager.Instance;

            if (boardManager == null)
            {
                Debug.LogError(
                    $"{nameof(SoloStartPhaseState)}: " +
                    $"{nameof(BoardManager)}が存在しません。");

                return false;
            }

            if (UnitManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(SoloStartPhaseState)}: " +
                    $"{nameof(UnitManager)}が存在しません。");

                return false;
            }

            if (MatchManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(SoloStartPhaseState)}: " +
                    $"{nameof(MatchManager)}が存在しません。");

                return false;
            }

            return true;
        }

        /// <summary>
        /// LocalBoardNetworkを取得または追加します。
        /// </summary>
        private static LocalBoardNetwork
            GetOrAddLocalBoardNetwork(
                BoardManager boardManager)
        {
            if (boardManager.TryGetComponent(
                    out LocalBoardNetwork network))
            {
                return network;
            }

            return boardManager.gameObject
                .AddComponent<LocalBoardNetwork>();
        }

        /// <summary>
        /// BoardCreatedイベントを解除します。
        /// </summary>
        private void UnsubscribeBoardCreatedEvent()
        {
            if (BoardManager.Instance == null)
            {
                return;
            }

            BoardManager.Instance.BoardCreated -=
                HandleBoardCreated;
        }

        /// <summary>
        /// 実行中の非同期処理を終了します。
        /// </summary>
        private void CancelPendingOperations()
        {
            if (_cancellationTokenSource == null)
            {
                return;
            }

            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            _cancellationTokenSource =
                null;
        }
    }
}
