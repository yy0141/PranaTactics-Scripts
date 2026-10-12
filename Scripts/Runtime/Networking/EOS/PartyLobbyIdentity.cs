using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PranaTactics.Networking.EOS
{
    /// <summary>
    /// 同じルームIDとパスワードをEOSの同一ロビーIDへ対応付けます。
    /// </summary>
    public static class PartyLobbyIdentity
    {
        /// <summary>
        /// 区切りの曖昧さを除いた資格情報から60文字以内の識別子を生成します。
        /// </summary>
        public static string Create(string roomId, string password)
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("ルームIDとパスワードが必要です。");
            }

            string input = roomId.Length.ToString(CultureInfo.InvariantCulture) + ":" + roomId + password;
            using SHA256 hash = SHA256.Create();
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            return "ptp_" + BitConverter.ToString(bytes, 0, 28).Replace("-", "").ToLowerInvariant();
        }
    }
}
