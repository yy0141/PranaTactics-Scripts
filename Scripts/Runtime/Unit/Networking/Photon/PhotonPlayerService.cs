using System;
using UnityEngine;
using Photon.Pun;

using PhotonPlayer = Photon.Realtime.Player;

namespace PranaTactics.Unit.Networking.Photon
{
    /// <summary>
    /// Photonルームに参加しているプレイヤーの
    /// ID、チームおよびキャラクター情報を取得します。
    /// </summary>
    public sealed class PhotonPlayerService : IPlayerService
    {
        /// <summary>
        /// プレイヤーIDが無効であることを表す値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// チームIDが無効であることを表す値です。
        /// </summary>
        private const int k_InvalidTeamId = -1;

        /// <summary>
        /// Photonカスタムプロパティに保存するチームIDのキーです。
        /// </summary>
        private const string k_TeamIdPropertyKey = "teamId";

        /// <summary>
        /// Photonカスタムプロパティに保存する
        /// キャラクター名のキーです。
        /// </summary>
        private const string k_CharacterNamePropertyKey =
            "characterName";

        public int LocalPlayerId
        {
            get
            {
                PhotonPlayer localPlayer =
                    PhotonNetwork.LocalPlayer;

                return localPlayer != null
                    ? localPlayer.ActorNumber
                    : k_InvalidPlayerId;
            }
        }

        /// <summary>
        /// 指定されたプレイヤーのチームIDを取得します。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のPhoton ActorNumberです。
        /// </param>
        /// <returns>
        /// プレイヤーのチームIDです。
        /// 取得できない場合は<c>-1</c>です。
        /// </returns>
        public int GetTeamId(
            int playerId)
        {
            if (TryGetTeamId(
                    playerId,
                    out int teamId))
            {
                return teamId;
            }

            return k_InvalidTeamId;
        }

        /// <summary>
        /// 指定されたプレイヤーのキャラクター名を取得します。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のPhoton ActorNumberです。
        /// </param>
        /// <returns>
        /// プレイヤーが使用するキャラクター名です。
        /// 取得できない場合は空文字列です。
        /// </returns>
        public string GetCharacterName(
            int playerId)
        {
            if (TryGetCharacterName(
                    playerId,
                    out string characterName))
            {
                return characterName;
            }

            return string.Empty;
        }

