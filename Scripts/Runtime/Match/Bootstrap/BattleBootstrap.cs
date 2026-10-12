using UnityEngine;

using PranaTactics.GameFlow;
using PranaTactics.Networking;
using PranaTactics.Unit.Networking.Photon;

namespace PranaTactics.Match
{
    /// <summary>
    /// オンライン対戦時にPhoton用UnitNetworkを登録します。
    /// ソロプレイ時はローカル生成を使用するため、
    /// PhotonUnitNetworkを登録しません。
    /// </summary>
    public sealed class BattleBootstrap : MonoBehaviour
    {
        /// <summary>
        /// オンライン対戦で使用するPhoton用UnitNetworkです。
        /// </summary>
        [SerializeField]
        private PhotonUnitNetwork _photonUnitNetwork;

        /// <summary>
        /// プレイモードに応じてUnitNetworkを初期化します。
        /// </summary>
        private void Awake()
        {
            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager != null &&
                gameFlowManager.IsSoloPlay)
            {
                Debug.Log(
                    $"{nameof(BattleBootstrap)}: " +
                    "ソロプレイのため、" +
                    $"{nameof(PhotonUnitNetwork)}を登録しません。",
                    this);

                return;
            }

            if (_photonUnitNetwork == null)
            {
                TryGetComponent(
                    out _photonUnitNetwork);
            }

            if (_photonUnitNetwork == null)
            {
                Debug.LogError(
                    $"{nameof(BattleBootstrap)}に" +
                    $"{nameof(PhotonUnitNetwork)}が" +
                    "設定されていません。",
                    this);

                return;
            }

            NetworkManager.RegisterUnitNetwork(
                _photonUnitNetwork);
        }
    }
}
