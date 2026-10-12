using System;
using UnityEngine;

using PranaTactics.Networking;
using PranaTactics.Unit.Networking;

namespace PranaTactics.Unit.Networking.EOS
{
    /// <summary>
    /// EOSを使用するオンラインゲームの
    /// プレイヤー情報を取得します。
    /// </summary>
    /// <remarks>
    /// 実際のプレイヤー情報は
    /// <see cref="NetworkServiceLocator"/>から取得します。
    /// </remarks>
    public sealed class EosPlayerService : IPlayerService
    {
        /// <summary>
        /// プレイヤーIDが無効であることを表す値です。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// チームIDが無効であることを表す値です。
        /// </summary>
        private const int k_InvalidTeamId = -1;

        public int LocalPlayerId
        {
            get
            {
                var onlineService =
                    NetworkServiceLocator.Online;

                return onlineService != null
                    ? onlineService.LocalPlayerId
                    : k_InvalidPlayerId;
            }
        }

        /// <summary>
        /// 指定されたプレイヤーのチームIDを取得します。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のプレイヤーIDです。
        /// </param>
        /// <returns>
        /// プレイヤーが所属するチームIDです。
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
        /// 取得対象のプレイヤーIDです。
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
        /// 取得対象のプレイヤーIDです。
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

            if (!TryFindPlayer(
                    playerId,
                    out OnlinePlayer player))
            {
                return false;
            }

            if (!player.TryGetProperty(
                    NetworkPropertyKeys.TeamId,
                    out teamId))
            {
                Debug.LogWarning(
                    $"{nameof(EosPlayerService)}: " +
                    $"{NetworkPropertyKeys.TeamId}が同期されていません。" +
                    $" PlayerId={playerId}");

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
        /// 取得対象のプレイヤーIDです。
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

            if (!TryFindPlayer(
                    playerId,
                    out OnlinePlayer player))
            {
                return false;
            }

            if (!player.TryGetProperty(
                    NetworkPropertyKeys.CharacterName,
                    out string value))
            {
                Debug.LogWarning(
                    $"{nameof(EosPlayerService)}: " +
                    $"{NetworkPropertyKeys.CharacterName}が同期されていません。" +
                    $" PlayerId={playerId}");

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    value))
            {
                Debug.LogWarning(
                    $"{nameof(EosPlayerService)}: " +
                    "キャラクター名が空です。" +
                    $" PlayerId={playerId}");

                return false;
            }

            characterName =
                value;

            return true;
        }

        /// <summary>
        /// 指定されたプレイヤーがオンラインプレイヤー一覧に
        /// 登録されているか確認します。
        /// </summary>
        /// <param name="playerId">
        /// 確認対象のプレイヤーIDです。
        /// </param>
        /// <returns>
        /// 登録されている場合は<c>true</c>です。
        /// </returns>
        public bool ContainsPlayer(
            int playerId)
        {
            return TryFindPlayer(
                playerId,
                out _);
        }

        /// <summary>
        /// 指定されたプレイヤー情報を取得します。
        /// </summary>
        /// <param name="playerId">
        /// 取得対象のプレイヤーIDです。
        /// </param>
        /// <param name="player">
        /// 取得されたオンラインプレイヤー情報です。
        /// </param>
        /// <returns>
        /// プレイヤー情報を取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryFindPlayer(
            int playerId,
            out OnlinePlayer player)
        {
            player =
                null;

            if (playerId <
                0)
            {
                Debug.LogWarning(
                    $"{nameof(EosPlayerService)}: " +
                    $"無効なプレイヤーIDです。PlayerId={playerId}");

                return false;
            }

            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                Debug.LogWarning(
                    $"{nameof(EosPlayerService)}: " +
                    "オンラインサービスが初期化されていません。");

                return false;
            }

            if (onlineService.Players == null)
            {
                Debug.LogWarning(
                    $"{nameof(EosPlayerService)}: " +
                    "オンラインプレイヤー一覧が初期化されていません。");

                return false;
            }

            foreach (OnlinePlayer candidate in
                    onlineService.Players)
            {
                if (candidate == null ||
                    candidate.PlayerId !=
                    playerId)
                {
                    continue;
                }

                player =
                    candidate;

                return true;
            }

            Debug.LogWarning(
                $"{nameof(EosPlayerService)}: " +
                "プレイヤーが見つかりません。" +
                $" PlayerId={playerId}");

            return false;
        }
    }
}