        /// <summary>
        /// 指定されたプレイヤーのチームID取得を試みます。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のPhoton ActorNumberです。
        /// </param>
        /// <param name="teamId">
        /// 取得されたチームIDです。
        /// </param>
        /// <returns>
        /// チームIDを取得できた場合は<c>true</c>です。
        /// </returns>
        public bool TryGetTeamId(
            int playerId,
            out int teamId)
        {
            teamId =
                k_InvalidTeamId;

            if (!TryGetPlayer(
                    playerId,
                    out PhotonPlayer player))
            {
                return false;
            }

            if (!player.CustomProperties.TryGetValue(
                    k_TeamIdPropertyKey,
                    out object propertyValue))
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPlayerService)}: " +
                    $"{k_TeamIdPropertyKey}が同期されていません。" +
                    $" PlayerId={playerId}");

                return false;
            }

            if (!TryConvertToInt(
                    propertyValue,
                    out teamId))
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPlayerService)}: " +
                    $"{k_TeamIdPropertyKey}を整数へ変換できません。" +
                    $" PlayerId={playerId}, " +
                    $"Value={propertyValue}, " +
                    $"Type={propertyValue?.GetType().Name ?? "null"}");

                teamId =
                    k_InvalidTeamId;

                return false;
            }

            return true;
        }

        /// <summary>
        /// 指定されたプレイヤーのキャラクター名取得を試みます。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のPhoton ActorNumberです。
        /// </param>
        /// <param name="characterName">
        /// 取得されたキャラクター名です。
        /// </param>
        /// <returns>
        /// キャラクター名を取得できた場合は<c>true</c>です。
        /// </returns>
        public bool TryGetCharacterName(
            int playerId,
            out string characterName)
        {
            characterName =
                string.Empty;

            if (!TryGetPlayer(
                    playerId,
                    out PhotonPlayer player))
            {
                return false;
            }

            if (!player.CustomProperties.TryGetValue(
                    k_CharacterNamePropertyKey,
                    out object propertyValue))
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPlayerService)}: " +
                    $"{k_CharacterNamePropertyKey}が同期されていません。" +
                    $" PlayerId={playerId}");

                return false;
            }

            if (propertyValue is not string value ||
                string.IsNullOrWhiteSpace(
                    value))
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPlayerService)}: " +
                    $"{k_CharacterNamePropertyKey}が有効な文字列ではありません。" +
                    $" PlayerId={playerId}");

                return false;
            }

            characterName =
                value;

            return true;
        }

        /// <summary>
        /// 指定されたプレイヤーが現在のPhotonルームに
        /// 参加しているか確認します。
        /// </summary>
        /// <param name="playerId">
        /// 確認対象のPhoton ActorNumberです。
        /// </param>
        /// <returns>
        /// ルームへ参加している場合は<c>true</c>です。
        /// </returns>
        public bool ContainsPlayer(
            int playerId)
        {
            return TryGetPlayer(
                playerId,
                out _);
        }

        /// <summary>
        /// 現在のPhotonルームからプレイヤーを取得します。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のPhoton ActorNumberです。
        /// </param>
        /// <param name="player">
        /// 取得されたPhotonプレイヤーです。
        /// </param>
        /// <returns>
        /// プレイヤーを取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetPlayer(
            int playerId,
            out PhotonPlayer player)
        {
            player =
                null;

            if (!PhotonNetwork.InRoom ||
                PhotonNetwork.CurrentRoom == null)
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPlayerService)}: " +
                    "Photonルームへ参加していません。");

                return false;
            }

            if (!PhotonNetwork.CurrentRoom.Players.TryGetValue(
                    playerId,
                    out player))
            {
                Debug.LogWarning(
                    $"{nameof(PhotonPlayerService)}: " +
                    $"プレイヤーが見つかりません。" +
                    $" PlayerId={playerId}");

                return false;
            }

            return true;
        }

        /// <summary>
        /// Photonカスタムプロパティの値を整数へ変換します。
        /// </summary>
        /// <remarks>
        /// PhotonやHashtableへ保存した数値が、
        /// <see cref="byte"/>、<see cref="short"/>、
        /// <see cref="long"/>などとして取得された場合にも対応します。
        /// </remarks>
        private static bool TryConvertToInt(
            object value,
            out int result)
        {
            result =
                default;

            if (value == null)
            {
                return false;
            }

            switch (value)
            {
                case int intValue:
                    result =
                        intValue;

                    return true;

                case byte byteValue:
                    result =
                        byteValue;

                    return true;

                case sbyte signedByteValue:
                    result =
                        signedByteValue;

                    return true;

                case short shortValue:
                    result =
                        shortValue;

                    return true;

                case ushort unsignedShortValue:
                    result =
                        unsignedShortValue;

                    return true;

                case long longValue
                    when longValue >= int.MinValue &&
                        longValue <= int.MaxValue:
                    result =
                        (int)longValue;

                    return true;

                case uint unsignedIntValue
                    when unsignedIntValue <= int.MaxValue:
                    result =
                        (int)unsignedIntValue;

                    return true;

                case string stringValue:
                    return int.TryParse(
                        stringValue,
                        out result);

                default:
                    try
                    {
                        result =
                            Convert.ToInt32(
                                value);

                        return true;
                    }
                    catch (
                        InvalidCastException)
                    {
                        return false;
                    }
                    catch (
                        FormatException)
                    {
                        return false;
                    }
                    catch (
                        OverflowException)
                    {
                        return false;
                    }
            }
        }
    }
}
