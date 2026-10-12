using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.AI.Solo;
using PranaTactics.Board;
using PranaTactics.Controls;
using PranaTactics.Controls.AI;
using PranaTactics.Match.Phases;
using PranaTactics.Networking;
using PranaTactics.Unit.Movement;
using PranaTactics.Unit.Networking;
using PranaTactics.Unit.Networking.Local;

namespace PranaTactics.Unit
{
    /// <summary>
    /// ユニットの生成、検索、AI制御、スポーンおよび
    /// ローカル再スポーンを管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnitManager : MonoBehaviour
    {
        /// <summary>
        /// 無効なプレイヤーIDです。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// 無効なチームIDです。
        /// </summary>
        private const int k_InvalidTeamId = -1;

        /// <summary>
        /// 再スポーン時にPranam初期化完了を待機する最大時間です。
        /// </summary>
        private const float k_PranamInitializationTimeoutSeconds = 5.0f;

        /// <summary>
        /// ランダムスポーン時に確保する
        /// Unit同士の最小Hex距離です。
        ///
        /// 7の場合、各Unitは最低7マス離れます。
        /// </summary>
        private const int k_MinimumRandomSpawnDistance = 7;

        /// <summary>
        /// ミクロ移動を同一の時間軸で裁定します。
        /// </summary>
        private readonly MicroMovementArbiter _movementArbiter = new();

        /// <summary>
        /// 裁定対象の再利用バッファです。
        /// </summary>
        private readonly List<IMicroMovementParticipant> _movementParticipants = new();

        public bool HasMicroMovementAuthority =>
            NetworkServiceLocator.Online == null ||
            !NetworkServiceLocator.Online.IsInRoom ||
            NetworkServiceLocator.Online.IsHost;

        /// <summary>
        /// 占有中の移動先への出発を禁止します。
        /// </summary>
        public bool IsMicroCellOccupied(Vector2Int coordinate, int exceptPlayerId)
        {
            foreach (UnitBehavior unit in GetAllUnitBehaviors())
            {
                MicroUnitBehavior micro = unit.GetMicroUnitBehavior();
                if (micro != null && !micro.IsDead && unit.GetOwnerId() != exceptPlayerId &&
                    micro.GetCurrentCoordinate() == coordinate)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 全Unitの入力と速度更新後に到着順を確定して同期します。
        /// </summary>
        private void LateUpdate()
        {
            if (MatchPhaseManager.Instance == null ||
                MatchPhaseManager.Instance.CurrentPhase != MatchPhase.Micro ||
                !HasMicroMovementAuthority)
            {
                return;
            }

            _movementParticipants.Clear();
            foreach (UnitBehavior unit in GetAllUnitBehaviors())
            {
                MicroUnitBehavior micro = unit.GetMicroUnitBehavior();
                if (micro != null && !micro.IsDead)
                {
                    _movementParticipants.Add(micro);
                }
            }

            _movementArbiter.Tick(_movementParticipants, Time.deltaTime);
            foreach (IMicroMovementParticipant participant in _movementParticipants)
            {
                ((MicroUnitBehavior)participant).PublishMovementState();
            }
        }

        /// <summary>
        /// 終了時の移動結果をホストから全Unitへ同期します。
        /// </summary>
        public void FinalizeMicroMovement()
        {
            if (!HasMicroMovementAuthority)
            {
                return;
            }

            foreach (UnitBehavior unit in GetAllUnitBehaviors())
            {
                unit.GetMicroUnitBehavior()?.FinalizeMicroMovement();
            }
        }

        public static UnitManager Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// プレイヤー情報を取得するサービスです。
        /// </summary>
        private IPlayerService _playerService;

        /// <summary>
        /// 現在の通信方式に対応したユニット生成Factoryです。
        /// </summary>
        private IUnitFactory _unitFactory;

        /// <summary>
        /// プレイヤーIDをキーとしたユニット一覧です。
        /// </summary>
        private readonly Dictionary<int, UnitBehavior> _units = new();

        /// <summary>
        /// このクライアントでAI制御するプレイヤーIDです。
        /// </summary>
        private readonly HashSet<int> _aiControlledPlayerIds = new();

        /// <summary>
        /// AIプレイヤーごとの移動許可状態です。
        /// </summary>
        private readonly Dictionary<int, bool> _aiMoveEnabledByPlayerId = new();

        /// <summary>
        /// AIプレイヤーごとの武器使用許可状態です。
        /// </summary>
        private readonly Dictionary<int, bool> _aiWeaponUsageEnabledByPlayerId = new();

        /// <summary>
        /// 実行待ちの再スポーン情報です。
        /// </summary>
        private readonly Dictionary<int, RespawnRequest> _respawnRequests = new();

        /// <summary>
        /// 再スポーン処理を実行するプレイヤーIDの作業領域です。
        /// </summary>
        private readonly List<int> _respawnExecutionBuffer = new();

        /// <summary>
        /// スポーン可能座標を一時的に保持します。
        /// </summary>
        private readonly List<Vector2Int> _spawnCoordinateBuffer = new();

        /// <summary>
        /// ローカルプレイヤーが所有するユニットです。
        /// </summary>
        private UnitBehavior _localUnit;

        public int ControlledUnitId => _localUnit != null ? _localUnit.PlayerId : LocalPlayerId;

        public bool IsInitialized
        {
            get
            {
                return _playerService != null &&
                    _unitFactory != null;
            }
        }

        public int LocalPlayerId
        {
            get
            {
                return _playerService != null
                    ? _playerService.LocalPlayerId
                    : k_InvalidPlayerId;
            }
        }

        public UnitBehavior LocalUnit
        {
            get
            {
                return _localUnit;
            }
        }

        public bool HasLocalUnit
        {
            get
            {
                return _localUnit != null;
            }
        }

        public UnitBehaviorBase LocalMacroUnitBehavior
        {
            get
            {
                return _localUnit != null
                    ? _localUnit.GetMacroUnitBehavior()
                    : null;
            }
        }

        public MicroUnitBehavior LocalMicroUnitBehavior
        {
            get
            {
                return _localUnit != null
                    ? _localUnit.GetMicroUnitBehavior()
                    : null;
            }
        }

        public UnitBehaviorBase LocalTacticalMapUnitBehavior
        {
            get
            {
                return _localUnit != null
                    ? _localUnit.GetTacticalMapUnitBehavior()
                    : null;
            }
        }

        /// <summary>
        /// UnitManagerの共有インスタンスを設定します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Destroy(
                    gameObject);

                return;
            }

            Instance =
                this;
        }

