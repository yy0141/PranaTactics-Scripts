using System;
using System.Collections;
using UnityEngine;

using PranaTactics.GameFlow;
using PranaTactics.Networking;
using PranaTactics.Unit;
using PranaTactics.UI.Navigation;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// リザルト画面からロビーへ戻る前に、
    /// ゲーム状態とオンラインルームを整理します。
    /// </summary>
    /// <remarks>
    /// ソロプレイの場合は現在のゲームルームから退出し、
    /// 通常のロビーへ戻ります。
    ///
    /// パーティプレイの場合は、ゲーム開始前に所属していた
    /// パーティルームへの再参加を試みます。
    ///
    /// パーティルーム情報を取得できない場合や、
    /// 再参加に失敗した場合は通常ロビーへ戻ります。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ReturnAfterResultProcess :
        MonoBehaviour,
        IBeforeSceneTransitionProcess
    {
        /// <summary>
        /// パーティルームの復帰情報を待機する既定時間です。
        /// </summary>
        private const float k_DefaultRoomInfoTimeout = 5.0f;

        /// <summary>
        /// ルーム退出を待機する既定時間です。
        /// </summary>
        private const float k_DefaultLeaveRoomTimeout = 10.0f;

        /// <summary>
        /// ロビーへの復帰を待機する既定時間です。
        /// </summary>
        private const float k_DefaultLobbyReturnTimeout = 10.0f;

        /// <summary>
        /// パーティルームへの参加を待機する既定時間です。
        /// </summary>
        private const float k_DefaultJoinRoomTimeout = 15.0f;

        /// <summary>
        /// 復帰先のパーティルーム情報を待機する最大時間です。
        /// </summary>
        [Header("Timeout")]
        [Min(0.1f)]
        [SerializeField]
        private float _roomInfoTimeout =
            k_DefaultRoomInfoTimeout;

        /// <summary>
        /// 現在のゲームルームからの退出を待機する最大時間です。
        /// </summary>
        [Min(0.1f)]
        [SerializeField]
        private float _leaveRoomTimeout =
            k_DefaultLeaveRoomTimeout;

        /// <summary>
        /// オンラインロビーへの復帰を待機する最大時間です。
        /// </summary>
        [Min(0.1f)]
        [SerializeField]
        private float _lobbyReturnTimeout =
            k_DefaultLobbyReturnTimeout;

        /// <summary>
        /// パーティルームへの再参加を待機する最大時間です。
        /// </summary>
        [Min(0.1f)]
        [SerializeField]
        private float _joinRoomTimeout =
            k_DefaultJoinRoomTimeout;

        /// <summary>
        /// 復帰処理の詳細をログへ出力するかどうかを示します。
        /// </summary>
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLogging;

        /// <summary>
        /// 遷移前処理の完了時に呼び出すコールバックです。
        /// </summary>
        private Action _onCompleted;

        /// <summary>
        /// 現在実行中の復帰処理です。
        /// </summary>
        private Coroutine _returnCoroutine;

        /// <summary>
        /// 現在復帰処理を実行中かどうかを示します。
        /// </summary>
        private bool _isExecuting;

        /// <summary>
        /// コンポーネントの無効化により処理を中断したかどうかです。
        /// </summary>
        private bool _isCancelled;

        /// <summary>
        /// リザルト画面から戻るための事前処理を実行します。
        /// </summary>
        /// <param name="onCompleted">
        /// 復帰処理が完了したときに呼び出すコールバックです。
        /// </param>
        public void Execute(
            Action onCompleted)
        {
            if (_isExecuting)
            {
                Debug.LogWarning(
                    $"{nameof(ReturnAfterResultProcess)}: " +
                    "リザルト画面からの復帰処理は既に実行中です。",
                    this);

                return;
            }

            _isCancelled =
                false;

            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager == null)
            {
                Debug.LogError(
                    $"{nameof(ReturnAfterResultProcess)}: " +
                    $"{nameof(GameFlowManager)}が存在しないため、" +
                    "ゲーム状態を初期化できません。",
                    this);

                onCompleted?.Invoke();

                return;
            }

            _isExecuting =
                true;

            _onCompleted =
                onCompleted;

            bool wasPartyPlay =
                gameFlowManager.IsPartyPlay;

            int returnTeamId =
                wasPartyPlay
                    ? GetCurrentTeamId()
                    : -1;

            int partySize =
                GetCurrentPartySize();

            PrepareGameStateForLobby(
                gameFlowManager);

            if (gameFlowManager.HasSelectedSoloStage)
            {
                CompleteProcess();
                return;
            }

            _returnCoroutine =
                StartCoroutine(
                    RunReturnProcess(
                        wasPartyPlay,
                        returnTeamId,
                        partySize));
        }

        /// <summary>
        /// ソロまたはパーティの状態に応じた復帰処理を実行します。
        /// </summary>
        /// <param name="wasPartyPlay">
        /// パーティプレイからの復帰である場合は<c>true</c>です。
        /// </param>
        /// <param name="returnTeamId">
        /// 復帰先情報の取得に使用するチームIDです。
        /// </param>
        /// <param name="partySize">
        /// 復帰するパーティルームの最大人数です。
        /// </param>
        private IEnumerator RunReturnProcess(
            bool wasPartyPlay,
            int returnTeamId,
            int partySize)
        {
            if (wasPartyPlay)
            {
                yield return ReturnToPartyRoutine(
                    returnTeamId,
                    partySize);
            }
            else
            {
                yield return ReturnToLobbyRoutine(
                    true);
            }

            CompleteProcess();
        }

        /// <summary>
        /// ゲーム終了後の状態をロビー用に初期化します。
        /// </summary>
        /// <param name="gameFlowManager">
        /// 状態を初期化するゲームフロー管理です。
        /// </param>
        private static void PrepareGameStateForLobby(
            GameFlowManager gameFlowManager)
        {
            gameFlowManager.SetGameplayState(
                false);

            gameFlowManager.SetTimeEnded(
                false);

            OnlinePlayerPropertyResetter.ResetForLobby();

            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            matchmakingManager?.ResetAfterGame();
        }

        /// <summary>
        /// 通常のオンラインロビーへ戻ります。
        /// </summary>
        /// <param name="clearPartyPlayState">
        /// パーティプレイ状態を消去する場合は<c>true</c>です。
        /// </param>
        private IEnumerator ReturnToLobbyRoutine(
            bool clearPartyPlayState)
        {
            if (clearPartyPlayState)
            {
                ClearPartyPlayState();
            }

            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                yield break;
            }

            if (onlineService.IsInRoom)
            {
                onlineService.LeaveRoom();
            }

            yield return WaitUntilRoomLeft();

            onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                yield break;
            }

            if (onlineService.IsInRoom)
            {
                Debug.LogWarning(
                    $"{nameof(ReturnAfterResultProcess)}: " +
                    "制限時間内に現在のルームから退出できませんでした。",
                    this);

                yield break;
            }

            yield return WaitUntilLobbyReturned();

            onlineService =
                NetworkServiceLocator.Online;

            if (onlineService != null &&
                onlineService.IsConnected &&
                !onlineService.IsInLobby)
            {
                Debug.LogWarning(
                    $"{nameof(ReturnAfterResultProcess)}: " +
                    "制限時間内にオンラインロビーへ戻れませんでした。",
                    this);
            }
        }

        /// <summary>
        /// ゲーム開始前に所属していたパーティルームへ戻ります。
        /// </summary>
        /// <param name="teamId">
        /// 復帰先情報の取得に使用するチームIDです。
        /// </param>
        /// <param name="partySize">
        /// パーティルームの最大人数です。
        /// </param>
        private IEnumerator ReturnToPartyRoutine(
            int teamId,
            int partySize)
        {
            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                yield return FallbackToLobby(
                    "オンラインサービスを取得できませんでした。");

                yield break;
            }

            if (teamId < 0)
            {
                yield return FallbackToLobby(
                    "ローカルプレイヤーのチームIDを取得できませんでした。");

                yield break;
            }

            yield return WaitForReturnRoomInfo(
                teamId);

            if (!TryGetReturnRoomInfo(
                    teamId,
                    out string roomName,
                    out string roomId,
                    out string password,
                    out string partyId))
            {
                yield return FallbackToLobby(
                    "パーティルームへの復帰情報を取得できませんでした。");

                yield break;
            }

            onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null)
            {
                yield return FallbackToLobby(
                    "ルーム退出前にオンラインサービスが失われました。");

                yield break;
            }

            if (onlineService.IsInRoom)
            {
                onlineService.LeaveRoom();
            }

            yield return WaitUntilRoomLeft();

            onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                onlineService.IsInRoom)
            {
                yield return FallbackToLobby(
                    "現在のゲームルームから退出できませんでした。");

                yield break;
            }

            yield return WaitUntilLobbyReturned();

            onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsConnected ||
                !onlineService.IsInLobby)
            {
                yield return FallbackToLobby(
                    "オンラインロビーへ戻れませんでした。");

                yield break;
            }

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(ReturnAfterResultProcess)}: " +
                    "パーティルームへの復帰を開始します。" +
                    $" RoomName={roomName}," +
                    $" RoomId={roomId}," +
                    $" PartyId={partyId}," +
                    $" PartySize={partySize}",
                    this);
            }

            onlineService.JoinOrCreatePartyRoom(
                roomName,
                roomId,
                password,
                partyId,
                Mathf.Max(
                    1,
                    partySize));

            yield return WaitUntilRoomJoined();

            onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                !onlineService.IsInRoom)
            {
                yield return FallbackToLobby(
                    "パーティルームへの再参加がタイムアウトしました。");

                yield break;
            }

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(ReturnAfterResultProcess)}: " +
                    "パーティルームへの復帰が完了しました。" +
                    $" RoomName={roomName}",
                    this);
            }
        }

        /// <summary>
        /// パーティルームへの復帰に失敗した場合に、
        /// パーティ状態を解除して通常ロビーへ戻ります。
        /// </summary>
        /// <param name="reason">
        /// 通常ロビーへ戻る理由です。
        /// </param>
        private IEnumerator FallbackToLobby(
            string reason)
        {
            Debug.LogWarning(
                $"{nameof(ReturnAfterResultProcess)}: " +
                $"{reason} 通常ロビーへ戻ります。",
                this);

            yield return ReturnToLobbyRoutine(
                true);
        }

        /// <summary>
        /// 現在のゲームルームから退出するまで待機します。
        /// </summary>
        private IEnumerator WaitUntilRoomLeft()
        {
            float elapsedTime =
                0.0f;

            while (elapsedTime <
                _leaveRoomTimeout)
            {
                var onlineService =
                    NetworkServiceLocator.Online;

                if (onlineService == null ||
                    !onlineService.IsInRoom)
                {
                    yield break;
                }

                elapsedTime +=
                    Time.unscaledDeltaTime;

                yield return null;
            }
        }

        /// <summary>
        /// オンラインロビーへ戻るまで待機します。
        /// </summary>
        private IEnumerator WaitUntilLobbyReturned()
        {
            float elapsedTime =
                0.0f;

            while (elapsedTime <
                _lobbyReturnTimeout)
            {
                var onlineService =
                    NetworkServiceLocator.Online;

                if (onlineService == null ||
                    !onlineService.IsConnected ||
                    onlineService.IsInLobby)
                {
                    yield break;
                }

                elapsedTime +=
                    Time.unscaledDeltaTime;

                yield return null;
            }
        }

        /// <summary>
        /// パーティルームへ参加するまで待機します。
        /// </summary>
        private IEnumerator WaitUntilRoomJoined()
        {
            float elapsedTime =
                0.0f;

            while (elapsedTime <
                _joinRoomTimeout)
            {
                var onlineService =
                    NetworkServiceLocator.Online;

                if (onlineService == null ||
                    onlineService.IsInRoom)
                {
                    yield break;
                }

                elapsedTime +=
                    Time.unscaledDeltaTime;

                yield return null;
            }
        }

        /// <summary>
        /// ゲームルームへ設定される
        /// パーティルーム復帰情報を待機します。
        /// </summary>
        /// <param name="teamId">
        /// 復帰先情報の取得に使用するチームIDです。
        /// </param>
        private IEnumerator WaitForReturnRoomInfo(
            int teamId)
        {
            float elapsedTime =
                0.0f;

            while (elapsedTime <
                _roomInfoTimeout)
            {
                if (HasReturnRoomInfo(
                        teamId))
                {
                    yield break;
                }

                elapsedTime +=
                    Time.unscaledDeltaTime;

                yield return null;
            }

            Debug.LogWarning(
                $"{nameof(ReturnAfterResultProcess)}: " +
                "パーティルーム復帰情報の待機がタイムアウトしました。" +
                $" TeamId={teamId}",
                this);
        }

        /// <summary>
        /// 指定されたチームの復帰先情報が
        /// 現在のルームへ登録されているか確認します。
        /// </summary>
        /// <param name="teamId">
        /// 確認するチームIDです。
        /// </param>
        /// <returns>
        /// 必要な復帰情報がすべて存在する場合は<c>true</c>です。
        /// </returns>
        private static bool HasReturnRoomInfo(
            int teamId)
        {
            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                onlineService.CurrentRoom == null ||
                onlineService.CurrentRoom.Properties == null)
            {
                return false;
            }

            var properties =
                onlineService.CurrentRoom.Properties;

            if (!properties.TryGetValue(
                    NetworkPropertyKeys.ReturnRoomReady,
                    out object readyValue) ||
                !TryConvertToBoolean(
                    readyValue,
                    out bool isReady) ||
                !isReady)
            {
                return false;
            }

            return properties.ContainsKey(
                    GetReturnRoomNameKey(
                        teamId)) &&
                properties.ContainsKey(
                    GetReturnRoomIdKey(
                        teamId)) &&
                properties.ContainsKey(
                    GetReturnPasswordKey(
                        teamId)) &&
                properties.ContainsKey(
                    GetReturnPartyIdKey(
                        teamId));
        }

        /// <summary>
        /// 現在のルームから指定されたチームの
        /// パーティルーム復帰情報を取得します。
        /// </summary>
        /// <param name="teamId">
        /// 復帰情報を取得するチームIDです。
        /// </param>
        /// <param name="roomName">
        /// 復帰先ルーム名です。
        /// </param>
        /// <param name="roomId">
        /// 復帰先ルームIDです。
        /// </param>
        /// <param name="password">
        /// 復帰先ルームのパスワードです。
        /// </param>
        /// <param name="partyId">
        /// 復帰先パーティIDです。
        /// </param>
        /// <returns>
        /// 必要な復帰情報を取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetReturnRoomInfo(
            int teamId,
            out string roomName,
            out string roomId,
            out string password,
            out string partyId)
        {
            roomName =
                string.Empty;

            roomId =
                string.Empty;

            password =
                string.Empty;

            partyId =
                string.Empty;

            var onlineService =
                NetworkServiceLocator.Online;

            if (onlineService == null ||
                onlineService.CurrentRoom == null ||
                onlineService.CurrentRoom.Properties == null)
            {
                return false;
            }

            var properties =
                onlineService.CurrentRoom.Properties;

            if (!properties.TryGetValue(
                    GetReturnRoomNameKey(teamId),
                    out object roomNameValue) ||
                !properties.TryGetValue(
                    GetReturnRoomIdKey(teamId),
                    out object roomIdValue) ||
                !properties.TryGetValue(
                    GetReturnPasswordKey(teamId),
                    out object passwordValue) ||
                !properties.TryGetValue(
                    GetReturnPartyIdKey(teamId),
                    out object partyIdValue))
            {
                return false;
            }

            roomName =
                roomNameValue?.ToString()?.Trim() ??
                string.Empty;

            roomId =
                roomIdValue?.ToString()?.Trim() ??
                string.Empty;

            password =
                passwordValue?.ToString() ??
                string.Empty;

            partyId =
                partyIdValue?.ToString()?.Trim() ??
                string.Empty;

            return !string.IsNullOrEmpty(
                    roomName) &&
                !string.IsNullOrEmpty(
                    roomId) &&
                !string.IsNullOrEmpty(
                    partyId);
        }

        /// <summary>
        /// 現在のローカルプレイヤーのチームIDを取得します。
        /// </summary>
        /// <returns>
        /// 取得したチームIDです。
        /// 取得できない場合は-1です。
        /// </returns>
        private static int GetCurrentTeamId()
        {
            var onlineService =
                NetworkServiceLocator.Online;

            UnitManager unitManager =
                UnitManager.Instance;

            if (onlineService == null ||
                unitManager == null ||
                onlineService.LocalPlayerId <= 0)
            {
                return -1;
            }

            return unitManager.GetTeamId(
                onlineService.LocalPlayerId);
        }

        /// <summary>
        /// 現在設定されているパーティ人数を取得します。
        /// </summary>
        /// <returns>
        /// 1以上のパーティ人数です。
        /// </returns>
        private static int GetCurrentPartySize()
        {
            MatchmakingManager matchmakingManager =
                MatchmakingManager.Instance;

            if (matchmakingManager == null)
            {
                return 1;
            }

            return Mathf.Max(
                1,
                matchmakingManager.GetPartySize());
        }

        /// <summary>
        /// パーティプレイ状態を解除します。
        /// </summary>
        private static void ClearPartyPlayState()
        {
            GameFlowManager gameFlowManager =
                GameFlowManager.Instance;

            if (gameFlowManager == null)
            {
                return;
            }

            gameFlowManager.SetPartyPlayState(
                false);

            gameFlowManager.ClearPartyPlayState();
        }

        /// <summary>
        /// 復帰処理を完了し、
        /// シーン遷移処理へ通知します。
        /// </summary>
        private void CompleteProcess()
        {
            if (!_isExecuting ||
                _isCancelled)
            {
                return;
            }

            _isExecuting =
                false;

            _returnCoroutine =
                null;

            Action onCompleted =
                _onCompleted;

            _onCompleted =
                null;

            onCompleted?.Invoke();
        }

        /// <summary>
        /// 実行中の復帰処理を中断し、
        /// シーン遷移コールバックを破棄します。
        /// </summary>
        private void CancelProcess()
        {
            _isCancelled =
                true;

            if (_returnCoroutine != null)
            {
                StopCoroutine(
                    _returnCoroutine);

                _returnCoroutine =
                    null;
            }

            _isExecuting =
                false;

            _onCompleted =
                null;
        }

        /// <summary>
        /// 値をboolへ変換します。
        /// </summary>
        /// <param name="value">
        /// 変換する値です。
        /// </param>
        /// <param name="result">
        /// 変換されたbool値です。
        /// </param>
        /// <returns>
        /// 変換できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryConvertToBoolean(
            object value,
            out bool result)
        {
            result =
                false;

            if (value is bool boolValue)
            {
                result =
                    boolValue;

                return true;
            }

            return bool.TryParse(
                value?.ToString(),
                out result);
        }

        /// <summary>
        /// 指定チームの復帰先ルーム名プロパティキーを生成します。
        /// </summary>
        private static string GetReturnRoomNameKey(
            int teamId)
        {
            return $"returnRoomName_{teamId}";
        }

        /// <summary>
        /// 指定チームの復帰先ルームIDプロパティキーを生成します。
        /// </summary>
        private static string GetReturnRoomIdKey(
            int teamId)
        {
            return $"returnRoomId_{teamId}";
        }

        /// <summary>
        /// 指定チームの復帰先パスワードプロパティキーを生成します。
        /// </summary>
        private static string GetReturnPasswordKey(
            int teamId)
        {
            return $"returnPassword_{teamId}";
        }

        /// <summary>
        /// 指定チームの復帰先パーティIDプロパティキーを生成します。
        /// </summary>
        private static string GetReturnPartyIdKey(
            int teamId)
        {
            return $"returnPartyId_{teamId}";
        }

        /// <summary>
        /// 処理中にコンポーネントが無効化された場合、
        /// コルーチンを停止して遷移待ちを解除します。
        /// </summary>
        private void OnDisable()
        {
            if (_isExecuting)
            {
                Debug.LogWarning(
                    $"{nameof(ReturnAfterResultProcess)}: " +
                    "復帰処理中にコンポーネントが無効化されたため、" +
                    "状態遷移を行わず処理を中断します。",
                    this);
            }

            CancelProcess();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定された待機時間を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _roomInfoTimeout =
                Mathf.Max(
                    0.1f,
                    _roomInfoTimeout);

            _leaveRoomTimeout =
                Mathf.Max(
                    0.1f,
                    _leaveRoomTimeout);

            _lobbyReturnTimeout =
                Mathf.Max(
                    0.1f,
                    _lobbyReturnTimeout);

            _joinRoomTimeout =
                Mathf.Max(
                    0.1f,
                    _joinRoomTimeout);
        }
#endif
    }
}
