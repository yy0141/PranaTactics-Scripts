using System;
using System.Collections.Generic;
using System.Globalization;

namespace PranaTactics.Networking
{
    /// <summary>
    /// ホストが公開したチーム編成を各端末で同じ値として取得します。
    /// </summary>
    public static class MatchTeamAssignment
    {
        private const string k_Prefix = "matchTeam_";

        /// <summary>
        /// プレイヤーのチーム編成を公開するキーを作成します。
        /// </summary>
        public static string GetKey(int playerId)
        {
            return k_Prefix + playerId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// EOSの属性名の大文字化を許容し、確定済みチームを取得します。
        /// </summary>
        public static bool TryGet(OnlineRoom room, int playerId, out int teamId)
        {
            teamId = -1;
            if (room == null)
            {
                return false;
            }

            string key = GetKey(playerId);
            foreach (KeyValuePair<string, object> property in room.Properties)
            {
                if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(Convert.ToString(property.Value, CultureInfo.InvariantCulture),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 0)
                {
                    teamId = value;
                    return true;
                }
            }

            return false;
        }
    }
}
