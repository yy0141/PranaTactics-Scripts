using Photon.Pun;
using ExitGames.Client.Photon;

namespace PranaTactics.Networking.Photon
{
    public static class PhotonPlayerPropertyResetter
    {
        public static void ResetForCharacterSelect()
        {
            if (PhotonNetwork.LocalPlayer == null)
            {
                return;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new Hashtable
                {
                    { "characterName", "" },
                    { "canStartGame", false },
                    { "characterReady", false },
                    { "inGameSceneReady", false }
                });
        }

        public static void ResetAfterGame()
        {
            if (PhotonNetwork.LocalPlayer == null)
            {
                return;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new Hashtable
                {
                    { "matchingReady", false },
                    { "ready", false },
                    { "partyId", "" },
                    { "roomId", "" },
                    { "matchGroupId", "" },
                    { "teamId", -1 },
                    { "characterName", "" },
                    { "canStartGame", false },
                    { "characterReady", false },
                    { "inGameSceneReady", false }
                });
        }

        public static void ResetForLobby()
        {
            if (PhotonNetwork.LocalPlayer == null)
            {
                return;
            }

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new Hashtable
                {
                    { "characterName", "" },
                    { "canStartGame", false },
                    { "characterReady", false },
                    { "matchingReady", false },
                    { "ready", false },
                    { "inGameSceneReady", false },
                    { "teamId", -1 },
                    { "partyId", "" },
                    { "roomId", "" },
                    { "matchGroupId", "" }
                });
        }
    }
}
