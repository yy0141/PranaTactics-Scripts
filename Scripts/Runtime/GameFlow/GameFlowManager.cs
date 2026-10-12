using System;
using UnityEngine;

using PranaTactics.GameFlow.States;
using PranaTactics.Match.Modes;
using PranaTactics.Match.Solo;
using PranaTactics.Match.Solo.Stages;
using PranaTactics.Networking;
using PranaTactics.Settings;
using PranaTactics.UI.Screens;

namespace PranaTactics.GameFlow
{
    /// <summary>
    /// ゲーム全体の状態遷移、試合状態および
    /// リザルト表示に必要な情報を管理します。
    /// </summary>
    /// <remarks>
    /// <para>同じルート上のPhotonView/EOSより先に、永続Managerの重複を除去します。</para>
    /// <para>現在有効なGameFlowManagerのインスタンスを取得します。</para>
    /// <para>兵装のデバッグ機能が有効かどうかを取得します。</para>
    /// <para>現在ゲームプレイ中であるかどうかを取得します。</para>
    /// <para>現在のゲーム時間が終了しているかどうかを取得します。</para>
    /// <para>1試合に参加できる最大チーム数を取得します。</para>
    /// <para>1チームに参加できる最大プレイヤー数を取得します。</para>
    /// <para>ソロプレイ時の1チームあたりUnit数を取得します。</para>
    /// <para>パーティを組んだ状態でゲームを開始したかどうかを取得します。</para>
    /// <para>現在ソロプレイが選択されているかどうかを取得します。</para>
    /// <para>現在オンライン対戦が選択されているかどうかを取得します。</para>
    /// <para>現在有効なゲーム状態を取得します。</para>
    /// </remarks>
    [DefaultExecutionOrder(-32000)]
    public sealed class GameFlowManager : MonoBehaviour
    {
        /// <summary>
        /// チーム順位が未設定であることを示す値です。
        /// </summary>
        private const int k_UnsetTeamRank = 0;

        /// <summary>
        /// 勝利したチームの順位です。
        /// </summary>
        private const int k_WinningTeamRank = 1;

        /// <summary>
        /// 兵装のデバッグ機能を有効にするかどうかを指定します。
        /// </summary>
        [SerializeField]
        private bool _isWeaponDebugEnabled;

        /// <summary>
        /// ゲームの状態遷移と現在の状態を管理します。
        /// </summary>
        private GameStateMachine _stateMachine;

        /// <summary>
        /// 現在のゲーム時間が終了しているかどうかを示します。
        /// </summary>
        private bool _isTimeEnded;

        /// <summary>
        /// ローカルプレイヤーが所属するチームの順位です。
        /// </summary>
        private int _localTeamRank;

        /// <summary>
        /// 1試合に参加できる最大チーム数です。
        /// </summary>
        private int _maximumTeamCount;

        /// <summary>
        /// 1チームに参加できる最大プレイヤー数です。
        /// </summary>
        private int _maximumPlayersPerTeam;

        /// <summary>
        /// パーティを組んだ状態でゲームを開始したかどうかを示します。
        /// </summary>
        private bool _isPartyPlay;

        /// <summary>
        /// 現在ゲームプレイ中であるかどうかを示します。
        /// </summary>
        private bool _isGameplay;

        /// <summary>
        /// 現在ソロプレイが選択されているかどうかを示します。
        /// </summary>
        private bool _isSoloPlay;

        /// <summary>
        /// 接続方式から独立して選択中のゲームルールを保持します。
        /// </summary>
        private string _gameModeId = GameModeRegistry.k_DefaultModeId;

        /// <summary>
        /// 現在のソロ試合で共有するJSON編成です。
        /// </summary>
        private SoloBattleConfiguration _soloBattleConfiguration;

        private SoloStageSession _soloStages;

        public SoloStageSession SoloStages => _soloStages ??= new SoloStageSession(
            SoloStageCatalog.Load(), new PlayerPrefsSoloStageProgressStore());

        public bool HasSelectedSoloStage => IsSoloPlay &&
            GameModeId == GameModeRegistry.k_DefaultModeId && _soloStages?.SelectedStage != null;

        public SoloBattleConfiguration SoloBattleConfiguration =>
            _soloBattleConfiguration ??= SoloBattleConfigurationLoader.Load();

        public static GameFlowManager Instance
        {
            get; private set;
        }

        public bool IsWeaponDebugEnabled
        {
            get
            {
                return _isWeaponDebugEnabled;
            }
        }

