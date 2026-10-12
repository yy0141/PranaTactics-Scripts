using UnityEngine;
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using PlayEveryWare.EpicOnlineServices;

public class EOSReloginTest : MonoBehaviour
{
    private void Start()
    {
        LoginWithAccountPortal();
    }

    private void LoginWithAccountPortal()
    {
        Debug.Log("[EOS] Account Portal login start");

        EOSManager.Instance.StartLoginWithLoginTypeAndToken(
            LoginCredentialType.AccountPortal,
            null,
            null,
            OnAuthLoginCompleted);
    }

    private void OnAuthLoginCompleted(LoginCallbackInfo info)
    {
        Debug.Log(
            $"[EOS] Auth Login Result = {info.ResultCode}");

        if (info.ResultCode != Result.Success)
        {
            Debug.LogError(
                $"[EOS] Auth Login failed: {info.ResultCode}");

            return;
        }

        Debug.Log(
            $"[EOS] Auth Login succeeded. LocalUserId={info.LocalUserId}");

        StartConnectLogin(info);
    }

    private void StartConnectLogin(LoginCallbackInfo authInfo)
    {
        Debug.Log("[EOS] Connect Login start");

        EOSManager.Instance.StartConnectLoginWithEpicAccount(
            authInfo.LocalUserId,
            connectInfo =>
            {
                Debug.Log(
                    $"[EOS] Connect Login Result = " +
                    $"{connectInfo.ResultCode}");

                if (connectInfo.ResultCode == Result.Success)
                {
                    Debug.Log(
                        "[EOS] Auth + Connect login completed.");

                    return;
                }

                if (connectInfo.ResultCode == Result.InvalidUser)
                {
                    Debug.Log(
                        "[EOS] Connect user does not exist. " +
                        "Creating user...");

                    EOSManager.Instance
                        .CreateConnectUserWithContinuanceToken(
                            connectInfo.ContinuanceToken,
                            createInfo =>
                            {
                                Debug.Log(
                                    $"[EOS] CreateUser Result = " +
                                    $"{createInfo.ResultCode}");

                                if (createInfo.ResultCode != Result.Success)
                                {
                                    return;
                                }

                                RetryConnectLogin(
                                    authInfo.LocalUserId);
                            });
                }
            });
    }

    private void RetryConnectLogin(
        EpicAccountId epicAccountId)
    {
        EOSManager.Instance.StartConnectLoginWithEpicAccount(
            epicAccountId,
            retryInfo =>
            {
                Debug.Log(
                    $"[EOS] Connect Login Retry = " +
                    $"{retryInfo.ResultCode}");
            });
    }
}
