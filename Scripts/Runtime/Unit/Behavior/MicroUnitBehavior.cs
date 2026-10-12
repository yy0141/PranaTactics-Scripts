using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Board;
using PranaTactics.Controls;
using PranaTactics.Controls.AI;
using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.Networking;
using PranaTactics.Pranam;
using PranaTactics.Settings;
using PranaTactics.UI.Gameplay;
using PranaTactics.UI.Screens;
using PranaTactics.Unit.Networking.EOS;
using PranaTactics.Unit.Movement;
using PranaTactics.Unit.Presentation;

namespace PranaTactics.Unit
{
    /// <summary>
    /// Micro Phaseにおけるユニットの移動、照準、Pranam操作、
    /// 視認状態および撃破時の表示を管理します。
    /// </summary>
    /// <remarks>
    /// 操作権限を持つクライアントでは、プレイヤーまたはAIから
    /// <see cref="UnitInputState"/>を取得してユニットを操作します。
    ///
    /// 操作権限を持たないクライアントでは、ネットワークから受信した
    /// 照準情報を補間して表示へ反映します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MicroUnitBehavior :
        UnitBehaviorBase,
        IPunObservable,
        IMicroMovementParticipant
    {
        /// <summary>
        /// 水平移動または落下移動の到着判定距離です。
        /// </summary>
        private const float k_ArrivalThreshold = 0.05f;

        /// <summary>
        /// 発射準備中のShooter弾を保持します。
        /// </summary>
        private readonly HashSet<UnityEngine.Object> _preparingShooterProjectiles = new();

        /// <summary>
        /// 硬直中の落下加速度です。
        /// </summary>
        [Min(0.01f)]
        [SerializeField]
        private float _recoveryFallAcceleration = 30.0f;

        /// <summary>
        /// 硬直中の現在落下速度です。
        /// </summary>
        private float _recoveryFallSpeed;

        public override bool IsMovementLocked
        {
            get
            {
                _preparingShooterProjectiles.RemoveWhere(projectile => projectile == null);
                return base.IsMovementLocked || IsSniperAimLocked || _preparingShooterProjectiles.Count > 0;
            }
        }

        /// <summary>
        /// 弾の発射準備が終了するまで移動をロックします。
        /// </summary>
        public void RegisterShooterPreparation(UnityEngine.Object projectile)
        {
            if (projectile != null)
            {
                _preparingShooterProjectiles.Add(projectile);
            }
        }

        /// <summary>
        /// 発射または破棄された弾の移動ロックを解除します。
        /// </summary>
        public void ReleaseShooterPreparation(UnityEngine.Object projectile)
        {
            _preparingShooterProjectiles.Remove(projectile);
        }

        /// <summary>
        /// 移動硬直中も接地するまで垂直に降下させます。
        /// </summary>
        /// <remarks>
        /// ボードのSpace ColliderはTriggerのため明示的に検出します。
        /// 落下後は現在位置から残りの移動を再構築し、行動力は再消費しません。
        /// </remarks>
        private void AdvanceRecoveryFall(float seconds)
        {
            Vector3 previous = transform.position;
            float modelHeight = Model != null ? Model.transform.lossyScale.y : 0.0f;
            Vector3 rayOrigin = previous + Vector3.up * k_ArrivalThreshold;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                    Mathf.Infinity, LayerMask.GetMask("Space", "Wall"), QueryTriggerInteraction.Collide))
            {
                _velocity = Vector3.zero;
                return;
            }

            float landingHeight = hit.point.y + modelHeight;
            if (previous.y <= landingHeight + k_ArrivalThreshold)
            {
                _recoveryFallSpeed = 0.0f;
                _velocity = Vector3.zero;
                return;
            }

