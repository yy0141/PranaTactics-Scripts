using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.Match;
using PranaTactics.Networking;
using PranaTactics.Unit;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// キャラクター選択画面のUI、選択状態、タイマーおよび
    /// オンラインプレイヤー間のキャラクター同期を管理します。
    /// </summary>
    /// <remarks>
    /// ローカルプレイヤーと同じチームのプレイヤーに対応する
    /// キャラクター表示スロットを生成し、
    /// プレイヤープロパティに保存されたキャラクター名をUIへ反映します。
    ///
    /// EOSなどでチームIDやプレイヤープロパティの同期が遅れる場合に備え、
    /// イベント通知に加えて一定間隔で状態を再確認します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterSelectUIManager :
        CharacterSelectUIManagerBase
    {
        /// <summary>
        /// キャラクター情報を再確認する既定間隔です。
        /// </summary>
        private const float k_DefaultCharacterPollInterval = 0.25f;

        /// <summary>
        /// キャラクター選択画面の既定制限時間です。
        /// </summary>
        private const int k_DefaultSelectionTime = 60;

        /// <summary>
        /// プレイヤーごとのキャラクター表示情報です。
        /// </summary>
        public sealed class SelectCharacter
        {
            public int PlayerId
            {
                get;
                set;
            }

            public UnitParams CharacterData
            {
                get;
                set;
            }

            public GameObject Model
            {
                get;
                set;
            }

            [Obsolete(
    "Use PlayerId instead.")]
            public int PLAYER_ID
            {
                get
                {
                    return PlayerId;
                }

                set
                {
                    PlayerId = value;
                }
            }

            [Obsolete(
    "Use CharacterData instead.")]
            public UnitParams characterData
            {
                get
                {
                    return CharacterData;
                }

                set
                {
                    CharacterData = value;
                }
            }

            [Obsolete(
    "Use Model instead.")]
            public GameObject model
            {
                get
                {
                    return Model;
                }

                set
                {
                    Model = value;
                }
            }

            public SelectCharacter(
    int playerId)
            {
                PlayerId =
                    playerId;
            }
        }

        /// <summary>
        /// キャラクターの大型画像を管理するUIです。
        /// </summary>
        [Header("Character UI")]
        [SerializeField]
        private SetImage _characterImage;

        /// <summary>
        /// キャラクタータイプのアイコンを管理するUIです。
        /// </summary>
        [SerializeField]
        private SetImage _iconImage;

        /// <summary>
        /// キャラクター名を管理するUIです。
        /// </summary>
        [SerializeField]
        private SetTextMeshProUGUI _characterNameText;

        /// <summary>
        /// キャラクターの3Dプレビューモデルを管理します。
        /// </summary>
        [SerializeField]
        private CharacterModelPreview _characterModelPreview;

        /// <summary>
        /// キャラクター選択画面の残り時間を表示するUIです。
        /// </summary>
        [Header("Timer")]
        [SerializeField]
        private ChangeTextMeshProUGUI _timerText;

        /// <summary>
        /// キャラクター選択画面の初期制限時間です。
        /// </summary>
        [Min(0)]
        [SerializeField]
        private int _initialSelectionTime =
            k_DefaultSelectionTime;

        /// <summary>
        /// ネットワーク上のキャラクター情報を再確認する間隔です。
        /// </summary>
        [Header("Synchronization")]
        [Min(0.05f)]
        [SerializeField]
        private float _characterPollInterval =
            k_DefaultCharacterPollInterval;

        /// <summary>
        /// 状態更新の詳細をログへ出力するかどうかを示します。
        /// </summary>
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLogging;

        /// <summary>
        /// キャラクター選択画面のタイマーです。
        /// </summary>
        private MatchTimer _timer;

        /// <summary>
        /// プレイヤーIDごとのキャラクター表示情報です。
        /// </summary>
        private readonly Dictionary<int, SelectCharacter>
            _characterUIs = new();

        /// <summary>
        /// UIスロット順に並んだプレイヤーIDです。
        /// </summary>
        private readonly List<int> _playerIds = new();

        /// <summary>
        /// チーム情報の同期待ちによって、
        /// まだUIへ反映できていないキャラクター名です。
        /// </summary>
        private readonly Dictionary<int, string>
            _pendingCharacterNames = new();

        /// <summary>
        /// 現在UIへ反映済みのキャラクター名です。
        /// </summary>
        private readonly Dictionary<int, string>
            _appliedCharacterNames = new();

        /// <summary>
        /// オンラインイベントの購読解除処理です。
        /// </summary>
        private Action _unregisterOnlineEvents;

        /// <summary>
        /// オンラインサービスの初期化を待つコルーチンです。
        /// </summary>
        private Coroutine _onlineRegistrationCoroutine;

        /// <summary>
        /// キャラクターUIの初期化を待つコルーチンです。
        /// </summary>
        private Coroutine _initializationCoroutine;

        /// <summary>
        /// 次回キャラクター情報を確認する時刻です。
        /// </summary>
        private float _nextCharacterPollTime;

        /// <summary>
        /// ローカルプレイヤーのチームIDです。
        /// </summary>
        private int _localTeamId = -1;

        /// <summary>
        /// ローカルプレイヤーIDです。
        /// </summary>
        private int _localPlayerId = -1;

        /// <summary>
        /// 最後にUIを構築した時点のチームIDです。
        /// </summary>
        private int _lastTeamId =
            int.MinValue;

        /// <summary>
        /// 最後にUIを構築した時点のルーム内プレイヤー数です。
        /// </summary>
        private int _lastPlayerCount = -1;

        /// <summary>
        /// キャラクターUIスロットの初期構築が完了しているかどうかです。
        /// </summary>
        private bool _isUiListBuilt;

        public static CharacterSelectUIManager Instance
        {
            get;
            private set;
        }

        public event Action TimeEnded;

        [Obsolete(
    "Use TimeEnded instead.")]
        public event Action OnTimeEnd
        {
            add
            {
                TimeEnded += value;
            }

            remove
            {
                TimeEnded -= value;
            }
        }

        /// <summary>
        /// 基底クラスとオンライン用シングルトン参照を初期化します。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (!IsPrimaryInstance)
            {
                return;
            }

            Instance =
                this;
        }

        /// <summary>
        /// オンラインイベントの登録と
        /// キャラクターUIの初期化待機を開始します。
        /// </summary>
        private void OnEnable()
        {
            if (!IsPrimaryInstance)
            {
                return;
            }

            StartOnlineRegistration();
            StartUiInitialization();
        }

        /// <summary>
        /// キャラクター選択タイマーを開始します。
        /// </summary>
        private void Start()
        {
            SetTimer(
                _initialSelectionTime);
        }

        /// <summary>
        /// タイマーとキャラクター同期状態を更新します。
        /// </summary>
        private void Update()
        {
            UpdateTimer();
            PollCharacterSelections();
        }

        /// <summary>
        /// 実行中のコルーチンとオンラインイベント購読を解除します。
        /// </summary>
        private void OnDisable()
        {
            StopInitializationCoroutines();
            UnregisterOnlineEvents();
        }

        /// <summary>
        /// タイマーとイベント購読を解除し、
        /// シングルトン参照を解放します。
        /// </summary>
        protected override void OnDestroy()
        {
            StopInitializationCoroutines();
            UnregisterOnlineEvents();
            UnsubscribeTimer();

            if (Instance == this)
            {
                Instance =
                    null;
            }

            base.OnDestroy();
        }

        /// <summary>
        /// キャラクター選択タイマーを設定します。
        /// </summary>
        /// <param name="durationSeconds">
        /// タイマーの初期秒数です。
        /// </param>
        public void SetTimer(
            int durationSeconds)
        {
            UnsubscribeTimer();

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager != null)
            {
                gameFlowManager.SetTimeEnded(
                    false);
            }

            int validDuration =
                Mathf.Max(
                    0,
                    durationSeconds);

            _timer =
                new MatchTimer(
                    validDuration);

            _timer.TimeEnded +=
                HandleTimerEnded;

            UpdateTimerText();
        }

        /// <summary>
        /// オンラインプレイヤープロパティへ
        /// キャラクター選択を反映します。
        /// </summary>
        protected override bool TryApplyCharacterSelection(
            UnitParams characterData)
        {
            if (characterData == null)
            {
                return false;
            }

            string characterName =
                characterData.UnitName?.Trim() ??
                string.Empty;

            if (string.IsNullOrEmpty(
                    characterName))
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "キャラクター名が空です。",
                    this);

                return false;
            }

            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "オンラインサービスが初期化されていません。",
                    this);

                return false;
            }

            SetReadySelectionLocked(
                false);

            OnlinePlayer localPlayer =
                OnlinePlayerUtility.GetLocalPlayer();

            if (localPlayer != null)
            {
                ApplyLocalSelectionProperties(
                    localPlayer,
                    characterName);

                UpdatePlayerCharacter(
                    localPlayer.PlayerId,
                    characterName,
                    true);
            }
            else if (onlineService.LocalPlayerId > 0)
            {
                UpdatePlayerCharacter(
                    onlineService.LocalPlayerId,
                    characterName,
                    true);
            }
            else
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "ローカルプレイヤーIDを取得できません。",
                    this);

                return false;
            }

            onlineService.SetLocalPlayerProperties(
                CreateCharacterSelectionProperties(
                    characterName));

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "キャラクター選択情報を送信しました。" +
                    $" Character={characterName}, Ready=False",
                    this);
            }

            return true;
        }

        /// <summary>
        /// キャラクター名を使用して
        /// オンラインキャラクター選択を反映します。
        /// </summary>
        /// <param name="characterName">
        /// 選択するキャラクター名です。
        /// </param>
        public void SendSelectCharacter(
            string characterName)
        {
            string validCharacterName =
                characterName?.Trim() ??
                string.Empty;

            if (string.IsNullOrEmpty(
                    validCharacterName))
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "キャラクター名が空のため選択できません。",
                    this);

                return;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    $"{nameof(GameDataRepository)}が存在しません。",
                    this);

                return;
            }

            UnitParams characterData =
                repository.GetCharacterData(
                    validCharacterName);

            if (characterData == null)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "キャラクターデータが見つかりません。" +
                    $" Character={validCharacterName}",
                    this);

                return;
            }

            if (TryApplyCharacterSelection(
                    characterData))
            {
                SetCharacterSelected(
                    true);
            }
        }

        /// <summary>
        /// 指定されたプレイヤーのキャラクター表示を更新します。
        /// </summary>
        /// <param name="playerId">
        /// 対象プレイヤーIDです。
        /// </param>
        /// <param name="characterData">
        /// 表示するキャラクターデータです。
        /// </param>
        /// <param name="index">
        /// 表示先となるUIスロットのインデックスです。
        /// </param>
        /// <returns>
        /// 生成されたキャラクターモデルです。
        /// </returns>
        public GameObject SetCharacterUI(
            int playerId,
            UnitParams characterData,
            int index)
        {
            if (characterData == null)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "表示するキャラクターデータがnullです。",
                    this);

                return null;
            }

            if (index < 0)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "UIスロットのインデックスが不正です。" +
                    $" Index={index}",
                    this);

                return null;
            }

            _characterImage?.SetSprite(
                characterData.PortraitSprite,
                index);

            GameDataRepository repository =
                GameDataRepository.Instance;

            Sprite typeIcon =
                repository != null
                    ? repository.GetIconSprite(
                        characterData.Type.ToString())
                    : null;

            _iconImage?.SetSprite(
                typeIcon,
                index);

            _characterNameText?.SetText(
                characterData.UnitName,
                index);

            if (_characterModelPreview == null)
            {
                return null;
            }

            return _characterModelPreview.GenerateCharacter(
                characterData.ModelPrefab,
                index);
        }

        /// <summary>
        /// タイマーを更新します。
        /// </summary>
        private void UpdateTimer()
        {
            if (_timer == null)
            {
                return;
            }

            _timer.Tick(
                Time.deltaTime);

            UpdateTimerText();
        }

        /// <summary>
        /// タイマー表示を更新します。
        /// </summary>
        private void UpdateTimerText()
        {
            if (_timer == null ||
                _timerText == null)
            {
                return;
            }

            _timerText.SetText(
                _timer.RemainingTime.ToString());
        }

        /// <summary>
        /// キャラクター選択時間の終了通知を処理します。
        /// </summary>
        private void HandleTimerEnded()
        {
            if (_timer != null)
            {
                _timer.TimeEnded -=
                    HandleTimerEnded;

                _timer =
                    null;
            }

            _timerText?.SetText(
                "0");

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager != null)
            {
                gameFlowManager.SetTimeEnded(
                    true);
            }

            TimeEnded?.Invoke();
        }

        /// <summary>
        /// 現在のタイマーからイベント購読を解除します。
        /// </summary>
        private void UnsubscribeTimer()
        {
            if (_timer == null)
            {
                return;
            }

            _timer.TimeEnded -=
                HandleTimerEnded;

            _timer =
                null;
        }

        /// <summary>
        /// オンラインサービスの生成待機を開始します。
        /// </summary>
        private void StartOnlineRegistration()
        {
            if (_onlineRegistrationCoroutine != null ||
                _unregisterOnlineEvents != null)
            {
                return;
            }

            _onlineRegistrationCoroutine =
                StartCoroutine(
                    RegisterOnlineEventsWhenReady());
        }

        /// <summary>
        /// キャラクターUIの初期化待機を開始します。
        /// </summary>
        private void StartUiInitialization()
        {
            if (_initializationCoroutine != null)
            {
                return;
            }

            _initializationCoroutine =
                StartCoroutine(
                    WaitAndInitialize());
        }

        /// <summary>
        /// オンラインサービスが利用可能になるまで待機し、
        /// プレイヤープロパティ変更イベントを購読します。
        /// </summary>
        private IEnumerator RegisterOnlineEventsWhenReady()
        {
            while (isActiveAndEnabled &&
                NetworkServiceLocator.Online == null)
            {
                yield return null;
            }

            _onlineRegistrationCoroutine =
                null;

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            RegisterOnlineEvents();
        }

        /// <summary>
        /// UIを構築できる状態になるまで待機し、
        /// プレイヤー一覧とキャラクター選択を反映します。
        /// </summary>
        private IEnumerator WaitAndInitialize()
        {
            while (isActiveAndEnabled &&
                !IsReadyToBuildUI())
            {
                yield return null;
            }

            _initializationCoroutine =
                null;

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            RebuildCharacterSlots(
                "Initial");

            ApplyCachedCharacterSelections();
        }

        /// <summary>
        /// プレイヤープロパティ変更イベントを購読します。
        /// </summary>
        private void RegisterOnlineEvents()
        {
            if (_unregisterOnlineEvents != null)
            {
                return;
            }

            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                return;
            }

            onlineService.PlayerPropertiesChanged +=
                HandlePlayerPropertiesChanged;

            _unregisterOnlineEvents =
                () =>
                {
                    onlineService.PlayerPropertiesChanged -=
                        HandlePlayerPropertiesChanged;
                };
        }

        /// <summary>
        /// プレイヤープロパティ変更イベントの購読を解除します。
        /// </summary>
        private void UnregisterOnlineEvents()
        {
            _unregisterOnlineEvents?.Invoke();

            _unregisterOnlineEvents =
                null;
        }

        /// <summary>
        /// 実行中の初期化コルーチンを停止します。
        /// </summary>
        private void StopInitializationCoroutines()
        {
            if (_onlineRegistrationCoroutine != null)
            {
                StopCoroutine(
                    _onlineRegistrationCoroutine);

                _onlineRegistrationCoroutine =
                    null;
            }

            if (_initializationCoroutine != null)
            {
                StopCoroutine(
                    _initializationCoroutine);

                _initializationCoroutine =
                    null;
            }
        }

        /// <summary>
        /// キャラクター情報の定期確認を行います。
        /// </summary>
        private void PollCharacterSelections()
        {
            if (Time.unscaledTime <
                _nextCharacterPollTime)
            {
                return;
            }

            _nextCharacterPollTime =
                Time.unscaledTime +
                Mathf.Max(
                    0.05f,
                    _characterPollInterval);

            RebuildUiListIfNeeded(
                "Poll");

            ApplyCachedCharacterSelections();
        }

        /// <summary>
        /// キャラクターUIを構築できる状態かどうかを判定します。
        /// </summary>
        /// <returns>
        /// オンラインルームとローカルプレイヤーが
        /// 利用可能な場合は<c>true</c>です。
        /// </returns>
        private static bool IsReadyToBuildUI()
        {
            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom ||
                onlineService.Players == null ||
                onlineService.Players.Count == 0 ||
                onlineService.LocalPlayerId <= 0)
            {
                return false;
            }

            return OnlinePlayerUtility.GetLocalPlayer() !=
                null;
        }

        /// <summary>
        /// チームやプレイヤー構成が変化した場合に、
        /// キャラクターUI一覧を再構築します。
        /// </summary>
        /// <param name="reason">
        /// 再構築確認を行った理由です。
        /// </param>
        private void RebuildUiListIfNeeded(
            string reason)
        {
            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom ||
                onlineService.Players == null ||
                onlineService.LocalPlayerId <= 0)
            {
                return;
            }

            int localTeamId =
                OnlinePlayerUtility.GetTeamId(
                    onlineService.LocalPlayerId);

            bool requiresRebuild =
                !_isUiListBuilt ||
                _lastTeamId != localTeamId ||
                _lastPlayerCount !=
                onlineService.Players.Count ||
                !_characterUIs.ContainsKey(
                    onlineService.LocalPlayerId);

            if (!requiresRebuild &&
                localTeamId >= 0)
            {
                foreach (OnlinePlayer player in
                        onlineService.Players)
                {
                    int playerTeamId =
                        OnlinePlayerUtility.GetTeamId(
                            player.PlayerId);

                    if (playerTeamId ==
                            localTeamId &&
                        !_characterUIs.ContainsKey(
                            player.PlayerId))
                    {
                        requiresRebuild =
                            true;

                        break;
                    }
                }
            }

            if (requiresRebuild)
            {
                RebuildCharacterSlots(
                    reason);
            }
        }

        /// <summary>
        /// ローカルプレイヤーと同じチームのプレイヤーに対応する
        /// キャラクターUIスロットを構築します。
        /// </summary>
        /// <param name="reason">
        /// UIを再構築する理由です。
        /// </param>
        private void RebuildCharacterSlots(
            string reason)
        {
            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                onlineService.Players == null)
            {
                return;
            }

            OnlinePlayer localPlayer =
                OnlinePlayerUtility.GetLocalPlayer();

            if (localPlayer == null)
            {
                return;
            }

            ClearCharacterSlots();

            _localPlayerId =
                onlineService.LocalPlayerId;

            _localTeamId =
                OnlinePlayerUtility.GetTeamId(
                    _localPlayerId);

            AddCharacterSlot(
                localPlayer.PlayerId);

            if (_localTeamId >= 0)
            {
                List<OnlinePlayer> teammates =
                    new();

                foreach (OnlinePlayer player in
                        onlineService.Players)
                {
                    if (player.PlayerId ==
                        _localPlayerId)
                    {
                        continue;
                    }

                    int playerTeamId =
                        OnlinePlayerUtility.GetTeamId(
                            player.PlayerId);

                    if (playerTeamId ==
                        _localTeamId)
                    {
                        teammates.Add(
                            player);
                    }
                }

                teammates.Sort(
                    (left, right) =>
                        left.PlayerId.CompareTo(
                            right.PlayerId));

                foreach (OnlinePlayer teammate in
                        teammates)
                {
                    AddCharacterSlot(
                        teammate.PlayerId);
                }
            }

            _lastTeamId =
                _localTeamId;

            _lastPlayerCount =
                onlineService.Players.Count;

            _isUiListBuilt =
                true;

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "キャラクターUI一覧を構築しました。" +
                    $" Reason={reason}," +
                    $" LocalPlayerId={_localPlayerId}," +
                    $" TeamId={_localTeamId}," +
                    $" UiCount={_characterUIs.Count}," +
                    $" PlayerCount={onlineService.Players.Count}",
                    this);
            }
        }

        /// <summary>
        /// 指定されたプレイヤーに対応する
        /// キャラクターUIスロットを追加します。
        /// </summary>
        /// <param name="playerId">
        /// 追加するプレイヤーIDです。
        /// </param>
        private void AddCharacterSlot(
            int playerId)
        {
            if (playerId <= 0 ||
                _characterUIs.ContainsKey(
                    playerId))
            {
                return;
            }

            _characterUIs.Add(
                playerId,
                new SelectCharacter(
                    playerId));

            _playerIds.Add(
                playerId);
        }

        /// <summary>
        /// 現在のキャラクターUIスロットと
        /// プレビューモデルを消去します。
        /// </summary>
        private void ClearCharacterSlots()
        {
            for (int index = 0;
                index < _playerIds.Count;
                index++)
            {
                _characterImage?.HideImage(
                    index);

                _iconImage?.HideImage(
                    index);

                _characterNameText?.HideText(
                    index);
            }

            _characterModelPreview?.ClearCharacters();

            _characterUIs.Clear();
            _playerIds.Clear();
            _appliedCharacterNames.Clear();
        }

        /// <summary>
        /// プレイヤープロパティ変更通知を処理します。
        /// </summary>
        /// <param name="targetPlayer">
        /// プロパティが変更されたプレイヤーです。
        /// </param>
        /// <param name="changedProperties">
        /// 変更されたプロパティです。
        /// </param>
        private void HandlePlayerPropertiesChanged(
            OnlinePlayer targetPlayer,
            Dictionary<string, object> changedProperties)
        {
            if (targetPlayer == null ||
                changedProperties == null)
            {
                return;
            }

            if (ContainsKeyIgnoreCase(
                    changedProperties,
                    NetworkPropertyKeys.TeamId))
            {
                RebuildCharacterSlots(
                    "TeamChanged");

                ApplyCachedCharacterSelections();
            }

            if (!ContainsKeyIgnoreCase(
                    changedProperties,
                    NetworkPropertyKeys.CharacterName))
            {
                return;
            }

            if (!targetPlayer.TryGetProperty(
                    NetworkPropertyKeys.CharacterName,
                    out string characterName) ||
                string.IsNullOrWhiteSpace(
                    characterName))
            {
                return;
            }

            int localPlayerId =
                NetworkServiceLocator.Online?
                    .LocalPlayerId ??
                -1;

            UpdatePlayerCharacter(
                targetPlayer.PlayerId,
                characterName,
                targetPlayer.PlayerId ==
                localPlayerId);
        }

        /// <summary>
        /// 現在キャッシュされているプレイヤーの
        /// キャラクター選択をUIへ反映します。
        /// </summary>
        private void ApplyCachedCharacterSelections()
        {
            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                onlineService.Players == null)
            {
                return;
            }

            foreach (OnlinePlayer player in
                    onlineService.Players)
            {
                if (!player.TryGetProperty(
                        NetworkPropertyKeys.CharacterName,
                        out string characterName) ||
                    string.IsNullOrWhiteSpace(
                        characterName))
                {
                    continue;
                }

                UpdatePlayerCharacter(
                    player.PlayerId,
                    characterName,
                    player.PlayerId ==
                    onlineService.LocalPlayerId);
            }

            if (_pendingCharacterNames.Count == 0)
            {
                return;
            }

            List<KeyValuePair<int, string>>
                pendingSelections =
                    new(
                        _pendingCharacterNames);

            List<int> appliedPlayerIds =
                new();

            foreach (KeyValuePair<int, string> selection in
                    pendingSelections)
            {
                bool wasApplied =
                    UpdatePlayerCharacter(
                        selection.Key,
                        selection.Value,
                        selection.Key ==
                        onlineService.LocalPlayerId);

                if (wasApplied)
                {
                    appliedPlayerIds.Add(
                        selection.Key);
                }
            }

            foreach (int playerId in
                    appliedPlayerIds)
            {
                _pendingCharacterNames.Remove(
                    playerId);
            }
        }

        /// <summary>
        /// 指定されたプレイヤーのキャラクター選択をUIへ反映します。
        /// </summary>
        /// <param name="targetPlayerId">
        /// 対象プレイヤーIDです。
        /// </param>
        /// <param name="characterName">
        /// 選択されたキャラクター名です。
        /// </param>
        /// <param name="forceLocal">
        /// チームIDの同期を待たず、ローカルスロットへ
        /// 反映する場合は<c>true</c>です。
        /// </param>
        /// <returns>
        /// UIへの反映が完了した場合は<c>true</c>です。
        /// </returns>
        private bool UpdatePlayerCharacter(
            int targetPlayerId,
            string characterName,
            bool forceLocal = false)
        {
            string validCharacterName =
                characterName?.Trim() ??
                string.Empty;

            if (targetPlayerId <= 0 ||
                string.IsNullOrEmpty(
                    validCharacterName))
            {
                return false;
            }

            if (_appliedCharacterNames.TryGetValue(
                    targetPlayerId,
                    out string appliedName) &&
                string.Equals(
                    appliedName,
                    validCharacterName,
                    StringComparison.Ordinal))
            {
                return true;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null)
            {
                _pendingCharacterNames[
                    targetPlayerId] =
                    validCharacterName;

                return false;
            }

            UnitParams characterData =
                repository.GetCharacterData(
                    validCharacterName);

            if (characterData == null)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "キャラクターデータが見つかりません。" +
                    $" Character={validCharacterName}",
                    this);

                return false;
            }

            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                return false;
            }

            int localPlayerId =
                onlineService.LocalPlayerId;

            if (forceLocal ||
                targetPlayerId ==
                localPlayerId)
            {
                EnsureLocalCharacterSlot(
                    localPlayerId);

                int localIndex =
                    _playerIds.IndexOf(
                        localPlayerId);

                if (localIndex < 0)
                {
                    return false;
                }

                UpdateCharacterUI(
                    localPlayerId,
                    characterData,
                    localIndex);

                return true;
            }

            int localTeamId =
                OnlinePlayerUtility.GetTeamId(
                    localPlayerId);

            int targetTeamId =
                OnlinePlayerUtility.GetTeamId(
                    targetPlayerId);

            if (localTeamId < 0 ||
                targetTeamId < 0)
            {
                _pendingCharacterNames[
                    targetPlayerId] =
                    validCharacterName;

                return false;
            }

            if (localTeamId !=
                targetTeamId)
            {
                return false;
            }

            if (!_characterUIs.ContainsKey(
                    targetPlayerId))
            {
                RebuildCharacterSlots(
                    "EnsureTeammateSlot");
            }

            int index =
                _playerIds.IndexOf(
                    targetPlayerId);

            if (index < 0)
            {
                _pendingCharacterNames[
                    targetPlayerId] =
                    validCharacterName;

                return false;
            }

            UpdateCharacterUI(
                targetPlayerId,
                characterData,
                index);

            return true;
        }

        /// <summary>
        /// ローカルプレイヤーのUIスロットが存在することを保証します。
        /// </summary>
        /// <param name="localPlayerId">
        /// ローカルプレイヤーIDです。
        /// </param>
        private void EnsureLocalCharacterSlot(
            int localPlayerId)
        {
            if (localPlayerId <= 0)
            {
                return;
            }

            if (!_characterUIs.ContainsKey(
                    localPlayerId))
            {
                RebuildCharacterSlots(
                    "EnsureLocalSlot");
            }

            if (!_characterUIs.ContainsKey(
                    localPlayerId))
            {
                _characterUIs.Add(
                    localPlayerId,
                    new SelectCharacter(
                        localPlayerId));
            }

            int currentIndex =
                _playerIds.IndexOf(
                    localPlayerId);

            if (currentIndex == 0)
            {
                return;
            }

            if (currentIndex > 0)
            {
                _playerIds.RemoveAt(
                    currentIndex);
            }

            _playerIds.Insert(
                0,
                localPlayerId);
        }

        /// <summary>
        /// 指定されたプレイヤーのキャラクターUIを更新し、
        /// 適用済み情報を保存します。
        /// </summary>
        private void UpdateCharacterUI(
            int playerId,
            UnitParams characterData,
            int index)
        {
            GameObject model =
                SetCharacterUI(
                    playerId,
                    characterData,
                    index);

            _characterUIs[
                playerId] =
                new SelectCharacter(
                    playerId)
                {
                    CharacterData =
                        characterData,

                    Model =
                        model
                };

            _appliedCharacterNames[
                playerId] =
                characterData.UnitName;

            _pendingCharacterNames.Remove(
                playerId);

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(CharacterSelectUIManager)}: " +
                    "キャラクターUIを更新しました。" +
                    $" PlayerId={playerId}," +
                    $" Character={characterData.UnitName}," +
                    $" Index={index}",
                    this);
            }
        }

        /// <summary>
        /// ローカルプレイヤーのキャッシュへ
        /// キャラクター選択とReady解除を反映します。
        /// </summary>
        private static void ApplyLocalSelectionProperties(
            OnlinePlayer localPlayer,
            string characterName)
        {
            if (localPlayer?.Properties == null)
            {
                return;
            }

            localPlayer.Properties[
                NetworkPropertyKeys.CharacterName] =
                characterName;

            localPlayer.Properties[
                NetworkPropertyKeys.Ready] =
                false;

            localPlayer.Properties[
                NetworkPropertyKeys.IsReady] =
                false;

            localPlayer.Properties[
                NetworkPropertyKeys.CanStartGame] =
                false;
        }

        /// <summary>
        /// キャラクター選択時に送信する
        /// プレイヤープロパティを生成します。
        /// </summary>
        private static Dictionary<string, object>
            CreateCharacterSelectionProperties(
                string characterName)
        {
            return new Dictionary<string, object>
            {
                {
                    NetworkPropertyKeys.CharacterName,
                    characterName
                },
                {
                    NetworkPropertyKeys.Ready,
                    false
                },
                {
                    NetworkPropertyKeys.IsReady,
                    false
                },
                {
                    NetworkPropertyKeys.CanStartGame,
                    false
                }
            };
        }

        /// <summary>
        /// Dictionaryに指定されたキーが
        /// 大文字と小文字を無視して含まれているか確認します。
        /// </summary>
        private static bool ContainsKeyIgnoreCase(
            Dictionary<string, object> properties,
            string key)
        {
            if (properties == null ||
                string.IsNullOrEmpty(
                    key))
            {
                return false;
            }

            foreach (string propertyKey in
                    properties.Keys)
            {
                if (string.Equals(
                        propertyKey,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定された値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _initialSelectionTime =
                Mathf.Max(
                    0,
                    _initialSelectionTime);

            _characterPollInterval =
                Mathf.Max(
                    0.05f,
                    _characterPollInterval);
        }
#endif
    }
}
