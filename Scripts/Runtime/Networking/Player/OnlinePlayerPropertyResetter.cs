using System.Collections.Generic;

namespace PranaTactics.Networking
{
    /// <summary>
    /// 画面遷移時にローカルプレイヤーの
    /// オンラインプロパティを初期状態へ戻します。
    /// </summary>
    public static class OnlinePlayerPropertyResetter
    {
        /// <summary>
        /// チームが未設定であることを示す識別子です。
        /// </summary>
        private const int k_UnassignedTeamId = -1;

        /// <summary>
        /// キャラクター選択画面へ遷移するときに、
        /// ローカルプレイヤーの準備状態と選択キャラクターをリセットします。
        /// </summary>
        /// <remarks>
        /// <see cref="NetworkPropertyKeys.IsReady"/>および
        /// <see cref="NetworkPropertyKeys.CanStartGame"/>は旧処理との互換性を
        /// 維持するため、<c>false</c>へ設定します。
        /// </remarks>
        public static void ResetForCharacterSelect()
        {
            Dictionary<string, object> properties =
                CreateCommonReadyResetProperties();

            properties[NetworkPropertyKeys.CharacterName] =
                string.Empty;

            ApplyLocalPlayerProperties(properties);
        }

        /// <summary>
        /// ロビー画面へ遷移するときに、
        /// ローカルプレイヤーの試合参加情報をリセットします。
        /// </summary>
        /// <remarks>
        /// 準備状態、パーティ情報、マッチング情報、チーム情報および
        /// 選択キャラクターを初期状態へ戻します。
        /// </remarks>
        public static void ResetForLobby()
        {
            Dictionary<string, object> properties =
                CreateCommonReadyResetProperties();

            properties[NetworkPropertyKeys.MatchingReady] =
                false;

            properties[NetworkPropertyKeys.PartyId] =
                string.Empty;

            properties[NetworkPropertyKeys.RoomId] =
                string.Empty;

            properties[NetworkPropertyKeys.MatchGroupId] =
                string.Empty;

            properties[NetworkPropertyKeys.TeamId] =
                k_UnassignedTeamId;

            properties[NetworkPropertyKeys.CharacterName] =
                string.Empty;

            ApplyLocalPlayerProperties(properties);
        }

        /// <summary>
        /// 画面遷移時に共通してリセットする
        /// プレイヤー準備状態を生成します。
        /// </summary>
        /// <returns>
        /// 準備状態を初期化するプレイヤープロパティの一覧です。
        /// </returns>
        private static Dictionary<string, object>
            CreateCommonReadyResetProperties()
        {
            return new Dictionary<string, object>
            {
                {
                    NetworkPropertyKeys.Ready,
                    false
                },
                {
                    NetworkPropertyKeys.IsReady,
                    false
                },
                {
                    NetworkPropertyKeys.CanStartGame,
                    false
                }
            };
        }

        /// <summary>
        /// 指定されたプロパティをローカルプレイヤーへ適用します。
        /// </summary>
        /// <param name="properties">
        /// ローカルプレイヤーへ設定するプロパティです。
        /// </param>
        private static void ApplyLocalPlayerProperties(
            Dictionary<string, object> properties)
        {
            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                return;
            }

            onlineService.SetLocalPlayerProperties(properties);
        }
    }
}