        /// <summary>
        /// 共有インスタンスを解除します。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance ==
                this)
            {
                Instance =
                    null;
            }
        }

        /// <summary>
        /// 再スポーン待機時間を更新します。
        /// </summary>
        private void Update()
        {
            UpdateRespawnRequests();
        }

        /// <summary>
        /// プレイヤーサービスとユニットFactoryを設定します。
        /// </summary>
        /// <param name="playerService">
        /// 現在の通信方式に対応したプレイヤーサービスです。
        /// </param>
        /// <param name="unitFactory">
        /// 現在の通信方式に対応したユニットFactoryです。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// いずれかの引数が<c>null</c>の場合に発生します。
        /// </exception>
        public void Initialize(
            IPlayerService playerService,
            IUnitFactory unitFactory)
        {
            if (playerService == null)
            {
                throw new ArgumentNullException(
                    nameof(playerService));
            }

            if (unitFactory == null)
            {
                throw new ArgumentNullException(
                    nameof(unitFactory));
            }

            _playerService =
                playerService;

            _unitFactory =
                unitFactory;

            if (_units.TryGetValue(
                    LocalPlayerId,
                    out UnitBehavior localUnit) &&
                localUnit != null)
            {
                SetLocalUnit(
                    localUnit);
            }
        }

        /// <summary>
        /// プレイヤーIDに対応するユニットを登録します。
        /// </summary>
        /// <param name="playerId">
        /// ユニットを所有するプレイヤーIDです。
        /// </param>
        /// <param name="unitObject">
        /// 登録するユニットのGameObjectです。
        /// </param>
        /// <returns>
        /// ユニットを登録できた場合は<c>true</c>です。
        /// </returns>
        public bool RegisterUnit(
            int playerId,
            GameObject unitObject)
        {
            if (playerId <
                0)
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    $"無効なPlayerIdです。PlayerId={playerId}",
                    this);

                return false;
            }

            if (unitObject == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    $"登録するUnitがnullです。PlayerId={playerId}",
                    this);

                return false;
            }

            if (!unitObject.TryGetComponent(
                    out UnitBehavior unitBehavior))
            {
                Debug.LogError(
                    $"{nameof(UnitManager)}: " +
                    $"{unitObject.name}に{nameof(UnitBehavior)}がありません。",
                    unitObject);

                return false;
            }

            if (_units.TryGetValue(
                    playerId,
                    out UnitBehavior currentUnit) &&
                currentUnit != null &&
                currentUnit != unitBehavior)
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    $"PlayerId={playerId}のUnitを置き換えます。" +
                    $" Old={currentUnit.name}, New={unitBehavior.name}",
                    this);
            }

            _units[playerId] =
                unitBehavior;

            if (playerId ==
                LocalPlayerId)
            {
                SetLocalUnit(
                    unitBehavior);
            }

            return true;
        }

        /// <summary>
        /// 指定されたプレイヤーのユニット登録を解除します。
        /// </summary>
        /// <param name="playerId">
        /// 登録を解除するプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 登録を解除できた場合は<c>true</c>です。
        /// </returns>
        public bool UnregisterUnit(
            int playerId)
        {
            if (!_units.Remove(
                    playerId))
            {
                return false;
            }

            if (_localUnit != null &&
                playerId ==
                _localUnit.PlayerId)
            {
                _localUnit =
                    null;
            }

            return true;
        }

        /// <summary>
        /// 指定されたプレイヤーのユニットを取得します。
        /// </summary>
        public bool TryGetUnit(
            int playerId,
            out UnitBehavior unitBehavior)
        {
            if (_units.TryGetValue(
                    playerId,
                    out unitBehavior) &&
                unitBehavior != null)
            {
                return true;
            }

            unitBehavior =
                null;

            return false;
        }

        /// <summary>
        /// 指定されたプレイヤーのユニットGameObjectを取得します。
        /// </summary>
        public GameObject GetUnitObject(
            int playerId)
        {
            return TryGetUnit(
                    playerId,
                    out UnitBehavior unitBehavior)
                ? unitBehavior.gameObject
                : null;
        }

        /// <summary>
        /// 登録されているすべての有効なユニットを列挙します。
        /// </summary>
        public IEnumerable<UnitBehavior> GetAllUnitBehaviors()
        {
            foreach (
                KeyValuePair<int, UnitBehavior> pair in
                _units)
            {
                if (pair.Value != null)
                {
                    yield return pair.Value;
                }
            }
        }

        /// <summary>
        /// 指定チームの生存ユニットが対象を視認しているか確認します。
        /// </summary>
        public bool IsUnitVisibleToTeam(int observerTeamId, int targetPlayerId)
        {
            return TeamVisibilityResolver.IsVisibleToTeam(
                GetVisibilityObservers(),
                observerTeamId,
                targetPlayerId);
        }

        /// <summary>
        /// チーム共有の視認情報と、ローカルプレイヤー向けの対象表示を更新します。
        /// </summary>
        public void RefreshTeamVisibility(int observerTeamId, int targetPlayerId)
        {
            bool isVisible = IsUnitVisibleToTeam(observerTeamId, targetPlayerId);

            foreach (UnitBehavior observer in GetAllUnitBehaviors())
            {
                if (observer.GetTeamId() != observerTeamId)
                {
                    continue;
                }

                TeamStatus team = observer.GetTeamStatus();

                if (isVisible)
                {
                    team.AddVisiblePlayer(targetPlayerId);
                }
                else
                {
                    team.RemoveVisiblePlayer(targetPlayerId);
                }
            }

            if (TryGetUnit(targetPlayerId, out UnitBehavior target))
            {
                target.RefreshStealthPresentation();
            }
        }

        /// <summary>
        /// 有効な観測者の個別視認情報を列挙します。
        /// </summary>
        private IEnumerable<UnitStatus> GetVisibilityObservers()
        {
            foreach (UnitBehavior unit in GetAllUnitBehaviors())
            {
                MicroUnitBehavior micro = unit.GetMicroUnitBehavior();

                if (micro != null && micro.gameObject.activeInHierarchy)
                {
                    yield return micro.GetUnitStatus();
                }
            }
        }

        /// <summary>
        /// 指定されたチーム以外のユニットを列挙します。
        /// </summary>
        public IEnumerable<UnitBehavior> GetEnemyUnits(
            int teamId)
        {
            foreach (UnitBehavior unitBehavior in
                    GetAllUnitBehaviors())
            {
                UnitStatus unitStatus =
                    unitBehavior.GetUnitStatus();

                if (unitStatus == null ||
                    unitStatus.TeamId ==
                    teamId)
                {
                    continue;
                }

                yield return unitBehavior;
            }
        }

        /// <summary>
        /// 指定されたプレイヤーをAI制御対象として登録します。
        /// </summary>
        /// <param name="playerId">
        /// AI制御対象として登録するプレイヤーIDです。
        /// </param>
        /// <param name="canMove">
        /// AIによる移動を許可するかどうかです。
        /// </param>
        /// <param name="canUseWeapons">
        /// AIによる武器使用を許可するかどうかです。
        /// </param>
        public void RegisterAIControlledUnit(
            int playerId,
            bool canMove = true,
            bool canUseWeapons = true)
        {
            if (playerId < 0)
            {
                return;
            }

            _aiControlledPlayerIds.Add(
                playerId);

            _aiMoveEnabledByPlayerId[playerId] =
                canMove;

            _aiWeaponUsageEnabledByPlayerId[playerId] =
                canUseWeapons;
        }

        /// <summary>
        /// 指定されたプレイヤーをAI制御対象から解除します。
        /// </summary>
        public void UnregisterAIControlledUnit(
            int playerId)
        {
            SoloAiDirector.Unregister(playerId);
            _aiControlledPlayerIds.Remove(
                playerId);

            _aiMoveEnabledByPlayerId.Remove(
                playerId);

            _aiWeaponUsageEnabledByPlayerId.Remove(
                playerId);
        }

        /// <summary>
        /// 指定されたプレイヤーがAI制御対象かどうかを確認します。
        /// </summary>
        public bool IsAIControlledUnit(
            int playerId)
        {
            return _aiControlledPlayerIds.Contains(
                playerId);
        }

        /// <summary>
        /// 指定されたAIユニットの移動可否を設定します。
        /// </summary>
        public bool SetAIMoveEnabled(
            int playerId,
            bool isEnabled)
        {
            if (!_aiControlledPlayerIds.Contains(
                    playerId))
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "AI登録されていないため移動設定を変更できません。" +
                    $" PlayerId={playerId}",
                    this);

                return false;
            }

            _aiMoveEnabledByPlayerId[playerId] =
                isEnabled;

            return true;
        }

        /// <summary>
        /// 指定されたAIユニットが移動可能かどうかを取得します。
        /// </summary>
        public bool IsAIMoveEnabled(
            int playerId)
        {
            if (!_aiControlledPlayerIds.Contains(
                    playerId))
            {
                return false;
            }

            return !_aiMoveEnabledByPlayerId.TryGetValue(
                    playerId,
                    out bool isEnabled) ||
                isEnabled;
        }

        /// <summary>
        /// 指定されたAIユニットの
        /// 武器使用可否を設定します。
        /// </summary>
        /// <param name="playerId">
        /// 設定するAIプレイヤーのIDです。
        /// </param>
        /// <param name="isEnabled">
        /// 武器使用を許可する場合は<c>true</c>です。
        /// </param>
        /// <returns>
        /// 設定できた場合は<c>true</c>です。
        /// </returns>
        public bool SetAIWeaponUsageEnabled(
            int playerId,
            bool isEnabled)
        {
            if (!_aiControlledPlayerIds.Contains(
                    playerId))
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "AI登録されていないため" +
                    "武器使用設定を変更できません。" +
                    $" PlayerId={playerId}",
                    this);

                return false;
            }

            _aiWeaponUsageEnabledByPlayerId[playerId] =
                isEnabled;

            return true;
        }

        /// <summary>
        /// 指定されたAIユニットが
        /// 武器を使用可能かどうかを取得します。
        /// </summary>
        /// <param name="playerId">
        /// 確認するAIプレイヤーのIDです。
        /// </param>
        /// <returns>
        /// 武器を使用可能な場合は<c>true</c>です。
        /// </returns>
        public bool IsAIWeaponUsageEnabled(
            int playerId)
        {
            if (!_aiControlledPlayerIds.Contains(
                    playerId))
            {
                return false;
            }

            return !_aiWeaponUsageEnabledByPlayerId.TryGetValue(
                    playerId,
                    out bool isEnabled) ||
                isEnabled;
        }

        /// <summary>
        /// 現在のクライアントが指定ユニットの
        /// ゲームロジックを実行できるかどうかを判定します。
        /// </summary>
        public bool CanExecuteUnitLogic(
            int playerId)
        {
            if (playerId <
                0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 指定されたユニットへAI入力を設定します。
        /// </summary>
        /// <param name="playerId">
        /// AI入力を設定するプレイヤーIDです。
        /// </param>
        /// <param name="canMove">
        /// AIによる移動を許可するかどうかです。
        /// </param>
        /// <param name="canUseWeapons">
        /// AIによる武器使用を許可するかどうかです。
        /// </param>
        /// <returns>
        /// AI入力を設定できた場合は<c>true</c>です。
        /// </returns>
        public bool EnableAI(
            int playerId,
            bool canMove = true,
            bool canUseWeapons = true)
        {
            if (!TryGetUnit(
                    playerId,
                    out UnitBehavior unitBehavior))
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "AIを有効化するUnitが存在しません。" +
                    $" PlayerId={playerId}",
                    this);

                return false;
            }

            MicroUnitBehavior microUnitBehavior =
                unitBehavior.GetMicroUnitBehavior();

            if (microUnitBehavior == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "AIを有効化するMicroUnitBehaviorがありません。" +
                    $" PlayerId={playerId}",
                    unitBehavior);

                return false;
            }

            RegisterAIControlledUnit(
                playerId,
                canMove,
                canUseWeapons);

            microUnitBehavior.SetUnitInput(
                new AIUnitInput(
                    unitBehavior));

            return true;
        }

        /// <summary>
        /// 指定されたユニットのAI入力を解除します。
        /// </summary>
        public bool DisableAI(
            int playerId)
        {
            bool inputCleared =
                false;

            if (TryGetUnit(
                    playerId,
                    out UnitBehavior unitBehavior))
            {
                MicroUnitBehavior microUnitBehavior =
                    unitBehavior.GetMicroUnitBehavior();

                if (microUnitBehavior != null)
                {
                    microUnitBehavior.ClearUnitInput();
                    inputCleared = true;
                }
            }

            UnregisterAIControlledUnit(
                playerId);

            return inputCleared;
        }

        /// <summary>
        /// ローカルプレイヤーの視点から見た，
        /// 全ユニットの表示状態を更新します。
        /// </summary>
        public bool RefreshLocalPresentation()
        {
            if (_localUnit == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "ローカルUnitが設定されていません。",
                    this);

                return false;
            }

            RefreshAllLocalPresentations();

            return true;
        }

        /// <summary>
        /// 指定されたプレイヤーのチームIDを取得します。
        /// </summary>
        public int GetTeamId(
            int playerId)
        {
            if (_playerService == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "PlayerServiceが初期化されていません。",
                    this);

                return k_InvalidTeamId;
            }

            return _playerService.GetTeamId(
                playerId);
        }

        /// <summary>
        /// PlayerInfo一覧からユニットを生成します。
        /// 座標が未指定の場合はランダムな有効座標を使用します。
        /// </summary>
        public void CreateUnits(
            IEnumerable<PlayerInfo> players)
        {
            if (players == null)
            {
                return;
            }

            List<PlayerInfo> playerList =
                new List<PlayerInfo>();

            foreach (PlayerInfo player in
                players)
            {
                if (player != null)
                {
                    playerList.Add(
                        player);
                }
            }

            HashSet<Vector2Int> usedCoordinates =
                new HashSet<Vector2Int>();

            foreach (PlayerInfo player in
                playerList)
            {
                if (player.Coordinate ==
                    Vector2Int.zero)
                {
                    continue;
                }

                if (!IsValidBoardCoordinate(
                        player.Coordinate))
                {
                    Debug.LogError(
                        $"{nameof(UnitManager)}: " +
                        "指定されたスポーン座標が無効です。" +
                        $" PlayerId={player.PlayerId}," +
                        $" Coordinate={player.Coordinate}",
                        this);

                    continue;
                }

                usedCoordinates.Add(
                    player.Coordinate);
            }

            foreach (PlayerInfo player in
                playerList)
            {
                Vector2Int spawnCoordinate =
                    player.Coordinate;

                if (spawnCoordinate ==
                    Vector2Int.zero)
                {
                    if (!TryGetRandomSpawnCoordinate(
                            usedCoordinates,
                            out spawnCoordinate))
                    {
                        Debug.LogError(
                            $"{nameof(UnitManager)}: " +
                            "ランダムスポーン地点を取得できません。" +
                            $" PlayerId={player.PlayerId}",
                            this);

                        continue;
                    }
                }

                CreateUnit(
                    player.PlayerId,
                    player.CharacterName,
                    spawnCoordinate.x,
                    spawnCoordinate.y,
                    player.Rotation,
                    player.InitialStats);
            }
        }

        /// <summary>
        /// ローカルプレイヤーのユニットを生成します。
        /// </summary>
        /// <param name="spawnX">
        /// 明示的に指定するスポーン座標のX値です。
        /// </param>
        /// <param name="spawnY">
        /// 明示的に指定するスポーン座標のY値です。
        /// </param>
        /// <returns>
        /// 生成されたユニットです。
        /// </returns>
        public UnitBehavior CreateLocalUnit(
            int? spawnX = null,
            int? spawnY = null)
        {
            if (!EnsureInitialized())
            {
                return null;
            }

            if (spawnX.HasValue !=
                spawnY.HasValue)
            {
                Debug.LogError(
                    $"{nameof(UnitManager)}: " +
                    "スポーン座標はXとYの両方を指定してください。",
                    this);

                return null;
            }

            Vector2Int spawnCoordinate;

            if (spawnX.HasValue &&
                spawnY.HasValue)
            {
                spawnCoordinate =
                    new Vector2Int(
                        spawnX.Value,
                        spawnY.Value);

                if (!IsValidBoardCoordinate(
                        spawnCoordinate))
                {
                    Debug.LogError(
                        $"{nameof(UnitManager)}: " +
                        $"指定されたスポーン座標が無効です。" +
                        $" Coordinate={spawnCoordinate}",
                        this);

                    return null;
                }
            }
            else
            {
                if (!TryGetRandomSpawnCoordinate(
                        new HashSet<Vector2Int>(),
                        out spawnCoordinate))
                {
                    return null;
                }
            }

            int playerId =
                LocalPlayerId;

            if (playerId <
                0)
            {
                Debug.LogError(
                    $"{nameof(UnitManager)}: " +
                    "ローカルPlayerIdを取得できません。",
                    this);

                return null;
            }

            string characterName =
                _playerService.GetCharacterName(
                    playerId);

            return CreateUnit(
                playerId,
                characterName,
                spawnCoordinate.x,
                spawnCoordinate.y,
                Vector3.zero);
        }

        /// <summary>
        /// 指定された情報からユニットを生成します。
        /// </summary>
        public UnitBehavior CreateUnit(
            int playerId,
            string characterName,
            int x,
            int y,
            Vector3 rotation,
            UnitInitialStats initialStats = null)
        {
            if (!EnsureInitialized())
            {
                return null;
            }

            int teamId =
                _playerService.GetTeamId(
                    playerId);

            int localTeamId =
                _playerService.GetTeamId(
                    LocalPlayerId);

            UnitInitializeData initializeData =
                new()
                {
                    OwnerId =
                        playerId,

                    TeamId =
                        teamId,

                    LocalTeamId =
                        localTeamId,

                    CharacterName =
                        characterName,

                    InitialStats =
                        initialStats,

                    StartX =
                        x,

                    StartY =
                        y,

                    Rotation =
                        rotation
                };

            return CreateUnit(
                initializeData);
        }

        /// <summary>
        /// オンラインプレイヤー一覧から
        /// Host共有用の生成情報を作成します。
        /// </summary>
        public List<UnitInitializeData> BuildOnlinePlayerSpawnData()
        {
            List<UnitInitializeData> result =
                new();

            HashSet<Vector2Int> usedCoordinates =
                new();

            if (!EnsureInitialized())
            {
                return result;
            }

            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                onlineService.Players == null)
            {
                AddLocalPlayerSpawnData(
                    result,
                    usedCoordinates);

                return result;
            }

            List<OnlinePlayer> onlinePlayers =
                new();

            foreach (OnlinePlayer player in
                    onlineService.Players)
            {
                if (player != null)
                {
                    onlinePlayers.Add(
                        player);
                }
            }

            onlinePlayers.Sort(
                static (
                    left,
                    right) =>
                    left.PlayerId.CompareTo(
                        right.PlayerId));

            int localTeamId =
                GetTeamId(
                    LocalPlayerId);

            foreach (OnlinePlayer player in
                    onlinePlayers)
            {
                int teamId =
                    _playerService.GetTeamId(
                        player.PlayerId);

                string characterName =
                    _playerService.GetCharacterName(
                        player.PlayerId);

                if (teamId <
                        0 ||
                    string.IsNullOrWhiteSpace(
                        characterName))
                {
                    Debug.LogWarning(
                        $"{nameof(UnitManager)}: " +
                        "生成に必要なプレイヤー情報が未同期です。" +
                        $" PlayerId={player.PlayerId}," +
                        $" TeamId={teamId}," +
                        $" CharacterName={characterName}",
                        this);

                    continue;
                }

                if (!TryGetRandomSpawnCoordinate(
                        usedCoordinates,
                        out Vector2Int coordinate))
                {
                    break;
                }

                result.Add(
                    new UnitInitializeData
                    {
                        OwnerId =
                            player.PlayerId,

                        TeamId =
                            teamId,

                        LocalTeamId =
                            localTeamId,

                        CharacterName =
                            characterName,

                        StartX =
                            coordinate.x,

                        StartY =
                            coordinate.y,

                        Rotation =
                            Vector3.zero
                    });
            }

            return result;
        }

        /// <summary>
        /// 初期化情報一覧からユニットを生成します。
        /// </summary>
        public void CreateUnits(
            IEnumerable<UnitInitializeData> initializeDataList)
        {
            if (initializeDataList == null)
            {
                return;
            }

            foreach (UnitInitializeData initializeData in
                    initializeDataList)
            {
                if (initializeData != null)
                {
                    CreateUnit(
                        initializeData);
                }
            }
        }

        /// <summary>
        /// 初期化情報からユニットを生成します。
        /// </summary>
        /// <returns>
        /// 生成または取得されたユニットです。
        /// </returns>
        public UnitBehavior CreateUnit(
            UnitInitializeData initializeData)
        {
            if (!EnsureInitialized())
            {
                return null;
            }

            if (!ValidateInitializeData(
                    initializeData))
            {
                return null;
            }

            if (TryGetUnit(
                    initializeData.OwnerId,
                    out UnitBehavior existingUnit))
            {
                Debug.Log(
                    $"{nameof(UnitManager)}: " +
                    "既にUnitが存在するため生成をスキップします。" +
                    $" PlayerId={initializeData.OwnerId}",
                    existingUnit);

                return existingUnit;
            }

            UnitBehavior unitBehavior;

            try
            {
                unitBehavior =
                    _unitFactory.Create(
                        initializeData);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(UnitManager)}: " +
                    "Unit生成中に例外が発生しました。" +
                    $" PlayerId={initializeData.OwnerId}," +
                    $" Character={initializeData.CharacterName}\n" +
                    exception,
                    this);

                return null;
            }

            if (unitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(UnitManager)}: " +
                    "UnitFactoryがnullを返しました。" +
                    $" PlayerId={initializeData.OwnerId}",
                    this);

                return null;
            }

            RegisterUnit(
                initializeData.OwnerId,
                unitBehavior.gameObject);

            if (initializeData.OwnerId ==
                LocalPlayerId)
            {
                SetLocalUnit(
                    unitBehavior);
            }

            return unitBehavior;
        }

        /// <summary>
        /// ローカルで管理しているユニットの
        /// 再スポーンを予約します。
        /// </summary>
        /// <param name="playerId">
        /// 再スポーンするプレイヤーIDです。
        /// </param>
        /// <param name="characterName">
        /// 再スポーン後に使用するキャラクター名です。
        /// </param>
        /// <param name="respawnDelay">
        /// 再スポーンまでの待機時間です。
        /// </param>
        /// <returns>
        /// 再スポーンを予約できた場合は<c>true</c>です。
        /// </returns>
        public bool RequestLocalRespawn(
            int playerId,
            string characterName,
            float respawnDelay = 0.0f)
        {
            if (!CanExecuteUnitLogic(
                    playerId))
            {
                return false;
            }

            if (_respawnRequests.ContainsKey(
                    playerId))
            {
                return false;
            }

            if (!TryGetUnit(
                    playerId,
                    out UnitBehavior oldUnitBehavior))
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "再スポーン対象のUnitが存在しません。" +
                    $" PlayerId={playerId}",
                    this);

                return false;
            }

            MicroUnitBehavior microUnitBehavior =
                oldUnitBehavior.GetMicroUnitBehavior();

            if (microUnitBehavior == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "再スポーン対象にMicroUnitBehaviorがありません。" +
                    $" PlayerId={playerId}",
                    oldUnitBehavior);

                return false;
            }

            UnitStatus unitStatus =
                oldUnitBehavior.GetUnitStatus();

            if (string.IsNullOrWhiteSpace(
                    characterName))
            {
                characterName =
                    unitStatus?.CharacterData != null
                        ? unitStatus.CharacterData.UnitName
                        : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(
                    characterName))
            {
                return false;
            }

            Vector2Int coordinate =
                microUnitBehavior.GetCurrentCoordinate();

            GameObject model =
                microUnitBehavior.GetModel();

            Vector3 rotation =
                model != null
                    ? model.transform.eulerAngles
                    : microUnitBehavior.transform.eulerAngles;

            if (playerId ==
                LocalPlayerId)
            {
                rotation =
                    Vector3.zero;
            }

            RespawnBot respawnBot = oldUnitBehavior.GetComponent<RespawnBot>();
            RespawnRequest request =
                new(
                    playerId,
                    characterName,
                    coordinate,
                    rotation,
                    Mathf.Max(
                        0.0f,
                        respawnDelay),
                    IsAIControlledUnit(
                        playerId),
                    IsAIMoveEnabled(
                        playerId),
                    IsAIWeaponUsageEnabled(
                        playerId),
                    respawnBot != null && respawnBot.CanRespawn,
                    respawnBot != null ? respawnBot.RespawnDelay : 0.0f);

            _respawnRequests.Add(
                playerId,
                request);

            UnregisterUnit(
                playerId);

            Destroy(
                oldUnitBehavior.gameObject);

            return true;
        }

        /// <summary>
        /// ローカルユニットを設定し、
        /// 全ユニットのローカル向け表示状態を更新します。
        /// </summary>
        private void SetLocalUnit(
            UnitBehavior unitBehavior)
        {
            _localUnit =
                unitBehavior;

            RefreshAllLocalPresentations();
        }

        /// <summary>
        /// 撃破された操作ユニットから、生存する同チームのNPCへ操作を引き継ぎます。
        /// </summary>
        /// <remarks>
        /// 接続プレイヤーIDとユニットの所有IDは変更せず、操作対象だけを変更します。
        /// 他プレイヤーのユニットは候補に含めません。
        /// </remarks>
        public bool HandleUnitDefeated(UnitBehavior defeated)
        {
            if (defeated == null)
            {
                return false;
            }

            UnregisterAIControlledUnit(defeated.PlayerId);

            if (defeated != _localUnit)
            {
                return false;
            }

            var candidates = new List<UnitBehavior>();

            foreach (UnitBehavior candidate in _units.Values)
            {
                if (candidate != null && candidate.isActiveAndEnabled && candidate != defeated &&
                    candidate.GetTeamId() == defeated.GetTeamId() &&
                    candidate.GetUnitStatus() != null && candidate.GetUnitStatus().IsAlive &&
                    candidate.GetMicroUnitBehavior() != null && !candidate.GetMicroUnitBehavior().IsDead &&
                    IsAIControlledUnit(candidate.PlayerId))
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            UnitBehavior successor = candidates[UnityEngine.Random.Range(0, candidates.Count)];

            DisableAI(successor.PlayerId);
            successor.GetMicroUnitBehavior().ReleaseControlInput();
            SetLocalUnit(successor);
            successor.RefreshControlledHud();

            return true;
        }

        /// <summary>
        /// ローカルプレイヤーとのチーム関係に応じて，
        /// 全ユニットのカメラ・マップ・ステルス表示を更新します。
        /// </summary>
        private void RefreshAllLocalPresentations()
        {
            foreach (UnitBehavior registeredUnit in
                    _units.Values)
            {
                if (registeredUnit == null)
                {
                    continue;
                }

                registeredUnit.RefreshLocalPresentation();
            }
        }

        /// <summary>
        /// UnitManagerが初期化済みか確認します。
        /// </summary>
        private bool EnsureInitialized()
        {
            if (IsInitialized)
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(UnitManager)}: " +
                $"{nameof(Initialize)}が呼ばれていません。",
                this);

            return false;
        }

        /// <summary>
        /// 初期化情報がユニット生成に使用できるか確認します。
        /// </summary>
        private bool ValidateInitializeData(
            UnitInitializeData initializeData)
        {
            if (initializeData == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "UnitInitializeDataがnullです。",
                    this);

                return false;
            }

            if (initializeData.OwnerId <
                0)
            {
                return false;
            }

            if (initializeData.TeamId <
                0)
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "TeamIdが確定していません。" +
                    $" PlayerId={initializeData.OwnerId}",
                    this);

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    initializeData.CharacterName))
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "CharacterNameが設定されていません。" +
                    $" PlayerId={initializeData.OwnerId}",
                    this);

                return false;
            }

            if (initializeData.InitialStats != null && !initializeData.InitialStats.IsValid())
            {
                Debug.LogError(
                    $"Unitの生成時ステータスが不正です。PlayerId={initializeData.OwnerId}",
                    this);
                return false;
            }

            return IsValidBoardCoordinate(
                initializeData.StartCoordinate);
        }

        /// <summary>
        /// ローカルプレイヤーの生成情報を追加します。
        /// </summary>
        private void AddLocalPlayerSpawnData(
            ICollection<UnitInitializeData> result,
            HashSet<Vector2Int> usedCoordinates)
        {
            int playerId =
                LocalPlayerId;

            if (playerId <
                0)
            {
                return;
            }

            int teamId =
                _playerService.GetTeamId(
                    playerId);

            string characterName =
                _playerService.GetCharacterName(
                    playerId);

            if (teamId <
                    0 ||
                string.IsNullOrWhiteSpace(
                    characterName))
            {
                return;
            }

            if (!TryGetRandomSpawnCoordinate(
                    usedCoordinates,
                    out Vector2Int coordinate))
            {
                return;
            }

            result.Add(
                new UnitInitializeData
                {
                    OwnerId =
                        playerId,

                    TeamId =
                        teamId,

                    LocalTeamId =
                        teamId,

                    CharacterName =
                        characterName,

                    StartX =
                        coordinate.x,

                    StartY =
                        coordinate.y,

                    Rotation =
                        Vector3.zero
                });
        }

        /// <summary>
        /// 他Unitから一定距離以上離れた
        /// 有効なスポーン座標をランダムに取得します。
        /// </summary>
        private bool TryGetRandomSpawnCoordinate(
            HashSet<Vector2Int> usedCoordinates,
            out Vector2Int coordinate)
        {
            coordinate =
                default;

            BoardManager boardManager =
                BoardManager.Instance;

            if (boardManager == null)
            {
                Debug.LogError(
                    $"{nameof(UnitManager)}: " +
                    $"{nameof(BoardManager)}が存在しません。",
                    this);

                return false;
            }

            _spawnCoordinateBuffer.Clear();

            int boardWidth =
                boardManager.GetBoardWidth();

            int boardHeight =
                boardManager.GetBoardHeight();

            for (int x = 1;
                x <= boardWidth;
                x++)
            {
                for (int y = 1;
                    y <= boardHeight;
                    y++)
                {
                    Vector2Int candidate =
                        new Vector2Int(
                            x,
                            y);

                    if (!boardManager.ContainsHexCell(
                            x,
                            y))
                    {
                        continue;
                    }

                    if (usedCoordinates.Contains(
                            candidate))
                    {
                        continue;
                    }

                    if (!IsSpawnDistanceValid(
                            boardManager,
                            candidate,
                            usedCoordinates))
                    {
                        continue;
                    }

                    _spawnCoordinateBuffer.Add(
                        candidate);
                }
            }

            if (_spawnCoordinateBuffer.Count ==
                0)
            {
                Debug.LogWarning(
                    $"{nameof(UnitManager)}: " +
                    "スポーン可能地点がありません。" +
                    $" MinimumDistance=" +
                    $"{k_MinimumRandomSpawnDistance}",
                    this);

                return false;
            }

            int randomIndex =
                UnityEngine.Random.Range(
                    0,
                    _spawnCoordinateBuffer.Count);

            coordinate =
                _spawnCoordinateBuffer[
                    randomIndex];

            usedCoordinates.Add(
                coordinate);

            return true;
        }

        /// <summary>
        /// 指定された候補座標が、
        /// 既存スポーン地点から必要な距離以上
        /// 離れているか確認します。
        /// </summary>
        private static bool IsSpawnDistanceValid(
            BoardManager boardManager,
            Vector2Int candidate,
            IEnumerable<Vector2Int> usedCoordinates)
        {
            if (k_MinimumRandomSpawnDistance <=
                0)
            {
                return true;
            }

            foreach (Vector2Int usedCoordinate in
                usedCoordinates)
            {
                if (IsWithinHexDistance(
                        boardManager,
                        candidate,
                        usedCoordinate,
                        k_MinimumRandomSpawnDistance - 1))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 2つのHexが指定マス数以内に存在するか確認します。
        /// </summary>
        private static bool IsWithinHexDistance(
            BoardManager boardManager,
            Vector2Int start,
            Vector2Int target,
            int maximumDistance)
        {
            if (start ==
                target)
            {
                return true;
            }

            if (maximumDistance <=
                0)
            {
                return false;
            }

            Queue<Vector2Int> queue =
                new Queue<Vector2Int>();

            Dictionary<Vector2Int, int> distances =
                new Dictionary<Vector2Int, int>();

            queue.Enqueue(
                start);

            distances[
                start] =
                    0;

            while (queue.Count >
                0)
            {
                Vector2Int current =
                    queue.Dequeue();

                int currentDistance =
                    distances[current];

                if (currentDistance >=
                    maximumDistance)
                {
                    continue;
                }

                foreach (Vector2Int adjacent in
                    boardManager
                        .GetAdjacentHexCellCoordinates(
                            current.x,
                            current.y))
                {
                    if (!boardManager.ContainsHexCell(
                            adjacent.x,
                            adjacent.y))
                    {
                        continue;
                    }

                    if (distances.ContainsKey(
                            adjacent))
                    {
                        continue;
                    }

                    int nextDistance =
                        currentDistance + 1;

                    if (adjacent ==
                        target)
                    {
                        return true;
                    }

                    distances[
                        adjacent] =
                            nextDistance;

                    queue.Enqueue(
                        adjacent);
                }
            }

            return false;
        }

        /// <summary>
        /// 指定された座標がボード上の有効なHexか確認します。
        /// </summary>
        private static bool IsValidBoardCoordinate(
            Vector2Int coordinate)
        {
            BoardManager boardManager =
                BoardManager.Instance;

            return boardManager != null &&
                boardManager.ContainsHexCell(
                    coordinate.x,
                    coordinate.y);
        }

        /// <summary>
        /// 再スポーン待機時間を更新します。
        /// </summary>
        private void UpdateRespawnRequests()
        {
            if (_respawnRequests.Count ==
                0)
            {
                return;
            }

            _respawnExecutionBuffer.Clear();

            foreach (
                KeyValuePair<int, RespawnRequest> pair in
                _respawnRequests)
            {
                pair.Value.RemainingTime -=
                    Time.deltaTime;

                if (pair.Value.RemainingTime <=
                    0.0f)
                {
                    _respawnExecutionBuffer.Add(
                        pair.Key);
                }
            }

            foreach (int playerId in
                    _respawnExecutionBuffer)
            {
                if (!_respawnRequests.TryGetValue(
                        playerId,
                        out RespawnRequest request))
                {
                    continue;
                }

                _respawnRequests.Remove(
                    playerId);

                _ = ExecuteRespawnAsync(
                    request);
            }

            _respawnExecutionBuffer.Clear();
        }

        /// <summary>
        /// 再スポーン情報からユニットを生成します。
        /// </summary>
        /// <remarks>
        /// UnitManagerの破棄によるキャンセルです。
        /// </remarks>
        private async Awaitable ExecuteRespawnAsync(
                    RespawnRequest request)
        {
            try
            {
                if (_playerService is
                    LocalPlayerService localPlayerService)
                {
                    localPlayerService.ChangeCharacter(
                        request.PlayerId,
                        request.CharacterName);
                }

                Vector3 spawnRotation =
                    CalculateFacingRotationTowardLocalPlayer(
                        request.Coordinate,
                        request.Rotation);

                UnitBehavior unitBehavior =
                    CreateUnit(
                        request.PlayerId,
                        request.CharacterName,
                        request.Coordinate.x,
                        request.Coordinate.y,
                        spawnRotation);

                if (unitBehavior == null)
                {
                    Debug.LogError(
                        $"{nameof(UnitManager)}: " +
                        "再スポーンに失敗しました。" +
                        $" PlayerId={request.PlayerId}",
                        this);

                    return;
                }

                if (request.CanAutoRespawn)
                {
                    RespawnBot respawnBot = unitBehavior.GetComponent<RespawnBot>();
                    if (respawnBot == null)
                    {
                        respawnBot = unitBehavior.gameObject.AddComponent<RespawnBot>();
                    }

                    respawnBot.SetRespawnDelay(request.AutoRespawnDelay);
                    respawnBot.SetRespawnEnabled(true);
                    respawnBot.enabled = true;
                }

                if (!request.WasAIControlled)
                {
                    return;
                }

                if (!await WaitForPranamInitializationAsync(
                        unitBehavior))
                {
                    Debug.LogError(
                        $"{nameof(UnitManager)}: " +
                        "再スポーンしたAIのPranam初期化に失敗しました。" +
                        $" PlayerId={request.PlayerId}",
                        unitBehavior);

                    return;
                }

                bool aiEnabled =
                    EnableAI(
                        request.PlayerId,
                        request.CanAIMove,
                        request.CanUseWeapons);

                if (!aiEnabled)
                {
                    Debug.LogError(
                        $"{nameof(UnitManager)}: " +
                        "再スポーンしたAIを有効化できませんでした。" +
                        $" PlayerId={request.PlayerId}",
                        unitBehavior);

                    return;
                }

                unitBehavior
                    .GetMacroUnitBehavior()?
                    .CancelAiMacroMovement();

                unitBehavior
                    .GetMicroUnitBehavior()?
                    .PrepareForMicroPhase();

                SoloAiDirector aiDirector =
                    SoloAiDirector.EnsureExists();

                aiDirector.PlanMacroPhaseImmediately();

                MatchPhaseManager phaseManager =
                    MatchPhaseManager.Instance;

                if (phaseManager != null &&
                    phaseManager.CurrentPhase ==
                        MatchPhase.Micro)
                {
                    aiDirector
                        .InitializeMicroPhaseImmediately();
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(UnitManager)}: " +
                    "AIユニットの再スポーン処理中に" +
                    "エラーが発生しました。",
                    this);

                Debug.LogException(
                    exception,
                    this);
            }
        }

        /// <summary>
        /// 指定されたユニットのPranamと扇形範囲の
        /// 初期化完了を待機します。
        /// </summary>
        private async Awaitable<bool> WaitForPranamInitializationAsync(
            UnitBehavior unitBehavior)
        {
            TacticalMapUnitBehavior tacticalMapUnitBehavior =
                unitBehavior?.GetTacticalMapUnitBehavior();

            if (tacticalMapUnitBehavior == null)
            {
                return false;
            }

            float timeoutAt =
                Time.realtimeSinceStartup +
                k_PranamInitializationTimeoutSeconds;

            while (!tacticalMapUnitBehavior.IsPranamInitialized)
            {
                destroyCancellationToken
                    .ThrowIfCancellationRequested();

                if (tacticalMapUnitBehavior
                        .HasPranamInitializationFailed)
                {
                    return false;
                }

                if (Time.realtimeSinceStartup >=
                    timeoutAt)
                {
                    Debug.LogError(
                        $"{nameof(UnitManager)}: " +
                        "Pranam初期化の待機がタイムアウトしました。" +
                        $" PlayerId={unitBehavior.PlayerId}," +
                        $" Timeout={k_PranamInitializationTimeoutSeconds:0.0}s",
                        unitBehavior);

                    return false;
                }

                await Awaitable.NextFrameAsync(
                    destroyCancellationToken);
            }

            return true;
        }

        /// <summary>
        /// 指定されたユニットを
        /// ローカルプレイヤーの方向へ向けます。
        /// </summary>
        /// <param name="unitBehavior">
        /// 向きを変更するユニットです。
        /// </param>
        /// <summary>
        /// 再スポーン位置からローカルプレイヤーを向く回転を計算します。
        /// </summary>
        /// <param name="spawnCoordinate">
        /// 再スポーンするユニットの座標です。
        /// </param>
        /// <param name="fallbackRotation">
        /// 回転を計算できない場合に使用する回転です。
        /// </param>
        /// <returns>
        /// Pranam初期化前に適用するY軸回転です。
        /// </returns>
        private Vector3 CalculateFacingRotationTowardLocalPlayer(
            Vector2Int spawnCoordinate,
            Vector3 fallbackRotation)
        {
            if (_localUnit == null)
            {
                return fallbackRotation;
            }

            MicroUnitBehavior localMicroUnit =
                _localUnit.GetMicroUnitBehavior();

            BoardManager boardManager =
                BoardManager.Instance;

            if (localMicroUnit == null ||
                boardManager == null)
            {
                return fallbackRotation;
            }

            Vector2Int targetCoordinate =
                localMicroUnit.GetCurrentCoordinate();

            Vector3 spawnPosition =
                boardManager.GetMapHexCellPosition(
                    spawnCoordinate.x,
                    spawnCoordinate.y);

            Vector3 targetPosition =
                boardManager.GetMapHexCellPosition(
                    targetCoordinate.x,
                    targetCoordinate.y);

            Vector3 direction =
                targetPosition -
                spawnPosition;

            direction.y =
                0.0f;

            if (direction.sqrMagnitude <=
                0.0001f)
            {
                return fallbackRotation;
            }

            Quaternion rotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);

            return new Vector3(
                0.0f,
                rotation.eulerAngles.y,
                0.0f);
        }

        /// <summary>
        /// 再スポーン待機中の情報です。
        /// </summary>
        private sealed class RespawnRequest
        {
            public int PlayerId
            {
                get;
            }

            public string CharacterName
            {
                get;
            }

            public Vector2Int Coordinate
            {
                get;
            }

            public Vector3 Rotation
            {
                get;
            }

            public float RemainingTime
            {
                get;
                set;
            }

            public bool WasAIControlled
            {
                get;
            }

            public bool CanAIMove
            {
                get;
            }

            public bool CanUseWeapons
            {
                get;
            }

            public bool CanAutoRespawn { get; }

            public float AutoRespawnDelay { get; }

            /// <summary>
            /// 再生成に必要な配置・AI・自動再スポーン設定を保存します。
            /// </summary>
            public RespawnRequest(
                int playerId,
                string characterName,
                Vector2Int coordinate,
                Vector3 rotation,
                float remainingTime,
                bool wasAIControlled,
                bool canAIMove,
                bool canUseWeapons,
                bool canAutoRespawn,
                float autoRespawnDelay)
            {
                PlayerId =
                    playerId;

                CharacterName =
                    characterName;

                Coordinate =
                    coordinate;

                Rotation =
                    rotation;

                RemainingTime =
                    remainingTime;

                WasAIControlled =
                    wasAIControlled;

                CanAIMove =
                    canAIMove;

                CanUseWeapons =
                    canUseWeapons;

                CanAutoRespawn = canAutoRespawn;
                AutoRespawnDelay = autoRespawnDelay;
            }
        }
    }
}
