using System;
using UnityEngine;

namespace PranaTactics.Unit.Networking.EOS
{
    /// <summary>
    /// EOS P2Pで送受信するユニット操作メッセージの
    /// 種別文字列を定義します。
    /// </summary>
    /// <remarks>
    /// これらの文字列は通信プロトコルの一部です。
    /// 送信側と受信側で同じ値を使用する必要があります。
    /// </remarks>
    public static class EosUnitP2PMessageTypes
    {
        /// <summary>
        /// 経路編集結果を同期するメッセージです。
        /// </summary>
        public const string SetMacroPath = "unit.setMacroPath";

        /// <summary>
        /// ホストが確定したミクロ移動状態です。
        /// </summary>
        public const string MicroMovement = "unit.microMovement";

        /// <summary>
        /// ユニットを撃破状態へ変更するメッセージです。
        /// </summary>
        public const string SetDead =
            "unit.setDead";

        /// <summary>
        /// チームスコアを加算するメッセージです。
        /// </summary>
        public const string AddTeamScore =
            "unit.addTeamScore";

        /// <summary>
        /// ユニットの移動経路へ座標を追加するメッセージです。
        /// </summary>
        public const string AddMigrationCoordinate =
            "unit.addMigrationCoordinate";

        /// <summary>
        /// ユニットを指定されたボード座標へ
        /// 瞬間移動させるメッセージです。
        /// </summary>
        public const string TeleportCoordinate =
            "unit.teleportCoordinate";

        /// <summary>
        /// ユニットを指定されたボード座標へ
        /// 補間移動させるメッセージです。
        /// </summary>
        public const string LerpCoordinate =
            "unit.lerpCoordinate";

        /// <summary>
        /// ユニットを指定されたワールド座標へ
        /// 補間移動させるメッセージです。
        /// </summary>
        public const string LerpPosition =
            "unit.lerpPosition";

        /// <summary>
        /// ユニットの移動を一定時間制限するメッセージです。
        /// </summary>
        public const string LockMovement =
            "unit.lockMovement";

        /// <summary>
        /// ユニットの移動速度を変更するメッセージです。
        /// </summary>
        public const string SetMoveSpeed =
            "unit.setMoveSpeed";

        /// <summary>
        /// ユニットのUnityレイヤーを変更するメッセージです。
        /// </summary>
        public const string ChangeLayer =
            "unit.changeLayer";

        /// <summary>
        /// ユニットのステルス状態を切り替えるメッセージです。
        /// </summary>
        public const string SwitchStealth =
            "unit.switchStealth";

        /// <summary>
        /// 観測者が発見した敵ユニットをマップに公開するメッセージです。
        /// </summary>
        public const string StealthDetection = "unit.stealthDetection";

        /// <summary>
        /// ユニットの照準状態を同期するメッセージです。
        /// </summary>
        public const string AimState =
            "unit.aimState";

        /// <summary>
        /// 指定された文字列が定義済みの
        /// ユニットメッセージ種別かどうかを確認します。
        /// </summary>
        /// <param name="messageType">
        /// 確認するメッセージ種別です。
        /// </param>
        /// <returns>
        /// 定義済みの種別である場合は<c>true</c>です。
        /// </returns>
        public static bool IsDefined(
            string messageType)
        {
            return messageType switch
            {
                MicroMovement => true,
                SetMacroPath => true,
                SetDead => true,
                AddTeamScore => true,
                AddMigrationCoordinate => true,
                TeleportCoordinate => true,
                LerpCoordinate => true,
                LerpPosition => true,
                LockMovement => true,
                SetMoveSpeed => true,
                ChangeLayer => true,
                SwitchStealth => true,
                StealthDetection => true,
                AimState => true,
                _ => false
            };
        }
    }

    /// <summary>
    /// EOS P2Pで送受信するユニット操作情報を保持します。
    /// </summary>
    /// <remarks>
    /// JSONシリアライザーとの互換性を維持するため、
    /// 通信対象の値は公開フィールドとして定義しています。
    ///
    /// メッセージ種別によって使用するフィールドが異なります。
    /// 使用しないフィールドには既定値が設定されます。
    /// </remarks>
    [Serializable]
    public sealed class EosUnitP2PMessage
    {
        /// <summary>
        /// 送信するユニット操作の種別です。
        /// </summary>
        public string MessageType =
            string.Empty;

        /// <summary>
        /// 操作対象ユニットを所有するプレイヤーIDです。
        /// </summary>
        public int OwnerId =
            -1;

        /// <summary>
        /// このメッセージを送信したプレイヤーIDです。
        /// </summary>
        public int SenderPlayerId =
            -1;

