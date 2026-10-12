using System;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PranaTactics.Networking
{
    /// <summary>
    /// 使用中のオンラインサービスに応じた方法で
    /// シーンの読み込みを実行します。
    /// </summary>
    /// <remarks>
    /// Photon使用時はホストのみが
    /// <see cref="PhotonNetwork.LoadLevel(string)"/>を実行し、
    /// クライアントはPhotonのシーン自動同期を待機します。
    ///
    /// Photon以外のオンラインサービスおよびオフライン環境では、
    /// 各クライアントが
    /// <see cref="SceneManager.LoadScene(string)"/>を実行します。
    /// </remarks>
    public static class NetworkSceneLoader
    {
        /// <summary>
        /// Photonのオンラインサービスを判別するために使用する文字列です。
        /// </summary>
        private const string k_PhotonServiceTypeName = "Photon";

        /// <summary>
        /// 使用中のオンラインサービスに応じた方法で、
        /// 指定されたシーンを読み込みます。
        /// </summary>
        /// <param name="sceneName">
        /// 読み込むシーンの名前です。
        /// </param>
        public static void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError(
                    $"{nameof(NetworkSceneLoader)}: " +
                    "読み込むシーン名が指定されていません。");

                return;
            }

            if (IsSceneActive(sceneName))
            {
                Debug.Log(
                    $"{nameof(NetworkSceneLoader)}: " +
                    "指定されたシーンはすでに読み込まれています。" +
                    $"Scene={sceneName}");

                return;
            }

            IOnlineService onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom)
            {
                LoadOfflineScene(sceneName);

                return;
            }

            if (IsPhotonOnlineService(onlineService))
            {
                LoadPhotonScene(
                    onlineService,
                    sceneName);

                return;
            }

            LoadNonPhotonOnlineScene(
                onlineService,
                sceneName);
        }

        /// <summary>
        /// 指定されたシーンが現在アクティブかどうかを確認します。
        /// </summary>
        /// <param name="sceneName">
        /// 確認するシーンの名前です。
        /// </param>
        /// <returns>
        /// 指定されたシーンがアクティブな場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool IsSceneActive(string sceneName)
        {
            Scene activeScene =
                SceneManager.GetActiveScene();

            return string.Equals(
                activeScene.name,
                sceneName,
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Photon環境でシーンを読み込みます。
        /// </summary>
        /// <param name="onlineService">
        /// 現在使用しているオンラインサービスです。
        /// </param>
        /// <param name="sceneName">
        /// 読み込むシーンの名前です。
        /// </param>
        private static void LoadPhotonScene(
            IOnlineService onlineService,
            string sceneName)
        {
            if (!onlineService.IsHost)
            {
                Debug.Log(
                    $"{nameof(NetworkSceneLoader)}: " +
                    "Photonクライアントのため、" +
                    "ホストからのシーン同期を待機します。" +
                    $"Scene={sceneName}");

                return;
            }

            Debug.Log(
                $"{nameof(NetworkSceneLoader)}: " +
                "Photonホストとしてシーンを読み込みます。" +
                $"Scene={sceneName}");

            PhotonNetwork.LoadLevel(sceneName);
        }

        /// <summary>
        /// Photon以外のオンラインサービスを使用している場合に、
        /// ローカルのシーン読み込みを実行します。
        /// </summary>
        /// <param name="onlineService">
        /// 現在使用しているオンラインサービスです。
        /// </param>
        /// <param name="sceneName">
        /// 読み込むシーンの名前です。
        /// </param>
        private static void LoadNonPhotonOnlineScene(
            IOnlineService onlineService,
            string sceneName)
        {
            Debug.Log(
                $"{nameof(NetworkSceneLoader)}: " +
                "Photon以外のオンライン環境として、" +
                "ローカルでシーンを読み込みます。" +
                $"Scene={sceneName}, " +
                $"IsHost={onlineService.IsHost}, " +
                $"Service={onlineService.GetType().Name}");

            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// オフライン環境としてローカルのシーン読み込みを実行します。
        /// </summary>
        /// <param name="sceneName">
        /// 読み込むシーンの名前です。
        /// </param>
        private static void LoadOfflineScene(string sceneName)
        {
            Debug.Log(
                $"{nameof(NetworkSceneLoader)}: " +
                "オフライン環境としてシーンを読み込みます。" +
                $"Scene={sceneName}");

            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// 指定されたオンラインサービスが
        /// Photon実装かどうかを確認します。
        /// </summary>
        /// <param name="onlineService">
        /// 判定するオンラインサービスです。
        /// </param>
        /// <returns>
        /// Photon実装の場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool IsPhotonOnlineService(
            IOnlineService onlineService)
        {
            if (onlineService == null)
            {
                return false;
            }

            Type serviceType =
                onlineService.GetType();

            string typeName =
                serviceType.FullName ??
                serviceType.Name;

            return typeName.IndexOf(
                    k_PhotonServiceTypeName,
                    StringComparison.OrdinalIgnoreCase) >=
                0;
        }
    }
}
