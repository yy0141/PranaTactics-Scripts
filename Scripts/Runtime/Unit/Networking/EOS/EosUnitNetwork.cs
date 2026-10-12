using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.Networking;
using PranaTactics.Networking.EOS;
using PranaTactics.Unit.Movement;

namespace PranaTactics.Unit.Networking.EOS
{
    /// <summary>
    /// EOS P2Pを使用してユニットの状態変更、
    /// 移動および照準状態を同期します。
    /// </summary>
    /// <remarks>
    /// ユニットの初期生成情報はルームプロパティで共有し、
    /// ゲーム中に発生する命令はEOS P2Pで同期します。
    ///
    /// Macro Phaseの移動計画は同じチームだけへ送信し、
    /// Micro Phaseの操作は全参加プレイヤーへ送信します。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitBehavior))]
    public sealed class EosUnitNetwork :
        MonoBehaviour,
        IUnitNetwork,
        IMicroMovementNetwork
    {
        /// <summary>
        /// 移動速度を再送信する最小差分です。
        /// </summary>
        private const float k_DefaultMoveSpeedSendThreshold =
            0.001f;

        /// <summary>
        /// EOS P2P命令を適用するユニットです。
        /// </summary>
        private UnitBehavior _unitBehavior;

        /// <summary>
        /// ホストが移動状態を全参加者へ同期します。
        /// </summary>
        public void PublishMicroMovement(MicroMovementSnapshot snapshot)
        {
            TryBindOnlineService();
            if (_onlineService == null || !_onlineService.IsHost ||
                !TryCreateMessage(EosUnitP2PMessageTypes.MicroMovement, out EosUnitP2PMessage message, false))
            {
                return;
            }

            message.Text = JsonUtility.ToJson(snapshot);
            Broadcast(message, NetworkSendMode.Reliable, false);
        }

        /// <summary>
        /// EOS P2Pの送受信を行うオンラインサービスです。
        /// </summary>
        private EosOnlineService _onlineService;

        /// <summary>
        /// このユニットから送信したメッセージの連番です。
        /// </summary>
        private int _sendSequence;

        /// <summary>
        /// 最後に送信した移動速度です。
        /// </summary>
        private float _lastSentMoveSpeed =
            float.NaN;

        /// <summary>
        /// 送信者とメッセージ種別ごとの
        /// 最終受信連番です。
        /// </summary>
        private readonly Dictionary<MessageStreamKey, int>
            _lastReceivedSequenceByStream = new();

        /// <summary>
        /// 次に照準送信ログを出力できる時刻です。
        /// </summary>
        private float _nextAimSendLogTime;

        /// <summary>
        /// 次に照準受信ログを出力できる時刻です。
        /// </summary>
        private float _nextAimReceiveLogTime;

        /// <summary>
        /// 移動速度を再送信する最小差分です。
        /// </summary>
        [Header("Synchronization")]
        [Min(0.0f)]
        [SerializeField]
        private float _moveSpeedSendThreshold =
            k_DefaultMoveSpeedSendThreshold;

        /// <summary>
        /// 通常命令の送受信ログを出力するかどうかです。
        /// </summary>
        [Header("Logging")]
        [SerializeField]
        private bool _enableCommandLogging;

        /// <summary>
        /// 照準状態の送受信ログを出力するかどうかです。
        /// </summary>
        [SerializeField]
        private bool _enableAimLogging;

        /// <summary>
        /// 照準状態のログを出力する間隔です。
        /// </summary>
        [Min(0.1f)]
        [SerializeField]
        private float _aimLogInterval = 1.0f;

        /// <summary>
        /// 経路編集結果を適用し、同じチームへ送信します。
        /// </summary>
        public void SetMacroPath(string snapshot)
        {
            if (!TryCreateMessage(EosUnitP2PMessageTypes.SetMacroPath, out EosUnitP2PMessage message))
            {
                return;
            }

            message.Text = snapshot;
            message.TeamId = GetOwnerTeamId();
            _unitBehavior.GetMacroUnitBehavior()?.ApplyPathSnapshot(snapshot);
            SendToTeam(message, message.TeamId);
        }

        /// <summary>
        /// 必要なコンポーネントを取得します。
        /// </summary>
        private void Awake()
        {
            _unitBehavior =
                GetComponent<UnitBehavior>();

            if (_unitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(EosUnitNetwork)}: " +
                    $"{nameof(UnitBehavior)}が見つかりません。",
                    this);
            }
        }

        /// <summary>
        /// オンラインサービスへイベントを登録します。
        /// </summary>
        private void OnEnable()
        {
            TryBindOnlineService();
        }

        /// <summary>
        /// オンラインサービスが後から初期化または
        /// 置き換えられた場合に再登録します。
        /// </summary>
        private void Update()
        {
            TryBindOnlineService();
        }

        /// <summary>
        /// オンラインサービスからイベントを解除します。
        /// </summary>
        private void OnDisable()
        {
            UnbindOnlineService();
        }

        /// <summary>
        /// EOS経由で共有済みの初期化情報を
        /// 対象ユニットへ適用します。
        /// </summary>
        /// <param name="data">
        /// ユニットの初期化情報です。
        /// </param>
        public void Instantiate(
            UnitInitializeData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(
                    nameof(data));
            }

            if (!EnsureUnitBehavior())
            {
                return;
            }

            _unitBehavior.ApplyInstantiate(
                data);
        }

        /// <summary>
        /// ユニットを撃破状態へ変更し、
        /// 他のクライアントへ同期します。
        /// </summary>
        public void SetDead()
        {
            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.SetDead,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            _unitBehavior.ApplySetDead();

            Broadcast(
                message);
        }

        /// <summary>
        /// 指定されたチームへスコアを加算し、
        /// 他のクライアントへ同期します。
        /// </summary>
        /// <param name="teamId">
        /// スコアを加算するチームIDです。
        /// </param>
        public void AddTeamScore(
            int teamId)
        {
            if (teamId < 0)
            {
                Debug.LogWarning(
                    $"{nameof(EosUnitNetwork)}: " +
                    $"無効なTeamIdです。TeamId={teamId}",
                    this);

                return;
            }

            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.AddTeamScore,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            message.TeamId =
                teamId;

            _unitBehavior.ApplyAddTeamScore(
                teamId);

            Broadcast(
                message);
        }

        /// <summary>
        /// ユニットの移動経路へ座標を追加し、
        /// 必要なクライアントへ同期します。
        /// </summary>
        /// <param name="x">
        /// 追加するボード座標のX値です。
        /// </param>
        /// <param name="y">
        /// 追加するボード座標のY値です。
        /// </param>
        public void AddMigrationCoordinate(
            int x,
            int y)
        {
            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.AddMigrationCoordinate,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            int ownerTeamId =
                GetOwnerTeamId();

            message.TeamId =
                ownerTeamId;

            message.X =
                x;

            message.Y =
                y;

            _unitBehavior.ApplyAddMigrationCoordinate(
                x,
                y);

            if (IsMacroPhase())
            {
                SendToTeam(
                    message,
                    ownerTeamId);

                return;
            }

            Broadcast(
                message);
        }

        /// <summary>
        /// ユニットを指定されたボード座標へ瞬間移動させ、
        /// 他のクライアントへ同期します。
        /// </summary>
        public void TeleportMove(
            int x,
            int y)
        {
            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.TeleportCoordinate,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            message.X =
                x;

            message.Y =
                y;

            _unitBehavior.ApplyTeleportMoveToCoordinate(
                x,
                y);

            Broadcast(
                message);
        }

        /// <summary>
        /// ユニットを指定されたボード座標へ補間移動させます。
        /// </summary>
        /// <param name="teamId">
        /// 移動を実行するユニットのチームIDです。
        /// </param>
        /// <param name="x">
        /// 移動先のX座標です。
        /// </param>
        /// <param name="y">
        /// 移動先のY座標です。
        /// </param>
        public void LerpMove(
            int teamId,
            int x,
            int y)
        {
            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.LerpCoordinate,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            message.TeamId =
                teamId;

            message.X =
                x;

            message.Y =
                y;
            message.BoolValue = !IsMacroPhase();
            message.Value = MatchManager.Instance != null ? MatchManager.Instance.CurrentTurn : 0;

            _unitBehavior.ApplyLerpMoveToCoordinate(
                teamId,
                x,
                y);

            if (IsMacroPhase())
            {
                SendToTeam(
                    message,
                    teamId);

                return;
            }

            Broadcast(
                message);
        }

        /// <summary>
        /// ユニットを指定されたワールド座標へ補間移動させます。
        /// </summary>
        /// <param name="position">
        /// 移動先のワールド座標です。
        /// </param>
        public void LerpMove(
            Vector3 position)
        {
            if (!IsFinite(
                    position))
            {
                Debug.LogWarning(
                    $"{nameof(EosUnitNetwork)}: " +
                    $"無効な移動先座標です。Position={position}",
                    this);

                return;
            }

            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.LerpPosition,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            message.Position =
                position;

            _unitBehavior.ApplyLerpMoveToPosition(
                position);

            Broadcast(
                message);
        }

        /// <summary>
        /// 指定された時間だけユニットの移動を制限します。
        /// </summary>
        /// <param name="duration">
        /// 移動を制限する秒数です。
        /// </param>
        public void LockMovement(
            float duration)
        {
            if (!IsFinite(
                    duration))
            {
                Debug.LogWarning(
                    $"{nameof(EosUnitNetwork)}: " +
                    $"無効な移動制限時間です。Duration={duration}",
                    this);

                return;
            }

            duration =
                Mathf.Max(
                    0.0f,
                    duration);

            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.LockMovement,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            message.Value =
                duration;

            _unitBehavior.ApplyLockMovement(
                duration);

            Broadcast(
                message);
        }

        /// <summary>
        /// ユニットの移動速度を変更します。
        /// </summary>
        /// <param name="value">
        /// 設定する移動速度です。
        /// </param>
        public void SetMoveSpeed(
            float value)
        {
            if (!IsFinite(
                    value))
            {
                Debug.LogWarning(
                    $"{nameof(EosUnitNetwork)}: " +
                    $"無効な移動速度です。Value={value}",
                    this);

                return;
            }

            value =
                Mathf.Max(
                    0.0f,
                    value);

            if (!HasSendAuthority())
            {
                return;
            }

            if (!float.IsNaN(
                    _lastSentMoveSpeed) &&
                Mathf.Abs(
                    _lastSentMoveSpeed -
                    value) <
                _moveSpeedSendThreshold)
            {
                return;
            }

            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.SetMoveSpeed,
                    out EosUnitP2PMessage message,
                    checkAuthority: false))
            {
                return;
            }

            _lastSentMoveSpeed =
                value;

            message.Value =
                value;

            _unitBehavior.ApplySetMoveSpeed(
                value);

            Broadcast(
                message);
        }

        /// <summary>
        /// ユニットのUnityレイヤーを変更します。
        /// </summary>
        /// <param name="layerName">
        /// 変更先のレイヤー名です。
        /// </param>
        public void ChangeLayer(
            string layerName)
        {
            if (string.IsNullOrWhiteSpace(
                    layerName))
            {
                return;
            }

            if (LayerMask.NameToLayer(
                    layerName) <
                0)
            {
                Debug.LogWarning(
                    $"{nameof(EosUnitNetwork)}: " +
                    $"存在しないレイヤーです。LayerName={layerName}",
                    this);

                return;
            }

            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.ChangeLayer,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            message.Text =
                layerName;

            _unitBehavior.ApplyChangeLayer(
                layerName);

            Broadcast(
                message);
        }

        /// <summary>
        /// 指定されたチームから見たステルス状態を切り替えます。
        /// </summary>
        /// <param name="teamId">
        /// 表示判定に使用するチームIDです。
        /// </param>
        /// <param name="isStealth">
        /// ステルス状態へ変更する場合は<c>true</c>です。
        /// </param>
        public void SwitchStealth(
            int teamId,
            bool isStealth)
        {
            if (teamId < 0)
            {
                return;
            }

            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.SwitchStealth,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            message.TeamId =
                teamId;

            message.BoolValue =
                isStealth;

            _unitBehavior.ApplySwitchStealth(
                teamId,
                isStealth);

            Broadcast(
                message);
        }

        /// <summary>
        /// 観測者の所有権を使い、直接視認した敵のマップ公開を同期します。
        /// </summary>
        public void ReportStealthDetection(int targetPlayerId)
        {
            if (targetPlayerId < 0 || !TryCreateMessage(
                    EosUnitP2PMessageTypes.StealthDetection, out EosUnitP2PMessage message))
            {
                return;
            }

            message.TargetPlayerId = targetPlayerId;
            _unitBehavior.ApplyStealthDetection(targetPlayerId);
            Broadcast(message);
        }

        /// <summary>
        /// Micro Phaseのユニット本体、Main、Subの
        /// 照準角度をEOS P2Pで送信します。
        /// </summary>
        /// <remarks>
        /// 照準状態は次のパケットで補正できるため、
        /// Unreliableモードで送信します。
        /// </remarks>
        public void SendAimState(
            float unitYaw,
            float pitch,
            float mainYaw,
            float subYaw,
            bool isMainPranamPerspective)
        {
            if (IsMacroPhase())
            {
                return;
            }

            if (!IsFinite(
                    unitYaw) ||
                !IsFinite(
                    pitch) ||
                !IsFinite(
                    mainYaw) ||
                !IsFinite(
                    subYaw))
            {
                return;
            }

            if (!TryCreateMessage(
                    EosUnitP2PMessageTypes.AimState,
                    out EosUnitP2PMessage message))
            {
                return;
            }

            message.UnitYaw =
                unitYaw;

            message.Pitch =
                pitch;

            message.MainYaw =
                mainYaw;

            message.SubYaw =
                subYaw;

            message.BoolValue =
                isMainPranamPerspective;

            Broadcast(
                message,
                NetworkSendMode.Unreliable,
                writeLog: false);

            LogAimSend(
                message);
        }

        /// <summary>
        /// EOS P2Pから受信したユニット命令を処理します。
        /// </summary>
        private void HandleUnitMessage(
            EosUnitP2PMessage message)
        {
            if (!ValidateReceivedMessage(
                    message))
            {
                return;
            }

            if (message.MessageType == EosUnitP2PMessageTypes.MicroMovement &&
                (_onlineService == null || !_onlineService.IsHostPlayer(message.SenderPlayerId)))
            {
                return;
            }

            if (ShouldRejectMacroMessage(
                    message))
            {
                return;
            }

            if (!AcceptSequence(
                    message))
            {
                return;
            }

            if (!ApplyReceivedMessage(
                    message))
            {
                return;
            }

            LogReceive(
                message);
        }

        /// <summary>
        /// 受信したメッセージの共通部分を検証します。
        /// </summary>
        private bool ValidateReceivedMessage(
            EosUnitP2PMessage message)
        {
            if (message == null ||
                !EnsureUnitBehavior())
            {
                return false;
            }

            if (!message.HasValidHeader ||
                !message.HasFiniteValues())
            {
                Debug.LogWarning(
                    $"{nameof(EosUnitNetwork)}: " +
                    $"不正なUnitメッセージを拒否しました。{message}",
                    this);

                return false;
            }

            if (message.OwnerId !=
                _unitBehavior.GetOwnerId())
            {
                return false;
            }

            if (!ContainsOnlinePlayer(
                    message.SenderPlayerId))
            {
                Debug.LogWarning(
                    $"{nameof(EosUnitNetwork)}: " +
                    "ルームに存在しない送信者からのメッセージを拒否しました。" +
                    $" Sender={message.SenderPlayerId}",
                    this);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Macro Phaseで敵チームの移動情報を受信した場合に拒否します。
        /// </summary>
        private bool ShouldRejectMacroMessage(
            EosUnitP2PMessage message)
        {
            if (!IsMacroPhase() ||
                !IsPrivateMacroMessage(
                    message.MessageType))
            {
                return false;
            }

            if (message.TeamId == GetOwnerTeamId() &&
                IsLocalPlayerTeam(GetOwnerTeamId()))
            {
                return false;
            }

            LogCommand(
                "Macro message ignored",
                message);

            return true;
        }

        /// <summary>
        /// 受信連番を検証し、重複または古いメッセージを除外します。
        /// </summary>
        private bool AcceptSequence(
            EosUnitP2PMessage message)
        {
            MessageStreamKey streamKey =
                new(
                    message.SenderPlayerId,
                    message.MessageType);

            if (_lastReceivedSequenceByStream.TryGetValue(
                    streamKey,
                    out int lastSequence) &&
                message.Sequence <=
                lastSequence)
            {
                return false;
            }

            _lastReceivedSequenceByStream[streamKey] =
                message.Sequence;

            return true;
        }

        /// <summary>
        /// 受信したメッセージをユニットへ適用します。
        /// </summary>
        private bool ApplyReceivedMessage(
            EosUnitP2PMessage message)
        {
            switch (message.MessageType)
            {
                case EosUnitP2PMessageTypes.MicroMovement:
                    if (string.IsNullOrEmpty(message.Text))
                    {
                        return false;
                    }

                    try
                    {
                        _unitBehavior.ApplyMicroMovement(JsonUtility.FromJson<MicroMovementSnapshot>(message.Text));
                        return true;
                    }
                    catch (ArgumentException exception)
                    {
                        Debug.LogWarning($"移動状態の形式が不正です。{exception.Message}", this);
                        return false;
                    }

                case EosUnitP2PMessageTypes.SetDead:
                    _unitBehavior.ApplySetDead();
                    return true;

                case EosUnitP2PMessageTypes.AddTeamScore:
                    if (message.TeamId < 0)
                    {
                        return false;
                    }

                    _unitBehavior.ApplyAddTeamScore(
                        message.TeamId);

                    return true;

                case EosUnitP2PMessageTypes.SetMacroPath:
                    _unitBehavior.GetMacroUnitBehavior()?.ApplyPathSnapshot(message.Text);
                    return true;

                case EosUnitP2PMessageTypes.AddMigrationCoordinate:
                    _unitBehavior.ApplyAddMigrationCoordinate(
                        message.X,
                        message.Y);

                    return true;

                case EosUnitP2PMessageTypes.TeleportCoordinate:
                    _unitBehavior.ApplyTeleportMoveToCoordinate(
                        message.X,
                        message.Y);

                    return true;

                case EosUnitP2PMessageTypes.LerpCoordinate:
                    if ((!message.BoolValue && !IsMacroPhase()) || (message.BoolValue &&
                        !CanApplyMicroMoveRequest(message)))
                    {
                        return false;
                    }

                    if (message.TeamId < 0)
                    {
                        return false;
                    }

                    _unitBehavior.ApplyLerpMoveToCoordinate(
                        message.TeamId,
                        message.X,
                        message.Y);

                    return true;

                case EosUnitP2PMessageTypes.LerpPosition:
                    _unitBehavior.ApplyLerpMoveToPosition(
                        message.Position);

                    return true;

                case EosUnitP2PMessageTypes.LockMovement:
                    _unitBehavior.ApplyLockMovement(
                        Mathf.Max(
                            0.0f,
                            message.Value));

                    return true;

                case EosUnitP2PMessageTypes.SetMoveSpeed:
                    _unitBehavior.ApplySetMoveSpeed(
                        Mathf.Max(
                            0.0f,
                            message.Value));

                    return true;

                case EosUnitP2PMessageTypes.ChangeLayer:
                    if (string.IsNullOrWhiteSpace(
                            message.Text) ||
                        LayerMask.NameToLayer(
                            message.Text) <
                        0)
                    {
                        return false;
                    }

                    _unitBehavior.ApplyChangeLayer(
                        message.Text);

                    return true;

                case EosUnitP2PMessageTypes.SwitchStealth:
                    if (message.TeamId < 0)
                    {
                        return false;
                    }

                    _unitBehavior.ApplySwitchStealth(
                        message.TeamId,
                        message.BoolValue);

                    return true;

                case EosUnitP2PMessageTypes.StealthDetection:
                    if (message.TargetPlayerId < 0)
                    {
                        return false;
                    }

                    _unitBehavior.ApplyStealthDetection(message.TargetPlayerId);
                    return true;

                case EosUnitP2PMessageTypes.AimState:
                    return ApplyAimState(
                        message);

                default:
                    return false;
            }
        }

        /// <summary>
        /// 受信した照準状態をMicro Unitへ適用します。
        /// </summary>
        private bool ApplyAimState(
            EosUnitP2PMessage message)
        {
            MicroUnitBehavior microUnitBehavior =
                _unitBehavior.GetMicroUnitBehavior();

            if (microUnitBehavior == null)
            {
                return false;
            }

            microUnitBehavior.ApplyNetworkAimState(
                message.UnitYaw,
                message.Pitch,
                message.MainYaw,
                message.SubYaw,
                message.BoolValue);

            return true;
        }

        /// <summary>
        /// 同一ターンの所有者またはホスト管理AIの移動要求だけを許可します。
        /// </summary>
        private bool CanApplyMicroMoveRequest(EosUnitP2PMessage message)
        {
            if (IsMacroPhase() || _onlineService == null || !_onlineService.IsHost ||
                message.Value != (MatchManager.Instance != null ? MatchManager.Instance.CurrentTurn : 0))
            {
                return false;
            }

            return message.SenderPlayerId == message.OwnerId ||
                (_onlineService.IsHostPlayer(message.SenderPlayerId) &&
                    UnitManager.Instance != null && UnitManager.Instance.IsAIControlledUnit(message.OwnerId));
        }

        /// <summary>
        /// 送信用メッセージを生成します。
        /// </summary>
        private bool TryCreateMessage(
            string messageType,
            out EosUnitP2PMessage message,
            bool checkAuthority = true)
        {
            message =
                null;

            if (!EosUnitP2PMessageTypes.IsDefined(
                    messageType))
            {
                Debug.LogError(
                    $"{nameof(EosUnitNetwork)}: " +
                    $"未定義のメッセージ種別です。Type={messageType}",
                    this);

                return false;
            }

            TryBindOnlineService();

            if (_onlineService == null ||
                !EnsureUnitBehavior())
            {
                return false;
            }

            if (checkAuthority &&
                !HasSendAuthority())
            {
                return false;
            }

            int ownerId =
                _unitBehavior.GetOwnerId();

            int senderPlayerId =
                _onlineService.LocalPlayerId;

            if (ownerId < 0 ||
                senderPlayerId < 0)
            {
                return false;
            }

            message =
                new EosUnitP2PMessage
                {
                    MessageType =
                        messageType,

                    OwnerId =
                        ownerId,

                    SenderPlayerId =
                        senderPlayerId,

                    Sequence =
                        GetNextSendSequence()
                };

            return true;
        }

        /// <summary>
        /// 次に使用する送信連番を取得します。
        /// </summary>
        private int GetNextSendSequence()
        {
            if (_sendSequence >=
                int.MaxValue)
            {
                _sendSequence = 0;
            }

            _sendSequence++;

            return _sendSequence;
        }

        /// <summary>
        /// 現在がMacro Phaseかどうかを判定します。
        /// </summary>
        private static bool IsMacroPhase()
        {
            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            return phaseManager != null &&
                phaseManager.CurrentPhase !=
                MatchPhase.Micro;
        }

        /// <summary>
        /// Macro Phaseで味方のみに共有する
        /// メッセージ種別かどうかを確認します。
        /// </summary>
        private static bool IsPrivateMacroMessage(
            string messageType)
        {
            return messageType ==
                    EosUnitP2PMessageTypes.AddMigrationCoordinate ||
                messageType == EosUnitP2PMessageTypes.SetMacroPath ||
                messageType ==
                    EosUnitP2PMessageTypes.LerpCoordinate;
        }

        /// <summary>
        /// ローカルプレイヤーのチームIDを取得します。
        /// </summary>
        private int GetLocalTeamId()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return -1;
            }

            int localPlayerId =
                _onlineService != null
                    ? _onlineService.LocalPlayerId
                    : unitManager.LocalPlayerId;

            int localTeamId =
                unitManager.GetTeamId(
                    localPlayerId);

            if (localTeamId < 0)
            {
                localTeamId =
                    OnlinePlayerUtility.GetTeamId(
                        localPlayerId);
            }

            return localTeamId;
        }

        /// <summary>
        /// 対象ユニットが所属するチームIDを取得します。
        /// </summary>
        private int GetOwnerTeamId()
        {
            if (!EnsureUnitBehavior())
            {
                return -1;
            }

            UnitStatus unitStatus =
                _unitBehavior.GetUnitStatus();

            if (unitStatus != null)
            {
                return unitStatus.TeamId;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return -1;
            }

            return unitManager.GetTeamId(
                _unitBehavior.GetOwnerId());
        }

        /// <summary>
        /// 指定されたチームがローカルプレイヤーと
        /// 同じチームかどうかを判定します。
        /// </summary>
        private bool IsLocalPlayerTeam(
            int teamId)
        {
            int localTeamId =
                GetLocalTeamId();

            return teamId >=
                    0 &&
                localTeamId >=
                    0 &&
                teamId ==
                    localTeamId;
        }

        /// <summary>
        /// 指定されたチームのプレイヤーだけへ
        /// メッセージを送信します。
        /// </summary>
        private void SendToTeam(
            EosUnitP2PMessage message,
            int teamId)
        {
            TryBindOnlineService();

            if (_onlineService == null ||
                message == null)
            {
                return;
            }

            if (teamId < 0)
            {
                Debug.LogWarning(
                    $"{nameof(EosUnitNetwork)}: " +
                    "TeamIdが未確定のため、チーム送信を中止しました。" +
                    $" Owner={message.OwnerId}, Type={message.MessageType}",
                    this);

                return;
            }

            if (_onlineService.Players == null)
            {
                return;
            }

            int sentCount =
                0;

            foreach (OnlinePlayer player in
                    _onlineService.Players)
            {
                if (player == null ||
                    player.PlayerId ==
                    _onlineService.LocalPlayerId)
                {
                    continue;
                }

                int targetTeamId =
                    GetPlayerTeamId(
                        player.PlayerId);

                if (targetTeamId !=
                    teamId)
                {
                    continue;
                }

                _onlineService.SendUnitMessageToPlayer(
                    player.PlayerId,
                    message,
                    NetworkSendMode.Reliable);

                sentCount++;
            }

            if (_enableCommandLogging)
            {
                Debug.Log(
                    $"{nameof(EosUnitNetwork)}: " +
                    "Team message sent." +
                    $" Owner={message.OwnerId}," +
                    $" Team={teamId}," +
                    $" Recipients={sentCount}," +
                    $" Type={message.MessageType}," +
                    $" Sequence={message.Sequence}",
                    this);
            }
        }

        /// <summary>
        /// 指定プレイヤーのチームIDを取得します。
        /// </summary>
        private static int GetPlayerTeamId(
            int playerId)
        {
            int teamId =
                -1;

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager != null)
            {
                teamId =
                    unitManager.GetTeamId(
                        playerId);
            }

            if (teamId < 0)
            {
                teamId =
                    OnlinePlayerUtility.GetTeamId(
                        playerId);
            }

            return teamId;
        }

        /// <summary>
        /// 全ての遠隔プレイヤーへメッセージを送信します。
        /// </summary>
        private void Broadcast(
            EosUnitP2PMessage message,
            NetworkSendMode sendMode = NetworkSendMode.Reliable,
            bool writeLog = true)
        {
            TryBindOnlineService();

            if (_onlineService == null ||
                message == null)
            {
                return;
            }

            _onlineService.BroadcastUnitMessage(
                message,
                sendMode);

            if (writeLog)
            {
                LogCommand(
                    $"Message sent ({sendMode})",
                    message);
            }
        }

        /// <summary>
        /// このクライアントが対象ユニットの
        /// 送信権限を持つかどうかを確認します。
        /// </summary>
        private bool HasSendAuthority()
        {
            TryBindOnlineService();

            if (_onlineService == null ||
                !EnsureUnitBehavior())
            {
                return false;
            }

            int ownerId =
                _unitBehavior.GetOwnerId();

            if (ownerId ==
                _onlineService.LocalPlayerId)
            {
                return true;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            return _onlineService.IsHost &&
                unitManager != null &&
                unitManager.IsAIControlledUnit(
                    ownerId);
        }

        /// <summary>
        /// 指定されたプレイヤーが現在の
        /// オンラインプレイヤー一覧に存在するか確認します。
        /// </summary>
        private bool ContainsOnlinePlayer(
            int playerId)
        {
            if (_onlineService == null ||
                _onlineService.Players == null)
            {
                return false;
            }

            foreach (OnlinePlayer player in
                    _onlineService.Players)
            {
                if (player != null &&
                    player.PlayerId ==
                    playerId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 現在のEOSオンラインサービスへ
        /// メッセージ受信イベントを登録します。
        /// </summary>
        private void TryBindOnlineService()
        {
            EosOnlineService currentService =
                NetworkServiceLocator.Online as
                    EosOnlineService;

            if (ReferenceEquals(
                    currentService,
                    _onlineService))
            {
                return;
            }

            UnbindOnlineService();

            _onlineService =
                currentService;

            if (_onlineService == null)
            {
                return;
            }

            _lastReceivedSequenceByStream.Clear();
            _sendSequence = 0;
            _lastSentMoveSpeed = float.NaN;

            _onlineService.UnitMessageReceived +=
                HandleUnitMessage;
        }

        /// <summary>
        /// EOSオンラインサービスから
        /// メッセージ受信イベントを解除します。
        /// </summary>
        private void UnbindOnlineService()
        {
            if (_onlineService != null)
            {
                _onlineService.UnitMessageReceived -=
                    HandleUnitMessage;
            }

            _onlineService =
                null;

            _lastReceivedSequenceByStream.Clear();
        }

        /// <summary>
        /// UnitBehaviorの参照を取得します。
        /// </summary>
        private bool EnsureUnitBehavior()
        {
            if (_unitBehavior == null)
            {
                _unitBehavior =
                    GetComponent<UnitBehavior>();
            }

            return _unitBehavior !=
                null;
        }

        /// <summary>
        /// 通常命令のログを必要に応じて出力します。
        /// </summary>
        private void LogCommand(
            string prefix,
            EosUnitP2PMessage message)
        {
            if (!_enableCommandLogging ||
                message == null)
            {
                return;
            }

            Debug.Log(
                $"{nameof(EosUnitNetwork)}: {prefix}." +
                $" LocalPlayer={_onlineService?.LocalPlayerId ?? -1}," +
                $" Sender={message.SenderPlayerId}," +
                $" Owner={message.OwnerId}," +
                $" Type={message.MessageType}," +
                $" Sequence={message.Sequence}",
                this);
        }

        /// <summary>
        /// 受信メッセージのログを必要に応じて出力します。
        /// </summary>
        private void LogReceive(
            EosUnitP2PMessage message)
        {
            if (message.MessageType ==
                EosUnitP2PMessageTypes.AimState)
            {
                if (!_enableAimLogging ||
                    Time.unscaledTime <
                    _nextAimReceiveLogTime)
                {
                    return;
                }

                _nextAimReceiveLogTime =
                    Time.unscaledTime +
                    _aimLogInterval;

                Debug.Log(
                    $"{nameof(EosUnitNetwork)}: Aim received." +
                    $" Sender={message.SenderPlayerId}," +
                    $" Owner={message.OwnerId}," +
                    $" UnitYaw={message.UnitYaw:0.0}," +
                    $" Pitch={message.Pitch:0.0}," +
                    $" MainYaw={message.MainYaw:0.0}," +
                    $" SubYaw={message.SubYaw:0.0}," +
                    $" Sequence={message.Sequence}",
                    this);

                return;
            }

            LogCommand(
                "Message received",
                message);
        }

        /// <summary>
        /// 照準状態の送信ログを必要に応じて出力します。
        /// </summary>
        private void LogAimSend(
            EosUnitP2PMessage message)
        {
            if (!_enableAimLogging ||
                Time.unscaledTime <
                _nextAimSendLogTime)
            {
                return;
            }

            _nextAimSendLogTime =
                Time.unscaledTime +
                _aimLogInterval;

            Debug.Log(
                $"{nameof(EosUnitNetwork)}: Aim sent." +
                $" Owner={message.OwnerId}," +
                $" UnitYaw={message.UnitYaw:0.0}," +
                $" Pitch={message.Pitch:0.0}," +
                $" MainYaw={message.MainYaw:0.0}," +
                $" SubYaw={message.SubYaw:0.0}," +
                $" Sequence={message.Sequence}",
                this);
        }

        /// <summary>
        /// 浮動小数点値が有限値かどうかを確認します。
        /// </summary>
        private static bool IsFinite(
            float value)
        {
            return !float.IsNaN(
                    value) &&
                !float.IsInfinity(
                    value);
        }

        /// <summary>
        /// Vector3の全成分が有限値かどうかを確認します。
        /// </summary>
        private static bool IsFinite(
            Vector3 value)
        {
            return IsFinite(
                    value.x) &&
                IsFinite(
                    value.y) &&
                IsFinite(
                    value.z);
        }

        private readonly struct MessageStreamKey :
    IEquatable<MessageStreamKey>
        {
            /// <summary>
            /// 送信者のプレイヤーIDです。
            /// </summary>
            private readonly int _senderPlayerId;

            /// <summary>
            /// メッセージ種別です。
            /// </summary>
            private readonly string _messageType;

            public MessageStreamKey(
    int senderPlayerId,
    string messageType)
            {
                _senderPlayerId =
                    senderPlayerId;

                _messageType =
                    messageType;
            }

            /// <summary>
            /// 別のキーと同一かどうかを確認します。
            /// </summary>
            public bool Equals(
                MessageStreamKey other)
            {
                return _senderPlayerId ==
                        other._senderPlayerId &&
                    string.Equals(
                        _messageType,
                        other._messageType,
                        StringComparison.Ordinal);
            }

            /// <summary>
            /// 指定されたオブジェクトが同一のキーか確認します。
            /// </summary>
            public override bool Equals(
                object obj)
            {
                return obj is MessageStreamKey other &&
                    Equals(
                        other);
            }

            /// <summary>
            /// Dictionaryで使用するハッシュ値を取得します。
            /// </summary>
            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode =
                        _senderPlayerId;

                    hashCode =
                        hashCode *
                        397 ^
                        (_messageType != null
                            ? StringComparer.Ordinal.GetHashCode(
                                _messageType)
                            : 0);

                    return hashCode;
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の設定値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _moveSpeedSendThreshold =
                Mathf.Max(
                    0.0f,
                    _moveSpeedSendThreshold);

            _aimLogInterval =
                Mathf.Max(
                    0.1f,
                    _aimLogInterval);
        }

        /// <summary>
        /// コンポーネント追加時にUnitBehaviorを取得します。
        /// </summary>
        private void Reset()
        {
            _unitBehavior =
                GetComponent<UnitBehavior>();
        }
#endif
    }
}
