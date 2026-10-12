using System;
using UnityEngine;

using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Platform;

using PlayEveryWare.EpicOnlineServices;

namespace PranaTactics.Networking.EOS
{
    /// <summary>
    /// Epic Online ServicesのConnect Interfaceを使用して、
    /// Device ID認証とProduct User IDの取得を管理します。
    /// </summary>
    public sealed class EosAuthService
    {
        /// <summary>
        /// 表示名を取得できない場合に使用する接頭辞です。
        /// </summary>
        private const string k_DefaultDisplayNamePrefix = "Player";

        /// <summary>
        /// ランダムな表示名へ付加する数値の最小値です。
        /// </summary>
        private const int k_MinimumDisplayNameNumber = 1000;

        /// <summary>
        /// ランダムな表示名へ付加する数値の最大値です。
        /// </summary>
        private const int k_MaximumDisplayNameNumber = 10000;

        /// <summary>
        /// EOSのConnect Interfaceです。
        /// </summary>
        private ConnectInterface _connectInterface;

        /// <summary>
        /// ログイン完了時に呼び出すコールバックです。
        /// </summary>
        /// <remarks>
        /// ログイン処理中に複数の要求を受けた場合に備えて、
        /// コールバックを複数保持します。
        /// </remarks>
        private Action<bool, string> _loginCompletedCallbacks;

        /// <summary>
        /// EOS認証サービスの初期化が完了しているかどうかを示します。
        /// </summary>
        private bool _isInitialized;

        /// <summary>
        /// EOSへのログイン処理を実行中かどうかを示します。
        /// </summary>
        private bool _isLoginInProgress;

        /// <summary>
        /// 現在のログイン処理中にConnect Userの
        /// 作成を要求済みかどうかを示します。
        /// </summary>
        private bool _hasRequestedConnectUserCreation;

        public bool IsLoggedIn
        {
            get; private set;
        }

        public bool IsInitialized
        {
            get
            {
                return _isInitialized;
            }
        }

        public bool IsLoginInProgress
        {
            get
            {
                return _isLoginInProgress;
            }
        }

        public ProductUserId LocalProductUserId
        {
            get; private set;
        }

        public string LocalProductUserIdString
        {
            get
            {
                if (LocalProductUserId == null)
                {
                    return string.Empty;
                }

                return LocalProductUserId.ToString();
            }
        }

        /// <summary>
        /// EOS Platform InterfaceとConnect Interfaceを取得し、
        /// 認証サービスを初期化します。
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized &&
                _connectInterface != null)
            {
                return;
            }

            _isInitialized = false;
            _connectInterface = null;

            EOSManager.EOSSingleton eosManager =
                EOSManager.Instance;

            PlatformInterface platformInterface =
                eosManager.GetEOSPlatformInterface();

            if (eosManager == null)
            {
                Debug.LogError(
                    $"{nameof(EosAuthService)}: " +
                    $"{nameof(EOSManager)}がシーンに存在しません。");

                return;
            }

            _connectInterface =
                platformInterface.GetConnectInterface();

            if (platformInterface == null)
            {
                Debug.LogError(
                    $"{nameof(EosAuthService)}: " +
                    "EOS Platform Interfaceを取得できません。" +
                    $"{nameof(EOSManager)}の初期化状態を確認してください。");

                return;
            }

            _connectInterface =
                eosManager.GetEOSConnectInterface();

            if (_connectInterface == null)
            {
                Debug.LogError(
                    $"{nameof(EosAuthService)}: " +
                    "EOS Connect Interfaceを取得できません。");

                return;
            }

            _isInitialized = true;