            float fallDistance = _recoveryFallSpeed * seconds +
                0.5f * _recoveryFallAcceleration * seconds * seconds;
            _recoveryFallSpeed += _recoveryFallAcceleration * seconds;
            transform.position = new Vector3(previous.x,
                Mathf.Max(landingHeight, previous.y - fallDistance), previous.z);
            _velocity = seconds > 0.0f ? (transform.position - previous) / seconds : Vector3.zero;
            if (_movementType != MovementType.None && !_isDirectMove && fallDistance > 0.0f)
            {
                _moveStartPosition = transform.position;
                _movementProgress = 0.0f;
                _movementType = Mathf.Abs(_moveStartPosition.y - _towardPosition.y) < k_ArrivalThreshold
                    ? MovementType.Horizontal : MovementType.Jump;
            }
        }

        /// <summary>
        /// 1マス移動の現在進捗です。
        /// </summary>
        private float _movementProgress;

        /// <summary>
        /// 待機再開時の行動力二重消費を防ぐ目標です。
        /// </summary>
        private Vector2Int? _paidMovementDestination;

        /// <summary>
        /// 移動による累積行動力消費です。
        /// </summary>
        private int _movementCost;

        /// <summary>
        /// 同期済みの到着歩数です。
        /// </summary>
        private int _completedMovementSteps;

        /// <summary>
        /// 移動状態の同期連番です。
        /// </summary>
        private int _movementSequence;

        /// <summary>
        /// 移動を開始したミクロフェイズのターンです。
        /// </summary>
        private int _movementTurn;

        /// <summary>
        /// フェイズ終了状態を同期済みかどうかです。
        /// </summary>
        private bool _hasFinalMovementPublished;

        /// <summary>
        /// 最後に受信した移動連番です。
        /// </summary>
        private int _receivedMovementSequence;

        /// <summary>
        /// 移動同期の次回送信時刻です。
        /// </summary>
        private float _nextMovementPublishTime;

        /// <summary>
        /// 遠隔Unitの補間先です。
        /// </summary>
        private Vector3 _remoteMovementPosition;

        /// <summary>
        /// 遠隔Unitの位置を受信済みかどうかです。
        /// </summary>
        private bool _hasRemoteMovementPosition;

        public int MovementPlayerId => _unitStatus != null ? _unitStatus.PlayerId : -1;
        public long OccupiedCell => EncodeCell(_currentCoordinate);
        public long DestinationCell => EncodeCell(_nextCoordinate);
        public bool IsMovementAlive => _unitStatus != null && !_isDead;
        public bool IsAdvancing => IsMovementAlive &&
            _movementType != MovementType.None && !_isDirectMove;
        public float SecondsToArrival
        {
            get
            {
                float rate = MovementRate;
                return !IsMovementValueFinite(rate) || rate <= 0.0f
                    ? float.PositiveInfinity : Mathf.Max(0.0f, 1.0f - _movementProgress) / rate;
            }
        }

        private float MovementRate
        {
            get
            {
                if (_unitStatus == null || IsMovementLocked)
                {
                    return 0.0f;
                }

                float speed = _unitStatus.CurrentMoveSpeed;
                if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0.0f)
                {
                    return 0.0f;
                }

                if (_movementType == MovementType.Jump && _moveStartPosition.y < _towardPosition.y)
                {
                    return speed / (Mathf.Max(0.05f, _jumpDuration) * Mathf.Max(0.001f, _unitStatus.MoveSpeed));
                }

                float horizontal = Vector2.Distance(
                    new Vector2(_moveStartPosition.x, _moveStartPosition.z),
                    new Vector2(_towardPosition.x, _towardPosition.z));
                float distance = _movementType == MovementType.Jump
                    ? horizontal + Mathf.Abs(_moveStartPosition.y - _towardPosition.y) / k_FallSpeedMultiplier
                    : Vector3.Distance(_moveStartPosition, _towardPosition);
                return speed / Mathf.Max(0.001f, distance);
            }
        }

        /// <summary>
        /// EOS照準情報を送信できる最短間隔です。
        /// </summary>
        private const float k_AimSendInterval = 0.05f;

        /// <summary>
        /// 照準が変化していない場合にも送信する間隔です。
        /// </summary>
        private const float k_AimHeartbeatInterval = 0.25f;

        /// <summary>
        /// 照準状態の変更として扱う最小角度です。
        /// </summary>
        private const float k_AimAngleThreshold = 0.5f;

        /// <summary>
        /// ネットワーク照準状態の補間速度です。
        /// </summary>
        private const float k_AimInterpolationSpeed = 18.0f;

        /// <summary>
        /// MainとSubの視点切り替え時の既定回転速度です。
        /// </summary>
        private const float k_DefaultPerspectiveTurnSpeed = 720.0f;

        /// <summary>
        /// プレイヤーの感度設定に依存しないAIの最大照準速度です。
        /// </summary>
        private const float k_AiAimDegreesPerSecond = 180.0f;

        /// <summary>
        /// 上下視点回転の既定最小角度です。
        /// </summary>
        private const float k_DefaultMinimumPitch = -89.0f;

        /// <summary>
        /// 上下視点回転の既定最大角度です。
        /// </summary>
        private const float k_DefaultMaximumPitch = 89.0f;

        /// <summary>
        /// ジャンプ移動の既定時間です。
        /// </summary>
        private const float k_DefaultJumpDuration = 1.0f;

        /// <summary>
        /// ジャンプ軌道の既定追加高度です。
        /// </summary>
        private const float k_DefaultJumpHeight = 2.0f;

        /// <summary>
        /// ジャンプ補間の既定イージング係数です。
        /// </summary>
        private const float k_DefaultJumpEasePower = 1.5f;

        /// <summary>
        /// 落下移動時の垂直移動速度倍率です。
        /// </summary>
        private const float k_FallSpeedMultiplier = 1.5f;

        /// <summary>
        /// 速度計算時の0除算を防ぐ最小経過時間です。
        /// </summary>
        private const float k_MinimumDeltaTime = 0.000001f;

        /// <summary>
        /// スナイパーの自動照準回転速度です。
        /// </summary>
        private const float k_SniperAutoAimTurnSpeed =
            180.0f;

        /// <summary>
        /// スナイパー自動照準を完了したと判定する角度差です。
        /// </summary>
        private const float k_SniperAutoAimTolerance =
            0.5f;

        /// <summary>
        /// 自動照準方向を有効と判断する
        /// 最小二乗距離です。
        /// </summary>
        private const float k_MinimumSniperAimSqrMagnitude =
            0.0001f;

        private enum MovementType
        {
            None,

            Horizontal,

            Jump
        }

        /// <summary>
        /// 戦術マップ上のユニット動作です。
        /// </summary>
        [Header("References")]
        [SerializeField]
        private TacticalMapUnitBehavior _mapUnitBehavior;

        /// <summary>
        /// MainおよびSubのPranam使用を管理します。
        /// </summary>
        [SerializeField]
        private PranamManager _pranamManager;

        /// <summary>
        /// Main Pranamの向きを参照するオブジェクトです。
        /// </summary>
        [FormerlySerializedAs("_mainTriggers")]
        [SerializeField]
        private GameObject _mainPranamRoot;

        /// <summary>
        /// Sub Pranamの向きを参照するオブジェクトです。
        /// </summary>
        [FormerlySerializedAs("_subTriggers")]
        [SerializeField]
        private GameObject _subPranamRoot;

        /// <summary>
        /// このユニットに対する攻撃予測を管理します。
        /// </summary>
        [SerializeField]
        private AttackPrediction _attackPrediction;

        /// <summary>
        /// 上下視点回転の最小角度です。
        /// </summary>
        [Header("Aim")]
        [FormerlySerializedAs("_MinimumPitch")]
        [Range(-89.0f, 0.0f)]
        [SerializeField]
        private float _minimumPitch =
            k_DefaultMinimumPitch;

        /// <summary>
        /// 上下視点回転の最大角度です。
        /// </summary>
        [FormerlySerializedAs("_MaximumPitch")]
        [Range(0.0f, 89.0f)]
        [SerializeField]
        private float _maximumPitch =
            k_DefaultMaximumPitch;

        /// <summary>
        /// MainとSubの視点切り替え時の回転速度です。
        /// </summary>
        [Min(1.0f)]
        [SerializeField]
        private float _perspectiveTurnSpeed =
            k_DefaultPerspectiveTurnSpeed;

        /// <summary>
        /// ジャンプ移動にかける時間です。
        /// </summary>
        [Header("Movement")]
        [Min(0.05f)]
        [SerializeField]
        private float _jumpDuration =
            k_DefaultJumpDuration;

        /// <summary>
        /// ジャンプ軌道に加える高さです。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _jumpHeight =
            k_DefaultJumpHeight;

        /// <summary>
        /// ジャンプ補間のイージング係数です。
        /// </summary>
        [Min(0.1f)]
        [SerializeField]
        private float _jumpEasePower =
            k_DefaultJumpEasePower;

        /// <summary>
        /// 照準および視認の詳細をログへ出力するかどうかです。
        /// </summary>
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLogging;

        /// <summary>
        /// AIなどから設定されるユニット入力です。
        /// </summary>
        private IUnitInput _unitInput;

        /// <summary>
        /// 現在の移動状態です。
        /// </summary>
        private MovementType _movementType =
            MovementType.Horizontal;

        /// <summary>
        /// ジャンプ移動開始時の位置です。
        /// </summary>
        private Vector3 _moveStartPosition;

        /// <summary>
        /// Main Pranam側の視点を使用しているかどうかです。
        /// </summary>
        private bool _isMainPranamPerspective =
            true;

        /// <summary>
        /// MainとSubの視点切り替え回転中かどうかです。
        /// </summary>
        private bool _isTurningPerspective;

        /// <summary>
        /// ユニット本体の水平回転角度です。
        /// </summary>
        private float _yaw;

        /// <summary>
        /// カメラの垂直回転角度です。
        /// </summary>
        private float _pitch;

        /// <summary>
        /// ネットワークから受信したユニット本体の水平回転角度です。
        /// </summary>
        private float _networkYaw;

        /// <summary>
        /// ネットワークから受信したカメラの垂直回転角度です。
        /// </summary>
        private float _networkPitch;

        /// <summary>
        /// ネットワークから受信したMain Pranamの水平回転角度です。
        /// </summary>
        private float _networkMainYaw;

        /// <summary>
        /// ネットワークから受信したSub Pranamの水平回転角度です。
        /// </summary>
        private float _networkSubYaw;

        /// <summary>
        /// ネットワークから受信した使用中の視点です。
        /// </summary>
        private bool _networkIsMainPranamPerspective =
            true;

        /// <summary>
        /// 視点切り替え後の目標水平角度です。
        /// </summary>
        private float _targetYaw;

        /// <summary>
        /// EOSで照準状態を同期するコンポーネントです。
        /// </summary>
        private EosUnitNetwork _eosUnitNetwork;

        /// <summary>
        /// EOSで一度以上照準状態を送信したかどうかです。
        /// </summary>
        private bool _hasSentAimState;

        /// <summary>
        /// EOSで最後に照準状態を送信した時刻です。
        /// </summary>
        private float _lastAimSendTime =
            float.NegativeInfinity;

        /// <summary>
        /// EOSで最後に送信したユニット水平角度です。
        /// </summary>
        private float _lastSentUnitYaw;

        /// <summary>
        /// EOSで最後に送信した垂直角度です。
        /// </summary>
        private float _lastSentPitch;

        /// <summary>
        /// EOSで最後に送信したMain Pranamの水平角度です。
        /// </summary>
        private float _lastSentMainYaw;

        /// <summary>
        /// EOSで最後に送信したSub Pranamの水平角度です。
        /// </summary>
        private float _lastSentSubYaw;

        /// <summary>
        /// EOSで最後に送信した使用中の視点です。
        /// </summary>
        private bool _lastSentIsMainPranamPerspective;

        /// <summary>
        /// 現在スナイパー照準操作を
        /// ロックしているプラーナムです。
        /// </summary>
        private SniperBase _activeSniper;

        /// <summary>
        /// 現在スナイパーによって
        /// 視点操作がロックされているかどうかを示します。
        /// </summary>
        /// <remarks>
        /// スナイパー処理全体のロックである
        /// <see cref="_activeSniper"/>とは独立して管理します。
        ///
        /// 発射後のスコープ表示が終了した時点で<c>false</c>となり、
        /// Pranamの硬直中であっても視点操作のみ再開できます。
        /// </remarks>
        private bool _isSniperViewLocked;

        /// <summary>
        /// 現在スナイパー照準中の武器が
        /// Main側かどうかを示します。
        /// </summary>
        private bool _sniperAimIsMain;

        /// <summary>
        /// Micro Phase開始時の初期化が完了しているかどうかです。
        /// </summary>
        private bool _isMicroPhaseInitialized;

        /// <summary>
        /// 移動完了後、移動ロック解除を待っているかどうかです。
        /// </summary>
        private bool _isMoveEndPending;

        /// <summary>
        /// 移動要求をネットワークへ送信済みかどうかです。
        /// </summary>
        private bool _isMoveRequestPending;

        /// <summary>
        /// ユニットが撃破状態かどうかです。
        /// </summary>
        private bool _isDead;

        /// <summary>
        /// UIによる操作ブロックが前フレームから継続しているかどうかです。
        /// </summary>
        private bool _wasBlockedByUi;

        /// <summary>
        /// 最後にネットワークへ要求した移動速度です。
        /// </summary>
        private float _lastRequestedMoveSpeed =
            float.NaN;

        public bool IsMainPranamPerspective
        {
            get
            {
                return _isMainPranamPerspective;
            }
        }

        public bool IsSniperAimLocked
        {
            get
            {
                CleanupInvalidSniperAimLock();

                return _activeSniper != null;
            }
        }

        public bool IsSniperViewLocked
        {
            get
            {
                CleanupInvalidSniperAimLock();

                return _activeSniper != null &&
                    _isSniperViewLocked;
            }
        }

        public bool IsSniperAimMain
        {
            get
            {
                return IsSniperAimLocked &&
                    _sniperAimIsMain;
            }
        }

        public bool IsDead
        {
            get
            {
                return _isDead;
            }
        }

        /// <summary>
        /// 初期カメラ状態とネットワーク照準状態を設定します。
        /// </summary>
        private void Awake()
        {
            InitializeNetworkAimState();
            ResolveEosUnitNetwork();

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            SwitchCamera(
                phaseManager != null &&
                phaseManager.CurrentPhase ==
                MatchPhase.Micro);

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager != null &&
                gameFlowManager.IsWeaponDebugEnabled &&
                UnitCamera != null)
            {
                UnitCamera.transform.position =
                    new Vector3(
                        0.0f,
                        20.0f,
                        -10.0f);
            }
        }

        /// <summary>
        /// 子オブジェクトの初期化後に照準状態を再取得します。
        /// </summary>
        private void Start()
        {
            InitializeNetworkAimState();
        }

        /// <summary>
        /// 移動ロック、フェイズ、入力、照準同期および移動を更新します。
        /// </summary>
        protected override void Update()
        {
            base.Update();

            if (_unitStatus == null)
            {
                return;
            }

            if (_isDead)
            {
                return;
            }

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null)
            {
                return;
            }

            if (phaseManager.CurrentPhase !=
                MatchPhase.Micro)
            {
                EndMicroPhase();

                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return;
            }

            bool isLocalPlayerUnit =
                _unitStatus.PlayerId ==
                unitManager.ControlledUnitId;

            bool canExecuteUnitLogic =
                unitManager.CanExecuteUnitLogic(
                    _unitStatus.PlayerId);

            if (canExecuteUnitLogic)
            {
                UpdateAuthoritativeUnit(
                    isLocalPlayerUnit);
            }
            else
            {
                ApplyRemoteAimInterpolation();
            }

            if (unitManager.HasMicroMovementAuthority)
            {
                if (_isDirectMove)
                {
                    DirectMove();
                }
            }
            else if (_hasRemoteMovementPosition)
            {
                transform.position = Vector3.Lerp(
                    transform.position, _remoteMovementPosition,
                    1.0f - Mathf.Exp(-25.0f * Time.deltaTime));
            }
        }

        /// <summary>
        /// Photon経由で照準状態を送受信します。
        /// </summary>
        /// <param name="stream">
        /// Photonの送受信ストリームです。
        /// </param>
        /// <param name="info">
        /// Photonメッセージ情報です。
        /// </param>
        public void OnPhotonSerializeView(
            PhotonStream stream,
            PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(
                    _yaw);

                stream.SendNext(
                    _pitch);

                stream.SendNext(
                    GetTransformWorldYaw(
                        GetMainPranamTransform(),
                        _yaw));

                stream.SendNext(
                    GetTransformWorldYaw(
                        GetSubPranamTransform(),
                        _yaw));

                stream.SendNext(
                    _isMainPranamPerspective);

                return;
            }

            _networkYaw =
                Convert.ToSingle(
                    stream.ReceiveNext());

            _networkPitch =
                Mathf.Clamp(
                    Convert.ToSingle(
                        stream.ReceiveNext()),
                    _minimumPitch,
                    _maximumPitch);

            _networkMainYaw =
                Convert.ToSingle(
                    stream.ReceiveNext());

            _networkSubYaw =
                Convert.ToSingle(
                    stream.ReceiveNext());

            _networkIsMainPranamPerspective =
                Convert.ToBoolean(
                    stream.ReceiveNext());
        }

        /// <summary>
        /// 現在のボード座標を取得します。
        /// </summary>
        public override Vector2Int GetCurrentCoordinate()
        {
            return _currentCoordinate;
        }

        /// <summary>
        /// このユニットを所有するプレイヤーIDを取得します。
        /// </summary>
        /// <returns>
        /// 所有プレイヤーIDです。
        /// </returns>
        public int PlayerId()
        {
            return _unitStatus != null
                ? _unitStatus.PlayerId
                : -1;
        }

        /// <summary>
        /// Main Pranamの向きを管理するTransformを取得します。
        /// </summary>
        public Transform GetMainPranamTransform()
        {
            return _mapUnitBehavior != null
                ? _mapUnitBehavior.GetMainTriggerTransform()
                : null;
        }

        /// <summary>
        /// Sub Pranamの向きを管理するTransformを取得します。
        /// </summary>
        public Transform GetSubPranamTransform()
        {
            return _mapUnitBehavior != null
                ? _mapUnitBehavior.GetSubTriggerTransform()
                : null;
        }

        /// <summary>
        /// Main Pranamの向きを管理するTransformを取得します。
        /// </summary>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="GetMainPranamTransform"/>を使用してください。
        /// </remarks>
        [Obsolete(
            "Use GetMainPranamTransform instead.")]
        public Transform GetMainTriggerTransform()
        {
            return GetMainPranamTransform();
        }

        /// <summary>
        /// Sub Pranamの向きを管理するTransformを取得します。
        /// </summary>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="GetSubPranamTransform"/>を使用してください。
        /// </remarks>
        [Obsolete(
            "Use GetSubPranamTransform instead.")]
        public Transform GetSubTriggerTransform()
        {
            return GetSubPranamTransform();
        }

        /// <summary>
        /// PranamManagerを初期化します。
        /// </summary>
        public void InitializePranam()
        {
            _pranamManager?.Initialize();
        }

        /// <summary>
        /// 指定されたMainまたはSubのPranamを切り替えます。
        /// </summary>
        /// <param name="isMain">
        /// Main側を切り替える場合は<c>true</c>、
        /// Sub側を切り替える場合は<c>false</c>です。
        /// </param>
        /// <param name="slotNumber">
        /// 切り替えるPranamのスロット番号です。
        /// </param>
        /// <returns>
        /// 切り替えに成功した場合は<c>true</c>です。
        /// </returns>
        /// <remarks>
        /// スナイパー照準中、または対象側のPranamが
        /// 硬直中の場合は切り替えません。
        /// </remarks>
        public bool TrySelectPranam(
            bool isMain,
            int slotNumber)
        {
            if (_pranamManager == null)
            {
                return false;
            }

            return _pranamManager.TrySelectPranam(
                isMain,
                slotNumber);
        }

        /// <summary>
        /// PranamManagerを初期化します。
        /// </summary>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="InitializePranam"/>を使用してください。
        /// </remarks>
        [Obsolete(
            "Use InitializePranam instead.")]
        public void AddTrigger()
        {
            InitializePranam();
        }

        /// <summary>
        /// このユニットを撃破状態へ変更します。
        /// </summary>
        public void SetDead()
        {
            if (_isDead)
            {
                return;
            }

            _isDead =
                true;

            ReleaseAllPranam();
            if (Model != null)
            {
                Model.SetActive(false);
            }
            ShowEliminationEffect();
        }

        /// <summary>
        /// ユニット本体の水平角度を設定します。
        /// </summary>
        public void SetYaw(
            float yaw)
        {
            _yaw =
                yaw;

            _networkYaw =
                yaw;

            transform.rotation =
                Quaternion.Euler(
                    0.0f,
                    _yaw,
                    0.0f);
        }

        /// <summary>
        /// 視点と主・副兵装の水平角度を揃え、視点切り替え中の回転を停止します。
        /// </summary>
        public void AlignPerspectiveAndPranamYaw(float yaw)
        {
            _isTurningPerspective = false;
            _targetYaw = yaw;
            SetYaw(yaw);
            SetWorldYaw(GetMainPranamTransform(), yaw);
            SetWorldYaw(GetSubPranamTransform(), yaw);
            _networkMainYaw = yaw;
            _networkSubYaw = yaw;
        }

        /// <summary>
        /// 主兵装の向きへ視点を戻し、切り替え途中の回転を終了します。
        /// </summary>
        public void ResetToMainPerspective()
        {
            _isMainPranamPerspective = true;
            _networkIsMainPranamPerspective = true;
            _isTurningPerspective = false;
            Transform mainPranam = GetMainPranamTransform();

            _targetYaw = mainPranam != null ? mainPranam.eulerAngles.y : transform.eulerAngles.y;
            SetYaw(_targetYaw);
        }

        public int MainPranamSlotIndex => _pranamManager?.MainPranamBehavior?.SelectedSlotIndex ?? -1;

        public int SubPranamSlotIndex => _pranamManager?.SubPranamBehavior?.SelectedSlotIndex ?? -1;

        public bool IsPerspectiveTransitioning => _isTurningPerspective;

        /// <summary>
        /// カメラの垂直角度を設定します。
        /// </summary>
        public void SetPitch(
            float pitch)
        {
            _pitch =
                Mathf.Clamp(
                    pitch,
                    _minimumPitch,
                    _maximumPitch);

            _networkPitch =
                _pitch;

            ApplyCameraPitch();
        }

        /// <summary>
        /// 指定されたスナイパーが
        /// 現在照準を開始可能か判定します。
        /// </summary>
        public bool CanBeginSniperAim(
            SniperBase sniper,
            bool isMain)
        {
            CleanupInvalidSniperAimLock();

            if (sniper == null)
            {
                return false;
            }

            if (_isTurningPerspective)
            {
                return false;
            }

            if (_isMainPranamPerspective !=
                isMain)
            {
                return false;
            }

            if (_activeSniper != null &&
                !ReferenceEquals(
                    _activeSniper,
                    sniper))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// スナイパー照準用の操作ロックを開始します。
        /// </summary>
        /// <param name="sniper">
        /// 照準を開始するスナイパーです。
        /// </param>
        /// <param name="isMain">
        /// Main側のスナイパーの場合は<c>true</c>です。
        /// </param>
        /// <returns>
        /// ロックを開始できた場合は<c>true</c>です。
        /// </returns>
        public bool TryBeginSniperAimLock(
            SniperBase sniper,
            bool isMain)
        {
            if (!CanBeginSniperAim(
                    sniper,
                    isMain))
            {
                return false;
            }

            _activeSniper =
                sniper;

            _sniperAimIsMain =
                isMain;

            _isSniperViewLocked =
                true;

            _isTurningPerspective =
                false;

            return true;
        }

        /// <summary>
        /// 無効化された旧スナイパーが照準ロックとして
        /// 残っている場合にロック状態を解除します。
        /// </summary>
        private void CleanupInvalidSniperAimLock()
        {
            if (_activeSniper == null)
            {
                _sniperAimIsMain =
                    false;

                _isSniperViewLocked =
                    false;

                return;
            }

            if (_activeSniper.isActiveAndEnabled &&
                _activeSniper.gameObject.activeInHierarchy)
            {
                return;
            }

            _activeSniper =
                null;

            _sniperAimIsMain =
                false;

            _isSniperViewLocked =
                false;
        }

        /// <summary>
        /// 指定されたスナイパーによる
        /// 視点操作ロックだけを解除します。
        /// </summary>
        /// <param name="sniper">
        /// 視点ロックを解除するスナイパーです。
        /// </param>
        /// <remarks>
        /// スナイパー処理全体のロックは解除しません。
        ///
        /// そのため、呼び出し後も
        /// <see cref="IsSniperAimLocked"/>は<c>true</c>のままとなり、
        /// 武器使用や武器切替などの制限は維持されます。
        /// </remarks>
        public void ReleaseSniperViewLock(
            SniperBase sniper)
        {
            if (_activeSniper == null ||
                !ReferenceEquals(
                    _activeSniper,
                    sniper))
            {
                return;
            }

            _isSniperViewLocked =
                false;
        }

        /// <summary>
        /// 指定されたスナイパーの
        /// 照準操作ロックを完全に解除します。
        /// </summary>
        public void ReleaseSniperAimLock(
            SniperBase sniper)
        {
            if (_activeSniper == null ||
                !ReferenceEquals(
                    _activeSniper,
                    sniper))
            {
                return;
            }

            _activeSniper =
                null;

            _sniperAimIsMain =
                false;

            _isSniperViewLocked =
                false;
        }

        /// <summary>
        /// スナイパー照準中にカメラとPranamを
        /// 指定されたワールド座標へ向けます。
        /// </summary>
        /// <returns>
        /// 対象中央への照準が完了している場合は<c>true</c>です。
        /// </returns>
        public bool UpdateSniperAutoAim(
            SniperBase sniper,
            Vector3 targetPosition)
        {
            if (_activeSniper == null ||
                !ReferenceEquals(
                    _activeSniper,
                    sniper))
            {
                return false;
            }

            Transform cameraTransform =
                UnitCamera != null
                    ? UnitCamera.transform
                    : transform;

            Vector3 direction =
                targetPosition -
                cameraTransform.position;

            if (direction.sqrMagnitude <
                k_MinimumSniperAimSqrMagnitude)
            {
                return false;
            }

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);

            float targetYaw =
                targetRotation.eulerAngles.y;

            float targetPitch =
                NormalizeSignedAngle(
                    targetRotation.eulerAngles.x);

            targetPitch =
                Mathf.Clamp(
                    targetPitch,
                    _minimumPitch,
                    _maximumPitch);

            float maximumDelta =
                k_SniperAutoAimTurnSpeed *
                Time.deltaTime;

            _yaw =
                Mathf.MoveTowardsAngle(
                    _yaw,
                    targetYaw,
                    maximumDelta);

            _pitch =
                Mathf.MoveTowardsAngle(
                    _pitch,
                    targetPitch,
                    maximumDelta);

            _pitch =
                Mathf.Clamp(
                    _pitch,
                    _minimumPitch,
                    _maximumPitch);

            transform.rotation =
                Quaternion.Euler(
                    0.0f,
                    _yaw,
                    0.0f);

            ApplyCameraPitch();

            Transform pranamTransform =
                _sniperAimIsMain
                    ? GetMainPranamTransform()
                    : GetSubPranamTransform();

            if (pranamTransform != null)
            {
                SetWorldYaw(
                    pranamTransform,
                    _yaw);
            }

            float yawDifference =
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        _yaw,
                        targetYaw));

            float pitchDifference =
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        _pitch,
                        targetPitch));

            return yawDifference <=
                    k_SniperAutoAimTolerance &&
                pitchDifference <=
                    k_SniperAutoAimTolerance;
        }

        /// <summary>
        /// 0～360度表現の角度を
        /// -180～180度へ変換します。
        /// </summary>
        private static float NormalizeSignedAngle(
            float angle)
        {
            if (angle > 180.0f)
            {
                angle -=
                    360.0f;
            }

            return angle;
        }

        /// <summary>
        /// EOS P2Pで受信した照準状態を
        /// 補間用の目標値へ反映します。
        /// </summary>
        public void ApplyNetworkAimState(
            float unitYaw,
            float pitch,
            float mainYaw,
            float subYaw,
            bool isMainPranamPerspective)
        {
            _networkYaw =
                unitYaw;

            _networkPitch =
                Mathf.Clamp(
                    pitch,
                    _minimumPitch,
                    _maximumPitch);

            _networkMainYaw =
                mainYaw;

            _networkSubYaw =
                subYaw;

            _networkIsMainPranamPerspective =
                isMainPranamPerspective;
        }

        /// <summary>
        /// Micro Phase盤面上の指定座標の位置を取得します。
        /// </summary>
        protected override Vector3 GetMoveCoordinatePosition(
            int x,
            int y)
        {
            BoardManager boardManager =
                BoardManager.Instance;

            return boardManager != null
                ? boardManager.GetMicroPhaseHexCellPosition(
                    x,
                    y)
                : transform.position;
        }

        /// <summary>
        /// Micro Phase盤面上の指定座標のスケールを取得します。
        /// </summary>
        protected override Vector3 GetMoveCoordinateScale(
            int x,
            int y)
        {
            BoardManager boardManager =
                BoardManager.Instance;

            return boardManager != null
                ? boardManager.GetMicroPhaseHexCellScale(
                    x,
                    y)
                : Vector3.zero;
        }

        /// <summary>
        /// 攻撃予測コンポーネントを取得します。
        /// </summary>
        public AttackPrediction GetAttackPrediction()
        {
            return _attackPrediction;
        }

        /// <summary>
        /// テレポート移動完了後の状態を更新します。
        /// </summary>
        protected override void TeleportMoveEnd(
            Vector2Int coordinate)
        {
            _currentCoordinate =
                coordinate;

            _nextCoordinate =
                default;

            _towardPosition =
                default;

            _isMoveRequestPending =
                false;

            _movementType =
                MovementType.None;
            _movementProgress = 0.0f;
            _paidMovementDestination = null;

            _unitStatus?.SetMovable(
                false);
            BoardManager.Instance?.Traps?.NotifyUnitEnteredCell(_unitBehavior, _currentCoordinate);
        }

        /// <summary>
        /// 指定されたMicro Phase座標への移動を開始します。
        /// </summary>
        public override void LerpMoveToCoordinate(
            int x,
            int y)
        {
            TryLerpMoveToCoordinate(x, y);
        }

        /// <summary>
        /// 実行中の移動と兵装で共有する行動力から、次の1マスを移動できるか判定します。
        /// </summary>
        public bool CanAffordMovement()
        {
            if (_unitStatus == null)
            {
                return false;
            }

            var rule = GameRuleManager.Instance?.CurrentRule;

            return rule != null && (_paidMovementDestination.HasValue || !rule.UseActionPoint ||
                _unitStatus.CanAffordProactiveness(_unitStatus.MoveProactivenessCost));
        }

        /// <summary>
        /// 移動を受理した場合だけtrueを返し、戦術マップとの同期に使用します。
        /// </summary>
        public bool TryLerpMoveToCoordinate(int x, int y)
        {
            UnitManager manager = UnitManager.Instance;
            if (_unitStatus == null || _isDead || manager == null || !manager.HasMicroMovementAuthority)
            {
                return false;
            }

            BeginMicroPhaseIfNeeded(MovementPlayerId == manager.ControlledUnitId);
            _nextMovementPublishTime = 0.0f;
            _isMoveRequestPending = false;
            Vector2Int destination = new Vector2Int(x, y);
            if (_isDead || destination == _currentCoordinate ||
                manager.IsMicroCellOccupied(destination, MovementPlayerId) ||
                _hasFinalMovementPublished || _isDirectMove || IsMovementLocked || !CanAffordMovement() || _unitStatus.IsMovable ||
                (_paidMovementDestination.HasValue && _paidMovementDestination.Value != destination &&
                    GameRuleManager.Instance?.CurrentRule?.UseActionPoint == true &&
                    !_unitStatus.CanAffordProactiveness(_unitStatus.MoveProactivenessCost)))
            {
                return false;
            }

            _nextCoordinate =
                new Vector2Int(
                    x,
                    y);

            base.LerpMoveToCoordinate(
                x,
                y);

            return _unitStatus.IsMovable;
        }

        /// <summary>
        /// 高さ差に応じて水平移動またはジャンプ移動を開始します。
        /// 1マスの移動開始時に、兵装と共有する行動力を消費します。
        /// </summary>
        protected override void LerpMoveStart()
        {
            _moveStartPosition = transform.position;
            _movementProgress = 0.0f;
            float heightDifference =
                Mathf.Abs(
                    transform.position.y -
                    _towardPosition.y);

            if (heightDifference <
                k_ArrivalThreshold)
            {
                _movementType =
                    MovementType.Horizontal;
            }
            else
            {
                _movementType =
                    MovementType.Jump;

                _moveStartPosition =
                    transform.position;

            }

            if (_unitStatus == null)
            {
                return;
            }

            if (!_paidMovementDestination.HasValue || _paidMovementDestination.Value != _nextCoordinate)
            {
                int cost = GameRuleManager.Instance?.CurrentRule?.UseActionPoint == true
                    ? _unitStatus.MoveProactivenessCost : 0;
                CalcProactiveness(cost);
                _movementCost += cost;
                _paidMovementDestination = _nextCoordinate;
            }

            _unitStatus.SetMovable(
                true);
        }

        /// <summary>
        /// 座標移動完了後に現在座標と移動経路を更新します。
        /// </summary>
        protected override void LerpMoveEnd()
        {
            _completedMovementSteps++;
            _paidMovementDestination = null;
            _currentCoordinate =
                _nextCoordinate;

            if (_unitStatus != null &&
                _unitStatus.MigrationPath != null &&
                _unitStatus.MigrationPath.Count > 0)
            {
                _unitStatus.RemoveNextMigrationCoordinate();
            }

            _nextCoordinate =
                default;

            _towardPosition =
                default;

            _movementType =
                MovementType.None;

            _isMoveRequestPending =
                false;

            if (IsMovementLocked)
            {
                _isMoveEndPending =
                    true;
            }
            else
            {
                _unitStatus?.SetMovable(
                    false);
            }

            _unitBehavior?
                .GetTacticalMapUnitBehavior()?
                .ForceCompleteLerpMove();
            BoardManager.Instance?.Traps?.NotifyUnitEnteredCell(_unitBehavior, _currentCoordinate);
        }

        /// <summary>
        /// 移動同期の位置ベクトルを検証します。
        /// </summary>
        private static bool IsMovementVectorFinite(Vector3 value)
        {
            return IsMovementValueFinite(value.x) && IsMovementValueFinite(value.y) && IsMovementValueFinite(value.z);
        }

        /// <summary>
        /// 移動同期の浮動小数点値を検証します。
        /// </summary>
        private static bool IsMovementValueFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// 座標を衝突判定用のキーへ変換します。
        /// </summary>
        private static long EncodeCell(Vector2Int coordinate)
        {
            return ((long)coordinate.x << 32) | (uint)coordinate.y;
        }

        /// <summary>
        /// 共通時間軸上で現在の速度に応じて移動を進めます。
        /// </summary>
        public void AdvanceMovement(float seconds)
        {
            if (IsMovementAlive && IsMovementLocked)
            {
                AdvanceRecoveryFall(seconds);
                return;
            }

            _recoveryFallSpeed = 0.0f;
            if (_movementType == MovementType.None || _isDirectMove || !IsMovementAlive)
            {
                return;
            }

            Vector3 previous = transform.position;
            float rate = MovementRate;
            float increment = IsMovementValueFinite(rate) ? rate * seconds : 0.0f;
            _movementProgress = Mathf.Clamp01(_movementProgress + increment);
            transform.position = GetMovementPosition(_movementProgress);
            _velocity = seconds > 0.0f ? (transform.position - previous) / seconds : Vector3.zero;
            _unitBehavior.GetTacticalMapUnitBehavior()?.ApplyMicroMovementPosition(
                _currentCoordinate, _nextCoordinate, _movementProgress);
        }

        /// <summary>
        /// 水平、上昇、落下の進捗から表示位置を取得します。
        /// </summary>
        private Vector3 GetMovementPosition(float progress)
        {
            if (_movementType == MovementType.Horizontal)
            {
                return Vector3.Lerp(_moveStartPosition, _towardPosition, progress);
            }

            if (_moveStartPosition.y > _towardPosition.y)
            {
                Vector3 corner = new Vector3(_towardPosition.x, _moveStartPosition.y, _towardPosition.z);
                float horizontal = Vector3.Distance(_moveStartPosition, corner);
                float vertical = Mathf.Abs(corner.y - _towardPosition.y) / k_FallSpeedMultiplier;
                float distance = progress * (horizontal + vertical);
                return distance < horizontal && horizontal > 0.0f
                    ? Vector3.Lerp(_moveStartPosition, corner, distance / horizontal)
                    : Vector3.Lerp(corner, _towardPosition, (distance - horizontal) / Mathf.Max(0.001f, vertical));
            }

            float eased = EaseInOut(progress);
            Vector3 middle = (_moveStartPosition + _towardPosition) * 0.5f;
            middle.y = Mathf.Max(_moveStartPosition.y, _towardPosition.y) + _jumpHeight;
            float inverse = 1.0f - eased;
            return inverse * inverse * _moveStartPosition + 2.0f * inverse * eased * middle +
                eased * eased * _towardPosition;
        }

        /// <summary>
        /// ホストの占有裁定で到着を確定します。
        /// </summary>
        public void CompleteMovement()
        {
            transform.position = _towardPosition;
            _velocity = Vector3.zero;
            _nextMovementPublishTime = 0.0f;
            LerpMoveEnd();
            _unitBehavior.GetTacticalMapUnitBehavior()?.ApplyMicroMovementPosition(
                _currentCoordinate, _currentCoordinate, 0.0f);
        }

        /// <summary>
        /// 経路を消費せずに直前の確定マスで停止します。
        /// </summary>
        /// <remarks>
        /// 競合解決と同じ更新内で位置を戻し、到着したUnitとの重なりを防ぎます。
        /// 停止や移動ロック中でも確定マスで待機します。
        /// </remarks>
        public void BlockMovement()
        {
            if (TryCalculateMoveTargetPosition(_currentCoordinate.x, _currentCoordinate.y, out Vector3 confirmedPosition))
            {
                transform.position = confirmedPosition;
            }
            _movementProgress = 0.0f;
            _movementType = MovementType.None;
            _isMoveRequestPending = false;
            _unitStatus.SetMovable(false);
            _velocity = Vector3.zero;
            _nextMovementPublishTime = 0.0f;
            _unitBehavior.GetTacticalMapUnitBehavior()?.ApplyMicroMovementPosition(
                _currentCoordinate, _currentCoordinate, 0.0f);
        }

        /// <summary>
        /// ホストが確定した現在状態を定期同期します。
        /// </summary>
        public void PublishMovementState(bool isFinal = false)
        {
            if (!isFinal && Time.unscaledTime < _nextMovementPublishTime)
            {
                return;
            }

            _nextMovementPublishTime = Time.unscaledTime + (_unitStatus.IsMovable || _isDirectMove ? 0.1f : 0.5f);
            _unitBehavior.PublishMicroMovement(new MicroMovementSnapshot
            {
                Turn = _movementTurn,
                IsFinal = isFinal,
                Sequence = ++_movementSequence,
                Coordinate = _currentCoordinate,
                Destination = _nextCoordinate,
                Position = transform.position,
                IsMoving = _unitStatus.IsMovable,
                MovementCost = _movementCost,
                CompletedSteps = _completedMovementSteps,
                StartPosition = _moveStartPosition,
                TargetPosition = _towardPosition,
                Progress = _movementProgress,
                MovementKind = (int)_movementType,
                HasPaidDestination = _paidMovementDestination.HasValue,
                PaidDestination = _paidMovementDestination.GetValueOrDefault(),
                IsDirectMoving = _isDirectMove,
                DirectTargetPosition = _directTargetPosition
            });
        }

        /// <summary>
        /// 未到着の移動を直前の確定マスで終了して最終状態を同期します。
        /// </summary>
        public void FinalizeMicroMovement()
        {
            if (_hasFinalMovementPublished || _unitStatus == null || _isDead)
            {
                return;
            }

            _hasFinalMovementPublished = true;
            if (TryCalculateMoveTargetPosition(_currentCoordinate.x, _currentCoordinate.y, out Vector3 position))
            {
                transform.position = position;
            }

            _movementType = MovementType.None;
            _movementProgress = 0.0f;
            _isDirectMove = false;
            _isMoveRequestPending = false;
            _isMoveEndPending = false;
            _paidMovementDestination = null;
            _velocity = Vector3.zero;
            _unitStatus.SetMovable(false);
            if (UnitManager.Instance != null && UnitManager.Instance.HasMicroMovementAuthority)
            {
                PublishMovementState(true);
                _unitBehavior.ApplyTeleportMoveToCoordinate(_currentCoordinate.x, _currentCoordinate.y);
            }
        }

        /// <summary>
        /// 同じターンの移動状態または直前ターンの終了状態だけを受理します。
        /// </summary>
        private static bool CanApplyMovementTurn(MicroMovementSnapshot snapshot)
        {
            MatchPhaseManager phaseManager = MatchPhaseManager.Instance;
            int turn = MatchManager.Instance != null ? MatchManager.Instance.CurrentTurn : 0;
            if (phaseManager == null)
            {
                return false;
            }

            if (phaseManager.CurrentPhase == MatchPhase.Micro)
            {
                return snapshot.Turn == turn;
            }

            return snapshot.IsFinal &&
                (phaseManager.CurrentPhase == MatchPhase.Macro || phaseManager.CurrentPhase == MatchPhase.MacroTransition) &&
                (snapshot.Turn == turn || snapshot.Turn == turn - 1);
        }

        /// <summary>
        /// ホストの確定状態だけを受信側へ反映します。
        /// </summary>
        public void ApplyMovementState(MicroMovementSnapshot snapshot)
        {
            if (snapshot == null || _unitStatus == null || _isDead ||
                !CanApplyMovementTurn(snapshot) ||
                UnitManager.Instance == null || UnitManager.Instance.HasMicroMovementAuthority ||
                snapshot.Sequence <= _receivedMovementSequence ||
                !IsMovementValueFinite(snapshot.Position.x) || !IsMovementValueFinite(snapshot.Position.y) || !IsMovementValueFinite(snapshot.Position.z) ||
                !IsMovementValueFinite(snapshot.Progress) || snapshot.Progress < 0.0f || snapshot.Progress > 1.0f ||
                !IsMovementVectorFinite(snapshot.StartPosition) || !IsMovementVectorFinite(snapshot.TargetPosition) ||
                !IsMovementVectorFinite(snapshot.DirectTargetPosition) ||
                snapshot.MovementKind < (int)MovementType.None || snapshot.MovementKind > (int)MovementType.Jump ||
                snapshot.MovementCost < _movementCost || snapshot.CompletedSteps < _completedMovementSteps)
            {
                return;
            }

            bool isMicro = MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Micro;
            if (isMicro)
            {
                BeginMicroPhaseIfNeeded(MovementPlayerId == UnitManager.Instance.ControlledUnitId);
            }

            Vector2Int previousCoordinate = _currentCoordinate;
            _receivedMovementSequence = snapshot.Sequence;
            _movementSequence = Mathf.Max(_movementSequence, snapshot.Sequence);
            _moveStartPosition = snapshot.StartPosition;
            _towardPosition = snapshot.TargetPosition;
            _movementProgress = snapshot.Progress;
            _movementType = (MovementType)snapshot.MovementKind;
            _paidMovementDestination = snapshot.HasPaidDestination ? snapshot.PaidDestination : (Vector2Int?)null;
            _isMoveEndPending = snapshot.IsMoving && _movementType == MovementType.None;
            _isDirectMove = snapshot.IsDirectMoving;
            _directTargetPosition = snapshot.DirectTargetPosition;
            if (isMicro)
            {
                CalcProactiveness(snapshot.MovementCost - _movementCost);
            }

            _movementCost = snapshot.MovementCost;
            int completed = snapshot.CompletedSteps - _completedMovementSteps;
            for (int step = 0; isMicro && step < completed && _unitStatus.MigrationPath.Count > 0; step++)
            {
                _unitStatus.RemoveNextMigrationCoordinate();
            }

            _completedMovementSteps = snapshot.CompletedSteps;
            _currentCoordinate = snapshot.Coordinate;
            _nextCoordinate = snapshot.Destination;
            if (isMicro)
            {
                _unitStatus.SetMovable(snapshot.IsMoving);
            }

            _isMoveRequestPending = false;
            _remoteMovementPosition = snapshot.Position;
            _velocity = _hasRemoteMovementPosition ? (snapshot.Position - transform.position) / 0.1f : Vector3.zero;
            _hasRemoteMovementPosition = true;
            if (!snapshot.IsMoving && !snapshot.IsDirectMoving)
            {
                transform.position = snapshot.Position;
                _velocity = Vector3.zero;
            }

            bool shouldUpdateMap = isMicro || previousCoordinate != _currentCoordinate ||
                _unitStatus.MigrationPath.Count == 0;
            if (shouldUpdateMap)
            {
                _unitBehavior.GetTacticalMapUnitBehavior()?.ApplyMicroMovementPosition(
                    _currentCoordinate, _nextCoordinate,
                    snapshot.IsMoving && snapshot.MovementKind != (int)MovementType.None ? snapshot.Progress : 0.0f);
            }

            _hasFinalMovementPublished = snapshot.IsFinal;
            if (snapshot.IsFinal && shouldUpdateMap)
            {
                _unitBehavior.ApplyTeleportMoveToCoordinate(_currentCoordinate.x, _currentCoordinate.y);
            }
        }

        /// <summary>
        /// ジャンプ補間用のEase In/Out値を計算します。
        /// </summary>
        private float EaseInOut(
            float value)
        {
            float clampedValue =
                Mathf.Clamp01(
                    value);

            float power =
                Mathf.Max(
                    0.1f,
                    _jumpEasePower);

            float first =
                Mathf.Pow(
                    clampedValue,
                    power);

            float second =
                Mathf.Pow(
                    1.0f -
                    clampedValue,
                    power);

            float denominator =
                first +
                second;

            return denominator >
                Mathf.Epsilon
                ? first /
                denominator
                : clampedValue;
        }

        /// <summary>
        /// AIなどが使用するユニット入力を設定します。
        /// </summary>
        public void SetUnitInput(
            IUnitInput unitInput)
        {
            _unitInput =
                unitInput;
        }

        /// <summary>
        /// 外部から設定されたユニット入力を解除します。
        /// </summary>
        public void ClearUnitInput()
        {
            _unitInput =
                null;
        }

        /// <summary>
        /// 操作引き継ぎ時にAIの押しっぱなし状態を解除します。移動経路と消費済み行動力は保持します。
        /// </summary>
        public void ReleaseControlInput()
        {
            ReleaseAllPranam();
            ClearUnitInput();
            ResetToMainPerspective();
        }

        /// <summary>
        /// 現在のユニットに対応する入力状態を取得します。
        /// </summary>
        private UnitInputState GetCurrentInputState()
        {
            if (_unitInput != null)
            {
                return _unitInput.CurrentState;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            InputManager inputManager =
                InputManager.Instance;

            if (unitManager != null &&
                inputManager != null &&
                _unitStatus != null &&
                _unitStatus.PlayerId ==
                unitManager.ControlledUnitId)
            {
                return inputManager.CurrentState;
            }

            return new UnitInputState
            {
                SelectedSlotNumber = -1
            };
        }

        /// <summary>
        /// 入力状態に応じて移動速度を変更します。
        /// </summary>
        private void UpdateMoveSpeed(
            UnitInputState state)
        {
            if (_unitStatus == null ||
                _unitBehavior == null)
            {
                return;
            }

            float targetSpeed;

            if (state.IsStopping)
            {
                targetSpeed =
                    0.0f;
            }
            else if (state.IsWalking)
            {
                targetSpeed =
                    _unitStatus.WalkMoveSpeed;
            }
            else
            {
                targetSpeed =
                    _unitStatus.MoveSpeed;
            }

            targetSpeed *= _unitBehavior.HitMovementMultiplier;

            if (!float.IsNaN(
                    _lastRequestedMoveSpeed) &&
                Mathf.Approximately(
                    _lastRequestedMoveSpeed,
                    targetSpeed))
            {
                return;
            }

            _lastRequestedMoveSpeed =
                targetSpeed;

            _unitBehavior.SetMoveSpeed(
                targetSpeed);
        }

        /// <summary>
        /// MainおよびSub Pranamの照準方向を入力に応じて回転します。
        /// </summary>
        private void RotatePranamPerspective(
            UnitInputState state)
        {
            GameRuleManager gameRuleManager =
                GameRuleManager.Instance;

            if (gameRuleManager == null ||
                !gameRuleManager
                    .CurrentRule
                    .AllowPranamRotate)
            {
                return;
            }

            Vector2 primaryAxis =
                state.PrimaryAxis;

            bool isAiInput = _unitInput is AIUnitInput;
            float aiRotationScale = k_AiAimDegreesPerSecond * Time.deltaTime;

            float primaryPitch =
                primaryAxis.y *
                (isAiInput ? aiRotationScale : SettingsManager.Current.MouseSensitivity);

            float primaryYaw =
                primaryAxis.x *
                (isAiInput ? aiRotationScale : SettingsManager.Current.MouseSensitivity);

            if (_isMainPranamPerspective)
            {
                RotateCamera(
                    primaryPitch,
                    primaryYaw);
            }

            if (!Mathf.Approximately(
                    primaryYaw,
                    0.0f))
            {
                _mapUnitBehavior?.RotateMainTrigger(
                    primaryYaw);
            }

            Vector2 secondaryAxis =
                state.SecondaryAxis;

            float secondaryPitch =
                secondaryAxis.y *
                (isAiInput ? aiRotationScale : SettingsManager.Current.KeyboardSensitivity * Time.deltaTime);

            float secondaryYaw =
                secondaryAxis.x *
                (isAiInput ? aiRotationScale : SettingsManager.Current.KeyboardSensitivity * Time.deltaTime);

            if (!_isMainPranamPerspective)
            {
                RotateCamera(
                    secondaryPitch,
                    secondaryYaw);
            }

            if (!Mathf.Approximately(
                    secondaryYaw,
                    0.0f))
            {
                _mapUnitBehavior?.RotateSubTrigger(
                    secondaryYaw);
            }
        }

        /// <summary>
        /// ユニットとカメラを指定された入力角度だけ回転します。
        /// </summary>
        private void RotateCamera(
            float pitchDelta,
            float yawDelta)
        {
            _yaw +=
                yawDelta;

            _pitch -=
                pitchDelta;

            _pitch =
                Mathf.Clamp(
                    _pitch,
                    _minimumPitch,
                    _maximumPitch);

            transform.rotation =
                Quaternion.Euler(
                    0.0f,
                    _yaw,
                    0.0f);

            ApplyCameraPitch();
        }

        /// <summary>
        /// MainとSubの視点切り替え入力を処理します。
        /// </summary>
        private void UpdatePerspectiveSwitch(
            UnitInputState state)
        {
            if (state.ShouldSwitchPerspective)
            {
                Transform targetTransform =
                    _isMainPranamPerspective
                        ? GetSubPranamTransform()
                        : GetMainPranamTransform();

                if (targetTransform == null)
                {
                    targetTransform =
                        _isMainPranamPerspective
                            ? _subPranamRoot?.transform
                            : _mainPranamRoot?.transform;
                }

                if (targetTransform != null)
                {
                    _targetYaw =
                        targetTransform.eulerAngles.y;

                    _isTurningPerspective =
                        true;
                }

                _isMainPranamPerspective =
                    !_isMainPranamPerspective;
            }

            if (_isTurningPerspective)
            {
                TurnPranamPerspective();
            }
        }

        /// <summary>
        /// ユニット本体を選択したPranamの方向へ滑らかに回転します。
        /// </summary>
        private void TurnPranamPerspective()
        {
            _yaw =
                Mathf.MoveTowardsAngle(
                    _yaw,
                    _targetYaw,
                    _perspectiveTurnSpeed *
                    Time.deltaTime);

            transform.rotation =
                Quaternion.Euler(
                    0.0f,
                    _yaw,
                    0.0f);

            if (Mathf.Abs(
                    Mathf.DeltaAngle(
                        _yaw,
                        _targetYaw)) >
                0.1f)
            {
                return;
            }

            _yaw =
                _targetYaw;

            transform.rotation =
                Quaternion.Euler(
                    0.0f,
                    _yaw,
                    0.0f);

            _isTurningPerspective =
                false;
        }

        /// <summary>
        /// 入力状態に応じてMainおよびSub Pranamを使用します。
        /// </summary>
        private void UsePranam(
            UnitInputState state)
        {
            if (_pranamManager == null)
            {
                return;
            }

            if (IsSniperAimLocked)
            {
                return;
            }

            bool shouldUseMain =
                state.IsPrimaryPressed ||
                state.IsPrimaryHeld &&
                _pranamManager.CanUseMainPranamHold;

            if (shouldUseMain)
            {
                _pranamManager.UseMainPranam();

                if (IsSniperAimLocked)
                {
                    return;
                }
            }

            bool shouldUseSub =
                state.IsSecondaryPressed ||
                state.IsSecondaryHeld &&
                _pranamManager.CanUseSubPranamHold;

            if (shouldUseSub)
            {
                _pranamManager.UseSubPranam();
            }

            if (state.IsPrimaryReleased)
            {
                _pranamManager.ReleaseMainPranam();
            }

            if (state.IsSecondaryReleased)
            {
                _pranamManager.ReleaseSubPranam();
            }
        }

        /// <summary>
        /// MainおよびSub Pranamの継続使用を解除します。
        /// </summary>
        private void ReleaseAllPranam()
        {
            if (_pranamManager == null)
            {
                return;
            }

            _pranamManager.ReleaseMainPranam();
            _pranamManager.ReleaseSubPranam();
        }

        /// <summary>
        /// 操作権限を持つユニットの入力と同期を更新します。
        /// </summary>
        private void UpdateAuthoritativeUnit(
            bool isLocalPlayerUnit)
        {
            BeginMicroPhaseIfNeeded(
                isLocalPlayerUnit);

            UnitInputState state =
                GetCurrentInputState();

            bool isBlockedByUi =
                isLocalPlayerUnit &&
                SettingsUIManager.Instance != null &&
                SettingsUIManager.Instance.IsOpen;

            if (isBlockedByUi)
            {
                if (!_wasBlockedByUi)
                {
                    ReleaseAllPranam();
                }
            }
            else
            {
                UsePranam(
                    state);

                if (!IsSniperViewLocked &&
                    !_isTurningPerspective)
                {
                    RotatePranamPerspective(
                        state);
                }

                if (!IsSniperAimLocked)
                {
                    UpdatePerspectiveSwitch(
                        state);
                }

                UpdateMoveSpeed(
                    state);
            }

            _wasBlockedByUi =
                isBlockedByUi;

            TrySendEosAimState();

            if (_isMoveEndPending &&
                !IsMovementLocked)
            {
                _isMoveEndPending =
                    false;

                _unitStatus.SetMovable(
                    false);
            }

            RequestNextMigrationMove();
        }

        /// <summary>
        /// Micro Phase開始時のカメラ、行動ポイントおよびHUDを初期化します。
        /// </summary>
        private void BeginMicroPhaseIfNeeded(
            bool isLocalPlayerUnit)
        {
            if (_isMicroPhaseInitialized)
            {
                return;
            }

            _movementTurn = MatchManager.Instance != null ? MatchManager.Instance.CurrentTurn : 0;
            _hasFinalMovementPublished = false;
            _paidMovementDestination = null;
            _movementType = MovementType.None;
            _movementProgress = 0.0f;
            _isDirectMove = false;
            _isMoveRequestPending = false;
            _unitStatus.SetMovable(false);
            SetProactiveness(
                _unitStatus.ActionPoint);

            _unitStatus.BeginMigrationExecution();

            if (isLocalPlayerUnit)
            {
                SwitchCamera(
                    true);

                HUDManager.Instance?.SetMapMode(
                    false);
            }

            _isMicroPhaseInitialized =
                true;
        }

        /// <summary>
        /// Micro Phase開始前に、前のユニット表現から残った
        /// 移動状態を初期化します。
        /// </summary>
        /// <remarks>
        /// <see cref="UnitStatus.MigrationPath"/>は保持します。
        /// SoloAiDirectorが設定した経路をMicro Phaseで実行するためです。
        /// </remarks>
        public void PrepareForMicroPhase()
        {
            _preparingShooterProjectiles.Clear();
            _recoveryFallSpeed = 0.0f;
            _movementProgress = 0.0f;
            _paidMovementDestination = null;
            _hasRemoteMovementPosition = false;
            _nextMovementPublishTime = 0.0f;
            _nextCoordinate =
                default;

            _towardPosition =
                default;

            _moveStartPosition =
                default;

            _velocity =
                Vector3.zero;

            _isDirectMove =
                false;

            _movementType =
                MovementType.None;

            _isMoveRequestPending =
                false;

            _isMoveEndPending =
                false;

            _isMicroPhaseInitialized =
                false;

            _unitStatus?.EndMigrationExecution(
                false);

            _unitStatus?.SetMovable(
                false);
        }

        /// <summary>
        /// Micro Phase外へ移行した際の表示と操作状態を終了します。
        /// </summary>
        private void EndMicroPhase()
        {
            if (!_isMicroPhaseInitialized)
            {
                return;
            }

            if (MatchPhaseManager.Instance == null ||
                MatchPhaseManager.Instance.CurrentPhase != MatchPhase.MacroTransition)
            {
                SwitchCamera(false);
            }

            FinalizeMicroMovement();
            ReleaseAllPranam();

            _isMicroPhaseInitialized =
                false;

            _wasBlockedByUi =
                false;

            _isMoveRequestPending =
                false;

            _unitStatus?.EndMigrationExecution(
                true);
        }

        /// <summary>
        /// 移動経路の次の座標への移動をネットワークへ要求します。
        /// </summary>
        private void RequestNextMigrationMove()
        {
            if (!PranaTactics.Tutorial.TutorialSession.AllowsMigration(this))
            {
                return;
            }

            if (_unitStatus == null ||
                            _unitBehavior == null ||
                            !CanAffordMovement() ||
                            IsMovementLocked ||
                            _isDirectMove ||
                            _hasFinalMovementPublished ||
                            _isMoveRequestPending ||
                            _unitStatus.IsMovable ||
                            _unitStatus.MigrationPath == null ||
                            _unitStatus.MigrationPath.Count == 0)
            {
                return;
            }

            Vector2Int nextCoordinate =
                _unitStatus.MigrationPath[0];

            if (UnitManager.Instance != null &&
                UnitManager.Instance.IsMicroCellOccupied(nextCoordinate, MovementPlayerId))
            {
                return;
            }

            _isMoveRequestPending =
                true;

            _unitBehavior.LerpMoveToCoordinate(
                _unitStatus.TeamId,
                nextCoordinate.x,
                nextCoordinate.y);
        }

        /// <summary>
        /// 個別の視認情報を維持し、味方全員の情報から対象表示を更新します。
        /// </summary>
        public void RefreshVisiblePlayer(MicroUnitBehavior target)
        {
            if (!TryGetEnemyTarget(target, out UnitBehavior targetUnit, out UnitStatus targetStatus))
            {
                return;
            }

            _unitStatus.AddVisiblePlayer(targetStatus.PlayerId);
            _unitBehavior.ReportStealthDetection(targetStatus.PlayerId);
            RefreshSharedVisibility(targetUnit, targetStatus.PlayerId);
        }

        /// <summary>
        /// 自分の視認対象へ追加し、チーム全体の視認状態を更新します。
        /// </summary>
        public void AddVisiblePlayer(MicroUnitBehavior target)
        {
            RefreshVisiblePlayer(target);
            LogVisibility($"Visible Enemy: PlayerId={target?.PlayerId() ?? -1}");
        }

        /// <summary>
        /// 自分の視認情報だけを解除し、他の味方が見ている間は対象表示を維持します。
        /// </summary>
        public void RemoveVisiblePlayer(MicroUnitBehavior target)
        {
            if (!TryGetEnemyTarget(target, out UnitBehavior targetUnit, out UnitStatus targetStatus))
            {
                return;
            }

            _unitStatus.RemoveVisiblePlayer(targetStatus.PlayerId);
            _unitBehavior.ReportStealthDetection(targetStatus.PlayerId, true);
            RefreshSharedVisibility(targetUnit, targetStatus.PlayerId);
            LogVisibility($"Lost Enemy: PlayerId={targetStatus.PlayerId}");
        }

        /// <summary>
        /// チーム共有状態を更新し、表示判断は対象ユニットのローカル観測チームへ委譲します。
        /// </summary>
        private void RefreshSharedVisibility(UnitBehavior targetUnit, int targetPlayerId)
        {
            if (UnitManager.Instance != null)
            {
                UnitManager.Instance.RefreshTeamVisibility(_unitStatus.TeamId, targetPlayerId);
            }
            else
            {
                targetUnit.RefreshStealthPresentation();
            }
        }

        /// <summary>
        /// 対象が敵ユニットであることを確認し、
        /// UnitBehaviorとUnitStatusを取得します。
        /// </summary>
        private bool TryGetEnemyTarget(
            MicroUnitBehavior target,
            out UnitBehavior targetUnit,
            out UnitStatus targetStatus)
        {
            targetUnit =
                null;

            targetStatus =
                null;

            if (target == null ||
                _unitStatus == null)
            {
                return false;
            }

            targetStatus =
                target.GetUnitStatus();

            if (targetStatus == null ||
                targetStatus.TeamId ==
                _unitStatus.TeamId)
            {
                return false;
            }

            targetUnit =
                target.GetUnitBehavior();

            return targetUnit != null;
        }

        /// <summary>
        /// EOS照準同期コンポーネントを取得します。
        /// </summary>
        private void ResolveEosUnitNetwork()
        {
            if (_eosUnitNetwork != null)
            {
                return;
            }

            if (_unitBehavior != null)
            {
                _eosUnitNetwork =
                    _unitBehavior.GetComponent<EosUnitNetwork>();
            }

            if (_eosUnitNetwork == null)
            {
                _eosUnitNetwork =
                    GetComponentInParent<EosUnitNetwork>();
            }
        }

        /// <summary>
        /// 必要な場合にEOSへ現在の照準状態を送信します。
        /// </summary>
        private void TrySendEosAimState()
        {
            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager != null &&
                gameFlowManager.IsSoloPlay)
            {
                return;
            }

            if (NetworkModeUtility.CurrentMode !=
                NetworkMode.Eos)
            {
                return;
            }

            ResolveEosUnitNetwork();

            if (_eosUnitNetwork == null)
            {
                return;
            }

            Transform mainPranam =
                GetMainPranamTransform();

            Transform subPranam =
                GetSubPranamTransform();

            if (mainPranam == null ||
                subPranam == null)
            {
                return;
            }

            float currentTime =
                Time.unscaledTime;

            if (_hasSentAimState &&
                currentTime -
                _lastAimSendTime <
                k_AimSendInterval)
            {
                return;
            }

            float unitYaw =
                transform.eulerAngles.y;

            float mainYaw =
                mainPranam.eulerAngles.y;

            float subYaw =
                subPranam.eulerAngles.y;

            bool hasAngleChanged =
                !_hasSentAimState ||
                HasAngleChanged(
                    _lastSentUnitYaw,
                    unitYaw) ||
                HasAngleChanged(
                    _lastSentPitch,
                    _pitch) ||
                HasAngleChanged(
                    _lastSentMainYaw,
                    mainYaw) ||
                HasAngleChanged(
                    _lastSentSubYaw,
                    subYaw);

            bool hasPerspectiveChanged =
                !_hasSentAimState ||
                _lastSentIsMainPranamPerspective !=
                _isMainPranamPerspective;

            bool heartbeatElapsed =
                !_hasSentAimState ||
                currentTime -
                _lastAimSendTime >=
                k_AimHeartbeatInterval;

            if (!hasAngleChanged &&
                !hasPerspectiveChanged &&
                !heartbeatElapsed)
            {
                return;
            }

            _eosUnitNetwork.SendAimState(
                unitYaw,
                _pitch,
                mainYaw,
                subYaw,
                _isMainPranamPerspective);

            _lastSentUnitYaw =
                unitYaw;

            _lastSentPitch =
                _pitch;

            _lastSentMainYaw =
                mainYaw;

            _lastSentSubYaw =
                subYaw;

            _lastSentIsMainPranamPerspective =
                _isMainPranamPerspective;

            _lastAimSendTime =
                currentTime;

            _hasSentAimState =
                true;
        }

        /// <summary>
        /// ネットワークから受信した照準状態を補間して表示します。
        /// </summary>
        private void ApplyRemoteAimInterpolation()
        {
            float interpolationAmount =
                k_AimInterpolationSpeed *
                Time.deltaTime;

            _yaw =
                Mathf.LerpAngle(
                    _yaw,
                    _networkYaw,
                    interpolationAmount);

            _pitch =
                Mathf.LerpAngle(
                    _pitch,
                    _networkPitch,
                    interpolationAmount);

            _pitch =
                Mathf.Clamp(
                    _pitch,
                    _minimumPitch,
                    _maximumPitch);

            transform.rotation =
                Quaternion.Euler(
                    0.0f,
                    _yaw,
                    0.0f);

            ApplyCameraPitch();

            Transform mainPranam =
                GetMainPranamTransform();

            if (mainPranam != null)
            {
                SetWorldYaw(
                    mainPranam,
                    Mathf.LerpAngle(
                        mainPranam.eulerAngles.y,
                        _networkMainYaw,
                        interpolationAmount));
            }

            Transform subPranam =
                GetSubPranamTransform();

            if (subPranam != null)
            {
                SetWorldYaw(
                    subPranam,
                    Mathf.LerpAngle(
                        subPranam.eulerAngles.y,
                        _networkSubYaw,
                        interpolationAmount));
            }

            _isMainPranamPerspective =
                _networkIsMainPranamPerspective;
        }

        /// <summary>
        /// 現在のTransform状態からネットワーク照準値を初期化します。
        /// </summary>
        private void InitializeNetworkAimState()
        {
            _yaw =
                transform.eulerAngles.y;

            _networkYaw =
                _yaw;

            _pitch =
                Mathf.Clamp(
                    _pitch,
                    _minimumPitch,
                    _maximumPitch);

            _networkPitch =
                _pitch;

            _networkMainYaw =
                GetTransformWorldYaw(
                    GetMainPranamTransform(),
                    _yaw);

            _networkSubYaw =
                GetTransformWorldYaw(
                    GetSubPranamTransform(),
                    _yaw);

            _networkIsMainPranamPerspective =
                _isMainPranamPerspective;
        }

        /// <summary>
        /// 視認可能なUnitの位置に独立した撃破演出を生成します。
        /// </summary>
        private void ShowEliminationEffect()
        {
            UnitManager manager = UnitManager.Instance;
            UnitEliminationProfile profile = GameDataRepository.Instance?.GetUnitData()?.EliminationProfile;
            if (manager == null || _unitStatus == null || profile == null)
            {
                return;
            }

            int observerTeamId = manager.GetTeamId(manager.ControlledUnitId);
            int targetTeamId = manager.GetTeamId(_unitStatus.PlayerId);
            if (observerTeamId < 0 ||
                (observerTeamId != targetTeamId &&
                 !manager.IsUnitVisibleToTeam(observerTeamId, _unitStatus.PlayerId)))
            {
                return;
            }

            Vector3 center = transform.position;
            bool hasBounds = false;
            Bounds bounds = default;
            if (Model != null)
            {
                foreach (Renderer renderer in Model.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                    {
                        continue;
                    }

                    if (hasBounds)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    else
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                }
            }

            if (hasBounds)
            {
                center = bounds.center;
            }

            UnitEliminationEffect.Spawn(profile, center, gameObject.scene);
        }

        /// <summary>
        /// 操作引き継ぎ後に再スポーンを要求し、撃破Unitを削除します。
        /// </summary>
        public void CompleteElimination()
        {
            if (!_isDead || _unitBehavior == null || _unitStatus == null)
            {
                return;
            }

            UnitManager manager = UnitManager.Instance;
            if (manager != null && manager.ControlledUnitId == _unitStatus.PlayerId)
            {
                BoardManager.Instance?.GetMinimapCameraObject()?.SetActive(true);
            }

            RespawnBot respawnBot = _unitBehavior.GetComponent<RespawnBot>();
            if (respawnBot != null && respawnBot.isActiveAndEnabled && respawnBot.TryRequestRespawn())
            {
                return;
            }

            manager?.UnregisterUnit(_unitStatus.PlayerId);
            Destroy(_unitBehavior.gameObject);
        }

        /// <summary>
        /// カメラへ現在の垂直回転角度を適用します。
        /// </summary>
        private void ApplyCameraPitch()
        {
            if (UnitCamera == null)
            {
                return;
            }

            UnitCamera.transform.localRotation =
                Quaternion.Euler(
                    _pitch,
                    0.0f,
                    0.0f);
        }

        /// <summary>
        /// 指定されたTransformのワールド水平角度を設定します。
        /// </summary>
        private static void SetWorldYaw(
            Transform target,
            float yaw)
        {
            if (target == null)
            {
                return;
            }

            Vector3 eulerAngles =
                target.eulerAngles;

            eulerAngles.y =
                yaw;

            target.eulerAngles =
                eulerAngles;
        }

        /// <summary>
        /// Transformが存在する場合は水平角度を返し、
        /// 存在しない場合は既定値を返します。
        /// </summary>
        private static float GetTransformWorldYaw(
            Transform target,
            float fallbackYaw)
        {
            return target != null
                ? target.eulerAngles.y
                : fallbackYaw;
        }

        /// <summary>
        /// 2つの角度差が送信閾値以上か判定します。
        /// </summary>
        private static bool HasAngleChanged(
            float previous,
            float current)
        {
            return Mathf.Abs(
                    Mathf.DeltaAngle(
                        previous,
                        current)) >=
                k_AimAngleThreshold;
        }

        /// <summary>
        /// 視認状態の変更内容を必要に応じてログへ出力します。
        /// </summary>
        private void LogVisibility(
            string message)
        {
            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(MicroUnitBehavior)}: {message}",
                    this);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定された参照と移動・照準値を検証します。
        /// </summary>
        private void OnValidate()
        {
            _minimumPitch =
                Mathf.Clamp(
                    _minimumPitch,
                    -89.0f,
                    0.0f);

            _maximumPitch =
                Mathf.Clamp(
                    _maximumPitch,
                    0.0f,
                    89.0f);

            if (_minimumPitch >
                _maximumPitch)
            {
                _minimumPitch =
                    _maximumPitch;
            }

            _perspectiveTurnSpeed =
                Mathf.Max(
                    1.0f,
                    _perspectiveTurnSpeed);

            _jumpDuration =
                Mathf.Max(
                    0.05f,
                    _jumpDuration);

            _jumpHeight =
                Mathf.Max(
                    0.0f,
                    _jumpHeight);

            _jumpEasePower =
                Mathf.Max(
                    0.1f,
                    _jumpEasePower);
        }
#endif
    }
}
