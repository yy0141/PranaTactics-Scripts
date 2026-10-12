using System;
using UnityEngine;

using PranaTactics.Core;

namespace PranaTactics.Unit
{
    /// <summary>
    /// ユニットを生成および初期化するために必要な情報を保持します。
    /// </summary>
    /// <remarks>
    /// Photon、EOS、ローカル通信で共通して使用する
    /// データ転送用クラスです。
    ///
    /// Unityの<see cref="JsonUtility"/>による変換との互換性を維持するため、
    /// シリアライズ対象の値は公開フィールドとして定義しています。
    /// </remarks>
    [Serializable]
    public sealed class UnitInitializeData
    {
        /// <summary>
        /// ユニットを所有するプレイヤーIDです。
        /// </summary>
        public int OwnerId;

        /// <summary>
        /// ユニットが所属するチームIDです。
        /// </summary>
        public int TeamId;

        /// <summary>
        /// この初期化情報を作成したクライアントにおける
        /// ローカルプレイヤーのチームIDです。
        /// </summary>
        /// <remarks>
        /// 味方・敵の表示判定など、クライアントごとの
        /// 表示初期化に使用します。
        ///
        /// ゲーム上の所属チームは<see cref="TeamId"/>です。
        /// </remarks>
        public int LocalTeamId;

        /// <summary>
        /// ユニットが使用するキャラクター名です。
        /// </summary>
        public string CharacterName =
            string.Empty;

        /// <summary>
        /// 生成時に上書きするステータスです。未指定の場合はキャラクター設定を使います。
        /// </summary>
        public UnitInitialStats InitialStats;

        /// <summary>
        /// ユニットを配置する初期ボード座標のX値です。
        /// </summary>
        public int StartX;

        /// <summary>
        /// ユニットを配置する初期ボード座標のY値です。
        /// </summary>
        public int StartY;

        /// <summary>
        /// ユニットの初期回転を表すオイラー角です。
        /// </summary>
        public SerializableVector3 Rotation;

        public Vector2Int StartCoordinate
        {
            get
            {
                return new Vector2Int(
                    StartX,
                    StartY);
            }

            set
            {
                StartX =
                    value.x;

                StartY =
                    value.y;
            }
        }

        public UnitInitializeData()
        {
        }

        public UnitInitializeData(
    int ownerId,
    int teamId,
    int localTeamId,
    string characterName,
    Vector2Int startCoordinate,
    SerializableVector3 rotation)
        {
            OwnerId =
                ownerId;

            TeamId =
                teamId;

            LocalTeamId =
                localTeamId;

            CharacterName =
                characterName ??
                string.Empty;

            StartX =
                startCoordinate.x;

            StartY =
                startCoordinate.y;

            Rotation =
                rotation;
        }

        public UnitInitializeData(
    int ownerId,
    int teamId,
    int localTeamId,
    string characterName,
    int startX,
    int startY,
    SerializableVector3 rotation)
    : this(
        ownerId,
        teamId,
        localTeamId,
        characterName,
        new Vector2Int(
            startX,
            startY),
        rotation)
        {
        }

        public bool HasCharacter
        {
            get
            {
                return !string.IsNullOrWhiteSpace(
                    CharacterName);
            }
        }

        /// <summary>
        /// 初期化情報を各値へ分解します。
        /// </summary>
        public void Deconstruct(
            out int ownerId,
            out int teamId,
            out int localTeamId,
            out string characterName,
            out Vector2Int startCoordinate,
            out SerializableVector3 rotation)
        {
            ownerId =
                OwnerId;

            teamId =
                TeamId;

            localTeamId =
                LocalTeamId;

            characterName =
                CharacterName;

            startCoordinate =
                StartCoordinate;

            rotation =
                Rotation;
        }

        /// <summary>
        /// ユニット初期化情報を確認しやすい文字列へ変換します。
        /// </summary>
        /// <returns>
        /// 所有者、チーム、キャラクター、座標および回転を含む文字列です。
        /// </returns>
        public override string ToString()
        {
            return
                $"{nameof(UnitInitializeData)}(" +
                $"{nameof(OwnerId)}={OwnerId}, " +
                $"{nameof(TeamId)}={TeamId}, " +
                $"{nameof(LocalTeamId)}={LocalTeamId}, " +
                $"{nameof(CharacterName)}={CharacterName}, " +
                $"{nameof(StartCoordinate)}={StartCoordinate}, " +
                $"{nameof(Rotation)}={Rotation})";
        }
    }
}
