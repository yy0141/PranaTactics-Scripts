using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Unit;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// ソロ・マルチ共通のユニット一覧を、行動力の左右に陣営別で表示します。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class TeamRosterView : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _actionPointsPanel;

        [SerializeField]
        private RectTransform _minimapPanel;

        [SerializeField]
        private RectTransform _turnPanel;

        [SerializeField, Min(0.0f)]
        private float _screenMargin = 16.0f;

        [SerializeField, Min(0.0f)]
        private float _panelGap = 6.0f;

        [SerializeField]
        private RectTransform _alliesRoot;

        [SerializeField]
        private RectTransform _enemiesRoot;

        [SerializeField]
        private TeamRosterIconView _iconPrefab;

        [SerializeField, Min(1.0f)]
        private float _iconSize = 44.0f;

        [SerializeField, Min(0.0f)]
        private float _spacing = 6.0f;

        private readonly Dictionary<int, TeamRosterIconView> _icons = new();

        private readonly HashSet<int> _seenPlayerIds = new();

        private readonly List<int> _removedPlayerIds = new();

        private readonly List<TeamRosterIconView> _allies = new();

        private readonly List<TeamRosterIconView> _enemies = new();

        private readonly Vector3[] _turnCorners = new Vector3[4];

        private readonly Vector3[] _minimapCorners = new Vector3[4];

        private Vector3 _availableLeft;

        private Vector3 _availableRight;

        private bool _hasAvailableBounds;

        private readonly Vector3[] _panelCorners = new Vector3[4];

        private UnitManager _unitManager;

        private int _localTeamId = -1;

        /// <summary>
        /// HP・死亡・操作引き継ぎ・再生成を、共通のUnitManagerから表示へ同期します。
        /// </summary>
        private void LateUpdate()
        {
            UnitManager manager = UnitManager.Instance;

            if (_unitManager != manager)
            {
                ClearIcons();
                _unitManager = manager;
            }

            if (manager == null || _iconPrefab == null || _alliesRoot == null || _enemiesRoot == null)
            {
                return;
            }

            if (manager.LocalUnit != null)
            {
                _localTeamId = manager.LocalUnit.GetTeamId();
            }

            if (_localTeamId < 0)
            {
                return;
            }

            _seenPlayerIds.Clear();

            foreach (UnitBehavior unit in manager.GetAllUnitBehaviors())
            {
                UnitStatus status = unit.GetUnitStatus();

                if (status == null)
                {
                    continue;
                }

                _seenPlayerIds.Add(status.PlayerId);
                bool isNew = !_icons.TryGetValue(status.PlayerId, out TeamRosterIconView icon);
                bool isAlly = status.TeamId == _localTeamId;

                if (isNew)
                {
                    icon = Instantiate(_iconPrefab, isAlly ? _alliesRoot : _enemiesRoot);
                    icon.name = $"Roster Player {status.PlayerId}";
                    _icons.Add(status.PlayerId, icon);
                }

                icon.Bind(status);
                icon.Refresh(isAlly, GetFrameColor(status.TeamId), isNew);
            }

            RefreshRows();
        }

        /// <summary>
        /// モデルと同じ保存済みチームカラーをアイコンの枠へ適用します。
        /// </summary>
        private Color GetFrameColor(int teamId)
        {
            return GameplayColorSettings.GetTeamColor(teamId);
        }

        /// <summary>
        /// パネルの表示範囲を各行の親座標へ変換し、移動と拡縮に追従させます。
        /// </summary>
        private void AlignRowsToPanel()
        {
            if (_actionPointsPanel == null)
            {
                return;
            }

            AlignPanelToMinimap();
            _actionPointsPanel.GetWorldCorners(_panelCorners);
            AlignRowToPanel(_alliesRoot, true);
            AlignRowToPanel(_enemiesRoot, false);
        }

        /// <summary>
        /// アイコンと余白が収まる場合は画面中央を優先し、それ以外は両パネル間の中央へ配置します。
        /// </summary>
        private void AlignPanelToMinimap()
        {
            _hasAvailableBounds = false;

            if (_minimapPanel == null || _actionPointsPanel.parent is not RectTransform parent)
            {
                return;
            }

            float left = parent.rect.xMin + _screenMargin;
            float right = parent.rect.xMax - _screenMargin;

            if (_turnPanel != null && _turnPanel.gameObject.activeInHierarchy)
            {
                _turnPanel.GetWorldCorners(_turnCorners);

                foreach (Vector3 corner in _turnCorners)
                {
                    left = Mathf.Max(left, parent.InverseTransformPoint(corner).x + _screenMargin);
                }
            }

            if (_minimapPanel.gameObject.activeInHierarchy)
            {
                _minimapPanel.GetWorldCorners(_minimapCorners);

                foreach (Vector3 corner in _minimapCorners)
                {
                    right = Mathf.Min(right, parent.InverseTransformPoint(corner).x - _screenMargin);
                }
            }

            right = Mathf.Max(left, right);
            Vector3 position = _actionPointsPanel.localPosition;
            float centerOffset = _actionPointsPanel.rect.center.x * _actionPointsPanel.localScale.x;
            float center = (left + right) * 0.5f;
            Canvas canvas = _actionPointsPanel.GetComponentInParent<Canvas>();
            RectTransform screen = canvas != null ? canvas.rootCanvas.transform as RectTransform : parent;
            float screenCenter = screen != null
                ? parent.InverseTransformPoint(screen.TransformPoint(screen.rect.center)).x
                : parent.rect.center.x;
            float halfPanelWidth = Mathf.Abs(_actionPointsPanel.rect.width * _actionPointsPanel.localScale.x) * 0.5f;
            float leftWidth = halfPanelWidth + GetPreferredRowWidth(_alliesRoot, _allies.Count, parent);
            float rightWidth = halfPanelWidth + GetPreferredRowWidth(_enemiesRoot, _enemies.Count, parent);

            if (screenCenter - leftWidth >= left && screenCenter + rightWidth <= right)
            {
                center = screenCenter;
            }

            position.x = center - centerOffset;
            _actionPointsPanel.localPosition = position;
            _availableLeft = parent.TransformPoint(new Vector3(left, position.y, 0));
            _availableRight = parent.TransformPoint(new Vector3(right, position.y, 0));
            _hasAvailableBounds = true;
        }

        /// <summary>
        /// 縮小前のアイコン列とパネル間余白を、行動力パネルの親座標で計算します。
        /// </summary>
        private float GetPreferredRowWidth(RectTransform root, int count, RectTransform parent)
        {
            if (root == null || count == 0)
            {
                return 0;
            }

            Vector3 panelHeight = _actionPointsPanel.TransformVector(
                Vector3.up * _actionPointsPanel.rect.height);
            float height = Mathf.Abs(root.InverseTransformVector(panelHeight).y);
            float size = Mathf.Min(_iconSize, Mathf.Max(1.0f, height));
            float rowWidth = count * size + (count - 1) * _spacing;
            float width = Mathf.Abs(
                parent.InverseTransformVector(
                root.TransformVector(
                Vector3.right * rowWidth)).x);
            float gap = Mathf.Abs(
                parent.InverseTransformVector(
                root.parent.TransformVector(
                Vector3.right * _panelGap)).x);

            return width + gap;
        }

        /// <summary>
        /// 行の内側をパネルの端へ合わせ、中央の高さと表示高さを揃えます。
        /// </summary>
        private void AlignRowToPanel(RectTransform root, bool isAlly)
        {
            if (root == null || root.parent == null)
            {
                return;
            }

            Vector3 minimum = root.parent.InverseTransformPoint(_panelCorners[0]);
            Vector3 maximum = minimum;

            for (int index = 1; index < _panelCorners.Length; index++)
            {
                Vector3 corner = root.parent.InverseTransformPoint(_panelCorners[index]);

                minimum = Vector3.Min(minimum, corner);
                maximum = Vector3.Max(maximum, corner);
            }

            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(root.localScale.y));

            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, (maximum.y - minimum.y) / scaleY);
            root.pivot = new Vector2(isAlly ? 1.0f : 0.0f, 0.5f);
            Vector3 position = root.localPosition;

            position.x = isAlly ? minimum.x - _panelGap : maximum.x + _panelGap;
            position.y = (minimum.y + maximum.y) * 0.5f;
            root.localPosition = position;

            if (_hasAvailableBounds)
            {
                float outer = root.parent.InverseTransformPoint(isAlly ? _availableLeft : _availableRight).x;
                float width = isAlly ? position.x - outer : outer - position.x;

                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    Mathf.Max(0, width) / Mathf.Max(0.0001f, Mathf.Abs(root.localScale.x)));
            }
        }

        /// <summary>
        /// 死亡者の表示を保持し、離脱した生存者を除いてプレイヤーID順に並べます。
        /// </summary>
        private void RefreshRows()
        {
            _removedPlayerIds.Clear();
            _allies.Clear();
            _enemies.Clear();

            foreach (KeyValuePair<int, TeamRosterIconView> pair in _icons)
            {
                TeamRosterIconView icon = pair.Value;

                if (!_seenPlayerIds.Contains(pair.Key) && !icon.IsDead)
                {
                    _removedPlayerIds.Add(pair.Key);
                    Destroy(icon.gameObject);
                    continue;
                }

                bool isAlly = icon.TeamId == _localTeamId;

                if (!_seenPlayerIds.Contains(pair.Key))
                {
                    icon.Refresh(isAlly, GetFrameColor(icon.TeamId));
                }

                (isAlly ? _allies : _enemies).Add(icon);
            }

            foreach (int playerId in _removedPlayerIds)
            {
                _icons.Remove(playerId);
            }

            _allies.Sort(ComparePlayerId);
            _enemies.Sort(ComparePlayerId);
            AlignRowsToPanel();
            ArrangeRow(_allies, _alliesRoot, true);
            ArrangeRow(_enemies, _enemiesRoot, false);
        }

        /// <summary>
        /// 生成順や被弾に左右されない順序で比較します。
        /// </summary>
        private static int ComparePlayerId(TeamRosterIconView left, TeamRosterIconView right)
        {
            return left.PlayerId.CompareTo(right.PlayerId);
        }

        /// <summary>
        /// 中央の行動力に接する位置から左右へ並べ、人数が多い場合は領域内へ縮小します。
        /// </summary>
        private void ArrangeRow(List<TeamRosterIconView> icons, RectTransform root, bool alignRight)
        {
            if (icons.Count == 0)
            {
                return;
            }

            float cellWidth = Mathf.Max(0.0f, root.rect.width) / icons.Count;
            float spacing = Mathf.Min(_spacing, cellWidth * 0.15f);
            float size = Mathf.Min(_iconSize, Mathf.Max(1.0f, cellWidth - spacing));

            size = Mathf.Min(size, Mathf.Max(1.0f, root.rect.height));

            for (int index = 0; index < icons.Count; index++)
            {
                RectTransform rect = icons[index].RectTransform;

                if (rect.parent != root)
                {
                    rect.SetParent(root, false);
                }

                rect.anchorMin = rect.anchorMax = new Vector2(alignRight ? 1.0f : 0.0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                float offset = size * 0.5f + index * (size + spacing);

                rect.anchoredPosition = new Vector2(alignRight ? -offset : offset, 0.0f);
            }
        }

        /// <summary>
        /// 試合の管理元が入れ替わった場合に、前の試合のアイコンを破棄します。
        /// </summary>
        private void ClearIcons()
        {
            foreach (TeamRosterIconView icon in _icons.Values)
            {
                if (icon != null)
                {
                    Destroy(icon.gameObject);
                }
            }

            _icons.Clear();
            _localTeamId = -1;
        }
    }
}
