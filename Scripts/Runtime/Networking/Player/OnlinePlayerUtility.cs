using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Match;

namespace PranaTactics.Networking
{
    /// <summary>
    /// オンラインプレイヤーの検索と、
    /// プレイヤープロパティに関する共通処理を提供します。
    /// </summary>
    public static class OnlinePlayerUtility
    {
        /// <summary>
        /// チームが未設定であることを示す識別子です。
        /// </summary>
        private const int k_UnassignedTeamId = -1;

        /// <summary>
        /// 単一パーティ対戦時に使用するチーム識別子です。
        /// </summary>
        private const int k_SinglePartyTeamId = 0;

        /// <summary>
        /// 現在のオンラインサービスから
        /// ローカルプレイヤーを取得します。
        /// </summary>
        /// <returns>
        /// ローカルプレイヤーです。
        /// オンラインサービスが存在しない場合や、
        /// 対応するプレイヤーが見つからない場合は<c>null</c>です。
        /// </returns>
        public static OnlinePlayer GetLocalPlayer()
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                return null;
            }

            return GetPlayer(
                onlineService,
                onlineService.LocalPlayerId);
        }

        /// <summary>
        /// 指定されたプレイヤーの所属チームIDを取得します。
        /// </summary>
        /// <param name="playerId">
        /// 所属チームを取得するプレイヤーの識別子です。
        /// </param>
        /// <returns>
        /// プレイヤーの所属チームIDです。
        /// 取得できない場合は<see cref="k_UnassignedTeamId"/>です。
        /// </returns>
        /// <remarks>
        /// 単一パーティ対戦では、TeamIdの同期が完了していない場合でも、
        /// 参加中の人間プレイヤーを同じチームとして扱います。
        /// </remarks>
        public static int GetTeamId(int playerId)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                return k_UnassignedTeamId;
            }

            if (MatchTeamAssignment.TryGet(onlineService.CurrentRoom, playerId, out int assignedTeam))
            {
                return assignedTeam;
            }

            OnlinePlayer player =
                GetPlayer(
                    onlineService,
                    playerId);

            if (player == null)
            {
                return k_UnassignedTeamId;
            }

            if (TryGetPropertyIgnoreCase(
                    player,
                    NetworkPropertyKeys.TeamId,
                    out int teamId) &&
                teamId >= 0)
            {
                return teamId;
            }

            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (matchmakingManager != null &&
                matchmakingManager.IsSinglePartyMatchMode())
            {
                return k_SinglePartyTeamId;
            }

            return k_UnassignedTeamId;
        }

        /// <summary>
        /// 指定されたプレイヤーが選択している
        /// キャラクター名を取得します。
        /// </summary>
        /// <param name="playerId">
        /// キャラクター名を取得するプレイヤーの識別子です。
        /// </param>
        /// <returns>
        /// プレイヤーが選択しているキャラクター名です。
        /// 取得できない場合は<c>null</c>です。
        /// </returns>
        public static string GetCharacterName(int playerId)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                return null;
            }

            OnlinePlayer player =
                GetPlayer(
                    onlineService,
                    playerId);

            if (player == null)
            {
                return null;
            }

            if (TryGetPropertyIgnoreCase(
                    player,
                    NetworkPropertyKeys.CharacterName,
                    out string characterName))
            {
                return characterName;
            }

            return null;
        }

        /// <summary>
        /// 全プレイヤーがキャラクター選択を完了し、
        /// ゲームを開始できるかどうかを確認します。
        /// </summary>
        /// <returns>
        /// 全プレイヤーが準備を完了している場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        /// <remarks>
        /// 既存コードとの互換性を維持するためのメソッドです。
        /// 新しい処理では
        /// <see cref="AllPlayersReadyForCharacterSelect(bool)"/>を使用します。
        /// </remarks>
        public static bool AllPlayersCanStartGame()
        {
            return AllPlayersReadyForCharacterSelect();
        }

        /// <summary>
        /// 必要な人数のプレイヤーが参加し、
        /// 全員がキャラクター選択を完了しているか確認します。
        /// </summary>
        /// <param name="logFailure">
        /// 準備が完了していない理由をログへ出力する場合は
        /// <c>true</c>を指定します。
        /// </param>
        /// <returns>
        /// 必要な人数のプレイヤー全員が準備を完了し、
        /// キャラクターを選択している場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        public static bool AllPlayersReadyForCharacterSelect(
            bool logFailure = false)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                LogReadyState(
                    logFailure,
                    "オンラインサービスが登録されていません。");

                return false;
            }

            if (!onlineService.IsInRoom)
            {
                LogReadyState(
                    logFailure,
                    "オンラインルームに参加していません。");

                return false;
            }

            if (onlineService.Players == null ||
                onlineService.Players.Count == 0)
            {
                LogReadyState(
                    logFailure,
                    "参加プレイヤーが存在しません。");

                return false;
            }

            int expectedHumanPlayerCount =
                GetExpectedHumanPlayerCount(
                    onlineService);

            if (onlineService.Players.Count <
                expectedHumanPlayerCount)
            {
                LogReadyState(
                    logFailure,
                    $"プレイヤーの参加を待機しています。" +
                    $"Current={onlineService.Players.Count}, " +
                    $"Expected={expectedHumanPlayerCount}");

                return false;
            }

            int checkedPlayerCount = 0;

            foreach (
                OnlinePlayer player
                in onlineService.Players)
            {
                if (player == null)
                {
                    LogReadyState(
                        logFailure,
                        "プレイヤー情報にnullが含まれています。");

                    return false;
                }

                checkedPlayerCount++;

                if (!TryGetBooleanProperty(
                        player,
                        NetworkPropertyKeys.Ready,
                        out bool isReady))
                {
                    LogReadyState(
                        logFailure,
                        $"Readyプロパティが設定されていません。" +
                        $"PlayerId={player.PlayerId}");

                    return false;
                }

                if (!isReady)
                {
                    LogReadyState(
                        logFailure,
                        $"キャラクター選択が完了していません。" +
                        $"PlayerId={player.PlayerId}");

                    return false;
                }

                if (!TryGetPropertyIgnoreCase(
                        player,
                        NetworkPropertyKeys.CharacterName,
                        out string characterName))
                {
                    LogReadyState(
                        logFailure,
                        $"CharacterNameプロパティが設定されていません。" +
                        $"PlayerId={player.PlayerId}");

                    return false;
                }

                if (string.IsNullOrWhiteSpace(characterName))
                {
                    LogReadyState(
                        logFailure,
                        $"キャラクターが選択されていません。" +
                        $"PlayerId={player.PlayerId}");

                    return false;
                }
            }

            bool areAllPlayersReady =
                checkedPlayerCount >= expectedHumanPlayerCount;

            if (areAllPlayersReady && logFailure)
            {
                Debug.Log(
                    $"{nameof(OnlinePlayerUtility)}: " +
                    "全プレイヤーのキャラクター選択が完了しました。" +
                    $"Players={checkedPlayerCount}/" +
                    $"{expectedHumanPlayerCount}");
            }

            return areAllPlayersReady;
        }

        /// <summary>
        /// オンラインサービスから指定された識別子の
        /// プレイヤーを取得します。
        /// </summary>
        /// <param name="onlineService">
        /// プレイヤー情報を保持するオンラインサービスです。
        /// </param>
        /// <param name="playerId">
        /// 取得するプレイヤーの識別子です。
        /// </param>
        /// <returns>
        /// 指定された識別子のプレイヤーです。
        /// 見つからない場合は<c>null</c>です。
        /// </returns>
        private static OnlinePlayer GetPlayer(
            IOnlineService onlineService,
            int playerId)
        {
            if (onlineService?.Players == null)
            {
                return null;
            }

            foreach (
                OnlinePlayer player
                in onlineService.Players)
            {
                if (player != null &&
                    player.PlayerId == playerId)
                {
                    return player;
                }
            }

            return null;
        }

        /// <summary>
        /// キャラクター選択の完了に必要な
        /// 人間プレイヤー数を取得します。
        /// </summary>
        /// <param name="onlineService">
        /// 現在使用しているオンラインサービスです。
        /// </param>
        /// <returns>
        /// キャラクター選択の完了に必要なプレイヤー数です。
        /// </returns>
        private static int GetExpectedHumanPlayerCount(
            IOnlineService onlineService)
        {
            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (matchmakingManager == null)
            {
                return onlineService.Players.Count;
            }

            return Mathf.Max(
                1,
                matchmakingManager
                    .GetExpectedHumanPlayerCount());
        }

        /// <summary>
        /// 指定されたプレイヤーからbool型の
        /// プロパティを取得します。
        /// </summary>
        /// <param name="player">
        /// プロパティを保持するプレイヤーです。
        /// </param>
        /// <param name="key">
        /// 取得するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// 取得されたbool値です。
        /// </param>
        /// <returns>
        /// bool値として取得または変換できた場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool TryGetBooleanProperty(
            OnlinePlayer player,
            string key,
            out bool value)
        {
            value = false;

            if (!TryGetRawPropertyIgnoreCase(
                    player,
                    key,
                    out object rawValue))
            {
                return false;
            }

            if (rawValue is bool booleanValue)
            {
                value = booleanValue;

                return true;
            }

            return bool.TryParse(
                rawValue?.ToString(),
                out value);
        }

        /// <summary>
        /// 指定されたプレイヤーから、キーの大文字と小文字を区別せずに
        /// 型指定されたプロパティを取得します。
        /// </summary>
        /// <typeparam name="T">
        /// 取得するプロパティ値の型です。
        /// </typeparam>
        /// <param name="player">
        /// プロパティを保持するプレイヤーです。
        /// </param>
        /// <param name="key">
        /// 取得するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// 取得されたプロパティ値です。
        /// </param>
        /// <returns>
        /// 指定された型として取得できた場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool TryGetPropertyIgnoreCase<T>(
            OnlinePlayer player,
            string key,
            out T value)
        {
            value = default;

            if (!TryGetRawPropertyIgnoreCase(
                    player,
                    key,
                    out object rawValue))
            {
                return false;
            }

            if (rawValue is not T typedValue)
            {
                return false;
            }

            value = typedValue;

            return true;
        }

        /// <summary>
        /// 指定されたプレイヤーから、キーの大文字と小文字を区別せずに
        ///プロパティの元の値を取得します。
        /// </summary>
        /// <param name="player">
        /// プロパティを保持するプレイヤーです。
        /// </param>
        /// <param name="key">
        /// 取得するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// 取得されたプロパティの値です。
        /// </param>
        /// <returns>
        /// プロパティを取得できた場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool TryGetRawPropertyIgnoreCase(
            OnlinePlayer player,
            string key,
            out object value)
        {
            value = null;

            if (player?.Properties == null ||
                string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (player.Properties.TryGetValue(
                    key,
                    out value))
            {
                return true;
            }

            foreach (
                KeyValuePair<string, object> property
                in player.Properties)
            {
                if (!string.Equals(
                        property.Key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = property.Value;

                return true;
            }

            return false;
        }

        /// <summary>
        /// キャラクター選択の準備待機状態をログへ出力します。
        /// </summary>
        /// <param name="shouldLog">
        /// ログを出力するかどうかを指定します。
        /// </param>
        /// <param name="message">
        /// 出力するメッセージです。
        /// </param>
        private static void LogReadyState(
            bool shouldLog,
            string message)
        {
            if (!shouldLog)
            {
                return;
            }

            Debug.Log(
                $"{nameof(OnlinePlayerUtility)}: " +
                $"キャラクター選択待機中。{message}");
        }
    }
}
