using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using PranaTactics.GameFlow;
using PranaTactics.GameFlow.States;
using PranaTactics.Match.Solo;
using PranaTactics.Match.Solo.Stages;

namespace PranaTactics.UI.Screens.SoloStageSelect
{
    /// <summary>
    /// シーン配置された一覧、編成紹介と出撃操作を既存UI処理で管理します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoloStageSelectView : MonoBehaviour
    {
        [SerializeField]
        private StageRow[] _rows = Array.Empty<StageRow>();

        [SerializeField]
        private TMP_Text _title;

        [SerializeField]
        private TMP_Text _description;

        [SerializeField]
        private TMP_Text _composition;

        [SerializeField]
        private TMP_Text _status;

        [SerializeField]
        private TMP_Text _pageLabel;

        [SerializeField]
        private TMP_Text _beginLabel;

        [SerializeField]
        private Image _beginBackground;

        private SoloStageSession _session;
        private GameFlowManager _gameFlow;
        private int _page;
        private int _selectedIndex;
        private bool _isChanging;

        /// <summary>
        /// 既存のUI入力管理とレイキャスト用の基盤を準備します。
        /// </summary>
        private void Awake()
        {
            _gameFlow = GameFlowManager.Instance;
            if (UIManager.Instance == null)
            {
                new GameObject("UIManager", typeof(UIManager));
            }

            if (EventSystem.current == null)
            {
                new GameObject("Stage Input", typeof(EventSystem));
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// 保存された進行を反映し、最初の未クリアステージを表示します。
        /// </summary>
        private void Start()
        {
            try
            {
                if (_gameFlow == null || _rows.Length == 0)
                {
                    throw new InvalidOperationException("GameFlowManagerまたはステージ行が未設定です。");
                }

                _session = _gameFlow.SoloStages;
                for (int index = 0; index < _session.Stages.Count; index++)
                {
                    if (!_session.IsCleared(index))
                    {
                        _selectedIndex = index;
                        break;
                    }
                }

                _page = _selectedIndex / _rows.Length;
                RefreshView();
            }
            catch (Exception exception)
            {
                _session = null;
                _status.text = "ステージ設定を読み込めませんでした。ロビーへ戻って設定を確認してください。";
                _beginLabel.text = "設定エラー";
                Debug.LogException(exception, this);
            }
        }

        /// <summary>
        /// 遷移中、設定画面表示中、未解放ステージの出撃を制限します。
        /// </summary>
        public bool CanExecute(StageSelectAction action, int rowIndex)
        {
            if (_isChanging || !isActiveAndEnabled ||
                (SettingsUIManager.Instance != null && SettingsUIManager.Instance.IsOpen))
            {
                return false;
            }

            if (action == StageSelectAction.Home)
            {
                return _gameFlow != null;
            }

            if (_session == null)
            {
                return false;
            }

            switch (action)
            {
                case StageSelectAction.Select:
                    return rowIndex >= 0 && rowIndex < _rows.Length &&
                        _page * _rows.Length + rowIndex < _session.Stages.Count;
                case StageSelectAction.Begin:
                    return _session.IsUnlocked(_selectedIndex);
                case StageSelectAction.PreviousPage:
                    return _page > 0;
                case StageSelectAction.NextPage:
                    return (_page + 1) * _rows.Length < _session.Stages.Count;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 既存クリック処理から渡された選択、出撃または画面遷移を実行します。
        /// </summary>
        public void Execute(StageSelectAction action, int rowIndex)
        {
            if (!CanExecute(action, rowIndex))
            {
                return;
            }

            switch (action)
            {
                case StageSelectAction.Select:
                    _selectedIndex = _page * _rows.Length + rowIndex;
                    RefreshView();
                    break;
                case StageSelectAction.Begin:
                    ChangeState(true);
                    break;
                case StageSelectAction.Home:
                    ChangeState(false);
                    break;
                case StageSelectAction.PreviousPage:
                    _page--;
                    RefreshView();
                    break;
                case StageSelectAction.NextPage:
                    _page++;
                    RefreshView();
                    break;
            }
        }

        /// <summary>
        /// 解放状態を再検証して出撃し、失敗時は選択画面で操作を再開します。
        /// </summary>
        private void ChangeState(bool shouldBegin)
        {
            _isChanging = true;
            try
            {
                if (shouldBegin)
                {
                    _session.Select(_selectedIndex);
                    _gameFlow.ChangeState(new Solo_CharacterSelectState());
                }
                else
                {
                    _gameFlow.ClearSoloStageSelection();
                    _gameFlow.ChangeState(new LobbyState());
                }
            }
            catch (Exception exception)
            {
                _isChanging = false;
                _status.text = "画面を開けませんでした。設定を確認してください。";
                Debug.LogException(exception, this);
            }
        }

        /// <summary>
        /// ページ内の固定行と選択中ステージの詳細を更新します。
        /// </summary>
        private void RefreshView()
        {
            for (int rowIndex = 0; rowIndex < _rows.Length; rowIndex++)
            {
                StageRow row = _rows[rowIndex];
                int stageIndex = _page * _rows.Length + rowIndex;
                bool exists = stageIndex < _session.Stages.Count;
                row.Root.SetActive(exists);
                if (!exists)
                {
                    continue;
                }

                bool unlocked = _session.IsUnlocked(stageIndex);
                row.Title.text = $"{stageIndex + 1:00}  /  {_session.Stages[stageIndex].DisplayName}";
                row.Status.text = _session.IsCleared(stageIndex) ? "CLEAR" : unlocked ? "OPEN" : "LOCKED";
                row.Background.color = unlocked ? new Color(0.075f, 0.12f, 0.15f) : new Color(0.05f, 0.065f, 0.08f);
                row.Selected.SetActive(stageIndex == _selectedIndex);
            }

            SoloStageDefinition stage = _session.Stages[_selectedIndex];
            bool canBegin = _session.IsUnlocked(_selectedIndex);
            _title.text = $"{_selectedIndex + 1:00}  /  {stage.DisplayName}";
            _description.text = stage.Description;
            _composition.text = DescribeComposition(stage.LoadConfiguration());
            _pageLabel.text = $"{_page + 1:00} / {(_session.Stages.Count + _rows.Length - 1) / _rows.Length:00}";
            _beginLabel.text = canBegin ? "出撃する" : "未解放";
            _beginBackground.color = canBegin ? new Color(0.42f, 0.86f, 0.9f) : new Color(0.25f, 0.3f, 0.33f);
            _status.text = canBegin
                ? "勝利でステージクリア。次のステージが解放されます。"
                : "先行するステージをクリアすると解放されます。";
        }

        /// <summary>
        /// 編成JSONのチーム人数を紹介用テキストへ変換します。
        /// </summary>
        private static string DescribeComposition(SoloBattleConfiguration configuration)
        {
            var description = new StringBuilder();
            foreach (SoloBattleTeam team in configuration.Teams)
            {
                description.Append(team.TeamId == 0 ? "味方" : $"敵チーム {team.TeamId}");
                description.Append($"  /  {team.PlayerCount}名\n");
            }

            description.Append("\n敵撃破 +2pt / 最多生存チーム +2pt");
            return description.ToString();
        }

        /// <summary>
        /// Inspector上のステージ行に必要な表示参照をまとめます。
        /// </summary>
        [Serializable]
        private sealed class StageRow
        {
            public GameObject Root;
            public TMP_Text Title;
            public TMP_Text Status;
            public Image Background;
            public GameObject Selected;
        }
    }
}
