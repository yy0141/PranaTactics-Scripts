using System;

using PranaTactics.Unit.Networking;

namespace PranaTactics.Networking
{
    /// <summary>
    /// ゲーム内で使用するネットワーク実装への
    /// 共通アクセスポイントを提供します。
    /// </summary>
    public static class NetworkManager
    {
        public static IUnitNetwork UnitNetwork
        {
            get; private set;
        }

        /// <summary>
        /// ゲーム内で使用するユニットネットワーク実装を登録します。
        /// </summary>
        /// <param name="unitNetwork">
        /// 登録するユニットネットワーク実装です。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="unitNetwork"/>が<c>null</c>の場合に発生します。
        /// </exception>
        public static void RegisterUnitNetwork(
            IUnitNetwork unitNetwork)
        {
            UnitNetwork = unitNetwork ??
                throw new ArgumentNullException(
                    nameof(unitNetwork));
        }

        /// <summary>
        /// 指定された実装が現在登録されている場合に限り、
        /// ユニットネットワークの登録を解除します。
        /// </summary>
        /// <param name="unitNetwork">
        /// 登録を解除するユニットネットワーク実装です。
        /// </param>
        public static void UnregisterUnitNetwork(
            IUnitNetwork unitNetwork)
        {
            if (!ReferenceEquals(
                    UnitNetwork,
                    unitNetwork))
            {
                return;
            }

            UnitNetwork = null;
        }
    }
}
