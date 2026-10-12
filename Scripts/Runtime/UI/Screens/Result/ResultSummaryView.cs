using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Match.Results;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 勝敗、自チームの得点内訳、全チームの順位を表示します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultSummaryView : MonoBehaviour
    {
        /// <summary>
        /// 勝敗見出しです。
        /// </summary>
        [SerializeField]
        private TMP_Text _outcome;
        /// <summary>
        /// 自チームの識別名です。
        /// </summary>
        [SerializeField]
        private TMP_Text _localTeam;
        /// <summary>
        /// 撃破、生存ボーナス、合計、生存人数の表示欄です。
        /// </summary>
        [SerializeField]
        private TMP_Text[] _values;
        /// <summary>
        /// チーム順位の表示行です。
        /// </summary>
        [SerializeField]
        private TeamRow[] _rows;
        /// <summary>
        /// 確定結果です。
        /// </summary>
        private MatchResultSnapshot _result;
        /// <summary>
        /// 一覧の先頭位置です。
        /// </summary>
        private int _offset;

        public string RangeLabel => _result == null || _result.Teams.Count == 0 ? "—" : $"{_offset + 1:00}–{Mathf.Min(_offset + _rows.Length, _result.Teams.Count):00} / {_result.Teams.Count:00} TEAMS";

        /// <summary>
        /// 自チームの集計値と勝敗色を反映します。
        /// </summary>
        public void Bind(MatchResultSnapshot result)
        {
            _result = result;
            TeamResult local = result?.Teams.FirstOrDefault(team => team.TeamId == result.LocalTeamId);
            _outcome.text = result == null ? "RESULT" : result.Outcome.ToString().ToUpperInvariant();
            _outcome.color = result == null || result.Outcome == MatchOutcome.Victory
                ? ResultTeamStyle.GetColor(0) : result.Outcome == MatchOutcome.Defeat ? ResultTeamStyle.GetColor(1) : ResultTeamStyle.GetColor(2);
            _localTeam.text = local == null ? "YOUR TEAM / —" : $"YOUR TEAM / {ResultTeamStyle.GetName(local.TeamId)}";
            _values[0].text = local?.DefeatScore.ToString() ?? "—";
            _values[1].text = local?.SurvivalBonus.ToString() ?? "—";
            _values[2].text = local?.Total.ToString() ?? "—";
            _values[3].text = local?.Survivors.ToString() ?? "—";
            RefreshRows();
        }

        /// <summary>
        /// 全チームを閲覧できるよう一覧を送ります。
        /// </summary>
        public void StepRows(int direction)
        {
            int lastPage = _result == null ? 0 : Mathf.Max(0, (_result.Teams.Count - 1) / _rows.Length);
            _offset = Mathf.Clamp(_offset / _rows.Length + direction, 0, lastPage) * _rows.Length;
            RefreshRows();
        }

        /// <summary>
        /// 同点順位、自チーム表示、チーム色を各行へ反映します。
        /// </summary>
        private void RefreshRows()
        {
            for (int index = 0; index < _rows.Length; index++)
            {
                TeamRow row = _rows[index];
                int position = _offset + index;
                bool exists = _result != null && position < _result.Teams.Count;
                row.Root.SetActive(exists);
                if (!exists)
                {
                    continue;
                }
                TeamResult team = _result.Teams[position];
                row.Background.color = ResultTeamStyle.GetRowColor(team.TeamId, team.TeamId == _result.LocalTeamId);
                row.Accent.color = ResultTeamStyle.GetColor(team.TeamId);
                row.Rank.text = $"{team.Rank:00}";
                row.Team.text = $"TEAM {ResultTeamStyle.GetName(team.TeamId)}" + (team.TeamId == _result.LocalTeamId ? "  / YOU" : string.Empty);
                row.Defeat.text = team.DefeatScore.ToString();
                row.Bonus.text = team.SurvivalBonus.ToString();
                row.Total.text = team.Total.ToString();
                row.Survivors.text = team.Survivors.ToString();
            }
        }

        /// <summary>
        /// チーム順位行のInspector参照です。
        /// </summary>
        [Serializable]
        private sealed class TeamRow
        {
            public GameObject Root;
            public Image Background;
            public Image Accent;
            public TMP_Text Rank;
            public TMP_Text Team;
            public TMP_Text Defeat;
            public TMP_Text Bonus;
            public TMP_Text Total;
            public TMP_Text Survivors;
        }
    }
}
