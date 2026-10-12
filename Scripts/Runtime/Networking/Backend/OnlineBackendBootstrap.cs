using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Networking.EOS;
using PranaTactics.Networking.Photon;

namespace PranaTactics.Networking
{
    /// <summary>
    /// 選択されたオンラインバックエンドに対応する
    /// オンラインサービスを生成します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineBackendBootstrap : MonoBehaviour
    {
        /// <summary>
        /// 使用するオンラインバックエンドです。
        /// </summary>
        [SerializeField]
        private OnlineBackend _selectedBackend;

        /// <summary>
        /// Photon使用時に生成するオンラインサービスのPrefabです。
        /// </summary>
        [SerializeField]
        private PhotonOnlineService _photonOnlineServicePrefab;

        /// <summary>
        /// Epic Online Services使用時に生成する
        /// オンラインサービスのPrefabです。
        /// </summary>
        [SerializeField]
        private EosOnlineService _eosOnlineServicePrefab;

        /// <summary>
        /// 選択されたオンラインバックエンドに対応する
        /// オンラインサービスを生成します。
        /// </summary>
        private void Awake()
        {
            if (NetworkServiceLocator.Online != null)
            {
                Debug.Log(
                    $"{nameof(OnlineBackendBootstrap)}: " +
                    "オンラインサービスはすでに登録されているため、" +
                    "新しいサービスを生成しません。",
                    this);

                return;
            }

            switch (_selectedBackend)
            {
                case OnlineBackend.Photon:
                    InstantiatePhotonOnlineService();
                    break;

                case OnlineBackend.Eos:
                    InstantiateEosOnlineService();
                    break;

                default:
                    Debug.LogError(
                        $"{nameof(OnlineBackendBootstrap)}: " +
                        "対応していないオンラインバックエンドです。" +
                        $"Backend={_selectedBackend}",
                        this);
                    break;
            }
        }

        /// <summary>
        /// Photon用のオンラインサービスを生成します。
        /// </summary>
        private void InstantiatePhotonOnlineService()
        {
            if (_photonOnlineServicePrefab == null)
            {
                Debug.LogError(
                    $"{nameof(OnlineBackendBootstrap)}: " +
                    $"{nameof(PhotonOnlineService)}のPrefabが" +
                    "設定されていません。",
                    this);

                return;
            }

            Instantiate(_photonOnlineServicePrefab);
        }

        /// <summary>
        /// Epic Online Services用のオンラインサービスを生成します。
        /// </summary>
        private void InstantiateEosOnlineService()
        {
            if (_eosOnlineServicePrefab == null)
            {
                Debug.LogError(
                    $"{nameof(OnlineBackendBootstrap)}: " +
                    $"{nameof(EosOnlineService)}のPrefabが" +
                    "設定されていません。",
                    this);

                return;
            }

            Instantiate(_eosOnlineServicePrefab);
        }
    }
}