            Debug.Log(
                $"{nameof(EosAuthService)}: " +
                "EOS認証サービスを初期化しました。");
        }

        /// <summary>
        /// Device IDを使用したEOS Connectへのログインを開始します。
        /// </summary>
        /// <param name="onCompleted">
        /// ログイン処理が完了したときに呼び出されるコールバックです。
        /// 第1引数には成功状態、第2引数にはエラーメッセージが渡されます。
        /// </param>
        public void LoginWithDeviceId(
            Action<bool, string> onCompleted)
        {
            if (IsLoggedIn &&
                LocalProductUserId != null)
            {
                onCompleted?.Invoke(
                    true,
                    null);

                return;
            }

            if (onCompleted != null)
            {
                _loginCompletedCallbacks +=
                    onCompleted;
            }

            if (_isLoginInProgress)
            {
                return;
            }

            if (!_isInitialized ||
                _connectInterface == null)
            {
                Initialize();
            }

            if (!_isInitialized ||
                _connectInterface == null)
            {
                CompleteLogin(
                    false,
                    "EOS認証サービスを初期化できませんでした。");

                return;
            }

            _isLoginInProgress = true;
            _hasRequestedConnectUserCreation = false;

            CreateDeviceId();
        }

        /// <summary>
        /// ローカル端末用のEOS Device IDを作成します。
        /// </summary>
        private void CreateDeviceId()
        {
            string deviceModel =
                GetDeviceModel();

            CreateDeviceIdOptions options =
                new CreateDeviceIdOptions
                {
                    DeviceModel = deviceModel
                };

            Debug.Log(
                $"{nameof(EosAuthService)}: " +
                "EOS Device IDの作成を開始します。");

            try
            {
                _connectInterface.CreateDeviceId(
                    ref options,
                    null,
                    HandleCreateDeviceIdCompleted);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(EosAuthService)}: " +
                    "EOS Device IDの作成開始中にエラーが発生しました。");

                Debug.LogException(exception);

                CompleteLogin(
                    false,
                    $"Device IDの作成を開始できませんでした。" +
                    $"Error={exception.Message}");
            }
        }

        /// <summary>
        /// EOS Device IDの作成結果を処理します。
        /// </summary>
        /// <param name="callbackInfo">
        /// Device ID作成処理の結果です。
        /// </param>
        private void HandleCreateDeviceIdCompleted(
            ref CreateDeviceIdCallbackInfo callbackInfo)
        {
            Debug.Log(
                $"{nameof(EosAuthService)}: " +
                "EOS Device IDの作成処理が完了しました。" +
                $"Result={callbackInfo.ResultCode}");

            bool canContinueLogin =
                callbackInfo.ResultCode == Result.Success ||
                callbackInfo.ResultCode ==
                Result.DuplicateNotAllowed;

            if (!canContinueLogin)
            {
                CompleteLogin(
                    false,
                    "Device IDの作成に失敗しました。" +
                    $"Result={callbackInfo.ResultCode}");

                return;
            }

            StartConnectLogin();
        }

        /// <summary>
        /// Device ID認証情報を使用して
        /// EOS Connectへのログインを開始します。
        /// </summary>
        private void StartConnectLogin()
        {
            EOSManager.EOSSingleton eosManager =
                EOSManager.Instance;

            if (eosManager == null)
            {
                CompleteLogin(
                    false,
                    $"{nameof(EOSManager)}が存在しません。");

                return;
            }

            string displayName =
                GetDisplayName();

            Debug.Log(
                $"{nameof(EosAuthService)}: " +
                "Device IDを使用したEOS Connectへの" +
                "ログインを開始します。");

            try
            {
                eosManager.StartConnectLoginWithOptions(
                    ExternalCredentialType.DeviceidAccessToken,
                    null,
                    displayName,
                    HandleConnectLoginCompleted);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(EosAuthService)}: " +
                    "EOS Connectへのログイン開始中に" +
                    "エラーが発生しました。");

                Debug.LogException(exception);

                CompleteLogin(
                    false,
                    "EOS Connectへのログインを開始できませんでした。" +
                    $"Error={exception.Message}");
            }
        }

        /// <summary>
        /// EOS Connectへのログイン結果を処理します。
        /// </summary>
        /// <param name="callbackInfo">
        /// EOS Connectログイン処理の結果です。
        /// </param>
        private void HandleConnectLoginCompleted(
            LoginCallbackInfo callbackInfo)
        {
            Debug.Log(
                $"{nameof(EosAuthService)}: " +
                "EOS Connectへのログイン処理が完了しました。" +
                $"Result={callbackInfo.ResultCode}");

            if (callbackInfo.ResultCode ==
                Result.Success)
            {
                if (callbackInfo.LocalUserId == null)
                {
                    CompleteLogin(
                        false,
                        "EOS Connectへのログインは成功しましたが、" +
                        "Product User IDを取得できませんでした。");

                    return;
                }

                LocalProductUserId =
                    callbackInfo.LocalUserId;

                IsLoggedIn = true;

                Debug.Log(
                    $"{nameof(EosAuthService)}: " +
                    "EOS Connectへのログインが完了しました。" +
                    $"ProductUserId={LocalProductUserIdString}");

                CompleteLogin(
                    true,
                    null);

                return;
            }

            if (callbackInfo.ResultCode ==
                Result.InvalidUser)
            {
                if (_hasRequestedConnectUserCreation)
                {
                    CompleteLogin(
                        false,
                        "Connect User作成後もEOS Connectへの" +
                        "ログインでInvalidUserが返されました。");

                    return;
                }

                _hasRequestedConnectUserCreation = true;

                Debug.Log(
                    $"{nameof(EosAuthService)}: " +
                    "EOS Connect Userが存在しないため、" +
                    "新しいユーザーを作成します。");

                CreateConnectUser(
                    callbackInfo.ContinuanceToken);

                return;
            }

            CompleteLogin(
                false,
                "EOS Connectへのログインに失敗しました。" +
                $"Result={callbackInfo.ResultCode}");
        }

        /// <summary>
        /// Continuance Tokenを使用して
        /// 新しいEOS Connect Userを作成します。
        /// </summary>
        /// <param name="continuanceToken">
        /// InvalidUser時にEOSから返されたContinuance Tokenです。
        /// </param>
        private void CreateConnectUser(
            ContinuanceToken continuanceToken)
        {
            if (continuanceToken == null)
            {
                CompleteLogin(
                    false,
                    "Connect Userを作成するための" +
                    "Continuance Tokenを取得できませんでした。");

                return;
            }

            if (_connectInterface == null)
            {
                CompleteLogin(
                    false,
                    "EOS Connect Interfaceが初期化されていません。");

                return;
            }

            CreateUserOptions options =
                new CreateUserOptions
                {
                    ContinuanceToken = continuanceToken
                };

            Debug.Log(
                $"{nameof(EosAuthService)}: " +
                "EOS Connect Userの作成を開始します。");

            try
            {
                _connectInterface.CreateUser(
                    ref options,
                    null,
                    HandleCreateConnectUserCompleted);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{nameof(EosAuthService)}: " +
                    "EOS Connect Userの作成開始中に" +
                    "エラーが発生しました。");

                Debug.LogException(exception);

                CompleteLogin(
                    false,
                    "EOS Connect Userの作成を開始できませんでした。" +
                    $"Error={exception.Message}");
            }
        }

        /// <summary>
        /// EOS Connect Userの作成結果を処理します。
        /// </summary>
        /// <param name="callbackInfo">
        /// Connect User作成処理の結果です。
        /// </param>
        private void HandleCreateConnectUserCompleted(
            ref CreateUserCallbackInfo callbackInfo)
        {
            Debug.Log(
                $"{nameof(EosAuthService)}: " +
                "EOS Connect Userの作成処理が完了しました。" +
                $"Result={callbackInfo.ResultCode}");

            if (callbackInfo.ResultCode !=
                Result.Success)
            {
                CompleteLogin(
                    false,
                    "EOS Connect Userの作成に失敗しました。" +
                    $"Result={callbackInfo.ResultCode}");

                return;
            }

            if (callbackInfo.LocalUserId != null)
            {
                LocalProductUserId =
                    callbackInfo.LocalUserId;

                IsLoggedIn = true;

                Debug.Log(
                    $"{nameof(EosAuthService)}: " +
                    "EOS Connect Userの作成とログインが完了しました。" +
                    $"ProductUserId={LocalProductUserIdString}");

                CompleteLogin(
                    true,
                    null);

                return;
            }

            StartConnectLogin();
        }

        /// <summary>
        /// ログイン処理を完了し、
        /// 登録されているコールバックへ結果を通知します。
        /// </summary>
        /// <param name="isSuccessful">
        /// ログインに成功した場合は<c>true</c>です。
        /// </param>
        /// <param name="errorMessage">
        /// ログインに失敗した場合のエラーメッセージです。
        /// </param>
        private void CompleteLogin(
            bool isSuccessful,
            string errorMessage)
        {
            _isLoginInProgress = false;
            _hasRequestedConnectUserCreation = false;

            if (!isSuccessful)
            {
                IsLoggedIn = false;
                LocalProductUserId = null;
            }

            Action<bool, string> callbacks =
                _loginCompletedCallbacks;

            _loginCompletedCallbacks = null;

            callbacks?.Invoke(
                isSuccessful,
                errorMessage);
        }

        /// <summary>
        /// EOS Device ID作成時に使用する端末モデル名を取得します。
        /// </summary>
        /// <returns>
        /// 端末モデル名または端末名です。
        /// </returns>
        private static string GetDeviceModel()
        {
            if (!string.IsNullOrWhiteSpace(
                    SystemInfo.deviceModel))
            {
                return SystemInfo.deviceModel;
            }

            if (!string.IsNullOrWhiteSpace(
                    SystemInfo.deviceName))
            {
                return SystemInfo.deviceName;
            }

            return Application.platform.ToString();
        }

        /// <summary>
        /// EOS Connectログイン時に使用する表示名を取得します。
        /// </summary>
        /// <returns>
        /// OSのユーザー名、または自動生成した表示名です。
        /// </returns>
        private static string GetDisplayName()
        {
            string displayName =
                Environment.UserName;

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }

            int randomNumber =
                UnityEngine.Random.Range(
                    k_MinimumDisplayNameNumber,
                    k_MaximumDisplayNameNumber);

            return $"{k_DefaultDisplayNamePrefix}_{randomNumber}";
        }
    }
}
