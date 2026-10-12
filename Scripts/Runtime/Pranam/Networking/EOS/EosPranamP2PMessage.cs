using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Pranam.Networking.EOS
{
    /// <summary>
    /// EOS P2Pで送受信するプラーナ同期メッセージの
    /// 種別を定義します。
    /// </summary>
    public static class EosPranamP2PMessageType
    {
        /// <summary>
        /// プラーナを生成するメッセージ種別です。
        /// </summary>
        public const string Instantiate =
            "pranam.instantiate";

        /// <summary>
        /// 命中結果を適用するメッセージ種別です。
        /// </summary>
        public const string Hit =
            "pranam.hit";

        /// <summary>
        /// プラーナを破棄するメッセージ種別です。
        /// </summary>
        public const string Destroy =
            "pranam.destroy";

        /// <summary>
        /// プラーナム効果音を再生するメッセージ種別です。
        /// </summary>
        public const string PlaySound =
            "pranam.playSound";

        /// <summary>
        /// プラーナの親オブジェクトを設定する
        /// メッセージ種別です。
        /// </summary>
        public const string SetParent =
            "pranam.setParent";

        /// <summary>
        /// 指定された文字列が既知のメッセージ種別か判定します。
        /// </summary>
        /// <param name="messageType">
        /// 判定するメッセージ種別です。
        /// </param>
        /// <returns>
        /// 対応しているメッセージ種別の場合は<c>true</c>です。
        /// </returns>
        public static bool IsDefined(
            string messageType)
        {
            return messageType == Instantiate ||
                messageType == Hit ||
                messageType == Destroy ||
                messageType == PlaySound ||
                messageType == SetParent;
        }
    }

    /// <summary>
    /// EOS P2Pを使用してプラーナの生成、命中、破棄および
    /// 効果音再生を同期するためのメッセージです。
    /// </summary>
    /// <remarks>
    /// <see cref="OwnerId"/>と<see cref="IsMain"/>の組み合わせにより、
    /// 所有ユニットのメインまたはサブに対応する
    /// <see cref="PranamBehavior"/>へメッセージを配送します。
    ///
    /// JSONのプロパティ名は、既存の通信データ形式との互換性を
    /// 維持するため、変更前と同じ名前を使用します。
    /// </remarks>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EosPranamP2PMessage
    {
        /// <summary>
        /// 発射時に確定した兵装スロットです。
        /// </summary>
        [JsonProperty("SourcePranamSlot")]
        public int SourcePranamSlot = -1;

        /// <summary>
        /// 命中時の減速率です。
        /// </summary>
        [JsonProperty("HitSlowdownRatio")]
        public float HitSlowdownRatio;

        /// <summary>
        /// 命中時の減速持続時間です。
        /// </summary>
        [JsonProperty("HitSlowdownDurationSeconds")]
        public float HitSlowdownDurationSeconds;

        /// <summary>
        /// 有効なIDが設定されていないことを示す値です。
        /// </summary>
        private const int k_InvalidId = -1;

        /// <summary>
        /// 有効な真偽値が設定されていないことを示す値です。
        /// </summary>
        private const bool k_InvalidBool = false;

        /// <summary>
        /// メッセージの処理種別です。
        /// </summary>
        [SerializeField]
        [JsonProperty("MessageType")]
        private string _messageType = string.Empty;

        /// <summary>
        /// プラーナムを所有するプレイヤーのIDです。
        /// </summary>
        [SerializeField]
        [JsonProperty("OwnerId")]
        private int _ownerId = k_InvalidId;

        /// <summary>
        /// メッセージを送信したプレイヤーのIDです。
        /// </summary>
        [SerializeField]
        [JsonProperty("SenderPlayerId")]
        private int _senderPlayerId = k_InvalidId;

        /// <summary>
        /// 送信ストリーム内でのメッセージ連番です。
        /// </summary>
        [SerializeField]
        [JsonProperty("Sequence")]
        private int _sequence;

        /// <summary>
        /// メインプラーナムに対応するメッセージかどうかを示します。
        /// </summary>
        [SerializeField]
        [JsonProperty("IsMain")]
        private bool _isMain;

        /// <summary>
        /// 生成するプラーナPrefabのResources相対パスです。
        /// </summary>
        [SerializeField]
        [JsonProperty("PrefabPath")]
        private string _prefabPath = string.Empty;

        /// <summary>
        /// プラーナの生成位置または効果音の再生位置です。
        /// </summary>
        [SerializeField]
        [JsonProperty("Position")]
        private Vector3 _position;

        /// <summary>
        /// プラーナ生成時の回転です。
        /// </summary>
        [SerializeField]
        [JsonProperty("Rotation")]
        private Quaternion _rotation = Quaternion.identity;

        /// <summary>
        /// JSON形式へ変換されたプラーナ生成情報です。
        /// </summary>
        [SerializeField]
        [JsonProperty("ContextJson")]
        private string _contextJson = string.Empty;

        /// <summary>
        /// 攻撃を行ったプレイヤーのIDです。
        /// </summary>
        [SerializeField]
        [JsonProperty("AttackerId")]
        private int _attackerId = k_InvalidId;

        /// <summary>
        /// 攻撃対象となるプレイヤーまたはAIのIDです。
        /// </summary>
        [SerializeField]
        [JsonProperty("TargetId")]
        private int _targetId = k_InvalidId;

        /// <summary>
        /// 攻撃対象へ適用するダメージ量です。
        /// </summary>
        [SerializeField]
        [JsonProperty("Damage")]
        private float _damage;

        /// <summary>
        /// 生成または破棄するプラーナの識別子です。
        /// </summary>
        [SerializeField]
        [JsonProperty("PranaId")]
        private int _pranaId = k_InvalidId;

        /// <summary>
        /// 親設定に使用するオブジェクト識別子です。
        /// </summary>
        [SerializeField]
        [JsonProperty("ParentId")]
        private int _parentId = k_InvalidId;

        /// <summary>
        /// trueの場合はMountRoot、
        /// falseの場合はWorldRootへ設定します。
        /// </summary>
        [SerializeField]
        [JsonProperty("IsMountRoot")]
        private bool _isMountRoot = k_InvalidBool;

        /// <summary>
        /// 効果音に使用するサウンドプロファイルの識別子です。
        /// </summary>
        [SerializeField]
        [JsonProperty("SoundProfileId")]
        private string _soundProfileId = string.Empty;

        /// <summary>
        /// 整数へ変換されたプラーナムサウンドイベントです。
        /// </summary>
        [SerializeField]
        [JsonProperty("SoundEvent")]
        private int _soundEvent;

        /// <summary>
        /// 3Dサウンドの方向として使用する前方ベクトルです。
        /// </summary>
        [SerializeField]
        [JsonProperty("Forward")]
        private Vector3 _forward = Vector3.forward;

        public string MessageType
        {
            get
            {
                return _messageType;
            }
            set
            {
                _messageType =
                    value ?? string.Empty;
            }
        }

        public int OwnerId
        {
            get
            {
                return _ownerId;
            }
            set
            {
                _ownerId = value;
            }
        }

        public int SenderPlayerId
        {
            get
            {
                return _senderPlayerId;
            }
            set
            {
                _senderPlayerId = value;
            }
        }

        public int Sequence
        {
            get
            {
                return _sequence;
            }
            set
            {
                _sequence =
                    Mathf.Max(
                        0,
                        value);
            }
        }

        public bool IsMain
        {
            get
            {
                return _isMain;
            }
            set
            {
                _isMain = value;
            }
        }

        public string PrefabPath
        {
            get
            {
                return _prefabPath;
            }
            set
            {
                _prefabPath =
                    value ?? string.Empty;
            }
        }

        public Vector3 Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
            }
        }

        public Quaternion Rotation
        {
            get
            {
                return _rotation;
            }
            set
            {
                _rotation = value;
            }
        }

        public string ContextJson
        {
            get
            {
                return _contextJson;
            }
            set
            {
                _contextJson =
                    value ?? string.Empty;
            }
        }

        public int AttackerId
        {
            get
            {
                return _attackerId;
            }
            set
            {
                _attackerId = value;
            }
        }

        public int TargetId
        {
            get
            {
                return _targetId;
            }
            set
            {
                _targetId = value;
            }
        }

        public float Damage
        {
            get
            {
                return _damage;
            }
            set
            {
                _damage =
                    Mathf.Max(
                        0.0f,
                        value);
            }
        }

        public int PranaId
        {
            get
            {
                return _pranaId;
            }
            set
            {
                _pranaId = value;
            }
        }

        public int ParentId
        {
            get
            {
                return _parentId;
            }
            set
            {
                _parentId = value;
            }
        }

        public bool IsMountRoot
        {
            get
            {
                return _isMountRoot;
            }
            set
            {
                _isMountRoot = value;
            }
        }

        public string SoundProfileId
        {
            get
            {
                return _soundProfileId;
            }
            set
            {
                _soundProfileId =
                    value ?? string.Empty;
            }
        }

        public int SoundEvent
        {
            get
            {
                return _soundEvent;
            }
            set
            {
                _soundEvent = value;
            }
        }

        public Vector3 Forward
        {
            get
            {
                return _forward;
            }
            set
            {
                _forward = value;
            }
        }

        [JsonIgnore]
        public bool HasValidEnvelope
        {
            get
            {
                return EosPranamP2PMessageType.IsDefined(
                        _messageType) &&
                    _ownerId >= 0 &&
                    _sequence > 0;
            }
        }
    }
}