        public bool IsGameplay
        {
            get
            {
                return _isGameplay;
            }
        }

        public bool IsTimeEnded
        {
            get
            {
                return _isTimeEnded;
            }
        }

        public int MaximumTeamCount
        {
            get
            {
                return _maximumTeamCount;
            }
        }

        public int MaximumPlayersPerTeam
        {
            get
            {
                return _maximumPlayersPerTeam;
            }
        }

#if UNITY_EDITOR || PV_CAPTURE
        /// <summary>
        /// PV撮影用の人数設定を、通常のJSON編成とは独立して設定します。
        /// </summary>
        public void SetCaptureTeamSize(int playersPerTeam)
        {
            _maximumTeamCount = 2;
            _maximumPlayersPerTeam = Mathf.Max(1, playersPerTeam);
        }
#endif

        public bool IsPartyPlay
        {
            get
            {
                return _isPartyPlay;
            }
        }

        public string GameModeId => _gameModeId;

        public PlayCategory PlayCategory => IsSoloPlay ? PlayCategory.Local : PlayCategory.Online;

        public bool IsSoloPlay
        {
            get
            {
                return _isSoloPlay;
            }
        }

        public bool IsOnlinePlay
        {
            get
            {
                return !_isSoloPlay;
            }
        }

        public GameState CurrentState
        {
            get
            {
                return _stateMachine?.CurrentState;
            }
        }

        /// <summary>
        /// シングルトンインスタンスを設定し、
        /// 保存されているゲーム設定を読み込みます。
        /// </summary>
        /// <remarks>
        /// <para>Destroyはフレーム末尾まで遅延するため、他コンポーネントのAwakeも止めます。</para>
        /// </remarks>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);

                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SettingsManager.Load();
            SetSoloPlayState(SettingsManager.Current.General.IsSoloPlay);
            string savedModeId = SettingsManager.Current.General.GameModeId;
            if (GameModeRegistry.Catalog.TryGet(savedModeId, PlayCategory, out GameMode savedMode))
            {
                _gameModeId = savedMode.Id;
            }
        }

        /// <summary>
        /// ゲームステートマシンとマッチング設定を初期化します。
        /// </summary>
        private async void Start()
        {
#if UNITY_EDITOR || PV_CAPTURE
            if (_isStartingInCaptureScene)
            {
                return;
            }

#endif
            InitializeMatchSettings();
            _stateMachine = new GameStateMachine(new TitleState());
            try
            {
                await SettingsUiBootstrap.EnsureLoadedAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError("設定UIの先行読み込みに失敗しました。ゲームの進行は継続します。", this);
                Debug.LogException(exception);
            }
        }

#if UNITY_EDITOR || PV_CAPTURE
        private bool _isStartingInCaptureScene;

        /// <summary>
        /// 撮影シーンから通常試合を開始するとき、タイトルへの初回遷移だけを省略します。
        /// </summary>
        public void StartInCaptureScene()
        {
            _isStartingInCaptureScene = true;
        }

#endif

        /// <summary>
        /// 現在のゲーム状態を更新し、
        /// 現在の状態に応じた処理を実行します。
        /// </summary>
        private void Update()
        {
            _stateMachine?.UpdateState();
        }

        /// <summary>
        /// このインスタンスが破棄されたとき、
        /// シングルトンに保持されている参照を解除します。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 現在のモードに対応する最大チーム数と1チームあたりの人数を設定します。
        /// </summary>
        private void InitializeMatchSettings()
        {
            if (IsSoloPlay)
            {
                _maximumTeamCount = SoloBattleConfiguration.Teams.Count;
                _maximumPlayersPerTeam = SoloBattleConfiguration.MaximumTeamSize;
                return;
            }

            if (MatchmakingManager.Instance == null)
            {
                Debug.LogWarning(
                    $"{nameof(GameFlowManager)}: " + $"{nameof(MatchmakingManager)}が存在しないため、" +
                    "チーム設定を取得できません。",
                    this);
                _maximumTeamCount = 0;
                _maximumPlayersPerTeam = 0;

                return;
            }

            _maximumTeamCount = MatchmakingManager.Instance.GetPartyCount();
            _maximumPlayersPerTeam = MatchmakingManager.Instance.GetPartySize();
        }

        /// <summary>
        /// ゲームプレイ中であるかどうかを設定します。
        /// </summary>
        public void SetGameplayState(bool isGameplay)
        {
            _isGameplay = isGameplay;
        }

