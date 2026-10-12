using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Match.Results;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// 全Unitを最大貢献者として獲得した撃破数の降順で表示し、チーム色を反映します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultUnitRankingView : MonoBehaviour
    {
        /// <summary>
        /// 戦績の表示行です。
        /// </summary>
        [SerializeField]
        private UnitRow[] _rows;
        /// <summary>
        /// 全Unitを含む結果です。
        /// </summary>
        private MatchResultSnapshot _result;
        /// <summary>
        /// 一覧の先頭位置です。
        /// </summary>
        private int _offset;

        public string RangeLabel => _result == null || _result.Units.Count == 0 ? "—" : $"{_offset + 1:00}–{Mathf.Min(_offset + _rows.Length, _result.Units.Count):00} / {_result.Units.Count:00} UNITS";

        /// <summary>
        /// 更新時も閲覧中の一覧位置を保持します。
        /// </summary>
        public void Bind(MatchResultSnapshot result)
        {
            _result = result;
            RefreshRows();
        }

        /// <summary>
        /// 最後のUnitまで一覧を送ります。
        /// </summary>
        public void StepRows(int direction)
        {
            int lastPage = _result == null ? 0 : Mathf.Max(0, (_result.Units.Count - 1) / _rows.Length);
            _offset = Mathf.Clamp(_offset / _rows.Length + direction, 0, lastPage) * _rows.Length;
            RefreshRows();
        }

        /// <summary>
        /// 同順位と各Unitの戦績を表示します。
        /// </summary>
        private void RefreshRows()
        {
            for (int index = 0; index < _rows.Length; index++)
            {
                UnitRow row = _rows[index];
                int position = _offset + index;
                bool exists = _result != null && position < _result.Units.Count;
                row.Root.SetActive(exists);
                if (!exists)
                {
                    continue;
                }
                UnitResult unit = _result.Units[position];
                int rank = position + 1;
                while (rank > 1 && _result.Units[rank - 2].Kills == unit.Kills)
                {
                    rank--;
                }
                Color tint = ResultTeamStyle.GetColor(unit.TeamId);
                row.Background.color = ResultTeamStyle.GetRowColor(unit.TeamId, unit.PlayerId == _result.LocalPlayerId);
                row.Accent.color = tint;
                row.Team.color = tint;
                row.Rank.text = $"{rank:00}";
                row.Team.text = ResultTeamStyle.GetName(unit.TeamId);
                row.Name.text = unit.Name;
                row.Role.text = unit.Role;
                row.Kills.text = unit.Kills.ToString();
                row.Dealt.text = unit.DamageDealt.ToString("0");
                row.Received.text = unit.DamageReceived.ToString("0");
                row.Alive.text = unit.IsAlive ? "○" : "×";
                row.Alive.color = unit.IsAlive ? tint : new Color(0.49f, 0.56f, 0.59f);
            }
        }

        /// <summary>
        /// Unit戦績行のInspector参照です。
        /// </summary>
        [Serializable]
        private sealed class UnitRow
        {
            public GameObject Root;
            public Image Background;
            public Image Accent;
            public TMP_Text Rank;
            public TMP_Text Team;
            public TMP_Text Name;
            public TMP_Text Role;
            public TMP_Text Kills;
            public TMP_Text Dealt;
            public TMP_Text Received;
            public TMP_Text Alive;
        }
    }
}
