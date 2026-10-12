using System;
using UnityEngine;

namespace PranaTactics.Unit
{
    /// <summary>
    /// プレイヤーが使用するユニットの基本情報を保持します。
    /// </summary>
    /// <remarks>
    /// Unityのシリアライズ、JSON変換および
    /// ネットワーク通信で使用できるデータ転送用クラスです。
    ///
    /// <see cref="Rotation"/>にはQuaternionではなく、
    /// オイラー角を格納します。
    /// </remarks>
    [Serializable]
    public sealed class PlayerInfo
    {
        /// <summary>
        /// プレイヤーが未設定であることを示すIDです。
        /// </summary>
        public const int k_InvalidPlayerId = -1;

        /// <summary>
        /// チームが未設定であることを示すIDです。
        /// </summary>
        public const int k_InvalidTeamId = -1;

        /// <summary>
        /// プレイヤーIDです。
        /// </summary>
        public int PlayerId = k_InvalidPlayerId;

        /// <summary>
        /// プレイヤーが所属するチームIDです。
        /// </summary>
        public int TeamId = k_InvalidTeamId;

        /// <summary>
        /// プレイヤーが使用するキャラクター名です。
        /// </summary>
        public string CharacterName = string.Empty;

        /// <summary>
        /// ステージで指定された生成時ステータスです。未指定の場合はキャラクター設定を使います。
        /// </summary>
        public UnitInitialStats InitialStats;

        /// <summary>
        /// ユニットを配置するボード座標です。
        /// </summary>
        public Vector2Int Coordinate = Vector2Int.zero;

        /// <summary>
        /// ユニットの初期回転を表すオイラー角です。
        /// </summary>
        public Vector3 Rotation = Vector3.zero;

        public PlayerInfo()
        {
        }

        public PlayerInfo(
    int playerId,
    int teamId,
    string characterName,
    Vector2Int coordinate,
    Vector3 rotation)
        {
            PlayerId =
                playerId;

            TeamId =
                teamId;

            CharacterName =
                characterName ??
                string.Empty;

            Coordinate =
                coordinate;

            Rotation =
                rotation;
        }

        public bool IsValid
        {
            get
            {
                return PlayerId !=
                        k_InvalidPlayerId &&
                    TeamId !=
                        k_InvalidTeamId &&
                    !string.IsNullOrWhiteSpace(
                        CharacterName);
            }
        }

        /// <summary>
        /// プレイヤー情報を各値へ分解します。
        /// </summary>
        public void Deconstruct(
            out int playerId,
            out int teamId,
            out string characterName,
            out Vector2Int coordinate,
            out Vector3 rotation)
        {
            playerId =
                PlayerId;

            teamId =
                TeamId;

            characterName =
                CharacterName;

            coordinate =
                Coordinate;

            rotation =
                Rotation;
        }

        /// <summary>
        /// プレイヤー情報を確認しやすい文字列へ変換します。
        /// </summary>
        /// <returns>
        /// プレイヤーID、チームID、キャラクター名、
        /// 座標および回転を含む文字列です。
        /// </returns>
        public override string ToString()
        {
            return
                $"{nameof(PlayerInfo)}(" +
                $"{nameof(PlayerId)}={PlayerId}, " +
                $"{nameof(TeamId)}={TeamId}, " +
                $"{nameof(CharacterName)}={CharacterName}, " +
                $"{nameof(Coordinate)}={Coordinate}, " +
                $"{nameof(Rotation)}={Rotation})";
        }
    }
}
