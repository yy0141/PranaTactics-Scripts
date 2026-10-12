using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Combat;
using PranaTactics.AI.Solo;
using PranaTactics.Audio;
using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.Networking;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit.Movement;
using PranaTactics.Unit.Networking;
using PranaTactics.Unit.Networking.EOS;
using PranaTactics.Unit.Networking.Local;
using PranaTactics.Unit.Networking.Photon;
using Outline = PranaTactics.Rendering.Outlines.Outline;

namespace PranaTactics.Unit
{
    /// <summary>
    /// ユニット全体の状態と、Macro、Micro、TacticalMapの
    /// 各ユニット表現を統括します。
    /// </summary>
    /// <remarks>
    /// ネットワークモードに応じて
    /// <see cref="IUnitNetwork"/>の実装を選択し、
    /// 外部から要求された処理を各クライアントへ同期します。
    ///
    /// ネットワークから受信した処理は、
    /// <c>Apply</c>から始まるメソッドでローカル状態へ反映します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UnitBehavior :
    MonoBehaviour,
    IDamageable
    {
        public static event System.Action<UnitBehavior, int> DamageApplied;

        /// <summary>
        /// 敵チームごとの直接視認によるマップ公開期限です。
        /// </summary>
        private readonly StealthDetectionMemory _stealthDetectionMemory = new();

        /// <summary>
        /// 視認通知の送信頻度を抑える対象ごとの次回送信時刻です。
        /// </summary>
        private readonly Dictionary<int, float> _nextDetectionReportByTarget = new();

        /// <summary>
        /// 視認中のネットワーク通知間隔です。
        /// </summary>
        private const float k_DetectionReportInterval = 0.25f;

        /// <summary>
        /// マップ公開期限切れを表示へ反映します。
        /// </summary>
        private void Update()
        {
            if (_stealthDetectionMemory.Expire(Time.time))
            {
                RefreshStealthPresentation();
            }
        }

        /// <summary>
        /// 自身の兵装コストを除く、最後の被ダメージ時刻です。
        /// </summary>
        private float _lastIncomingDamageTime = float.NegativeInfinity;

        public float LastIncomingDamageTime => _lastIncomingDamageTime;

        /// <summary>
        /// 最後に攻撃を実行した時刻です。入力のみでは更新しません。
        /// </summary>
        private float _lastAttackTime = float.NegativeInfinity;

        public float LastAttackTime => _lastAttackTime;

        /// <summary>
        /// 攻撃成立時刻を記録します。連続攻撃では最後の攻撃時刻へ更新します。
        /// </summary>
        public void RecordAttack()
        {
            _lastAttackTime = Time.time;
        }

        /// <summary>
        /// 有効なプレイヤーIDを取得できなかった場合の値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// 有効なチームIDを取得できなかった場合の値です。
        /// </summary>
        private const int k_InvalidTeamId = -1;

        /// <summary>
        /// 通常状態のユニットレイヤー名です。
        /// </summary>
        private const string k_UnitLayerName = "Unit";

        /// <summary>
        /// ステルス状態のユニットレイヤー名です。
        /// </summary>
        private const string k_StealthLayerName = "Stealth";

        /// <summary>
        /// Macro Phaseで使用するユニットオブジェクトです。
        /// </summary>
        [Header("Unit Objects")]
        [FormerlySerializedAs("MOVE_UNIT")]
        [SerializeField]
        private GameObject _macroUnitObject;

        /// <summary>
        /// Micro Phaseで使用するユニットオブジェクトです。
        /// </summary>
        [FormerlySerializedAs("ACTION_UNIT")]
        [SerializeField]
        private GameObject _microUnitObject;

        /// <summary>
        /// 戦術マップで使用するユニットオブジェクトです。
        /// </summary>
        [FormerlySerializedAs("MAP_UNIT")]
        [SerializeField]
        private GameObject _tacticalMapUnitObject;

        /// <summary>
        /// ローカルプレイヤーの一人称表示時に
        /// 非表示にする身体メッシュです。
        /// </summary>
        [Header("Presentation")]
        [FormerlySerializedAs("bodyMeshes")]
        [SerializeField]
        private MeshRenderer[] _bodyMeshes;

        /// <summary>
        /// ユニットに向けられた攻撃予測を管理します。
        /// </summary>
        [Header("Combat")]
        [SerializeField]
        private AttackPrediction _attackPrediction;

        /// <summary>
        /// ユニット同期に使用するネットワーク実装です。
        /// </summary>
        private IUnitNetwork _network;

        /// <summary>
        /// ユニットが所属するチームIDです。
        /// </summary>
        private int _teamId =
            k_InvalidTeamId;

        /// <summary>
        /// ユニットを所有するプレイヤーIDです。
        /// </summary>
        private int _ownerId =
            k_InvalidPlayerId;

        /// <summary>
        /// ユニットの能力値と現在状態です。
        /// </summary>
        private UnitStatus _unitStatus;

        /// <summary>
        /// ユニットが所属するチームの状態です。
        /// </summary>
        private readonly TeamStatus _teamStatus =
            new();

        /// <summary>
        /// Macro Phaseで使用するユニット動作です。
        /// </summary>
        private MacroUnitBehavior _macroUnitBehavior;

        /// <summary>
        /// Micro Phaseで使用するユニット動作です。
        /// </summary>
        private MicroUnitBehavior _microUnitBehavior;

        /// <summary>
        /// 戦術マップで使用するユニット動作です。
        /// </summary>
        private TacticalMapUnitBehavior _tacticalMapUnitBehavior;

        /// <summary>
        /// ネットワーク実装の初期化が完了しているかどうかです。
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// 撃破処理を要求済みかどうかです。
        /// </summary>
        private bool _isDeathRequested;

        /// <summary>
        /// 現在の生存期間に受けた攻撃のチーム別貢献を集計します。
        /// </summary>
        private readonly DamageContributionTracker _damageContributions = new();

        /// <summary>
        /// 命中時の減速率と有効期限を管理します。
        /// </summary>
        private readonly HitSlowdownState _hitSlowdown = new();

        /// <summary>
        /// この生存期間の撃破ポイントが適用済みかを示します。
        /// </summary>
        private bool _isDefeatScoreApplied;

        public float HitMovementMultiplier => _hitSlowdown.GetMovementMultiplier(Time.timeAsDouble);

        /// <summary>
        /// ステルスを使用しているユニットの所属チームIDです。
        /// このチームからは通常表示を維持します。
        /// </summary>
        private int _stealthOwnerTeamId =
            k_InvalidTeamId;

        public int PlayerId
        {
            get
            {
                if (_unitStatus != null)
                {
                    return _unitStatus.PlayerId;
                }

                return _ownerId;
            }
        }

        public int TeamId
        {
            get
            {
                if (_unitStatus != null)
                {
                    return _unitStatus.TeamId;
                }

                return _teamId;
            }
        }

        public UnitStatus Status
        {
            get
            {
                return _unitStatus;
            }
        }

        public TeamStatus TeamStatus
        {
            get
            {
                return _teamStatus;
            }
        }

        public MacroUnitBehavior MacroUnitBehavior
        {
            get
            {
                return _macroUnitBehavior;
            }
        }

        public MicroUnitBehavior MicroUnitBehavior
        {
            get
            {
                return _microUnitBehavior;
            }
        }

        public TacticalMapUnitBehavior TacticalMapUnitBehavior
        {
            get
            {
                return _tacticalMapUnitBehavior;
            }
        }

        public AttackPrediction AttackPrediction
        {
            get
            {
                return _attackPrediction;
            }
        }

        public bool IsInitialized
        {
            get
            {
                return _isInitialized;
            }
        }

        public bool IsStealthActive
        {
            get;
            private set;
        }

        public int StealthOwnerTeamId
        {
            get
            {
                return _stealthOwnerTeamId;
            }
        }

        /// <summary>
        /// ネットワーク実装を初期化します。
        /// </summary>
        private void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// 現在のプレイモードと通信方式に対応する
        /// <see cref="IUnitNetwork"/>実装を設定します。
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized &&
                _network != null)
            {
                return;
            }

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            bool isSoloPlay =
                gameFlowManager != null &&
                gameFlowManager.IsSoloPlay;

