using System;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.UI.Screens.GamePlayMode;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// ロビー画面のマッチング状態と、
    /// パーティルーム用ポップアップへの通知を管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyUIManager : MonoBehaviour
    {
        /// <summary>
        /// パーティルームの作成・参加情報を表示するUIです。
        /// </summary>
        [FormerlySerializedAs("_popUpUI")]
        [SerializeField]
        private PopupUI _popupUI;

        /// <summary>
        /// マルチモードでのみ表示するルーム作成・参加ボタンです。
        /// </summary>
        [SerializeField]
        private GameObject _roomButton;

        /// <summary>
        /// 現在マッチング中かどうかを示します。
        /// </summary>
        private bool _isMatchmaking;

        public static LobbyUIManager Instance
        {
            get;
            private set;
        }

        public bool IsMatchmaking
        {
            get
            {
                return _isMatchmaking;
            }
        }

        public event Action<bool> MatchmakingStateChanged;

        /// <summary>
        /// シングルトンインスタンスを設定します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Debug.LogWarning(
                    $"{nameof(LobbyUIManager)}が複数存在するため、" +
                    $"{gameObject.name}を破棄します。",
                    this);

                Destroy(
                    gameObject);

                return;
            }

            Instance =
                this;
        }

        /// <summary>
        /// ロビーを表示した時点のモードをボタンへ反映します。
        /// </summary>
        private void OnEnable()
        {
            RefreshRoomButtonVisibility();
        }

        /// <summary>
        /// モード変更に合わせてルームボタンの表示を更新します。
        /// </summary>
        private void Update()
        {
            RefreshRoomButtonVisibility();
        }

        /// <summary>
        /// マルチモードのときだけルームボタンを有効にします。
        /// </summary>
        private void RefreshRoomButtonVisibility()
        {
            bool shouldShow = !PlayModeSelection.IsSoloPlay;
            if (_roomButton != null && _roomButton.activeSelf != shouldShow)
            {
                _roomButton.SetActive(shouldShow);
            }
        }

        /// <summary>
        /// シングルトン参照を解放します。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance =
                    null;
            }
        }

        /// <summary>
        /// 現在のマッチング状態を設定します。
        /// </summary>
        /// <param name="isMatchmaking">
        /// マッチング中の場合は<c>true</c>です。
        /// </param>
        public void SetIsMatchmaking(
            bool isMatchmaking)
        {
            if (_isMatchmaking ==
                isMatchmaking)
            {
                return;
            }

            _isMatchmaking =
                isMatchmaking;

            MatchmakingStateChanged?.Invoke(
                _isMatchmaking);
        }

        /// <summary>
        /// パーティルームが作成されたことを
        /// ポップアップUIへ通知します。
        /// </summary>
        /// <param name="roomName">
        /// 作成されたルーム名です。
        /// </param>
        public void NotifyRoomCreated(
            string roomName)
        {
            if (_popupUI == null)
            {
                Debug.LogWarning(
                    $"{nameof(LobbyUIManager)}: " +
                    $"{nameof(PopupUI)}が設定されていないため、" +
                    "ルーム作成結果を表示できません。",
                    this);

                return;
            }

            _popupUI.OnCreateRoom(
                roomName);
        }

        /// <summary>
        /// パーティルームへ参加したことを
        /// ポップアップUIへ通知します。
        /// </summary>
        /// <param name="roomName">
        /// 参加したルーム名です。
        /// </param>
        public void NotifyRoomJoined(
            string roomName)
        {
            if (_popupUI == null)
            {
                Debug.LogWarning(
                    $"{nameof(LobbyUIManager)}: " +
                    $"{nameof(PopupUI)}が設定されていないため、" +
                    "ルーム参加結果を表示できません。",
                    this);

                return;
            }

            _popupUI.OnJoinRoom(
                roomName);
        }

        /// <summary>
        /// 現在のマッチング状態を設定します。
        /// </summary>
        /// <param name="value">
        /// マッチング中の場合は<c>true</c>です。
        /// </param>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="SetIsMatchmaking"/>を使用してください。
        /// </remarks>
        [Obsolete(
            "Use SetIsMatchmaking instead.")]
        public void SetIsMatchiMaking(
            bool value)
        {
            SetIsMatchmaking(
                value);
        }

        /// <summary>
        /// 現在マッチング中かどうかを取得します。
        /// </summary>
        /// <returns>
        /// マッチング中の場合は<c>true</c>です。
        /// </returns>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="IsMatchmaking"/>を使用してください。
        /// </remarks>
        [Obsolete(
            "Use IsMatchmaking instead.")]
        public bool GetIsMatchiMaking()
        {
            return IsMatchmaking;
        }

        /// <summary>
        /// パーティルームが作成されたことを通知します。
        /// </summary>
        /// <param name="roomName">
        /// 作成されたルーム名です。
        /// </param>
        /// <remarks>
        /// 既存コードやUnityEventとの互換性維持用です。
        /// 新しいコードでは<see cref="NotifyRoomCreated"/>を使用してください。
        /// </remarks>
        public void OnCreateRoom(
            string roomName)
        {
            NotifyRoomCreated(
                roomName);
        }

        /// <summary>
        /// パーティルームへ参加したことを通知します。
        /// </summary>
        /// <param name="roomName">
        /// 参加したルーム名です。
        /// </param>
        /// <remarks>
        /// 既存コードやUnityEventとの互換性維持用です。
        /// 新しいコードでは<see cref="NotifyRoomJoined"/>を使用してください。
        /// </remarks>
        public void OnJoinRoom(
            string roomName)
        {
            NotifyRoomJoined(
                roomName);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の参照設定を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_popupUI == null)
            {
                Debug.LogWarning(
                    $"{nameof(LobbyUIManager)}: " +
                    $"{nameof(PopupUI)}が設定されていません。",
                    this);
            }
        }
#endif
    }
}
