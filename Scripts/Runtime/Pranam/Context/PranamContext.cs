using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Core;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// ネットワークを介して生成するプラーナの所有者、
    /// 攻撃性能、生成位置および対象情報を保持します。
    /// </summary>
    /// <remarks>
    /// JSONのプロパティ名は既存の通信形式との互換性を維持するため、
    /// 変更前のフィールド名を使用します。
    /// </remarks>
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PranamContext
    {
        /// <summary>
        /// 同じPrefabを共有する兵装の固定設定を識別するアセット名です。
        /// </summary>
        [JsonProperty("DefinitionId")]
        public string DefinitionId;

        /// <summary>
        /// 通信用の生成情報から、この弾だけが所有する実行状態を複製します。
        /// </summary>
        /// <remarks>
        /// 爆発設定も複製し、威力の減少や初期化によって送受信情報や他の弾を変更しません。
        /// </remarks>
        public PranamContext CreateSnapshot()
        {
            var snapshot = (PranamContext)MemberwiseClone();
            snapshot.Explosion = Explosion?.CreateSnapshot();
            return snapshot;
        }

        /// <summary>
        /// 発射時に固定した着弾爆発設定です。通常弾では設定されません。
        /// </summary>
        [JsonProperty("Explosion")]
        public ShooterExplosionSettings Explosion;

        /// <summary>
        /// 近接攻撃の生成位置から当たり判定までの最大距離です。
        /// </summary>
        [JsonProperty("MeleeRange")]
        public float MeleeRange;

        /// <summary>
        /// 攻撃用兵装の設定から命中時の減速情報を固定します。
        /// </summary>
        public void ConfigureHitSlowdown(IHitSlowdownPranamData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            HitSlowdownRatio = data.HitSlowdownRatio;
            HitSlowdownDurationSeconds = data.HitSlowdownDurationSeconds;
        }

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
        /// 同じ兵装使用で生成した弾が共有するSEグループです。
        /// </summary>
        [JsonProperty("SoundGroupId")]
        public string SoundGroupId;

        /// <summary>
        /// プラーナを生成したプレイヤーのIDです。
        /// </summary>
        [SerializeField]
        [JsonProperty("PlayerId")]
        private int _playerId;

        /// <summary>
        /// 生成されたプラーナオブジェクト固有のIDです。
        /// </summary>
        [SerializeField]
        [JsonProperty("ID")]
        private int _id;

        /// <summary>
        /// プラーナムの装備スロット番号です。
        /// </summary>
        [SerializeField]
        [JsonProperty("PranamNumber")]
        private int _pranamNumber;

        /// <summary>
        /// メインプラーナムから生成されたかどうかを示します。
        /// </summary>
        [SerializeField]
        [JsonProperty("IsMain")]
        private bool _isMain;

        /// <summary>
        /// MicroUnitを追従するかを示します。
        /// </summary>
        [SerializeField]
        [JsonProperty("IsMountRoot")]
        public bool IsMountRoot;

        /// <summary>
        /// プラーナの攻撃力です。
        /// </summary>
        [SerializeField]
        [JsonProperty("Attack")]
        private float _attack;

        /// <summary>
        /// プラーナの防御力です。
        /// </summary>
        [SerializeField]
        [JsonProperty("Defense")]
        private float _defense;

        /// <summary>
        /// プラーナの移動速度です。
        /// </summary>
        [SerializeField]
        [JsonProperty("Speed")]
        private float _speed;

        /// <summary>
        /// プラーナの弾道へ適用する拡散角度です。
        /// </summary>
        [SerializeField]
        [JsonProperty("SpreadAngle")]
        private float _spreadAngle;

        /// <summary>
        /// プラーナを所有するPhotonオブジェクトのView IDです。
        /// </summary>
        /// <remarks>
        /// Photon以外の通信方式では使用しない場合があります。
        /// </remarks>
        [SerializeField]
        [JsonProperty("OwnerViewID")]
        private int _ownerViewId;

        /// <summary>
        /// 攻撃対象となるプレイヤーのIDです。
        /// </summary>
        [SerializeField]
        [JsonProperty("TargetPlayerID")]
        private int _targetPlayerId = -1;

        /// <summary>
        /// プラーナの生成または軌道計算に使用する基準位置です。
        /// </summary>
        [SerializeField]
        [JsonProperty("GeneratePosition")]
        private SerializableVector3 _generatePosition;

        /// <summary>
        /// プラーナの効果音に使用する
        /// サウンドプロファイルの識別子です。
        /// </summary>
        [SerializeField]
        [JsonProperty("SoundProfileId")]
        private string _soundProfileId = string.Empty;

        public int PlayerId
        {
            get
            {
                return _playerId;
            }
            set
            {
                _playerId = value;
            }
        }

        public int Id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
            }
        }

        public int PranamNumber
        {
            get
            {
                return _pranamNumber;
            }
            set
            {
                _pranamNumber = value;
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

        public float Attack
        {
            get
            {
                return _attack;
            }
            set
            {
                _attack = value;
            }
        }

        public float Defense
        {
            get
            {
                return _defense;
            }
            set
            {
                _defense = value;
            }
        }

        public float Speed
        {
            get
            {
                return _speed;
            }
            set
            {
                _speed = value;
            }
        }

        public float SpreadAngle
        {
            get
            {
                return _spreadAngle;
            }
            set
            {
                _spreadAngle = value;
            }
        }

        public int OwnerViewId
        {
            get
            {
                return _ownerViewId;
            }
            set
            {
                _ownerViewId = value;
            }
        }

        public int TargetPlayerId
        {
            get
            {
                return _targetPlayerId;
            }
            set
            {
                _targetPlayerId = value;
            }
        }

        public SerializableVector3 GeneratePosition
        {
            get
            {
                return _generatePosition;
            }
            set
            {
                _generatePosition = value;
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
    }
}
