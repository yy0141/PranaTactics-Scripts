using System;

namespace PranaTactics.Networking
{
    /// <summary>
    /// ネットワーク経由で送受信するメッセージの情報を保持します。
    /// </summary>
    /// <remarks>
    /// メッセージの種類、送信元、送信先および
    /// JSON形式のペイロードを格納します。
    /// </remarks>
    [Serializable]
    public sealed class NetMessage
    {
        /// <summary>
        /// ネットワークメッセージの種類です。
        /// </summary>
        public NetMessageType Type;

        /// <summary>
        /// メッセージを送信したプレイヤーの識別子です。
        /// </summary>
        public int SenderPlayerId;

        /// <summary>
        /// メッセージの送信先となるプレイヤーの識別子です。
        /// </summary>
        /// <remarks>
        /// 全プレイヤー宛てのメッセージでは、
        /// 実装側で定義された無効値またはブロードキャスト用の値を設定します。
        /// </remarks>
        public int TargetPlayerId;

        /// <summary>
        /// メッセージに含めるJSON形式のペイロードです。
        /// </summary>
        public string Json;
    }
}
