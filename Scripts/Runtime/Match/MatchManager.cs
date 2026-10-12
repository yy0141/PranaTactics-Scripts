using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

using PranaTactics.GameFlow;
using PranaTactics.GameFlow.States;
using PranaTactics.Match.Modes;
using PranaTactics.Match.Solo;
using PranaTactics.Match.Phases;
using PranaTactics.Match.Results;
using PranaTactics.Networking;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;

namespace PranaTactics.Match
{
    /// <summary>
    /// 試合中のフェイズ、ターン、チームスコア、生存人数および
    /// リザルト画面への遷移を管理します。
    /// </summary>
    /// <remarks>
    /// Photon使用時はRPC、EOS使用時はルームプロパティを使用して、
    /// 各クライアントのフェイズ状態を同期します。
    /// </remarks>
    public sealed class MatchManager : MonoBehaviourPunCallbacks
    {
        /// <summary>
        /// Unityオブジェクトの破棄後も集計可能な戦績です。
        /// </summary>
        private readonly MatchResultTracker _resultTracker = new();

        /// <summary>
        /// 同期済みの致死攻撃記録をHUDへ提供します。
        /// </summary>
        public IEnumerable<UnitBattleRecord> GetBattleRecords()
        {
            MatchResultSession.MergeOnline(_resultTracker,
                GameFlowManager.Instance != null && GameFlowManager.Instance.IsSoloPlay
                    ? null : NetworkServiceLocator.Online);
            return _resultTracker.Records;
        }

        /// <summary>
        /// 既存ルールが加算した生存ボーナスの内訳です。
        /// </summary>
        private readonly Dictionary<int, int> _resultSurvivalBonuses = new();

        /// <summary>
        /// プレイヤー退出による終了時の順位判定を使用するかどうかです。
        /// </summary>
        private bool _endedByDeparture;

        /// <summary>
        /// 参加Unitの名前とロールを登録し、同期初期値を設定します。
        /// </summary>
        public void RegisterResultUnit(UnitStatus status)
        {
            if (status == null)
            {
                return;
            }

            string displayName = status.PlayerId == 0 ? "Player" : $"AI {status.PlayerId}";
            IOnlineService online = NetworkServiceLocator.Online;
            if (GameFlowManager.Instance != null && !GameFlowManager.Instance.IsSoloPlay && online != null)
            {
                displayName = $"Unit {status.PlayerId}";
                foreach (OnlinePlayer player in online.Players)
                {
                    if (player.PlayerId == status.PlayerId)
                    {
                        displayName = player.DisplayName;
                        break;
                    }
                }
            }

            UnitBattleRecord record = _resultTracker.Register(status.PlayerId, status.TeamId, displayName, status.CharacterData.Type.ToString());
            PublishResultRecord(record);
        }

        /// <summary>
        /// 確定したHP減少量を記録し、被攻撃側の権限で累積値を同期します。
        /// </summary>
        /// <param name="targetId">
        /// ダメージを受けたユニットのIDです。
        /// </param>
        /// <param name="attackerId">
        /// 攻撃元ユニットのIDです。
        /// </param>
        /// <param name="damage">
        /// 実際に減少したHPです。
        /// </param>
        /// <param name="isFatal">
        /// この攻撃でHPが0になった場合はtrueです。
        /// </param>
        /// <param name="isMain">
        /// 攻撃元がメイン兵装の場合はtrueです。
        /// </param>
        /// <param name="sourcePranamSlot">
        /// 発射時の兵装スロットです。不明な場合は-1です。
        /// </param>
        public void RecordResultDamage(int targetId, int attackerId, float damage, bool isFatal, bool isMain = false, int sourcePranamSlot = -1)
        {
            UnitBattleRecord record = _resultTracker.RecordDamage(targetId, attackerId, damage, isFatal, isMain, sourcePranamSlot);
            if (record != null && isFatal)
            {
                Debug.Log(
                    $"[UnitDefeat] Target={targetId}, Source={attackerId}, " +
                    $"Finisher={record.DefeatedBy}, Damage={damage}",
                    this);
            }

            PublishResultRecord(record);
        }

        /// <summary>
        /// 全攻撃者の最大貢献者と、その所属チームの撃破ポイントを保存します。
        /// </summary>
        public void RecordResultDefeatCredit(int targetId, int scoringTeamId, float maximumHealth)
        {
            UnitBattleRecord record = _resultTracker.RecordDefeatCredit(targetId, scoringTeamId, maximumHealth);
            if (record == null)
            {
                return;
            }

            Debug.Log($"[DefeatCredit] Target={targetId}, ScoringTeam={scoringTeamId}, CreditedTo={record.CreditedPlayerId}", this);
            PublishResultRecord(record);
        }

        /// <summary>
        /// 戦績を変更したUnitの実行権限を確認して既存サービスへ送信します。
        /// </summary>
        private void PublishResultRecord(UnitBattleRecord record)
        {
            IOnlineService online = NetworkServiceLocator.Online;
            if (record == null || GameFlowManager.Instance == null || GameFlowManager.Instance.IsSoloPlay ||
                online == null || !online.IsInRoom || UnitManager.Instance == null ||
                (record.PlayerId != online.LocalPlayerId &&
                !(online.IsHost && UnitManager.Instance.IsAIControlledUnit(record.PlayerId))))
            {
                return;
            }

            if (online.CurrentRoom != null &&
                online.CurrentRoom.TryGetProperty(MatchResultSession.k_RoundKey, out string roundId))
            {
                record.RoundId = roundId;
                online.SetLocalPlayerProperty(MatchResultSession.k_RecordPrefix + record.PlayerId, JsonUtility.ToJson(record));
            }
        }

