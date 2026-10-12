using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Networking;

namespace PranaTactics.Board.Networking.EOS
{
    /// <summary>
    /// EOSを使用してボードの生成状態を同期します。
    /// ホストがボードを生成した後、ルームプロパティを通して
    /// 他のクライアントへ生成完了を通知します。
    /// </summary>
    /// <remarks>
    /// ボードオブジェクト自体はネットワーク経由で送信せず、
    /// 各クライアントが共通のBoardDataを使用してローカルに生成します。
    /// </remarks>
    public sealed class EosBoardNetwork : MonoBehaviour, IBoardNetwork
    {
        /// <summary>
        /// ボードの生成完了状態を保存するルームプロパティのキーです。
        /// </summary>
        private const string k_BoardCreatedPropertyKey =
            "battleBoardCreated";

        /// <summary>
        /// EOSを含むオンライン機能へのアクセスを提供するサービスです。
        /// </summary>
        private IOnlineService _onlineService;

        /// <summary>
        /// このクライアントでボードが生成済みかどうかを示します。
        /// </summary>
        private bool _isBoardCreated;

        /// <summary>
        /// オンラインサービスを取得し、
        /// ルームプロパティの変更イベントを購読します。
        /// </summary>
        private void Awake()
        {
            _onlineService = NetworkServiceLocator.Online;

            if (_onlineService == null)
            {
                Debug.LogWarning(
                    $"{nameof(EosBoardNetwork)}: " +
                    "オンラインサービスを取得できませんでした。",
                    this);

                return;
            }

            _onlineService.RoomPropertiesChanged +=
                HandleRoomPropertiesChanged;
        }

        /// <summary>
        /// 現在のルームプロパティにボード生成済みの状態が
        /// 保存されているか確認します。
        /// </summary>
        private void Start()
        {
            TryCreateBoardFromCurrentRoom("Start");
        }

        /// <summary>
        /// ルームプロパティの変更イベントの購読を解除します。
        /// </summary>
        private void OnDestroy()
        {
            if (_onlineService == null)
            {
                return;
            }

            _onlineService.RoomPropertiesChanged -=
                HandleRoomPropertiesChanged;
        }

        /// <summary>
        /// このクライアントでボードを生成します。
        /// ホストの場合は、ボード生成済みの状態を
        /// ルームプロパティへ保存します。
        /// </summary>
        public void CreateBoard()
        {
            CreateBoardLocally("HostCreate");

            if (_onlineService == null ||
                !_onlineService.IsInRoom ||
                !_onlineService.IsHost)
            {
                return;
            }

            Debug.Log(
                $"{nameof(EosBoardNetwork)}: " +
                $"{k_BoardCreatedPropertyKey}を" +
                "ルームプロパティへ送信します。",
                this);

            _onlineService.SetRoomProperty(
                k_BoardCreatedPropertyKey,
                true);
        }

        /// <summary>
        /// ルームプロパティが変更されたとき、
        /// ボード生成済みの状態を確認します。
        /// </summary>
        /// <param name="changedProperties">
        /// 変更されたルームプロパティの一覧です。
        /// </param>
        private void HandleRoomPropertiesChanged(
            Dictionary<string, object> changedProperties)
        {
            if (!TryGetProperty(
                    changedProperties,
                    k_BoardCreatedPropertyKey,
                    out object propertyValue))
            {
                return;
            }

            if (!ConvertToBoolean(propertyValue))
            {
                return;
            }

            CreateBoardLocally("RoomPropertiesChanged");
        }

        /// <summary>
        /// 現在参加しているルームのプロパティを確認し、
        /// ボードが生成済みの場合はローカルボードを生成します。
        /// </summary>
        /// <param name="reason">
        /// ボード生成処理を実行した理由を表す文字列です。
        /// </param>
        private void TryCreateBoardFromCurrentRoom(string reason)
        {
            if (_onlineService == null ||
                _onlineService.CurrentRoom == null)
            {
                return;
            }

            if (!TryGetProperty(
                    _onlineService.CurrentRoom.Properties,
                    k_BoardCreatedPropertyKey,
                    out object propertyValue))
            {
                return;
            }

            if (!ConvertToBoolean(propertyValue))
            {
                return;
            }

            CreateBoardLocally(reason);
        }

        /// <summary>
        /// BoardManagerを使用して、このクライアント上に
        /// ボードを一度だけ生成します。
        /// </summary>
        /// <param name="reason">
        /// ボード生成処理を実行した理由を表す文字列です。
        /// </param>
        private void CreateBoardLocally(string reason)
        {
            if (_isBoardCreated)
            {
                return;
            }

            if (BoardManager.Instance == null)
            {
                Debug.LogWarning(
                    $"{nameof(EosBoardNetwork)}: " +
                    $"{nameof(BoardManager)}のインスタンスが" +
                    "存在しないため、ボードを生成できません。",
                    this);

                return;
            }

            _isBoardCreated = true;

            Debug.Log(
                $"{nameof(EosBoardNetwork)}: " +
                $"ボードをローカル生成します。Reason={reason}",
                this);

            BoardManager.Instance.CreateBoard();
        }

        /// <summary>
        /// オブジェクトの値をbool値へ変換します。
        /// </summary>
        /// <param name="value">
        /// bool値へ変換するオブジェクトです。
        /// </param>
        /// <returns>
        /// 値が<c>true</c>を表している場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool ConvertToBoolean(object value)
        {
            if (value is bool booleanValue)
            {
                return booleanValue;
            }

            return bool.TryParse(
                    value?.ToString(),
                    out bool parsedValue) &&
                parsedValue;
        }

        /// <summary>
        /// プロパティの一覧から、指定されたキーに対応する値を取得します。
        /// キーの大文字と小文字は区別しません。
        /// </summary>
        /// <param name="properties">
        /// 検索対象となるプロパティの一覧です。
        /// </param>
        /// <param name="key">
        /// 検索するプロパティのキーです。
        /// </param>
        /// <param name="value">
        /// プロパティが存在する場合に取得される値です。
        /// </param>
        /// <returns>
        /// 指定されたキーが存在する場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private static bool TryGetProperty(
            IReadOnlyDictionary<string, object> properties,
            string key,
            out object value)
        {
            value = null;

            if (properties == null ||
                string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (properties.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (
                KeyValuePair<string, object> property
                in properties)
            {
                if (!string.Equals(
                        property.Key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                value = property.Value;

                return true;
            }

            return false;
        }
    }
}
