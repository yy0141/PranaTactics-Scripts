using System;
using UnityEngine;

namespace PranaTactics.Networking
{
    /// <summary>
    /// 現在使用しているオンラインサービスへの
    /// 共通アクセスポイントを提供します。
    /// </summary>
    public static class NetworkServiceLocator
    {
        public static IOnlineService Online
        {
            get; private set;
        }

        public static bool IsRegistered
        {
            get
            {
                return Online != null;
            }
        }

        /// <summary>
        /// ゲームで使用するオンラインサービスを登録します。
        /// </summary>
        /// <param name="service">
        /// 登録するオンラインサービスです。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="service"/>が<c>null</c>の場合に発生します。
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// 別のオンラインサービスがすでに登録されている場合に発生します。
        /// </exception>
        public static void RegisterOnlineService(
            IOnlineService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(
                    nameof(service));
            }

            if (ReferenceEquals(
                    Online,
                    service))
            {
                return;
            }

            if (Online != null)
            {
                throw new InvalidOperationException(
                    $"{nameof(NetworkServiceLocator)}には、" +
                    $"すでに{nameof(IOnlineService)}が登録されています。" +
                    $"Current={Online.GetType().Name}, " +
                    $"Requested={service.GetType().Name}");
            }

            Online = service;

            Debug.Log(
                $"{nameof(NetworkServiceLocator)}: " +
                "オンラインサービスを登録しました。" +
                $"Service={service.GetType().Name}, " +
                $"Backend={service.Backend}");
        }

        /// <summary>
        /// 指定されたサービスが現在登録されている場合に限り、
        /// オンラインサービスの登録を解除します。
        /// </summary>
        /// <param name="service">
        /// 登録を解除するオンラインサービスです。
        /// </param>
        public static void UnregisterOnlineService(
            IOnlineService service)
        {
            if (service == null ||
                !ReferenceEquals(
                    Online,
                    service))
            {
                return;
            }

            Debug.Log(
                $"{nameof(NetworkServiceLocator)}: " +
                "オンラインサービスの登録を解除しました。" +
                $"Service={service.GetType().Name}");

            Online = null;
        }
    }
}