        /// <summary>
        /// シーン遷移前にチームの内訳、順位、全Unitの戦績を保存します。
        /// </summary>
        public void SaveResultSnapshot()
        {
            if (_teamScoreByTeamId.Count == 0)
            {
                return;
            }

            IOnlineService online = NetworkServiceLocator.Online;
            bool isSolo = GameFlowManager.Instance != null && GameFlowManager.Instance.IsSoloPlay;
            int localId = isSolo ? 0 : online?.LocalPlayerId ?? -1;
            int localTeam = _playerTeamIdByPlayerId.TryGetValue(localId, out int teamId) ? teamId : -1;
            MatchResultSession.MergeOnline(_resultTracker, isSolo ? null : online);
            IReadOnlyDictionary<int, int> ranks = _matchRule.CalculateRanks(GetRuleContext().Scores);
            var teams = new List<TeamResult>();
            foreach (KeyValuePair<int, int> score in _teamScoreByTeamId)
            {
                _resultSurvivalBonuses.TryGetValue(score.Key, out int bonus);
                _survivorCountByTeamId.TryGetValue(score.Key, out int survivors);
                int rank = _endedByDeparture ? _matchRule.CalculateDepartureRank(GetRuleContext(), score.Key) : ranks[score.Key];
                teams.Add(new TeamResult(score.Key, rank, score.Value - bonus, bonus, survivors));
            }

            MatchResultSession.Save(localId, localTeam, teams, _resultTracker);
            foreach (UnitResult unit in MatchResultSession.Current.Units)
            {
                Debug.Log(
                    $"[UnitResult] PlayerId={unit.PlayerId}, TeamId={unit.TeamId}, " +
                    $"Kills={unit.Kills}, DamageDealt={unit.DamageDealt}, " +
                    $"DamageReceived={unit.DamageReceived}, IsAlive={unit.IsAlive}",
                    this);
            }
        }
        /// <summary>
        /// 現在のゲームプレイフェイズを保存する
        /// ルームプロパティのキーです。
        /// </summary>
        private const string k_GameplayStatePropertyKey =
            "inGameState";

        /// <summary>
        /// チーム別の帰還先ルームIDを保存する
        /// ルームプロパティの接頭辞です。
        /// </summary>
        private const string k_ReturnRoomIdPropertyPrefix =
            "returnRoomId_";

        /// <summary>
        /// チーム別の帰還先ルームパスワードを保存する
        /// ルームプロパティの接頭辞です。
        /// </summary>
        private const string k_ReturnPasswordPropertyPrefix =
            "returnPassword_";

        /// <summary>
        /// チーム別の帰還先パーティIDを保存する
        /// ルームプロパティの接頭辞です。
        /// </summary>
        private const string k_ReturnPartyIdPropertyPrefix =
            "returnPartyId_";

        /// <summary>
        /// チーム別の帰還先ルーム名を保存する
        /// ルームプロパティの接頭辞です。
        /// </summary>
        private const string k_ReturnRoomNamePropertyPrefix =
            "returnRoomName_";

        /// <summary>
        /// 帰還先パーティルーム名に使用する接頭辞です。
        /// </summary>
        private const string k_ReturnRoomNamePrefix = "party_";

        /// <summary>
        /// チーム撃破時に加算する既定のスコアです。
        /// </summary>
        private const int k_DefaultTeamScore = 2;

        /// <summary>
        /// 有効なチームが見つからないことを示す識別子です。
        /// </summary>
        private const int k_InvalidTeamId = -1;

        /// <summary>
        /// チーム順位が未確定であることを示す値です。
        /// </summary>
        private const int k_UnsetTeamRank = 0;

        /// <summary>
        /// 帰還先ルームに使用する数字コードの最大値です。
        /// </summary>
        private const int k_RoomCodeMaximumExclusive = 10000;

        /// <summary>
        /// 帰還先ルームに使用する数字コードの桁数です。
        /// </summary>
        private const string k_RoomCodeFormat = "D4";

        /// <summary>
        /// 最後の1チームが残ってから
        /// リザルトへ遷移するまでの待機時間です。
        /// </summary>
        private const float k_ResultTransitionDelaySeconds = 3.0f;

        /// <summary>
        /// Photonのオンラインサービスを判別するために使用する文字列です。
        /// </summary>
        private const string k_PhotonServiceTypeName = "Photon";

        /// <summary>
        /// 試合フェイズの状態遷移を管理します。
        /// </summary>
        private MatchPhaseStateMachine _stateMachine;

        /// <summary>
        /// この試合専用の勝敗判定Strategyです。
        /// </summary>
        private IMatchRule _matchRule = new EliminationRule();

        /// <summary>
        /// チーム情報を変更せずルールへ渡す判定コンテキストです。
        /// </summary>
        private MatchRuleContext _ruleContext;

        /// <summary>
        /// 現在のターン数です。
        /// </summary>
        private int _currentTurn;

        /// <summary>
        /// 試合に参加できる最大チーム数です。
        /// </summary>
        private int _maximumTeamCount;

        /// <summary>
        /// 1チームに参加できる最大プレイヤー数です。
        /// </summary>
        private int _maximumPlayersPerTeam;

        /// <summary>
        /// 現在生存しているチーム数です。
        /// </summary>
        private int _aliveTeamCount;

        /// <summary>
        /// プレイヤーIDから所属チームIDを取得するキャッシュです。
        /// </summary>
        private readonly Dictionary<int, int>
            _playerTeamIdByPlayerId = new();

