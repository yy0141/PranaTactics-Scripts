using System;
using Newtonsoft.Json;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

using PranaTactics.Combat;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.Networking;
using PranaTactics.Unit;
using PranaTactics.Unit.Movement;
using PranaTactics.Unit.Networking;

namespace PranaTactics.Unit.Networking.Photon
{
    /// <summary>
    /// Photon RPCを使用してユニットの状態変更や
    /// 移動命令を各クライアントへ同期します。
    /// </summary>
    /// <remarks>
    /// RPCの送信は、原則として対象の
    /// <see cref="PhotonView"/>を所有するクライアントだけが行います。
    /// 受信側でも送信者を検証し、不正なRPCの適用を防ぎます。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(UnitBehavior))]
    public sealed class PhotonUnitNetwork :
    MonoBehaviourPun,
    IUnitNetwork,
    ICombatFeedbackNetwork,
    IMicroMovementNetwork
    {
        /// <summary>
        /// このユニットから送信した確認結果の通し番号です。
        /// </summary>
        private int _feedbackSequence;

        /// <summary>
        /// 所有者に依存せず、Master Clientから移動の確定状態を送信します。
        /// </summary>
        public void PublishMicroMovement(MicroMovementSnapshot snapshot)
        {
            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && photonView != null)
            {
                photonView.RPC(nameof(RpcMicroMovement), RpcTarget.Others, JsonUtility.ToJson(snapshot));
            }
        }

