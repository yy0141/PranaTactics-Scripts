using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

using PranaTactics.Audio;
using PranaTactics.GameFlow;
using PranaTactics.GameFlow.States;
using PranaTactics.Match.Results;
using PranaTactics.Networking;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 共通リザルトのページ、戦績更新、既存処理による復帰を調整します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultUIManager : MonoBehaviour
    {
        /// <summary>
        /// チーム集計ページです。
        /// </summary>
        [SerializeField]
        private ResultSummaryView _summary;
        /// <summary>
        /// 全Unit戦績ページです。
        /// </summary>
        [SerializeField]
        private ResultUnitRankingView _ranking;
        /// <summary>
        /// 現在のページ名です。
        /// </summary>
        [SerializeField]
        private TMP_Text _pageLabel;
        /// <summary>
        /// データ欠落と復帰中の案内です。
        /// </summary>
        [SerializeField]
        private TMP_Text _status;
        /// <summary>
        /// 現在の一覧範囲です。
        /// </summary>
        [SerializeField]
        private TMP_Text _rangeLabel;
        /// <summary>
        /// 既存のロビー復帰前処理です。
        /// </summary>
        [SerializeField]
        private ReturnAfterResultProcess _returnProcess;
        /// <summary>
        /// 最終戦績の同期元です。
        /// </summary>
        private IOnlineService _online;
        /// <summary>
        /// 選択中のページです。
        /// </summary>
        private int _page;
        /// <summary>
        /// 復帰を実行中かどうかです。
        /// </summary>
        private bool _isReturning;

        public bool CanInteract => isActiveAndEnabled && !_isReturning;

        /// <summary>
        /// 既存UI入力とレイキャストの基盤を準備します。
        /// </summary>
        private void Awake()
        {
            if (UIManager.Instance == null)
            {
                new GameObject("Result UI Input", typeof(UIManager));
            }
            if (EventSystem.current == null)
            {
                new GameObject("Result Raycast", typeof(EventSystem));
            }
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// 遅延した戦績通知を購読します。
        /// </summary>
        private void OnEnable()
        {
            _online = GameFlowManager.Instance != null && GameFlowManager.Instance.IsSoloPlay
                ? null : NetworkServiceLocator.Online;
            if (_online != null)
            {
                _online.RoomPropertiesChanged += OnRoomPropertiesChanged;
                _online.PlayerPropertiesChanged += OnPlayerPropertiesChanged;
            }
        }

        /// <summary>
        /// 初期結果を表示します。
        /// </summary>
        private void Start()
        {
            RefreshResult();
        }

        /// <summary>
        /// 通知の購読を解除します。
        /// </summary>
        private void OnDisable()
        {
            if (_online != null)
            {
                _online.RoomPropertiesChanged -= OnRoomPropertiesChanged;
                _online.PlayerPropertiesChanged -= OnPlayerPropertiesChanged;
            }
        }

        /// <summary>
        /// 戦績パケット到着時に表示を更新します。
        /// </summary>
        private void OnRoomPropertiesChanged(Dictionary<string, object> properties)
        {
            foreach (string key in properties.Keys)
            {
                if (key == MatchResultSession.k_RoundKey ||
                    key.StartsWith(MatchResultSession.k_RecordPrefix, System.StringComparison.Ordinal))
                {
                    RefreshResult();
                    return;
                }
            }
        }

        /// <summary>
        /// 各所有者から届く最終戦績を反映します。
        /// </summary>
        private void OnPlayerPropertiesChanged(OnlinePlayer player, Dictionary<string, object> properties)
        {
            OnRoomPropertiesChanged(properties);
        }

        /// <summary>
        /// 保存済みの結果を表示機能へ渡します。
        /// </summary>
        private void RefreshResult()
        {
            MatchResultSession.RefreshOnline(_online);
            MatchResultSnapshot result = MatchResultSession.Current;
            if (!_isReturning && result != null)
            {
                SoundManager.Instance?.PlayResultBgm(result.Outcome);
            }
            _status.text = result == null ? "戦績データがありません。試合終了後に結果が表示されます。" : string.Empty;
            _summary.Bind(result);
            _ranking.Bind(result);
            RefreshPage();
        }

        /// <summary>
        /// チーム集計またはUnit戦績を選択します。
        /// </summary>
        public void SelectPage(int page)
        {
            if (!CanInteract)
            {
                return;
            }
            _page = Mathf.Clamp(page, 0, 1);
            RefreshPage();
        }

        /// <summary>
        /// 現在のページの一覧を前後へ送ります。
        /// </summary>
        public void StepRows(int direction)
        {
            if (!CanInteract)
            {
                return;
            }
            if (_page == 0)
            {
                _summary.StepRows(direction);
            }
            else
            {
                _ranking.StepRows(direction);
            }
            RefreshPage();
        }

        /// <summary>
        /// 選択ページと一覧範囲を反映します。
        /// </summary>
        private void RefreshPage()
        {
            _summary.gameObject.SetActive(_page == 0);
            _ranking.gameObject.SetActive(_page == 1);
            _pageLabel.text = _page == 0 ? "01 / 02   TEAM RESULT" : "02 / 02   UNIT RECORD";
            _rangeLabel.text = _page == 0 ? _summary.RangeLabel : _ranking.RangeLabel;
        }

        /// <summary>
        /// 既存復帰処理後にプレイ方式に対応した画面へ戻ります。
        /// </summary>
        public void Return()
        {
            GameFlowManager flow = GameFlowManager.Instance;
            if (!CanInteract || flow == null || _returnProcess == null)
            {
                return;
            }
            _isReturning = true;
            _status.text = "復帰処理中…";
            bool stages = flow.HasSelectedSoloStage;
            _returnProcess.Execute(() =>
            {
                if (this != null && flow != null)
                {
                    flow.ChangeState(stages ? (GameState)new SoloStageSelectState() : new LobbyState());
                }
            });
        }
    }
}