        /// <summary>
        /// 死亡または退出で生存人数から除外済みのプレイヤーです。
        /// </summary>
        private readonly HashSet<int> _eliminatedPlayerIds = new();

        /// <summary>
        /// チームIDから現在のスコアを取得する辞書です。
        /// </summary>
        private readonly Dictionary<int, int>
            _teamScoreByTeamId = new();

        /// <summary>
        /// チームIDから生存しているプレイヤー数を取得する辞書です。
        /// </summary>
        private readonly Dictionary<int, int>
            _survivorCountByTeamId = new();

        /// <summary>
        /// リザルト画面への遷移処理を開始済みかどうかを示します。
        /// </summary>
        private bool _isTransitioningToResult;

        /// <summary>
        /// リザルト画面へ遷移するまでの残り待機時間です。
        /// </summary>
        private float _resultTransitionDelayRemaining =
            k_ResultTransitionDelaySeconds;

        /// <summary>
        /// オンラインサービスのイベントを購読済みかどうかを示します。
        /// </summary>
        private bool _areOnlineEventsRegistered;

        /// <summary>
        /// イベントを購読しているオンラインサービスです。
        /// </summary>
        private IOnlineService _registeredOnlineService;

        /// <summary>
        /// 現在適用されているフェイズ状態のクラス名です。
        /// </summary>
        private string _currentStateName =
            nameof(StartPhaseState);

        /// <summary>
        /// ボードとユニットの初期生成が完了しているかどうかを示します。
        /// </summary>
        private bool _isInitialSetupComplete;

        /// <summary>
        /// 初期生成の完了後に適用する保留中のフェイズ状態名です。
        /// </summary>
        private string _pendingStateName;

        public static MatchManager Instance
        {
            get; private set;
        }

        public int CurrentTurn
        {
            get
            {
                return _currentTurn;
            }
        }

        public bool IsInitialSetupComplete
        {
            get
            {
                return _isInitialSetupComplete;
            }
        }

        /// <summary>
        /// オンラインサービスが利用可能になるまで待機し、
        /// プレイヤー退出とルームプロパティ変更イベントを購読します。
        /// </summary>
        private void OnEnable()
        {
            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.IsSoloPlay)
            {
                return;
            }

            StartCoroutine(
                RegisterOnlineEventsWhenReady());
        }

        /// <summary>
        /// オンラインサービスのイベント購読を解除します。
        /// </summary>
        private void OnDisable()
        {
            UnregisterOnlineEvents();
        }

        /// <summary>
        /// MatchManagerのシングルトンインスタンスを設定します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;
            MatchResultSession.Clear();
        }

        /// <summary>
        /// 試合フェイズ、チーム数、スコアおよび生存人数を初期化します。
        /// </summary>
        private void Start()
        {
            IOnlineService resultOnline = NetworkServiceLocator.Online;
            if (GameFlowManager.Instance != null && !GameFlowManager.Instance.IsSoloPlay &&
                resultOnline != null && resultOnline.IsHost && resultOnline.CurrentRoom != null)
            {
                string roundId = Guid.NewGuid().ToString("N");
                resultOnline.CurrentRoom.Properties[MatchResultSession.k_RoundKey] = roundId;
                resultOnline.SetRoomProperty(MatchResultSession.k_RoundKey, roundId);
            }

            InitializeMatchRule();
            _stateMachine =
                new MatchPhaseStateMachine(
                    new StartPhaseState());

            _currentStateName =
                nameof(StartPhaseState);

            InitializeTeamSettings();
            InitializeTeamData();

            Debug.Log(
                $"{nameof(MatchManager)}: " +
                $"MaximumTeamCount={_maximumTeamCount}, " +
                $"MaximumPlayersPerTeam={_maximumPlayersPerTeam}",
                this);
        }

        /// <summary>
        /// 選択されたモードから独立したルールを生成します。
        /// </summary>
        private void InitializeMatchRule()
        {
            GameFlowManager flow = GameFlowManager.Instance;
            if (flow != null && GameModeRegistry.Catalog.TryGet(flow.GameModeId, flow.PlayCategory, out GameMode mode))
            {
                _matchRule = mode.CreateRule();
            }

            _ruleContext = new MatchRuleContext(_teamScoreByTeamId, _survivorCountByTeamId);
        }

        /// <summary>
        /// 最新のチーム状態を勝敗判定へ渡します。
        /// </summary>
        private MatchRuleContext GetRuleContext()
        {
            _ruleContext ??= new MatchRuleContext(_teamScoreByTeamId, _survivorCountByTeamId);
            _ruleContext.Refresh(_aliveTeamCount, HasMultiplePlayers());
            return _ruleContext;
        }

        /// <summary>
        /// 現在のフェイズを更新し、
        /// 選択ルールの終了条件が成立した場合はリザルト遷移を処理します。
        /// </summary>
        private void Update()
        {
            _stateMachine?.UpdateState();

            UpdateResultTransition();
        }

        /// <summary>
        /// このインスタンスが破棄されたとき、
        /// イベント購読とシングルトン参照を解除します。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                SaveResultSnapshot();
            }

            UnregisterOnlineEvents();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// GameFlowManagerから最大チーム数と
        /// 1チームあたりの最大プレイヤー数を取得します。
        /// </summary>
        private void InitializeTeamSettings()
        {
            if (GameFlowManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(MatchManager)}: " +
                    $"{nameof(GameFlowManager)}が存在しません。",
                    this);

                _maximumTeamCount = 0;
                _maximumPlayersPerTeam = 0;
                _aliveTeamCount = 0;

                return;
            }

            _maximumTeamCount =
                GameFlowManager.Instance.MaximumTeamCount;

            _maximumPlayersPerTeam =
                GameFlowManager.Instance.MaximumPlayersPerTeam;

            _aliveTeamCount = _maximumTeamCount;
        }