            if (isSoloPlay)
            {
                _network =
                    GetOrAddNetwork<LocalUnitNetwork>();
            }
            else
            {
                _network =
                    NetworkModeUtility.CurrentMode switch
                    {
                        NetworkMode.Photon =>
                            GetOrAddNetwork<PhotonUnitNetwork>(),

                        NetworkMode.Eos =>
                            GetOrAddNetwork<EosUnitNetwork>(),

                        _ =>
                            GetOrAddNetwork<LocalUnitNetwork>()
                    };
            }

            DisableInactiveNetworkComponents(
                _network);

            if (_network == null)
            {
                _isInitialized =
                    false;

                Debug.LogError(
                    $"{nameof(UnitBehavior)}: " +
                    "IUnitNetworkの初期化に失敗しました。" +
                    $" IsSoloPlay={isSoloPlay}," +
                    $" Mode={NetworkModeUtility.CurrentMode}," +
                    $" Object={name}",
                    this);

                return;
            }

            _isInitialized =
                true;

            Debug.Log(
                $"{nameof(UnitBehavior)}: " +
                "ネットワーク実装を初期化しました。" +
                $" IsSoloPlay={isSoloPlay}," +
                $" Mode={NetworkModeUtility.CurrentMode}," +
                $" Network={_network.GetType().Name}," +
                $" Object={name}",
                this);
        }

        /// <summary>
        /// ユニットのステータスを取得します。
        /// </summary>
        /// <returns>
        /// ユニットのステータスです。
        /// </returns>
        public UnitStatus GetUnitStatus()
        {
            return Status;
        }

        /// <summary>
        /// ユニットが所属するチームIDを取得します。
        /// </summary>
        /// <returns>
        /// ユニットのチームIDです。
        /// </returns>
        public int GetTeamId()
        {
            return TeamId;
        }

        /// <summary>
        /// 攻撃を受けたユニットのHPを減少させます。
        /// </summary>
        /// <param name="playerId">
        /// 攻撃を行ったプレイヤーIDです。
        /// </param>
        /// <param name="value">
        /// 与えるダメージ量です。
        /// </param>
        /// <param name="animation">
        /// HUDのダメージアニメーションを再生する場合は<c>true</c>です。
        /// </param>
        public void ApplyDamage(
            int playerId,
            float value,
            bool animation = true)
        {
            ApplyDamageInternal(playerId, value, animation, CombatFeedbackSide.Unknown);
        }

        /// <summary>
        /// 兵装の発動元を保持して確定ダメージを適用します。
        /// </summary>
        public void ApplyCombatDamage(int playerId, float value, CombatFeedbackSide side, int sourcePranamSlot = -1)
        {
            ApplyDamageInternal(playerId, value, true, side, sourcePranamSlot);
        }

        /// <summary>
        /// HP、貢献度、確定した攻撃結果を更新します。
        /// </summary>
        private void ApplyDamageInternal(
            int playerId,
            float value,
            bool animation,
            CombatFeedbackSide side,
            int sourcePranamSlot = -1)
        {
            if (_unitStatus == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitBehavior)}: " +
                    "UnitStatusが設定されていないため、" +
                    "ダメージを適用できません。",
                    this);

                return;
            }

            if (_isDeathRequested ||
                _unitStatus.CurrentHealth <= 0.0f ||
                value <= 0.0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                return;
            }

            float previousHealth = _unitStatus.CurrentHealth;
            float newHealth =
                Mathf.Max(
                    0.0f,
                    _unitStatus.CurrentHealth -
                    value);

            _unitStatus.SetHealth(
                newHealth);

            MatchManager.Instance?.RecordResultDamage(PlayerId, playerId, previousHealth - newHealth, newHealth <= 0.0f,
                side == CombatFeedbackSide.Main, sourcePranamSlot);

            UnitManager unitManager = UnitManager.Instance;
            _damageContributions.Record(
                playerId,
                unitManager != null ? unitManager.GetTeamId(playerId) : -1,
                TeamId,
                previousHealth - newHealth,
                _unitStatus.MaxHealth,
                newHealth <= 0.0f);

            if (playerId != PlayerId && previousHealth > newHealth)
            {
                _lastIncomingDamageTime = Time.time;
            }

            DamageApplied?.Invoke(this, playerId);

            if (animation && unitManager != null && playerId >= 0 &&
                playerId != PlayerId && unitManager.GetTeamId(playerId) >= 0 &&
                unitManager.GetTeamId(playerId) != TeamId)
            {
                var result = new CombatFeedbackResult(
                    playerId,
                    PlayerId,
                    newHealth <= 0.0f ? CombatFeedbackKind.Elimination : CombatFeedbackKind.UnitHit,
                    side);

                if (_network is ICombatFeedbackNetwork feedbackNetwork)
                {
                    feedbackNetwork.PublishCombatFeedback(result);
                }
                else
                {
                    CombatFeedbackEvents.Publish(result);
                }
            }

            Debug.LogWarning(
                $"[Damage] " +
                $"Target={PlayerId}, " +
                $"Source={playerId}, " +
                $"Local={unitManager?.LocalPlayerId ?? -1}, " +
                $"IsTargetAI=" +
                $"{(unitManager != null && unitManager.IsAIControlledUnit(PlayerId))}, " +
                $"Value={value}, " +
                $"Animation={animation}, " +
                $"Before={previousHealth}, " +
                $"Object={name}",
                this);

            if (unitManager != null &&
                playerId == PlayerId)
            {
                Debug.Log(
                    $"{nameof(UnitBehavior)}: " +
                    $"PlayerId = {playerId} からの攻撃, " +
                    $"AIユニットのHP={newHealth}",
                    this);
            }

            RefreshLocalHealthGauge(
                animation);

            if (animation && playerId != PlayerId && unitManager != null &&
                PlayerId >= 0 && unitManager.ControlledUnitId == PlayerId)
            {
                SoundManager.Instance?.PlayDamageReceivedSE();
            }

            if (_unitStatus.CurrentHealth >
                0.0f)
            {
                return;
            }

            _isDeathRequested =
                true;

            SetDead();

            if (unitManager == null)
            {
                return;
            }

            if (_damageContributions.TryGetWinningTeam(out int scoringTeamId))
            {
                AddTeamScore(scoringTeamId);
            }
        }

        /// <summary>
        /// 生存中のユニットへ命中時の減速を適用します。
        /// </summary>
        /// <param name="ratio">
        /// 通常・歩行速度を減らす割合です。0から1で指定します。
        /// </param>
        /// <param name="durationSeconds">
        /// 減速が持続するゲーム内秒数です。
        /// </param>
        public void ApplyHitSlowdown(float ratio, float durationSeconds)
        {
            if (_unitStatus != null && !_unitStatus.IsDead && !_isDeathRequested)
            {
                _hitSlowdown.Apply(ratio, durationSeconds, Time.timeAsDouble);
            }
        }

        /// <summary>
        /// ユニットの撃破処理をネットワークへ要求します。
        /// </summary>
        public void SetDead()
        {
            _isDeathRequested =
                true;

            if (!TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.SetDead();
        }

        /// <summary>
        /// ネットワークから受信した撃破処理を
        /// ローカルユニットへ適用します。
        /// </summary>
        public void ApplySetDead()
        {
            if (_unitStatus == null || _unitStatus.IsDead)
            {
                return;
            }

            _unitStatus.SetHealth(0.0f);
            _unitStatus.SetDead(true);
            MatchManager.Instance?.RegisterPlayerElimination(PlayerId, TeamId);
            AiPublicStateRegistry.ReportAlive(PlayerId, false);
            _isDeathRequested =
                true;

            if (_microUnitBehavior == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitBehavior)}: " +
                    "MicroUnitBehaviorが設定されていないため、" +
                    "撃破状態を適用できません。",
                    this);

                return;
            }

            _microUnitBehavior.SetDead();
            UnitManager.Instance?.HandleUnitDefeated(this);
            _microUnitBehavior.CompleteElimination();
        }

        /// <summary>
        /// 操作引き継ぎ先の現在値をHUDへ反映します。兵装や経路は再初期化しません。
        /// </summary>
        public void RefreshControlledHud()
        {
            RefreshLocalHealthGauge(false);
            HUDManager.Instance?.SetProactiveness(_unitStatus.Proactiveness);
            HUDManager.Instance?.SetMapMode(false);
            _tacticalMapUnitBehavior.RefreshControlledPranamUi();
        }

        /// <summary>
        /// 指定されたチームへのスコア加算をネットワークへ要求します。
        /// </summary>
        /// <param name="teamId">
        /// スコアを加算するチームIDです。
        /// </param>
        public void AddTeamScore(
            int teamId)
        {
            if (teamId < 0 ||
                !TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.AddTeamScore(
                teamId);
        }

        /// <summary>
        /// ネットワークから受信したチームスコア加算を適用します。
        /// </summary>
        /// <param name="teamId">
        /// スコアを加算するチームIDです。
        /// </param>
        public void ApplyAddTeamScore(
            int teamId)
        {
            if (teamId < 0)
            {
                return;
            }

            if (_isDefeatScoreApplied || teamId == TeamId)
            {
                return;
            }

            _isDefeatScoreApplied = true;

            MatchManager matchManager =
                MatchManager.Instance;

            matchManager?.AddTeamScore(
                teamId);
            matchManager?.RecordResultDefeatCredit(PlayerId, teamId, _unitStatus.MaxHealth);
        }

        /// <summary>
        /// 攻撃予測コンポーネントを取得します。
        /// </summary>
        /// <returns>
        /// 攻撃予測コンポーネントです。
        /// </returns>
        public AttackPrediction GetAttackPrediction()
        {
            return AttackPrediction;
        }

        /// <summary>
        /// ユニットへ向けられた脅威情報を通知します。
        /// </summary>
        /// <param name="data">
        /// 通知する脅威情報です。
        /// </param>
        public void NotifyThreat(
            ThreatData data)
        {
            _attackPrediction?.NotifyThreat(
                data);
        }

        /// <summary>
        /// ユニットが所属するチームの状態を取得します。
        /// </summary>
        /// <returns>
        /// チーム状態です。
        /// </returns>
        public TeamStatus GetTeamStatus()
        {
            return TeamStatus;
        }

        /// <summary>
        /// ユニット生成をネットワークへ要求します。
        /// </summary>
        /// <param name="data">
        /// ユニットの初期化情報です。
        /// </param>
        public void Instantiate(
            UnitInitializeData data)
        {
            if (!TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.Instantiate(
                data);
        }

        /// <summary>
        /// ネットワークから受信したユニット生成情報を適用します。
        /// </summary>
        /// <param name="data">
        /// ユニットの初期化情報です。
        /// </param>
        public void ApplyInstantiate(
            UnitInitializeData data)
        {
            if (data == null || (data.InitialStats != null && !data.InitialStats.IsValid()))
            {
                Debug.LogError("Unitの初期化情報または生成時ステータスが不正です。", this);
                return;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            Debug.Log("UnitBehavior Instantinate");

            if (repository == null)
            {
                Debug.LogError(
                    $"{nameof(UnitBehavior)}: " +
                    "GameDataRepositoryが存在しないため、" +
                    "ユニットを初期化できません。",
                    this);

                return;
            }

            UnitParams characterData =
                repository.GetCharacterData(
                    data.CharacterName);

            if (characterData == null)
            {
                Debug.LogError(
                    $"{nameof(UnitBehavior)}: " +
                    "キャラクターデータが見つかりません。" +
                    $" CharacterName={data.CharacterName}",
                    this);

                return;
            }

            UnitData unitData =
                repository.GetUnitData();

            if (unitData == null)
            {
                Debug.LogError(
                    $"{nameof(UnitBehavior)}: " +
                    "UnitDataが見つからないため、" +
                    "ユニットを初期化できません。",
                    this);

                return;
            }

            _teamId =
                data.TeamId;

            _ownerId =
                data.OwnerId;

            AiPublicStateRegistry.ReportAlive(_ownerId, true);

            _isDeathRequested =
                false;
            _isDefeatScoreApplied = false;
            _damageContributions.Reset();
            _hitSlowdown.Reset();
            _lastIncomingDamageTime = float.NegativeInfinity;
            _lastAttackTime = float.NegativeInfinity;
            _stealthDetectionMemory.Clear();
            _nextDetectionReportByTarget.Clear();

            IsStealthActive =
                false;

            _stealthOwnerTeamId =
                k_InvalidTeamId;

            gameObject.name =
                $"Unit_{_ownerId}";

            _unitStatus = CreateInitialStatus(data, characterData, unitData);

            if (!TryInitializeChildObjects(
                    unitData,
                    _unitStatus,
                    data.StartX,
                    data.StartY,
                    data.Rotation))
            {
                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                Debug.LogError(
                    $"{nameof(UnitBehavior)}: " +
                    "UnitManagerが存在しないため、" +
                    "生成したユニットを登録できません。",
                    this);

                return;
            }

            unitManager.RegisterUnit(
                _unitStatus.PlayerId,
                gameObject);

            MatchManager.Instance?.RegisterResultUnit(_unitStatus);

            RefreshLocalBodyVisibility();

            Debug.Log(
                $"[UnitInit] ApplyInstantiate開始 " +
                $"Frame={Time.frameCount}," +
                $"OwnerId={data.OwnerId}",
                this);
        }

        /// <summary>
        /// ステージの指定値とキャラクター設定から、生成時のステータスを組み立てます。
        /// </summary>
        /// <remarks>
        /// キャラクターの共有アセットを変更せず、歩行・ダッシュにも指定した移動速度を使用します。
        /// 行動力は初期残量と各Macro Phaseの回復量に共通して使用します。
        /// </remarks>
        private static UnitStatus CreateInitialStatus(UnitInitializeData data, UnitParams characterData, UnitData unitData)
        {
            UnitInitialStats stats = data.InitialStats;
            float moveSpeed = stats != null && stats.HasMoveSpeed ? stats.MoveSpeed : characterData.MoveSpeed;
            float pranaCapacity = stats != null && stats.HasPranaCapacity ? stats.PranaCapacity : characterData.PranaCapacity;
            int actionPoint = stats != null && stats.HasActionPoint ? stats.ActionPoint : characterData.InitialProactiveness;
            var status = new UnitStatus(
                data.OwnerId,
                data.TeamId,
                data.CharacterName,
                actionPoint,
                pranaCapacity,
                moveSpeed,
                moveSpeed * unitData.DashSpeedMultiplier,
                moveSpeed * unitData.WalkSpeedMultiplier,
                unitData.MoveProactivenessCostCost,
                characterData);
            status.SetProactiveness(actionPoint);
            return status;
        }

        /// <summary>
        /// Macro、Micro、TacticalMapの各動作を初期化します。
        /// </summary>
        /// <param name="unitData">
        /// 共通のユニット設定です。
        /// </param>
        /// <param name="unitStatus">
        /// 初期化するユニットステータスです。
        /// </param>
        /// <param name="x">
        /// 初期X座標です。
        /// </param>
        /// <param name="y">
        /// 初期Y座標です。
        /// </param>
        /// <param name="rotation">
        /// 初期回転角度です。
        /// </param>
        public void InstantiateChildObject(
            UnitData unitData,
            UnitStatus unitStatus,
            int x,
            int y,
            Vector3 rotation)
        {
            TryInitializeChildObjects(
                unitData,
                unitStatus,
                x,
                y,
                rotation);
        }

        /// <summary>
        /// ローカルプレイヤーの変更後に、
        /// カメラとマップ表示を更新します。
        /// </summary>
        public void RefreshLocalPresentation()
        {
            if (_unitStatus == null)
            {
                return;
            }

            if (!_unitStatus.IsAlive)
            {
                if (_macroUnitBehavior != null)
                {
                    _macroUnitBehavior.SwitchCamera(false);
                }

                if (_microUnitBehavior != null)
                {
                    _microUnitBehavior.SwitchCamera(false);
                }

                if (_tacticalMapUnitBehavior != null)
                {
                    _tacticalMapUnitBehavior.SwitchCamera(false);
                }

                return;
            }

            bool isControlled = UnitManager.Instance != null &&
                UnitManager.Instance.ControlledUnitId == PlayerId;
            MatchPhaseManager phase = MatchPhaseManager.Instance;

            if (_macroUnitBehavior != null)
            {
                _macroUnitBehavior.SwitchCamera(
                    isControlled && phase != null &&
                    phase.CurrentPhase == MatchPhase.Macro);
            }

            if (_microUnitBehavior != null)
            {
                _microUnitBehavior.SwitchCamera(
                    isControlled && phase != null &&
                    phase.CurrentPhase == MatchPhase.Micro);
            }

            _tacticalMapUnitBehavior?.RefreshLocalMapView();

            RefreshLocalBodyVisibility();
            RefreshTeamMaterials();
            RefreshStealthPresentation();

            _tacticalMapUnitBehavior
                ?.RefreshPranamRangeVisibility();

            UnitManager unitManager =
                UnitManager.Instance;

            bool isLocalPlayer =
                unitManager != null &&
                PlayerId ==
                unitManager.ControlledUnitId;

            Debug.Log(
                $"{nameof(UnitBehavior)}: " +
                "ローカル表示を更新しました。" +
                $" Owner={_ownerId}," +
                $" IsLocal={isLocalPlayer}," +
                $" MapCameraActive=" +
                $"{_tacticalMapUnitBehavior != null && _tacticalMapUnitBehavior.IsLocalShouldToggleMapCameraActive()}",
                this);
        }

        /// <summary>
        /// 指定されたオブジェクトへマテリアルを設定します。
        /// </summary>
        /// <param name="targetObject">
        /// マテリアルを設定するオブジェクトです。
        /// </param>
        /// <param name="material">
        /// 設定するマテリアルです。
        /// </param>
        public void SetMaterial(
            GameObject targetObject,
            Material material)
        {
            if (targetObject == null ||
                material == null)
            {
                return;
            }

            Renderer targetRenderer =
                targetObject.GetComponent<Renderer>();

            if (targetRenderer == null)
            {
                targetRenderer =
                    targetObject.GetComponentInChildren<Renderer>(
                        true);
            }

            if (targetRenderer == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitBehavior)}: " +
                    "マテリアルを設定するRendererが見つかりません。" +
                    $" Object={targetObject.name}",
                    targetObject);

                return;
            }

            targetRenderer.sharedMaterial =
                material;
        }

        /// <summary>
        /// ユニットを所有するプレイヤーIDを取得します。
        /// </summary>
        /// <returns>
        /// ユニット所有者のプレイヤーIDです。
        /// </returns>
        public int GetOwnerId()
        {
            return _ownerId;
        }

        /// <summary>
        /// Macro Phase用ユニット動作を取得します。
        /// </summary>
        public MacroUnitBehavior GetMacroUnitBehavior()
        {
            return MacroUnitBehavior;
        }

        /// <summary>
        /// Micro Phase用ユニット動作を取得します。
        /// </summary>
        public MicroUnitBehavior GetMicroUnitBehavior()
        {
            return MicroUnitBehavior;
        }

        /// <summary>
        /// 戦術マップ用ユニット動作を取得します。
        /// </summary>
        public TacticalMapUnitBehavior GetTacticalMapUnitBehavior()
        {
            return TacticalMapUnitBehavior;
        }

        /// <summary>
        /// 指定されたチームがこのユニットと同じチームか判定します。
        /// </summary>
        public bool IsSameTeam(
            int targetTeamId)
        {
            return TeamId ==
                targetTeamId;
        }

        /// <summary>
        /// 指定されたチームがローカルプレイヤーのチームか判定します。
        /// </summary>
        public bool IsLocalPlayerTeam(
            int teamId)
        {
            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return false;
            }

            int localPlayerId =
                unitManager.ControlledUnitId;

            if (localPlayerId < 0)
            {
                return false;
            }

            int localTeamId =
                unitManager.GetTeamId(
                    localPlayerId);

            if (localTeamId < 0)
            {
                return false;
            }

            return localTeamId ==
                teamId;
        }

        /// <summary>
        /// 移動予定経路への座標追加をネットワークへ要求します。
        /// </summary>
        public void AddMigrationCoordinate(
            int x,
            int y)
        {
            if (!TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.AddMigrationCoordinate(
                x,
                y);
        }

        /// <summary>
        /// 移動予定経路へ座標を追加します。
        /// </summary>
        public void ApplyAddMigrationCoordinate(
            int x,
            int y)
        {
            UnitStatus status =
                _macroUnitBehavior?.GetUnitStatus() ??
                _unitStatus;

            status?.AddMigrationCoordinate(
                new Vector2Int(
                    x,
                    y));
        }

        /// <summary>
        /// 指定された座標へのテレポート移動を
        /// ネットワークへ要求します。
        /// </summary>
        public void TeleportMoveToCoordinate(
            int x,
            int y)
        {
            if (!TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.TeleportMove(
                x,
                y);
        }

        /// <summary>
        /// 指定された座標へのテレポート移動を適用します。
        /// </summary>
        public void ApplyTeleportMoveToCoordinate(
            int x,
            int y)
        {
            _macroUnitBehavior?.TeleportMoveToCoordinate(
                x,
                y);

            _microUnitBehavior?.TeleportMoveToCoordinate(
                x,
                y);

            _tacticalMapUnitBehavior?.TeleportMoveToCoordinate(
                x,
                y);
        }

        /// <summary>
        /// 指定されたボード座標への補間移動を
        /// ネットワークへ要求します。
        /// </summary>
        public void LerpMoveToCoordinate(
            int teamId,
            int x,
            int y)
        {
            if (!TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.LerpMove(
                teamId,
                x,
                y);
        }

        /// <summary>
        /// 現在のフェイズに対応するユニット表現へ
        /// 補間移動を適用します。
        /// </summary>
        /// <remarks>
        /// 片方だけを動かして、盤面表示と実座標がずれるのを防ぎます。
        /// </remarks>
        public void ApplyLerpMoveToCoordinate(
                    int teamId,
                    int x,
                    int y)
        {
            _ = teamId;

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitBehavior)}: " +
                    "MatchPhaseManagerが存在しないため、" +
                    "補間移動を適用できません。",
                    this);

                return;
            }

            if (phaseManager.CurrentPhase !=
                MatchPhase.Micro)
            {

                if (_macroUnitBehavior == null || !_macroUnitBehavior.CanAffordMovement())
                {
                    return;
                }

                _macroUnitBehavior?.LerpMoveToCoordinate(
                                    x,
                                    y);

                _tacticalMapUnitBehavior?.LerpMoveToCoordinate(
                    x,
                    y);

                return;
            }

            if (_microUnitBehavior == null ||
                !_microUnitBehavior.TryLerpMoveToCoordinate(x, y))
            {
                return;
            }

            _tacticalMapUnitBehavior?.ApplyMicroMovementPosition(
                _microUnitBehavior.GetCurrentCoordinate(),
                _microUnitBehavior.GetCurrentCoordinate(),
                0.0f);
        }

        /// <summary>
        /// 移動同期を通信実装へ委譲します。
        /// </summary>
        public void PublishMicroMovement(MicroMovementSnapshot snapshot)
        {
            if (_network is IMicroMovementNetwork movementNetwork)
            {
                movementNetwork.PublishMicroMovement(snapshot);
            }
        }

        /// <summary>
        /// 受信したホスト移動状態を適用します。
        /// </summary>
        public void ApplyMicroMovement(MicroMovementSnapshot snapshot)
        {
            _microUnitBehavior?.ApplyMovementState(snapshot);
        }

        /// <summary>
        /// 味方AIの確定済み経路をMacro Phaseで表示するための
        /// 再生状態を開始します。
        /// </summary>
        /// <returns>
        /// MacroUnitとMapUnitの両方が存在し、再生を開始できた場合は
        /// <c>true</c>です。
        /// </returns>
        public bool BeginAiMacroPathVisualization()
        {
            if (_macroUnitBehavior == null ||
                _tacticalMapUnitBehavior == null)
            {
                return false;
            }

            return _macroUnitBehavior
                .BeginAiPlannedPathPlayback();
        }

        /// <summary>
        /// MacroUnitとMapUnitを、AIが計画した次の座標へ
        /// 既存のLerpMoveToCoordinate処理で補間移動させます。
        /// </summary>
        /// <param name="x">移動先のX座標です。</param>
        /// <param name="y">移動先のY座標です。</param>
        /// <returns>
        /// 表示用移動を開始できた場合は<c>true</c>です。
        /// </returns>
        public bool LerpAiMacroPathVisualizationToCoordinate(
            int x,
            int y)
        {
            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null ||
                phaseManager.CurrentPhase !=
                    MatchPhase.Macro ||
                _macroUnitBehavior == null ||
                !_macroUnitBehavior.IsAiPlannedPathPlayback ||
                _tacticalMapUnitBehavior == null)
            {
                return false;
            }

            ApplyLerpMoveToCoordinate(
                GetTeamId(),
                x,
                y);

            return true;
        }

        /// <summary>
        /// MacroUnitが次の経路座標へ到着した後、
        /// MapUnitも同じ座標へ確実に同期します。
        /// </summary>
        public void CompleteAiMacroPathVisualizationStep()
        {
            _tacticalMapUnitBehavior?
                .ForceCompleteLerpMove();
        }

        /// <summary>
        /// 味方AIのMacro経路表示を終了します。
        /// </summary>
        public void EndAiMacroPathVisualization()
        {
            _macroUnitBehavior?
                .EndAiPlannedPathPlayback();
        }

        /// <summary>
        /// 指定されたワールド位置への直接移動を
        /// ネットワークへ要求します。
        /// </summary>
        public void LerpMoveToPosition(
            Vector3 targetPosition)
        {
            if (!TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.LerpMove(
                targetPosition);
        }

        /// <summary>
        /// 指定されたワールド位置への直接移動を適用します。
        /// </summary>
        public void ApplyLerpMoveToPosition(
            Vector3 targetPosition)
        {
            if (MatchPhaseManager.Instance?.CurrentPhase != MatchPhase.Micro ||
                UnitManager.Instance == null || !UnitManager.Instance.HasMicroMovementAuthority)
            {
                return;
            }

            _microUnitBehavior?.LerpMoveToPosition(
                targetPosition);
        }

        /// <summary>
        /// 指定された時間の移動ロックをネットワークへ要求します。
        /// </summary>
        public void LockMovement(
            float duration)
        {
            if (duration <= 0.0f ||
                !TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.LockMovement(
                duration);
        }

        /// <summary>
        /// 指定された時間の移動ロックを適用します。
        /// </summary>
        public void ApplyLockMovement(
            float duration)
        {
            if (duration <= 0.0f)
            {
                return;
            }

            _microUnitBehavior?.LockMovement(
                duration);

            _tacticalMapUnitBehavior?.LockMovement(
                duration);
        }

        /// <summary>
        /// ユニットの移動速度変更をネットワークへ要求します。
        /// </summary>
        public void SetMoveSpeed(
            float value)
        {
            if (!TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.SetMoveSpeed(
                Mathf.Max(
                    0.0f,
                    value));
        }

        /// <summary>
        /// ユニットの移動速度変更を適用します。
        /// </summary>
        public void ApplySetMoveSpeed(
            float value)
        {
            float validSpeed =
                Mathf.Max(
                    0.0f,
                    value);

            _microUnitBehavior?.SetMoveSpeed(
                validSpeed);

            _tacticalMapUnitBehavior?.SetMoveSpeed(
                validSpeed);
        }

        /// <summary>
        /// ユニットのレイヤー変更をネットワークへ要求します。
        /// </summary>
        public void ChangeLayer(
            string layerName)
        {
            if (string.IsNullOrWhiteSpace(
                    layerName) ||
                !TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.ChangeLayer(
                layerName.Trim());
        }

        /// <summary>
        /// Macro、Micro、TacticalMapの各モデル階層だけを対象として、
        /// レイヤーを再帰的に変更します。
        /// Camera、視認センサー、武器および生成済みPranaの
        /// レイヤーは変更しません。
        /// </summary>
        public void ApplyChangeLayer(
            string layerName)
        {
            if (string.IsNullOrWhiteSpace(
                    layerName))
            {
                return;
            }

            int layer =
                LayerMask.NameToLayer(
                    layerName.Trim());

            if (layer < 0)
            {
                Debug.LogWarning(
                    $"{nameof(UnitBehavior)}: " +
                    $"指定されたレイヤーが存在しません。 Layer={layerName}",
                    this);

                return;
            }

            SetBehaviorModelLayerRecursively(
                _macroUnitBehavior,
                layer);

            SetBehaviorModelLayerRecursively(
                _microUnitBehavior,
                layer);

            SetBehaviorModelLayerRecursively(
                _tacticalMapUnitBehavior,
                layer);
        }

        /// <summary>
        /// 自分が直接視認した隠密ユニットを、自分の所有権でネットワークへ通知します。
        /// </summary>
        /// <param name="targetPlayerId">
        /// 発見した敵ユニットのIDです。
        /// </param>
        /// <param name="forceRefresh">
        /// 視認終了時に最後の観測期限を確実に送る場合は<c>true</c>です。
        /// </param>
        public void ReportStealthDetection(int targetPlayerId, bool forceRefresh = false)
        {
            UnitManager manager = UnitManager.Instance;
            if (_unitStatus == null || !_unitStatus.IsAlive || manager == null ||
                !manager.CanExecuteUnitLogic(PlayerId) ||
                !manager.TryGetUnit(targetPlayerId, out UnitBehavior target) ||
                !target.IsStealthActive || target.GetUnitStatus() == null || !target.GetUnitStatus().IsAlive ||
                GetTeamId() < 0 || target.GetTeamId() < 0 || target.GetTeamId() == GetTeamId())
            {
                return;
            }

            if (!forceRefresh && _nextDetectionReportByTarget.TryGetValue(targetPlayerId, out float nextReport) &&
                Time.time < nextReport)
            {
                return;
            }

            if (TryGetNetwork(out IUnitNetwork network))
            {
                _nextDetectionReportByTarget[targetPlayerId] = Time.time + k_DetectionReportInterval;
                network.ReportStealthDetection(targetPlayerId);
            }
        }

        /// <summary>
        /// 所有権検証済みの観測者から届いた視認通知を適用します。再送信は行いません。
        /// </summary>
        public void ApplyStealthDetection(int targetPlayerId)
        {
            UnitManager manager = UnitManager.Instance;
            if (_unitStatus == null || !_unitStatus.IsAlive || manager == null ||
                !manager.TryGetUnit(targetPlayerId, out UnitBehavior target) ||
                target.GetUnitStatus() == null || !target.GetUnitStatus().IsAlive ||
                GetTeamId() < 0 || target.GetTeamId() < 0 || target.GetTeamId() == GetTeamId())
            {
                return;
            }

            target._stealthDetectionMemory.Record(GetTeamId(), Time.time);
            target.RefreshStealthPresentation();
        }

        /// <summary>
        /// 指定チームが隠密ユニットの現在位置をマップで追跡できるか確認します。
        /// </summary>
        public bool IsMapRevealedToTeam(int observerTeamId)
        {
            return _stealthDetectionMemory.IsRevealed(observerTeamId, Time.time);
        }

        /// <summary>
        /// ステルス状態の変更をネットワークへ要求します。
        /// </summary>
        public void SwitchStealth(
            int teamId,
            bool isStealth)
        {
            int stealthOwnerTeamId =
                teamId >= 0
                    ? teamId
                    : TeamId;

            if (stealthOwnerTeamId < 0)
            {
                Debug.LogWarning(
                    $"{nameof(UnitBehavior)}: " +
                    "ステルス使用者のTeamIdを取得できません。" +
                    $" PlayerId={PlayerId}",
                    this);

                return;
            }

            if (!TryGetNetwork(
                    out IUnitNetwork network))
            {
                return;
            }

            network.SwitchStealth(
                stealthOwnerTeamId,
                isStealth);
        }

        /// <summary>
        /// ステルス状態をローカル表示へ適用します。
        /// </summary>
        public void ApplySwitchStealth(
            int teamId,
            bool isStealth)
        {
            int stealthOwnerTeamId =
                teamId >= 0
                    ? teamId
                    : TeamId;

            IsStealthActive =
                isStealth;

            _stealthOwnerTeamId =
                isStealth
                    ? stealthOwnerTeamId
                    : k_InvalidTeamId;

            RefreshStealthPresentation();
        }

        /// <summary>
        /// 指定された観測チームに対して，
        /// このユニットがステルス対象かどうかを判定します。
        /// </summary>
        public bool IsStealthHiddenFromTeam(
            int observerTeamId)
        {
            if (!IsStealthActive)
            {
                return false;
            }

            if (observerTeamId < 0 ||
                _stealthOwnerTeamId < 0)
            {
                return true;
            }

            return observerTeamId !=
                _stealthOwnerTeamId;
        }

        /// <summary>
        /// ローカルプレイヤーのチームを観測者として，
        /// ステルス用レイヤーを更新します。
        /// </summary>
        public void RefreshStealthPresentation()
        {
            UnitManager unitManager = UnitManager.Instance;
            int observerTeamId = unitManager != null ? unitManager.GetTeamId(unitManager.ControlledUnitId) : k_InvalidTeamId;
            bool isVisible = unitManager != null && unitManager.IsUnitVisibleToTeam(observerTeamId, PlayerId);

            _unitStatus?.SetVisible(isVisible);

            if (!IsStealthActive)
            {
                ApplyChangeLayer(
                    k_UnitLayerName);

                return;
            }

            bool isAllyObserver =
                IsLocalPlayerTeam(
                    _stealthOwnerTeamId);

            ApplyChangeLayer(
                isAllyObserver || isVisible
                    ? k_UnitLayerName
                    : k_StealthLayerName);

            if (!isAllyObserver && !isVisible && IsMapRevealedToTeam(observerTeamId))
            {
                SetBehaviorModelLayerRecursively(
                    _tacticalMapUnitBehavior,
                    LayerMask.NameToLayer(k_UnitLayerName));
            }
        }

        /// <summary>
        /// Microユニットのハイライト表示を切り替えます。
        /// </summary>
        /// <param name="isHighlighted">
        /// ハイライトを有効にする場合は<c>true</c>です。
        /// </param>
        public void SetHighlight(
            bool isHighlighted)
        {
            GameObject targetModel =
                _microUnitBehavior?.GetModel();

            if (targetModel == null)
            {
                return;
            }

            Outline outline =
                targetModel.GetComponent<Outline>();

            if (outline == null)
            {
                outline =
                    targetModel.GetComponentInChildren<Outline>(
                        true);
            }

            if (outline != null)
            {
                outline.enabled =
                    isHighlighted;
            }
        }

        /// <summary>
        /// Microユニットのハイライト表示を切り替えます。
        /// </summary>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="SetHighlight"/>を使用してください。
        /// </remarks>
        public void SwitchHighLight(
            bool ishighlight)
        {
            SetHighlight(
                ishighlight);
        }

        /// <summary>
        /// 指定されたネットワーク実装を取得し、
        /// 存在しない場合は追加します。
        /// </summary>
        private T GetOrAddNetwork<T>()
            where T : MonoBehaviour, IUnitNetwork
        {
            T network =
                GetComponent<T>();

            if (network == null)
            {
                network =
                    gameObject.AddComponent<T>();
            }

            network.enabled =
                true;

            return network;
        }

        /// <summary>
        /// 現在使用していないIUnitNetwork実装を無効にします。
        /// </summary>
        private void DisableInactiveNetworkComponents(
            IUnitNetwork activeNetwork)
        {
            MonoBehaviour[] behaviours =
                GetComponents<MonoBehaviour>();

            foreach (MonoBehaviour behaviour in
                    behaviours)
            {
                if (behaviour is not IUnitNetwork unitNetwork)
                {
                    continue;
                }

                behaviour.enabled =
                    ReferenceEquals(
                        unitNetwork,
                        activeNetwork);
            }
        }

        /// <summary>
        /// 現在のネットワーク実装を取得します。
        /// </summary>
        private bool TryGetNetwork(
            out IUnitNetwork network)
        {
            if (_network == null)
            {
                Initialize();
            }

            network =
                _network;

            if (network != null)
            {
                return true;
            }

            Debug.LogWarning(
                $"{nameof(UnitBehavior)}: " +
                "IUnitNetworkが初期化されていません。",
                this);

            return false;
        }

        /// <summary>
        /// Macro、Micro、TacticalMapの各動作を取得し、
        /// 初期ステータスと座標を設定します。
        /// </summary>
        private bool TryInitializeChildObjects(
            UnitData unitData,
            UnitStatus unitStatus,
            int x,
            int y,
            Vector3 rotation)
        {
            if (unitData == null ||
                unitStatus == null)
            {
                return false;
            }

            _macroUnitBehavior =
                GetUnitBehaviorFromObject<MacroUnitBehavior>(
                    _macroUnitObject);

            _microUnitBehavior =
                GetUnitBehaviorFromObject<MicroUnitBehavior>(
                    _microUnitObject);

            _tacticalMapUnitBehavior =
                GetUnitBehaviorFromObject<TacticalMapUnitBehavior>(
                    _tacticalMapUnitObject);

            if (_macroUnitBehavior == null ||
                _microUnitBehavior == null ||
                _tacticalMapUnitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(UnitBehavior)}: " +
                    "Macro、Micro、TacticalMapのいずれかの" +
                    "UnitBehaviorを取得できません。" +
                    $" Macro={_macroUnitBehavior != null}," +
                    $" Micro={_microUnitBehavior != null}," +
                    $" Map={_tacticalMapUnitBehavior != null}",
                    this);

                return false;
            }

            _macroUnitBehavior.SetUnitBehavior(
                this);

            _microUnitBehavior.SetUnitBehavior(
                this);

            _tacticalMapUnitBehavior.SetUnitBehavior(
                this);

            _macroUnitBehavior.SetUnitStatus(
                unitStatus);

            _microUnitBehavior.SetUnitStatus(
                unitStatus);

            _tacticalMapUnitBehavior.SetUnitStatus(
                unitStatus);

            _microUnitBehavior.SetYaw(
                rotation.y);

            ApplyTeleportMoveToCoordinate(
                x,
                y);

            _tacticalMapUnitBehavior.Initialize(
                Mathf.RoundToInt(
                    rotation.y));

            RefreshTeamMaterials();

            Transform camera = _macroUnitBehavior.UnitCamera.gameObject.transform;
            Vector3 position = camera.position;

            position.y = unitData.CameraHeight;
            camera.position = position;

            _macroUnitBehavior.LocalCameraOn();
            _microUnitBehavior.LocalCameraOn();
            _tacticalMapUnitBehavior.LocalCameraOn();

            return true;
        }

        /// <summary>
        /// 指定されたオブジェクトまたはその親から
        /// ユニット動作コンポーネントを取得します。
        /// </summary>
        private static T GetUnitBehaviorFromObject<T>(
            GameObject sourceObject)
            where T : Component
        {
            if (sourceObject == null)
            {
                return null;
            }

            return sourceObject.GetComponentInParent<T>();
        }

        /// <summary>
        /// ローカルプレイヤーとのチーム関係に応じた
        /// マテリアルをMacroとTacticalMapへ適用します。
        /// </summary>
        private void RefreshTeamMaterials()
        {
            GameDataRepository repository =
                GameDataRepository.Instance;

            UnitData unitData =
                repository?.GetUnitData();

            if (unitData == null)
            {
                return;
            }

            ApplyTeamMaterial(
                unitData,
                _macroUnitBehavior?.GetModel(),
                TeamId);

            ApplyTeamMaterial(
                unitData,
                _tacticalMapUnitBehavior?.GetModel(),
                TeamId);

            UnitTeamColorView colorView = GetComponent<UnitTeamColorView>();
            if (colorView == null)
            {
                colorView = gameObject.AddComponent<UnitTeamColorView>();
            }

            colorView.Invalidate();
        }

        /// <summary>
        /// ローカルプレイヤーとのチーム関係に応じた
        /// マテリアルを設定します。
        /// </summary>
        private void ApplyTeamMaterial(
            UnitData unitData,
            GameObject targetObject,
            int targetTeamId)
        {
            if (unitData == null ||
                targetObject == null)
            {
                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return;
            }

            int localPlayerId =
                unitManager.ControlledUnitId;

            int localTeamId =
                unitManager.GetTeamId(
                    localPlayerId);

            bool isSameTeam =
                localTeamId ==
                targetTeamId;

            Material material =
                isSameTeam
                    ? unitData.AllyMaterial
                    : unitData.EnemyMaterial;

            SetMaterial(
                targetObject,
                material);
        }

        /// <summary>
        /// ローカルプレイヤーの一人称表示時に、
        /// 自身の身体メッシュを非表示にします。
        /// </summary>
        private void RefreshLocalBodyVisibility()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            bool shouldHideBody =
                unitManager != null &&
                PlayerId ==
                unitManager.ControlledUnitId &&
                gameFlowManager != null &&
                !gameFlowManager.IsWeaponDebugEnabled;

            if (_bodyMeshes == null)
            {
                return;
            }

            foreach (MeshRenderer bodyMesh in
                    _bodyMeshes)
            {
                if (bodyMesh != null)
                {
                    bodyMesh.enabled =
                        !shouldHideBody;
                }
            }
        }

#if UNITY_EDITOR || PV_CAPTURE
        /// <summary>
        /// 製作者向け撮影カメラから自身の身体メッシュも表示します。
        /// </summary>
        public void ShowBodyForCapture()
        {
            if (_bodyMeshes == null)
            {
                return;
            }

            foreach (MeshRenderer bodyMesh in _bodyMeshes)
            {
                if (bodyMesh != null)
                {
                    bodyMesh.enabled = true;
                }
            }
        }

#endif

        /// <summary>
        /// ローカルプレイヤーのユニットである場合に、
        /// HUDへ現在のHPを反映します。
        /// </summary>
        private void RefreshLocalHealthGauge(
            bool animation)
        {
            if (_unitStatus == null ||
                _unitStatus.MaxHealth <= 0.0f)
            {
                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            HUDManager hudManager =
                HUDManager.Instance;

            if (unitManager == null ||
                hudManager == null ||
                PlayerId !=
                unitManager.ControlledUnitId)
            {
                return;
            }

            float healthRate =
                Mathf.Clamp01(
                    _unitStatus.CurrentHealth /
                    _unitStatus.MaxHealth);

            hudManager.SetHealthGauge(
                healthRate,
                animation);
        }

        /// <summary>
        /// 指定されたオブジェクト以下のレイヤーを再帰的に変更します。
        /// </summary>
        private static void SetLayerRecursively(
            GameObject targetObject,
            int layer)
        {
            if (targetObject == null)
            {
                return;
            }

            targetObject.layer =
                layer;

            foreach (Transform child in
                    targetObject.transform)
            {
                SetLayerRecursively(
                    child.gameObject,
                    layer);
            }
        }

        /// <summary>
        /// Unity上で有効なUnitBehavior表現のModel階層だけに
        /// レイヤー変更を適用します。
        /// </summary>
        /// <remarks>
        /// Cameraや視認センサーなど、Modelの外側にあるオブジェクトは
        /// Prefabで設定されたレイヤーを維持します。
        /// また、破棄済みのUnityオブジェクトへアクセスしないよう、
        /// 明示的なUnity null判定を行います。
        /// </remarks>
        private static void SetBehaviorModelLayerRecursively(
            UnitBehaviorBase targetBehavior,
            int layer)
        {
            if (targetBehavior == null)
            {
                return;
            }

            GameObject targetModel =
                targetBehavior.GetModel();

            if (targetModel == null)
            {
                return;
            }

            SetLayerRecursively(
                targetModel,
                layer);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定されたユニット参照を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_macroUnitObject == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitBehavior)}: " +
                    "Macro Unit Objectが設定されていません。",
                    this);
            }

            if (_microUnitObject == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitBehavior)}: " +
                    "Micro Unit Objectが設定されていません。",
                    this);
            }

            if (_tacticalMapUnitObject == null)
            {
                Debug.LogWarning(
                    $"{nameof(UnitBehavior)}: " +
                    "Tactical Map Unit Objectが設定されていません。",
                    this);
            }
        }
#endif
    }
}