        /// <summary>
        /// ローカルプレイヤーが所属するチームの順位を設定します。
        /// </summary>
        public void SetLocalTeamRank(int rank)
        {
            _localTeamRank = Mathf.Max(k_UnsetTeamRank, rank);
        }

        /// <summary>
        /// ゲーム時間が終了しているかどうかを設定します。
        /// </summary>
        public void SetTimeEnded(bool isTimeEnded)
        {
            _isTimeEnded = isTimeEnded;
        }

        /// <summary>
        /// パーティを組んだ状態でゲームを開始したかどうかを設定します。
        /// </summary>
        public void SetPartyPlayState(bool isPartyPlay)
        {
            _isPartyPlay = isPartyPlay;
        }

        /// <summary>
        /// ソロプレイとオンライン対戦を切り替えます。
        /// </summary>
        /// <param name="isSoloPlay">
        /// ソロプレイの場合は<c>true</c>、
        /// オンライン対戦の場合は<c>false</c>です。
        /// </param>
        public void SetSoloPlayState(bool isSoloPlay)
        {
            _isSoloPlay = GameBuildSettings.IsSoloOnly || isSoloPlay;

            if (!GameModeRegistry.Catalog.TryGet(_gameModeId, PlayCategory, out _))
            {
                _gameModeId = GameModeRegistry.k_DefaultModeId;
            }

            if (_isSoloPlay)
            {
                _maximumTeamCount = SoloBattleConfiguration.Teams.Count;
                _maximumPlayersPerTeam = SoloBattleConfiguration.MaximumTeamSize;
                Debug.Log(
                    $"{nameof(GameFlowManager)}: " + "ソロプレイを設定しました。" +
                    $" MaximumTeamCount={_maximumTeamCount}," +
                    $" MaximumPlayersPerTeam={_maximumPlayersPerTeam}");

                return;
            }

            InitializeMatchSettings();
            Debug.Log(
                $"{nameof(GameFlowManager)}: " + "オンライン対戦を設定しました。" +
                $" MaximumTeamCount={_maximumTeamCount}," +
                $" MaximumPlayersPerTeam={_maximumPlayersPerTeam}");
        }

        /// <summary>
        /// パーティプレイ状態を解除します。
        /// </summary>
        public void ClearPartyPlayState()
        {
            _isPartyPlay = false;
        }

        /// <summary>
        /// ソロ試合開始前にJSONを再読込し、生成と集計の設定を固定します。
        /// </summary>
        public void ReloadSoloBattleConfiguration()
        {
            _soloBattleConfiguration = HasSelectedSoloStage
                ? SoloStages.BeginBattle()
                : SoloBattleConfigurationLoader.Load();
            InitializeMatchSettings();
        }

        /// <summary>
        /// ステージ選択を解除し、以前の試合結果の記録を防ぎます。
        /// </summary>
        public void ClearSoloStageSelection()
        {
            _soloStages?.ClearSelection();
            _soloBattleConfiguration = null;
        }

        /// <summary>
        /// 試合終了時の勝敗を選択ステージへ記録します。
        /// </summary>
        public void RecordSoloStageResult(int rank)
        {
            if (HasSelectedSoloStage)
            {
                SoloStages.RecordResult(rank == k_WinningTeamRank);
            }
        }

        /// <summary>
        /// 接続区分とゲームルールを一組として検証し、選択状態を更新します。
        /// </summary>
        public void SetGameMode(PlayCategory category, string gameModeId)
        {
            if ((GameBuildSettings.IsSoloOnly && category == PlayCategory.Online) ||
                !GameModeRegistry.Catalog.TryGet(gameModeId, category, out GameMode mode))
            {
                throw new ArgumentException("利用できないゲームモードです。", nameof(gameModeId));
            }

            SetSoloPlayState(category == PlayCategory.Local);
            _gameModeId = mode.Id;
            ClearSoloStageSelection();
        }

        /// <summary>
        /// 試合終了後に保持しているゲーム状態を初期値へ戻します。
        /// </summary>
        public void ResetAfterGame()
        {
            _isGameplay = false;
            _isTimeEnded = false;
            _localTeamRank = k_UnsetTeamRank;
            _isPartyPlay = false;
        }

        /// <summary>
        /// 現在のゲーム状態を終了し、
        /// 指定された新しい状態へ遷移します。
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }

            if (_stateMachine == null)
            {
                _stateMachine = new GameStateMachine(newState);

                return;
            }

            _stateMachine.ChangeState(newState);
        }
    }
}