        /// <summary>
        /// Master Clientの移動状態だけを受理します。
        /// </summary>
        [PunRPC]
        private void RpcMicroMovement(string json, PhotonMessageInfo info)
        {
            if (info.Sender == null || PhotonNetwork.MasterClient == null ||
                info.Sender.ActorNumber != PhotonNetwork.MasterClient.ActorNumber || string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                UnitBehavior.ApplyMicroMovement(JsonUtility.FromJson<MicroMovementSnapshot>(json));
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"移動状態の形式が不正です。{exception.Message}", this);
            }
        }

        /// <summary>
        /// 適用済みの確認結果の最大通し番号です。
        /// </summary>
        private int _lastFeedbackSequence;

        /// <summary>
        /// 被弾側で確定した結果を攻撃者を含む他のクライアントへ通知します。
        /// </summary>
        public void PublishCombatFeedback(CombatFeedbackResult result)
        {
            if (!CanSendRpc(nameof(RpcCombatFeedback)))
            {
                return;
            }

            CombatFeedbackEvents.Publish(result);
            photonView.RPC(
                nameof(RpcCombatFeedback),
                RpcTarget.Others,
                ++_feedbackSequence,
                result.AttackerId,
                (int)result.Kind,
                (int)result.Side);
        }

        /// <summary>
        /// 所有者が確定した結果を、ダメージ再適用や再送信なしで提示します。
        /// </summary>
        [PunRPC]
        private void RpcCombatFeedback(
            int sequence,
            int attackerId,
            int kind,
            int side,
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(messageInfo, nameof(RpcCombatFeedback)) ||
                sequence <= _lastFeedbackSequence || attackerId < 0 ||
                !Enum.IsDefined(typeof(CombatFeedbackKind), kind) ||
                !Enum.IsDefined(typeof(CombatFeedbackSide), side))
            {
                return;
            }

            _lastFeedbackSequence = sequence;
            CombatFeedbackEvents.Publish(new CombatFeedbackResult(
                attackerId,
                UnitBehavior.PlayerId,
                (CombatFeedbackKind)kind,
                (CombatFeedbackSide)side));
        }

        /// <summary>
        /// 通常のユニット命令に使用するRPC送信先です。
        /// </summary>
        /// <remarks>
        /// サーバーを経由させることで、
        /// 送信元を含む全クライアントでRPCの実行順序を統一します。
        /// </remarks>
        private const RpcTarget k_CommandRpcTarget =
            RpcTarget.AllViaServer;

        /// <summary>
        /// RPC命令を適用するユニットです。
        /// </summary>
        private UnitBehavior _unitBehavior;

        private UnitBehavior UnitBehavior
        {
            get
            {
                if (_unitBehavior == null)
                {
                    _unitBehavior =
                        GetComponent<UnitBehavior>();
                }

                if (_unitBehavior == null)
                {
                    throw new MissingComponentException(
                        $"{nameof(PhotonUnitNetwork)}には、" +
                        $"同じGameObject上の{nameof(UnitBehavior)}が必要です。");
                }

                return _unitBehavior;
            }
        }

        /// <summary>
        /// 必要なコンポーネントを取得します。
        /// </summary>
        protected void Awake()
        {
            _unitBehavior =
                GetComponent<UnitBehavior>();

            if (_unitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    $"{nameof(UnitBehavior)}が見つかりません。",
                    this);
            }
        }

        /// <summary>
        /// ユニットの初期化情報を全クライアントへ送信します。
        /// </summary>
        /// <param name="data">
        /// 所有者、チーム、キャラクター、座標および
        /// 回転を含む初期化情報です。
        /// </param>
        public void Instantiate(
            UnitInitializeData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(
                    nameof(data));
            }

            if (!CanSendRpc(
                    nameof(Instantiate)))
            {
                return;
            }

            string json;

            try
            {
                json =
                    JsonConvert.SerializeObject(
                        data);
            }
            catch (JsonException exception)
            {
                Debug.LogError(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    "ユニット初期化情報のJSON変換に失敗しました。\n" +
                    exception,
                    this);

                return;
            }

            photonView.RPC(
                nameof(RpcInstantiate),
                RpcTarget.AllBuffered,
                json);
        }

        /// <summary>
        /// ユニットの初期化情報を適用します。
        /// </summary>
        [PunRPC]
        private void RpcInstantiate(
            string json,
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(
                    messageInfo,
                    nameof(RpcInstantiate)))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    json))
            {
                Debug.LogWarning(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    "空のユニット初期化情報を受信しました。",
                    this);

                return;
            }

            UnitInitializeData data;

            try
            {
                data =
                    JsonConvert.DeserializeObject<UnitInitializeData>(
                        json);
            }
            catch (JsonException exception)
            {
                Debug.LogError(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    "ユニット初期化情報の復元に失敗しました。\n" +
                    exception,
                    this);

                return;
            }

            if (data == null)
            {
                Debug.LogError(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    "復元されたユニット初期化情報がnullです。",
                    this);

                return;
            }

            UnitBehavior.ApplyInstantiate(
                data);
        }

        /// <summary>
        /// ユニットを撃破状態へ変更します。
        /// </summary>
        public void SetDead()
        {
            SendRpc(
                nameof(RpcSetDead));
        }

        /// <summary>
        /// ユニットを撃破状態へ変更します。
        /// </summary>
        [PunRPC]
        private void RpcSetDead(
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(
                    messageInfo,
                    nameof(RpcSetDead)))
            {
                return;
            }

            UnitBehavior.ApplySetDead();
        }

        /// <summary>
        /// 指定されたチームへスコアを加算します。
        /// </summary>
        /// <param name="teamId">
        /// スコアを加算するチームIDです。
        /// </param>
        public void AddTeamScore(
            int teamId)
        {
            SendRpc(
                nameof(RpcAddTeamScore),
                teamId);
        }

        /// <summary>
        /// 指定されたチームへスコアを加算します。
        /// </summary>
        [PunRPC]
        private void RpcAddTeamScore(
            int teamId,
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(
                    messageInfo,
                    nameof(RpcAddTeamScore)))
            {
                return;
            }

            UnitBehavior.ApplyAddTeamScore(
                teamId);
        }

        /// <summary>
        /// ユニットの移動経路へ座標を追加します。
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
            SendRpc(
                nameof(RpcAddMigrationCoordinate),
                x,
                y);
        }

        /// <summary>
        /// ユニットの移動経路へ座標を追加します。
        /// </summary>
        [PunRPC]
        private void RpcAddMigrationCoordinate(
            int x,
            int y,
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(
                    messageInfo,
                    nameof(RpcAddMigrationCoordinate)))
            {
                return;
            }

            UnitBehavior.ApplyAddMigrationCoordinate(
                x,
                y);
        }

        /// <summary>
        /// ユニットの基本移動速度を設定します。
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
                    $"{nameof(PhotonUnitNetwork)}: " +
                    $"無効な移動速度です。Value={value}",
                    this);

                return;
            }

            SendRpc(
                nameof(RpcSetMoveSpeed),
                value);
        }

        /// <summary>
        /// ユニットの基本移動速度を設定します。
        /// </summary>
        [PunRPC]
        private void RpcSetMoveSpeed(
            float value,
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(
                    messageInfo,
                    nameof(RpcSetMoveSpeed)) ||
                !IsFinite(
                    value))
            {
                return;
            }

            UnitBehavior.ApplySetMoveSpeed(
                value);
        }

        /// <summary>
        /// 指定時間だけユニットの移動を制限します。
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
                    $"{nameof(PhotonUnitNetwork)}: " +
                    $"無効な移動制限時間です。Duration={duration}",
                    this);

                return;
            }

            SendRpc(
                nameof(RpcLockMovement),
                duration);
        }

        /// <summary>
        /// 指定時間だけユニットの移動を制限します。
        /// </summary>
        [PunRPC]
        private void RpcLockMovement(
            float duration,
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(
                    messageInfo,
                    nameof(RpcLockMovement)) ||
                !IsFinite(
                    duration))
            {
                return;
            }

            UnitBehavior.ApplyLockMovement(
                duration);
        }

        /// <summary>
        /// ユニットを指定されたボード座標へ瞬間移動させます。
        /// </summary>
        /// <param name="x">
        /// 移動先のボード座標のX値です。
        /// </param>
        /// <param name="y">
        /// 移動先のボード座標のY値です。
        /// </param>
        public void TeleportMove(
            int x,
            int y)
        {
            SendRpc(
                nameof(RpcTeleportMove),
                x,
                y);
        }

        /// <summary>
        /// ユニットを指定されたボード座標へ瞬間移動させます。
        /// </summary>
        [PunRPC]
        private void RpcTeleportMove(
            int x,
            int y,
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(
                    messageInfo,
                    nameof(RpcTeleportMove)))
            {
                return;
            }

            UnitBehavior.ApplyTeleportMoveToCoordinate(
                x,
                y);
        }

        /// <summary>
        /// ユニットを指定されたボード座標へ補間移動させます。
        /// </summary>
        /// <param name="teamId">
        /// 移動を要求したチームIDです。
        /// </param>
        /// <param name="x">
        /// 移動先のボード座標のX値です。
        /// </param>
        /// <param name="y">
        /// 移動先のボード座標のY値です。
        /// </param>
        public void LerpMove(
            int teamId,
            int x,
            int y)
        {
            if (MatchPhaseManager.Instance?.CurrentPhase == MatchPhase.Micro)
            {
                if (CanSendRpc(nameof(RpcMicroMoveRequest)))
                {
                    photonView.RPC(nameof(RpcMicroMoveRequest), RpcTarget.MasterClient,
                        teamId, x, y, MatchManager.Instance != null ? MatchManager.Instance.CurrentTurn : 0);
                }

                return;
            }

            SendRpc(
                nameof(RpcLerpMoveToCoordinate),
                teamId,
                x,
                y);
        }

        /// <summary>
        /// ユニットを指定されたボード座標へ補間移動させます。
        /// </summary>
        [PunRPC]
        private void RpcLerpMoveToCoordinate(
            int teamId,
            int x,
            int y,
            PhotonMessageInfo messageInfo)
        {
            if (MatchPhaseManager.Instance?.CurrentPhase == MatchPhase.Micro || !CanApplyRpc(
                    messageInfo,
                    nameof(RpcLerpMoveToCoordinate)))
            {
                return;
            }

            UnitBehavior.ApplyLerpMoveToCoordinate(
                teamId,
                x,
                y);
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
                    $"{nameof(PhotonUnitNetwork)}: " +
                    $"無効な移動先座標です。Position={position}",
                    this);

                return;
            }

            SendRpc(
                nameof(RpcLerpMoveToPosition),
                position);
        }

        /// <summary>
        /// ユニットを指定されたワールド座標へ補間移動させます。
        /// </summary>
        [PunRPC]
        private void RpcLerpMoveToPosition(
            Vector3 position,
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(
                    messageInfo,
                    nameof(RpcLerpMoveToPosition)) ||
                !IsFinite(
                    position))
            {
                return;
            }

            UnitBehavior.ApplyLerpMoveToPosition(
                position);
        }

        /// <summary>
        /// ユニットのレイヤーを変更します。
        /// </summary>
        /// <param name="layerName">
        /// 変更先のUnityレイヤー名です。
        /// </param>
        public void ChangeLayer(
            string layerName)
        {
            if (string.IsNullOrWhiteSpace(
                    layerName))
            {
                Debug.LogWarning(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    "変更先レイヤー名が設定されていません。",
                    this);

                return;
            }

            SendRpc(
                nameof(RpcChangeLayer),
                layerName);
        }

        /// <summary>
        /// ユニットのレイヤーを変更します。
        /// </summary>
        [PunRPC]
        private void RpcChangeLayer(
            string layerName,
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(
                    messageInfo,
                    nameof(RpcChangeLayer)) ||
                string.IsNullOrWhiteSpace(
                    layerName))
            {
                return;
            }

            UnitBehavior.ApplyChangeLayer(
                layerName);
        }

        /// <summary>
        /// 指定されたチームから見たユニットの
        /// ステルス状態を切り替えます。
        /// </summary>
        /// <param name="teamId">
        /// ステルス表示判定に使用するチームIDです。
        /// </param>
        /// <param name="isStealth">
        /// ステルス状態へ変更する場合は<c>true</c>です。
        /// </param>
        public void SwitchStealth(
            int teamId,
            bool isStealth)
        {
            SendRpc(
                nameof(RpcSwitchStealth),
                teamId,
                isStealth);
        }

        /// <summary>
        /// 観測者の所有権を使い、発見した敵のマップ公開を同期します。
        /// </summary>
        public void ReportStealthDetection(int targetPlayerId)
        {
            SendRpc(nameof(RpcStealthDetection), targetPlayerId);
        }

        /// <summary>
        /// 観測者の送信権限を検証して、発見情報を適用します。
        /// </summary>
        [PunRPC]
        private void RpcStealthDetection(int targetPlayerId, PhotonMessageInfo messageInfo)
        {
            if (CanApplyRpc(messageInfo, nameof(RpcStealthDetection)))
            {
                UnitBehavior.ApplyStealthDetection(targetPlayerId);
            }
        }

        /// <summary>
        /// 指定されたチームから見たユニットの
        /// ステルス状態を切り替えます。
        /// </summary>
        [PunRPC]
        private void RpcSwitchStealth(
            int teamId,
            bool isStealth,
            PhotonMessageInfo messageInfo)
        {
            if (!CanApplyRpc(
                    messageInfo,
                    nameof(RpcSwitchStealth)))
            {
                return;
            }

            UnitBehavior.ApplySwitchStealth(
                teamId,
                isStealth);
        }

        /// <summary>
        /// 経路編集結果を既存の所有権検証付きRPCで同期します。
        /// </summary>
        public void SetMacroPath(string snapshot)
        {
            SendRpc(nameof(RpcSetMacroPath), snapshot);
        }

        /// <summary>
        /// 所有権を確認して受信した経路編集を適用します。
        /// </summary>
        [PunRPC]
        private void RpcSetMacroPath(string snapshot, PhotonMessageInfo messageInfo)
        {
            if (CanApplyRpc(messageInfo, nameof(RpcSetMacroPath)))
            {
                UnitBehavior.GetMacroUnitBehavior()?.ApplyPathSnapshot(snapshot);
            }
        }

        /// <summary>
        /// 敵へ送信しない計画中の移動RPCか確認します。
        /// </summary>
        private static bool IsPrivatePlanningRpc(string method)
        {
            bool isPlanning = MatchPhaseManager.Instance != null &&
                MatchPhaseManager.Instance.CurrentPhase != MatchPhase.Micro;
            return method == nameof(RpcSetMacroPath) ||
                (isPlanning && (method == nameof(RpcAddMigrationCoordinate) ||
                    method == nameof(RpcLerpMoveToCoordinate)));
        }

        /// <summary>
        /// 計画中の移動は味方に限定し、その他のRPCは通常の宛先へ送信します。
        /// </summary>
        private void SendRpc(
            string rpcMethodName,
            params object[] parameters)
        {
            if (!CanSendRpc(
                    rpcMethodName))
            {
                return;
            }

            if (IsPrivatePlanningRpc(rpcMethodName))
            {
                int teamId = UnitBehavior.GetUnitStatus()?.TeamId ?? -1;
                foreach (Player player in PhotonNetwork.PlayerList)
                {
                    if (teamId >= 0 && OnlinePlayerUtility.GetTeamId(player.ActorNumber) == teamId)
                    {
                        photonView.RPC(rpcMethodName, player, parameters);
                    }
                }

                return;
            }

            photonView.RPC(
                rpcMethodName,
                k_CommandRpcTarget,
                parameters);
        }

        /// <summary>
        /// 同じターンの所有者からのミクロ移動要求をホストだけが適用します。
        /// </summary>
        [PunRPC]
        private void RpcMicroMoveRequest(int teamId, int x, int y, int turn, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient ||
                MatchPhaseManager.Instance?.CurrentPhase != MatchPhase.Micro ||
                turn != (MatchManager.Instance != null ? MatchManager.Instance.CurrentTurn : 0) ||
                !CanApplyRpc(info, nameof(RpcMicroMoveRequest)))
            {
                return;
            }

            UnitBehavior.ApplyLerpMoveToCoordinate(teamId, x, y);
        }

        /// <summary>
        /// 現在のクライアントがRPCを送信できるか確認します。
        /// </summary>
        private bool CanSendRpc(
            string rpcMethodName)
        {
            if (!PhotonNetwork.InRoom ||
                PhotonNetwork.CurrentRoom == null)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    $"Photonルーム外では{rpcMethodName}を送信できません。",
                    this);

                return false;
            }

            if (photonView == null)
            {
                Debug.LogError(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    $"{nameof(PhotonView)}が設定されていません。",
                    this);

                return false;
            }

            if (!photonView.IsMine)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    $"所有していないPhotonViewから" +
                    $"{rpcMethodName}を送信しようとしました。" +
                    $" ViewId={photonView.ViewID}",
                    this);

                return false;
            }

            return true;
        }

        /// <summary>
        /// 受信したRPCを対象ユニットへ適用できるか確認します。
        /// </summary>
        private bool CanApplyRpc(
            PhotonMessageInfo messageInfo,
            string rpcMethodName)
        {
            if (IsPrivatePlanningRpc(rpcMethodName))
            {
                int teamId = UnitBehavior.GetUnitStatus()?.TeamId ?? -1;
                if (teamId < 0 || PhotonNetwork.LocalPlayer == null ||
                    OnlinePlayerUtility.GetTeamId(PhotonNetwork.LocalPlayer.ActorNumber) != teamId)
                {
                    return false;
                }
            }

            Player sender =
                messageInfo.Sender;

            if (sender == null)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    $"送信者不明の{rpcMethodName}を拒否しました。",
                    this);

                return false;
            }

            int ownerActorNumber =
                photonView.OwnerActorNr;

            if (ownerActorNumber > 0)
            {
                if (sender.ActorNumber ==
                    ownerActorNumber)
                {
                    return true;
                }

                Debug.LogWarning(
                    $"{nameof(PhotonUnitNetwork)}: " +
                    $"所有者以外から送信された{rpcMethodName}を拒否しました。" +
                    $" Sender={sender.ActorNumber}, " +
                    $"Owner={ownerActorNumber}, " +
                    $"ViewId={photonView.ViewID}",
                    this);

                return false;
            }

            Player masterClient =
                PhotonNetwork.MasterClient;

            if (masterClient != null &&
                sender.ActorNumber ==
                masterClient.ActorNumber)
            {
                return true;
            }

            Debug.LogWarning(
                $"{nameof(PhotonUnitNetwork)}: " +
                $"Master Client以外から送信された" +
                $"{rpcMethodName}を拒否しました。" +
                $" Sender={sender.ActorNumber}, " +
                $"ViewId={photonView.ViewID}",
                this);

            return false;
        }

        /// <summary>
        /// 浮動小数点値がNaNまたは無限値でないか確認します。
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
        /// Vector3のすべての成分が有効な値か確認します。
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

#if UNITY_EDITOR
        /// <summary>
        /// コンポーネント追加時に必要な参照を自動取得します。
        /// </summary>
        private void Reset()
        {
            _unitBehavior =
                GetComponent<UnitBehavior>();
        }
#endif
    }
}