        /// <summary>
        /// 同じ送信者から送られるメッセージの連番です。
        /// </summary>
        /// <remarks>
        /// 重複メッセージや古いメッセージを
        /// 除外するために使用します。
        /// </remarks>
        public int Sequence;

        /// <summary>
        /// チームIDを必要とする操作で使用する値です。
        /// </summary>
        public int TeamId =
            -1;

        /// <summary>
        /// ボード座標のX値です。
        /// </summary>
        public int X;

        /// <summary>
        /// ボード座標のY値です。
        /// </summary>
        public int Y;

        /// <summary>
        /// ワールド座標を必要とする操作で使用する値です。
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// 移動速度や移動制限時間などの
        /// 浮動小数点値を保持します。
        /// </summary>
        public float Value;

        /// <summary>
        /// レイヤー名などの文字列値を保持します。
        /// </summary>
        public string Text =
            string.Empty;

        /// <summary>
        /// ステルス状態などの真偽値を保持します。
        /// </summary>
        public bool BoolValue;

        /// <summary>
        /// 観測者が直接視認した敵ユニットのIDです。
        /// </summary>
        public int TargetPlayerId = -1;

        /// <summary>
        /// ユニット本体のY軸回転角度です。
        /// </summary>
        public float UnitYaw;

        /// <summary>
        /// ユニットの上下照準角度です。
        /// </summary>
        public float Pitch;

        /// <summary>
        /// Main PranamのY軸照準角度です。
        /// </summary>
        public float MainYaw;

        /// <summary>
        /// Sub PranamのY軸照準角度です。
        /// </summary>
        public float SubYaw;

        public bool HasValidHeader
        {
            get
            {
                return OwnerId >=
                        0 &&
                    SenderPlayerId >=
                        0 &&
                    Sequence >=
                        0 &&
                    EosUnitP2PMessageTypes.IsDefined(
                        MessageType);
            }
        }

        /// <summary>
        /// 浮動小数点フィールドにNaNまたは無限値が
        /// 含まれていないか確認します。
        /// </summary>
        /// <returns>
        /// すべての浮動小数点値が有限値の場合は
        /// <c>true</c>です。
        /// </returns>
        public bool HasFiniteValues()
        {
            return IsFinite(
                    Position.x) &&
                IsFinite(
                    Position.y) &&
                IsFinite(
                    Position.z) &&
                IsFinite(
                    Value) &&
                IsFinite(
                    UnitYaw) &&
                IsFinite(
                    Pitch) &&
                IsFinite(
                    MainYaw) &&
                IsFinite(
                    SubYaw);
        }

        /// <summary>
        /// メッセージ内容を確認しやすい文字列へ変換します。
        /// </summary>
        public override string ToString()
        {
            return
                $"{nameof(EosUnitP2PMessage)}(" +
                $"{nameof(MessageType)}={MessageType}, " +
                $"{nameof(OwnerId)}={OwnerId}, " +
                $"{nameof(SenderPlayerId)}={SenderPlayerId}, " +
                $"{nameof(Sequence)}={Sequence}, " +
                $"{nameof(TeamId)}={TeamId}, " +
                $"{nameof(X)}={X}, " +
                $"{nameof(Y)}={Y}, " +
                $"{nameof(Position)}={Position}, " +
                $"{nameof(Value)}={Value}, " +
                $"{nameof(Text)}={Text}, " +
                $"{nameof(BoolValue)}={BoolValue})";
        }

        /// <summary>
        /// 指定された値がNaNまたは無限値でないか確認します。
        /// </summary>
        private static bool IsFinite(
            float value)
        {
            return !float.IsNaN(
                    value) &&
                !float.IsInfinity(
                    value);
        }
    }

    /// <summary>
    /// EOS P2Pパケットの種別とJSONペイロードを保持します。
    /// </summary>
    /// <remarks>
    /// P2Pソケットから受信したパケットが、
    /// どの機能へ渡すデータかを識別するために使用します。
    /// </remarks>
    [Serializable]
    internal sealed class EosP2PPacketEnvelope
    {
        /// <summary>
        /// パケットに格納されているデータの種別です。
        /// </summary>
        public string Kind =
            string.Empty;

        /// <summary>
        /// パケットに格納するJSON文字列です。
        /// </summary>
        public string Payload =
            string.Empty;

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(
                        Kind) &&
                    !string.IsNullOrWhiteSpace(
                        Payload);
            }
        }

        public EosP2PPacketEnvelope()
        {
        }

        public EosP2PPacketEnvelope(
    string kind,
    string payload)
        {
            Kind =
                kind ??
                string.Empty;

            Payload =
                payload ??
                string.Empty;
        }
    }
}