#if UNITY_EDITOR || PV_CAPTURE
        public PranaTactics.Promo.PvGameplayScenario CaptureScenario
        {
            get; set;
        }

#endif

        /// <summary>
        /// 各チームの初期スコアと生存人数を設定します。
        /// </summary>
        private void InitializeTeamData()
        {
            _eliminatedPlayerIds.Clear();
#if UNITY_EDITOR || PV_CAPTURE
            if (CaptureScenario != null)
            {
                _teamScoreByTeamId.Clear();
                _survivorCountByTeamId.Clear();
                _playerTeamIdByPlayerId.Clear();

                for (int index = 0; index < CaptureScenario.Actors.Length; index++)
                {
                    int team = CaptureScenario.Actors[index].Team;

                    _teamScoreByTeamId[team] = 0;
                    _survivorCountByTeamId.TryGetValue(team, out int count);
                    _survivorCountByTeamId[team] = count + 1;
                    _playerTeamIdByPlayerId[index] = team;
                }

                _aliveTeamCount = _survivorCountByTeamId.Count;

                return;
            }

#endif

            _teamScoreByTeamId.Clear();
            _survivorCountByTeamId.Clear();
            _playerTeamIdByPlayerId.Clear();

            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.IsSoloPlay)
            {
                SoloBattleConfiguration configuration = GameFlowManager.Instance.SoloBattleConfiguration;
                int playerId = 0;
                foreach (SoloBattleTeam team in configuration.Teams)
                {
                    _teamScoreByTeamId.Add(team.TeamId, 0);
                    _survivorCountByTeamId.Add(team.TeamId, team.PlayerCount);
                    for (int memberIndex = 0; memberIndex < team.PlayerCount; memberIndex++)
                    {
                        _playerTeamIdByPlayerId.Add(playerId++, team.TeamId);
                    }
                }

                _aliveTeamCount = configuration.Teams.Count;

                return;
            }

            for (
                int teamId = 0;
                teamId < _maximumTeamCount;
                teamId++)
            {
                _teamScoreByTeamId.Add(
                    teamId,
                    0);

                _survivorCountByTeamId.Add(
                    teamId,
                    _maximumPlayersPerTeam);
            }
        }

        /// <summary>
        /// ターン数を1増加させ、HUDのターン表示を更新します。
        /// </summary>
        public void IncrementTurn()
        {
            _currentTurn++;

            if (HUDManager.Instance == null)
            {
                Debug.LogWarning(
                    $"{nameof(MatchManager)}: " +
                    $"{nameof(HUDManager)}が存在しないため、" +
                    "ターン表示を更新できません。",
                    this);

                return;
            }

            HUDManager.Instance.SetTurnText(_currentTurn);
        }

        /// <summary>
        /// 4桁の数字コードを生成します。
        /// </summary>
        /// <returns>
        /// 先頭の0を含む4桁の数字文字列です。
        /// </returns>
        private static string GenerateFourDigitCode()
        {
            return UnityEngine.Random
                .Range(
                    0,
                    k_RoomCodeMaximumExclusive)
                .ToString(k_RoomCodeFormat);
        }

        /// <summary>
        /// 試合終了後に各チームが戻る
        /// パーティルームの情報を生成します。
        /// </summary>
        private void PrepareReturnRooms()
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsHost ||
                onlineService.CurrentRoom == null)
            {
                return;
            }

            if (onlineService.CurrentRoom.TryGetProperty(
                    NetworkPropertyKeys.ReturnRoomReady,
                    out bool isReturnRoomReady) &&
                isReturnRoomReady)
            {
                return;
            }

            Dictionary<string, object> properties = new();

            for (
                int teamId = 0;
                teamId < _maximumTeamCount;
                teamId++)
            {
                string roomId = GenerateFourDigitCode();
                string password = GenerateFourDigitCode();

                properties[
                    $"{k_ReturnRoomIdPropertyPrefix}{teamId}"] =
                    roomId;

                properties[
                    $"{k_ReturnPasswordPropertyPrefix}{teamId}"] =
                    password;

                properties[
                    $"{k_ReturnPartyIdPropertyPrefix}{teamId}"] =
                    $"{roomId}_{password}";

                properties[
                    $"{k_ReturnRoomNamePropertyPrefix}{teamId}"] =
                    $"{k_ReturnRoomNamePrefix}{roomId}_{password}";
            }

            properties[
                NetworkPropertyKeys.ReturnRoomReady] = true;

            onlineService.SetRoomProperties(properties);

            Debug.Log(
                $"{nameof(MatchManager)}: " +
                "チーム別の帰還先ルーム情報を生成しました。",
                this);
        }

        /// <summary>
        /// 現在参加しているオンラインプレイヤーから、
        /// プレイヤーIDと所属チームIDの対応をキャッシュします。
        /// </summary>
        public void CachePlayerTeamIds()
        {
            Debug.Log(
                $"{nameof(MatchManager)}: " +
                $"{nameof(CachePlayerTeamIds)}を開始します。",
                this);

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                Debug.LogWarning(
                    "オンラインサービスが登録されていません。",
                    this);

                return;
            }

            if (!onlineService.IsConnected)
            {
                Debug.LogWarning(
                    "オンラインサービスに接続されていません。",
                    this);

                return;
            }

            if (!onlineService.IsInRoom)
            {
                Debug.LogWarning(
                    "オンラインルームに参加していません。",
                    this);

                return;
            }

            _playerTeamIdByPlayerId.Clear();

            foreach (OnlinePlayer player in onlineService.Players)
            {
                if (player == null)
                {
                    continue;
                }

                if (!player.TryGetProperty(
                        NetworkPropertyKeys.TeamId,
                        out int teamId))
                {
                    Debug.LogWarning(
                        $"プレイヤーにTeamIdが設定されていません: " +
                        $"PlayerId={player.PlayerId}",
                        this);

                    continue;
                }

                _playerTeamIdByPlayerId[player.PlayerId] =
                    teamId;

                Debug.Log(
                    $"プレイヤーのTeamIdを保存しました: " +
                    $"PlayerId={player.PlayerId}, TeamId={teamId}",
                    this);
            }
        }

        /// <summary>
        /// ユニット生成情報から、
        /// プレイヤーIDと所属チームIDの対応をキャッシュします。
        /// </summary>
        /// <param name="spawnDataList">
        /// キャッシュに使用するユニット生成情報です。
        /// </param>
        public void CachePlayerTeamIdsFromSpawns(
            IEnumerable<UnitInitializeData> spawnDataList)
        {
            if (spawnDataList == null)
            {
                Debug.LogWarning(
                    "ユニット生成情報がnullのため、" +
                    "TeamIdをキャッシュできません。",
                    this);

                return;
            }

            _playerTeamIdByPlayerId.Clear();

            int cachedCount = 0;

            foreach (
                UnitInitializeData spawnData
                in spawnDataList)
            {
                _playerTeamIdByPlayerId[
                    spawnData.OwnerId] =
                    spawnData.TeamId;

                cachedCount++;

                Debug.Log(
                    $"生成情報からTeamIdを保存しました: " +
                    $"PlayerId={spawnData.OwnerId}, " +
                    $"TeamId={spawnData.TeamId}",
                    this);
            }

            Debug.Log(
                $"{nameof(CachePlayerTeamIdsFromSpawns)}完了: " +
                $"Count={cachedCount}",
                this);
        }

        /// <summary>
        /// キャッシュから指定されたプレイヤーの所属チームIDを取得します。
        /// </summary>
        /// <param name="playerId">
        /// 所属チームを取得するプレイヤーの識別子です。
        /// </param>
        /// <returns>
        /// プレイヤーの所属チームIDです。
        /// キャッシュに存在しない場合は
        /// <see cref="k_InvalidTeamId"/>です。
        /// </returns>
        public int GetCachedTeamId(int playerId)
        {
            if (_playerTeamIdByPlayerId.TryGetValue(
                    playerId,
                    out int teamId))
            {
                return teamId;
            }

            Debug.LogWarning(
                $"キャッシュされたTeamIdが見つかりません: " +
                $"PlayerId={playerId}",
                this);

            return k_InvalidTeamId;
        }

        /// <summary>
        /// 指定されたチームへスコアを加算します。
        /// </summary>
        /// <param name="teamId">
        /// スコアを加算するチームの識別子です。
        /// </param>
        /// <param name="score">
        /// 加算するスコアです。
        /// </param>
        public void AddScore(
            int teamId,
            int score = k_DefaultTeamScore)
        {
            if (!_teamScoreByTeamId.ContainsKey(teamId))
            {
                Debug.LogWarning(
                    $"スコアを加算するチームが存在しません: " +
                    $"TeamId={teamId}",
                    this);

                return;
            }

            _teamScoreByTeamId[teamId] += score;
        }

        /// <summary>
        /// 死亡または退出したプレイヤーを一度だけ生存人数から除外します。
        /// </summary>
        public void RegisterPlayerElimination(int playerId, int teamId)
        {
            if (playerId < 0 || !_eliminatedPlayerIds.Add(playerId))
            {
                return;
            }

            DecreaseSurvivor(teamId);
            PublishResultRecord(_resultTracker.Eliminate(playerId));
        }

        /// <summary>
        /// 指定されたチームの生存プレイヤー数を1減少させます。
        /// </summary>
        /// <param name="teamId">
        /// 生存プレイヤー数を減少させるチームの識別子です。
        /// </param>
        private void DecreaseSurvivor(int teamId)
        {
            if (!_survivorCountByTeamId.TryGetValue(
                    teamId,
                    out int survivorCount))
            {
                Debug.LogWarning(
                    $"生存人数を管理していないチームです: " +
                    $"TeamId={teamId}",
                    this);

                return;
            }

            if (survivorCount <= 0)
            {
                return;
            }

            survivorCount--;

            _survivorCountByTeamId[teamId] =
                survivorCount;

            if (survivorCount > 0)
            {
                return;
            }

            _aliveTeamCount =
                Mathf.Max(
                    0,
                    _aliveTeamCount - 1);
        }

        /// <summary>
        /// 指定されたチームへ撃破スコアを加算します。
        /// </summary>
        /// <remarks>
        /// 生存人数は得点チームとは独立して、死亡適用時に死亡側で更新します。
        /// </remarks>
        /// <param name="teamId">
        /// 撃破スコアを加算するチームの識別子です。
        /// </param>
        public void AddTeamScore(int teamId)
        {
            AddScore(teamId, _matchRule.DefeatScore);

        }

        /// <summary>
        /// オンラインサービスが登録されるまで待機し、
        /// イベントを購読します。
        /// </summary>
        /// <returns>
        /// オンラインサービスの登録を待機するコルーチンです。
        /// </returns>
        private IEnumerator RegisterOnlineEventsWhenReady()
        {
            while (NetworkServiceLocator.Online == null)
            {
                yield return null;
            }

            RegisterOnlineEvents();
        }

        /// <summary>
        /// オンラインサービスのプレイヤー退出イベントと
        /// ルームプロパティ変更イベントを購読します。
        /// </summary>
        private void RegisterOnlineEvents()
        {
            if (_areOnlineEventsRegistered)
            {
                return;
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                return;
            }

            _registeredOnlineService = onlineService;

            _registeredOnlineService.PlayerLeft +=
                HandlePlayerLeftGame;

            _registeredOnlineService.RoomPropertiesChanged +=
                HandleRoomPropertiesChanged;

            _areOnlineEventsRegistered = true;

            ApplyGameplayStateFromCurrentRoom(
                "RegisterOnlineEvents");
        }

        /// <summary>
        /// オンラインサービスから登録済みのイベントを解除します。
        /// </summary>
        private void UnregisterOnlineEvents()
        {
            if (!_areOnlineEventsRegistered)
            {
                return;
            }

            if (_registeredOnlineService != null)
            {
                _registeredOnlineService.PlayerLeft -=
                    HandlePlayerLeftGame;

                _registeredOnlineService.RoomPropertiesChanged -=
                    HandleRoomPropertiesChanged;
            }

            _registeredOnlineService = null;
            _areOnlineEventsRegistered = false;
        }

        /// <summary>
        /// 生存チームに基づいてローカルチームの順位を設定し、
        /// リザルト画面へ遷移します。
        /// </summary>
        private void TransitionToResultBySurvivor()
        {
            if (GameFlowManager.Instance != null && !GameFlowManager.Instance.IsSoloPlay &&
                MatchmakingManager.Instance != null && MatchmakingManager.Instance.IsSinglePartyMatchMode())
            {
                return;
            }

#if UNITY_EDITOR || PV_CAPTURE
            if (CaptureScenario != null && !CaptureScenario.AllowMatchEnd)
            {
                return;
            }

#endif

            if (_isTransitioningToResult)
            {
                return;
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                Debug.LogError(
                    "オンラインサービスを取得できないため、" +
                    "リザルトへ遷移できません。",
                    this);

                return;
            }

            if (GameFlowManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(GameFlowManager)}が存在しないため、" +
                    "リザルトへ遷移できません。",
                    this);

                return;
            }

            _isTransitioningToResult = true;

            int localTeamId = GetCachedTeamId(onlineService.LocalPlayerId);
            _endedByDeparture = true;
            int localTeamRank = _matchRule.CalculateDepartureRank(GetRuleContext(), localTeamId);

            GameFlowManager.Instance.SetLocalTeamRank(
                localTeamRank);

            GameFlowManager.Instance.ChangeState(
                new ResultState());
        }

        /// <summary>
        /// オンラインルームからプレイヤーが退出したとき、
        /// 退出したプレイヤーの所属チームの生存人数を更新します。
        /// </summary>
        /// <param name="leftPlayer">
        /// オンラインルームから退出したプレイヤーです。
        /// </param>
        private void HandlePlayerLeftGame(
            OnlinePlayer leftPlayer)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsHost ||
                _isTransitioningToResult ||
                leftPlayer == null)
            {
                return;
            }

            if (GameFlowManager.Instance == null ||
                !GameFlowManager.Instance.IsGameplay)
            {
                return;
            }

            int leftPlayerId = leftPlayer.PlayerId;

            if (!_playerTeamIdByPlayerId.TryGetValue(
                    leftPlayerId,
                    out int leftTeamId))
            {
                Debug.LogWarning(
                    $"退出したプレイヤーのTeamIdが保存されていません: " +
                    $"PlayerId={leftPlayerId}",
                    this);

                return;
            }

            Debug.Log(
                $"プレイヤー退出: " +
                $"PlayerId={leftPlayerId}, TeamId={leftTeamId}",
                this);

            RegisterPlayerElimination(leftPlayerId, leftTeamId);

            if (_matchRule.ShouldFinishAfterDeparture(GetRuleContext()))
            {
                TransitionToResultBySurvivor();
            }
        }

        /// <summary>
        /// ソロプレイの味方全滅時は即座に終了し、それ以外はルールの終了条件に応じて、
        /// リザルト画面への遷移待機時間を更新します。
        /// </summary>
        private void UpdateResultTransition()
        {
#if UNITY_EDITOR || PV_CAPTURE
            if (CaptureScenario != null && !CaptureScenario.AllowMatchEnd)
            {
                return;
            }

#endif

            if (_isTransitioningToResult)
            {
                return;
            }

            if (IsSoloPlayerTeamEliminated())
            {
                TransitionToResultScene();
                return;
            }

            if (!_matchRule.ShouldFinish(GetRuleContext()))
            {
                return;
            }

            _resultTransitionDelayRemaining -=
                Time.deltaTime;

            if (_resultTransitionDelayRemaining > 0.0f)
            {
                return;
            }

            TransitionToResultScene();
        }

        /// <summary>
        /// ソロプレイ中にプレイヤーの所属チームが全滅したか確認します。
        /// </summary>
        private bool IsSoloPlayerTeamEliminated()
        {
            GameFlowManager flow = GameFlowManager.Instance;
            return flow != null && flow.IsSoloPlay && flow.IsGameplay &&
                _playerTeamIdByPlayerId.TryGetValue(0, out int localTeamId) &&
                _survivorCountByTeamId.TryGetValue(localTeamId, out int survivors) &&
                survivors == 0;
        }

        /// <summary>
        /// この試合に複数のプレイヤーが参加する設定かどうかを確認します。
        /// </summary>
        /// <returns>
        /// 最大参加人数が2人以上の場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool HasMultiplePlayers()
        {
            if (GameFlowManager.Instance != null && !GameFlowManager.Instance.IsSoloPlay &&
                MatchmakingManager.Instance != null && MatchmakingManager.Instance.IsSinglePartyMatchMode())
            {
                return false;
            }

#if UNITY_EDITOR || PV_CAPTURE
            if (CaptureScenario != null)
            {
                return CaptureScenario.AllowMatchEnd && CaptureScenario.Actors.Length > 1;
            }

#endif
            if (GameFlowManager.Instance != null && GameFlowManager.Instance.IsSoloPlay)
            {
                return GameFlowManager.Instance.SoloBattleConfiguration.TotalPlayerCount > 1;
            }

            return _maximumPlayersPerTeam * _maximumTeamCount > 1;
        }

        /// <summary>
        /// 現在のチームスコアからローカルチームの順位を計算し、
        /// リザルト画面へ遷移します。
        /// </summary>
        public void TransitionToResultScene()
        {
#if UNITY_EDITOR || PV_CAPTURE
            if (CaptureScenario != null && !CaptureScenario.AllowMatchEnd)
            {
                return;
            }

#endif

            if (_isTransitioningToResult)
            {
                return;
            }

            if (GameFlowManager.Instance == null)
            {
                Debug.LogError(
                    $"{nameof(GameFlowManager)}が存在しないため、" +
                    "リザルトへ遷移できません。",
                    this);

                return;
            }

            if (GameFlowManager.Instance.IsSoloPlay)
            {
                _isTransitioningToResult =
                    true;

                const int soloLocalTeamId =
                    0;

                if (GameFlowManager.Instance.GameModeId == GameModeRegistry.k_DefaultModeId)
                {
                    var scoresBeforeBonus = new Dictionary<int, int>(_teamScoreByTeamId);
                    SoloSurvivalBonus.Apply(_teamScoreByTeamId, GetRuleContext().Survivors);
                    foreach (KeyValuePair<int, int> score in _teamScoreByTeamId)
                    {
                        _resultSurvivalBonuses[score.Key] = score.Value - scoresBeforeBonus[score.Key];
                    }
                }

                IReadOnlyDictionary<int, int> soloTeamRanks =
                    _matchRule.CalculateRanks(
                        GetRuleContext().Scores);

                if (soloTeamRanks.TryGetValue(
                        soloLocalTeamId,
                        out int soloLocalTeamRank))
                {
                    GameFlowManager.Instance.SetLocalTeamRank(
                        soloLocalTeamRank);
                    GameFlowManager.Instance.RecordSoloStageResult(soloLocalTeamRank);
                }
                else
                {
                    Debug.LogError(
                        "ソロプレイのローカルチームが" +
                        "順位表に存在しません。",
                        this);

                    GameFlowManager.Instance.SetLocalTeamRank(
                        k_UnsetTeamRank);
                }

                GameFlowManager.Instance.ChangeState(
                    new ResultState());

                return;
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                Debug.LogError(
                    "オンラインサービスを取得できないため、" +
                    "リザルトへ遷移できません。",
                    this);

                return;
            }

            _isTransitioningToResult =
                true;

            PrepareReturnRooms();

            int localTeamId =
                GetCachedTeamId(
                    onlineService.LocalPlayerId);

            IReadOnlyDictionary<int, int> teamRanks =
                _matchRule.CalculateRanks(
                    GetRuleContext().Scores);

            if (teamRanks.TryGetValue(
                    localTeamId,
                    out int localTeamRank))
            {
                GameFlowManager.Instance.SetLocalTeamRank(
                    localTeamRank);
            }
            else
            {
                Debug.LogError(
                    "ローカルチームが順位表に存在しません: " +
                    $"TeamId={localTeamId}",
                    this);

                GameFlowManager.Instance.SetLocalTeamRank(
                    k_UnsetTeamRank);
            }

            GameFlowManager.Instance.ChangeState(
                new ResultState());
        }

        /// <summary>
        /// ボードとユニットの初期生成が完了したことを通知します。
        /// 保留中のフェイズ状態がある場合は、その状態を適用します。
        /// </summary>
        public void NotifyInitialSetupComplete()
        {
            if (_isInitialSetupComplete)
            {
                return;
            }

            _isInitialSetupComplete = true;

            Debug.Log(
                $"{nameof(MatchManager)}: 初期生成が完了しました。" +
                $"PendingState={_pendingStateName ?? "None"}",
                this);

            if (!string.IsNullOrEmpty(
                    _pendingStateName))
            {
                string pendingStateName =
                    _pendingStateName;

                _pendingStateName = null;

                ApplyGameplayState(
                    pendingStateName,
                    "DeferredUntilInitialSetupComplete");

                return;
            }

            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.IsSoloPlay)
            {
                return;
            }

            ApplyGameplayStateFromCurrentRoom(
                "InitialSetupComplete");
        }

        /// <summary>
        /// ホストから参加中のすべてのクライアントへ
        /// 指定されたフェイズ状態への変更を通知します。
        /// </summary>
        /// <param name="newState">
        /// 遷移先となるフェイズ状態です。
        /// </param>
        public void ChangeState(
            MatchPhaseState newState)
        {
            if (newState == null)
            {
                Debug.LogWarning(
                    "遷移先のフェイズ状態が指定されていません。",
                    this);

                return;
            }

            string stateClassName =
                newState.GetType().Name;

            if (GameFlowManager.Instance != null &&
                GameFlowManager.Instance.IsSoloPlay)
            {
                ApplyGameplayState(
                    stateClassName,
                    "SoloLocal");

                return;
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsHost)
            {
                return;
            }

            if (IsPhotonOnlineService(
                    onlineService))
            {
                photonView.RPC(
                    nameof(RpcChangeState),
                    RpcTarget.All,
                    stateClassName);

                return;
            }

            onlineService.SetRoomProperty(
                k_GameplayStatePropertyKey,
                stateClassName);

            ApplyGameplayState(
                stateClassName,
                "HostLocal");
        }

        /// <summary>
        /// 指定されたオンラインサービスがPhoton実装かどうかを確認します。
        /// </summary>
        /// <param name="onlineService">
        /// 判定するオンラインサービスです。
        /// </param>
        /// <returns>
        /// Photon実装の場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool IsPhotonOnlineService(
            IOnlineService onlineService)
        {
            if (onlineService == null)
            {
                return false;
            }

            return onlineService
                .GetType()
                .Name
                .Contains(
                    k_PhotonServiceTypeName,
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// ルームプロパティが変更されたとき、
        /// 同期されたフェイズ状態を適用します。
        /// </summary>
        /// <param name="changedProperties">
        /// 変更されたルームプロパティです。
        /// </param>
        private void HandleRoomPropertiesChanged(
            Dictionary<string, object> changedProperties)
        {
            if (!TryGetPropertyIgnoreCase(
                    changedProperties,
                    k_GameplayStatePropertyKey,
                    out object rawStateName))
            {
                return;
            }

            ApplyGameplayState(
                rawStateName?.ToString(),
                "RoomPropertyChanged");
        }

        /// <summary>
        /// 現在参加しているルームのプロパティから
        /// 同期済みのフェイズ状態を取得して適用します。
        /// </summary>
        /// <param name="reason">
        /// 状態適用処理を実行した理由です。
        /// </param>
        private void ApplyGameplayStateFromCurrentRoom(
            string reason)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                onlineService.CurrentRoom == null)
            {
                return;
            }

            if (!TryGetPropertyIgnoreCase(
                    onlineService.CurrentRoom.Properties,
                    k_GameplayStatePropertyKey,
                    out object rawStateName))
            {
                return;
            }

            ApplyGameplayState(
                rawStateName?.ToString(),
                reason);
        }

        /// <summary>
        /// プロパティ一覧から、指定されたキーに対応する値を取得します。
        /// キーの大文字と小文字は区別しません。
        /// </summary>
        /// <param name="properties">
        /// 検索するプロパティの一覧です。
        /// </param>
        /// <param name="key">
        /// 検索するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// キーが存在する場合に取得される値です。
        /// </param>
        /// <returns>
        /// キーが存在する場合は<c>true</c>、
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

            if (properties.TryGetValue(key, out value))
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
        /// Photon RPCによって通知されたフェイズ状態を適用します。
        /// </summary>
        /// <param name="stateClassName">
        /// 適用するフェイズ状態のクラス名です。
        /// </param>
        [PunRPC]
        private void RpcChangeState(string stateClassName)
        {
            ApplyGameplayState(
                stateClassName,
                "PhotonRPC");
        }

        /// <summary>
        /// 指定されたクラス名に対応するフェイズ状態を生成し、
        /// ローカルのステートマシンへ適用します。
        /// </summary>
        /// <param name="stateClassName">
        /// 適用するフェイズ状態のクラス名です。
        /// </param>
        /// <param name="reason">
        /// 状態適用処理を実行した理由です。
        /// </param>
        private void ApplyGameplayState(
            string stateClassName,
            string reason)
        {
            if (string.IsNullOrEmpty(stateClassName))
            {
                return;
            }

            if (_matchRule.ShouldFinish(GetRuleContext()))
            {
                return;
            }

            bool isStartPhase =
                string.Equals(
                    stateClassName,
                    nameof(StartPhaseState),
                    StringComparison.Ordinal);

            if (!_isInitialSetupComplete &&
                !isStartPhase)
            {
                _pendingStateName = stateClassName;

                Debug.Log(
                    $"{nameof(MatchManager)}: " +
                    "初期生成前の状態遷移を保留します。" +
                    $"State={stateClassName}, Reason={reason}",
                    this);

                return;
            }

            if (string.Equals(
                    _currentStateName,
                    stateClassName,
                    StringComparison.Ordinal))
            {
                Debug.Log(
                    $"{nameof(MatchManager)}: " +
                    "同じ状態への遷移をスキップします。" +
                    $"State={stateClassName}, Reason={reason}",
                    this);

                return;
            }

            MatchPhaseState nextState =
                CreatePhaseState(stateClassName);

            if (nextState == null)
            {
                Debug.LogError(
                    $"不明なフェイズ状態名です: {stateClassName}",
                    this);

                return;
            }

            Debug.Log(
                $"{nameof(MatchManager)}: " +
                "フェイズ状態を適用します。" +
                $"State={stateClassName}, Reason={reason}",
                this);

            _currentStateName = stateClassName;

            _stateMachine.ChangeState(nextState);
        }

        /// <summary>
        /// 指定されたクラス名に対応するフェイズ状態を生成します。
        /// </summary>
        /// <param name="stateClassName">
        /// 生成するフェイズ状態のクラス名です。
        /// </param>
        /// <returns>
        /// 生成されたフェイズ状態です。
        /// 対応する状態がない場合は<c>null</c>です。
        /// </returns>
        private static MatchPhaseState CreatePhaseState(
            string stateClassName)
        {
            switch (stateClassName)
            {
                case nameof(StartPhaseState):
                    return new StartPhaseState();

                case nameof(MacroPhaseState):
                    return new MacroPhaseState();

                case nameof(MicroPhaseState):
                    return new MicroPhaseState();

                default:
                    return null;
            }
        }
    }
}
