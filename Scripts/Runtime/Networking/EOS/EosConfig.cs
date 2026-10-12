using UnityEngine;

namespace PranaTactics.Networking.EOS
{
    /// <summary>
    /// Epic Online Servicesへの接続、Lobby検索および
    /// P2P通信に使用する設定を保持します。
    /// </summary>
    [CreateAssetMenu(
        fileName = "EosConfig",
        menuName = "Prana Tactics/Networking/EOS Config",
        order = 0)]
    public class EosConfig : ScriptableObject
    {
        /// <summary>
        /// Epic Online Servicesへ通知する製品名です。
        /// </summary>
        [Header("EOS Product Settings")]
        public string ProductName = "Prana Tactics";

        /// <summary>
        /// Epic Online Servicesへ通知する製品バージョンです。
        /// </summary>
        public string ProductVersion = "0.1.0";

        /// <summary>
        /// Epic Developer Portalで発行されたProduct IDです。
        /// </summary>
        [Tooltip("Developer Portal の Product ID")]
        public string ProductId;

        /// <summary>
        /// Epic Developer Portalで発行されたSandbox IDです。
        /// </summary>
        [Tooltip("Developer Portal の Sandbox ID")]
        public string SandboxId;

        /// <summary>
        /// Epic Developer Portalで発行されたDeployment IDです。
        /// </summary>
        [Tooltip("Developer Portal の Deployment ID")]
        public string DeploymentId;

        /// <summary>
        /// Epic Developer Portalで発行されたClient IDです。
        /// </summary>
        [Tooltip("Developer Portal の Client ID")]
        public string ClientId;

        /// <summary>
        /// Epic Developer Portalで発行されたClient Secretです。
        /// </summary>
        [Tooltip("Developer Portal の Client Secret")]
        public string ClientSecret;

        /// <summary>
        /// Lobby検索で取得する最大検索結果数です。
        /// </summary>
        [Header("Lobby")]
        public uint SearchMaxResults = 50;

        /// <summary>
        /// パーティ用Lobbyを分類するBucket IDです。
        /// </summary>
        public string PartyBucketId = "prana_party";

        /// <summary>
        /// マッチング用Lobbyを分類するBucket IDです。
        /// </summary>
        public string MatchingBucketId = "prana_matching";

        /// <summary>
        /// Lobby属性にゲームバージョンを保存するときに使用するキーです。
        /// </summary>
        public string GameVersionKey = "gameVersion";

        /// <summary>
        /// P2P接続で使用するSocket名です。
        /// </summary>
        [Header("P2P")]
        public string SocketName = "prana_tactics";

        /// <summary>
        /// P2Pパケットの送受信に使用するチャンネル番号です。
        /// </summary>
        public byte P2PChannel = 0;

        /// <summary>
        /// 1つのP2Pパケットとして扱う最大データサイズです。
        /// </summary>
        public uint MaximumPacketSize = 1170;
    }
}
