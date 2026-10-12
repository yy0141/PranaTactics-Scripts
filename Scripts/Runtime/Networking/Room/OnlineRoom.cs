using System.Collections.Generic;

namespace PranaTactics.Networking
{
    /// <summary>
    /// オンラインルームの基本情報と
    /// カスタムルームプロパティを保持します。
    /// </summary>
    public sealed class OnlineRoom
    {
        public string Name
        {
            get; set;
        }

        public int PlayerCount
        {
            get; set;
        }

        public int MaxPlayers
        {
            get; set;
        }

        public bool IsOpen
        {
            get; set;
        }

        public bool IsVisible
        {
            get; set;
        }

        public Dictionary<string, object> Properties { get; } = new();

        /// <summary>
        /// 指定されたキーに対応するプロパティを取得します。
        /// </summary>
        /// <typeparam name="T">
        /// 取得するプロパティ値の型です。
        /// </typeparam>
        /// <param name="key">
        /// 取得するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// プロパティが存在し、型が一致した場合に取得される値です。
        /// </param>
        /// <returns>
        /// 指定されたキーが存在し、値を
        /// <typeparamref name="T"/>として取得できた場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        public bool TryGetProperty<T>(
            string key,
            out T value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = default;

                return false;
            }

            if (Properties.TryGetValue(
                    key,
                    out object rawValue) &&
                rawValue is T typedValue)
            {
                value = typedValue;

                return true;
            }

            value = default;

            return false;
        }

        /// <summary>
        /// 指定されたキーに対応するプロパティを取得します。
        /// プロパティが存在しない場合や型が一致しない場合は、
        /// 指定された既定値を返します。
        /// </summary>
        /// <typeparam name="T">
        /// 取得するプロパティ値の型です。
        /// </typeparam>
        /// <param name="key">
        /// 取得するプロパティのキーです。
        /// </param>
        /// <param name="defaultValue">
        /// プロパティを取得できない場合に返す値です。
        /// </param>
        /// <returns>
        /// 取得されたプロパティ値、または
        /// <paramref name="defaultValue"/>です。
        /// </returns>
        public T GetPropertyOrDefault<T>(
            string key,
            T defaultValue = default)
        {
            if (TryGetProperty(
                    key,
                    out T value))
            {
                return value;
            }

            return defaultValue;
        }
    }
}
