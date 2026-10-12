using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using PranaTactics.Pranam;

namespace PranaTactics.UI.Screens.PranamProfile
{
    /// <summary>
    /// シーンに配置した兵装一覧、説明および性能を実データから更新します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PranamProfileView : MonoBehaviour
    {
        [SerializeField]
        private PranamData[] _weapons = Array.Empty<PranamData>();

        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _englishName;

        [SerializeField]
        private TMP_Text _number;

        [SerializeField]
        private TMP_Text _category;

        [SerializeField]
        private TMP_Text _summary;

        [SerializeField]
        private TMP_Text _description;

        [SerializeField]
        private Image _artwork;

        [SerializeField]
        private TMP_Text _listStatus;

        [SerializeField]
        private PranamProfileRow[] _rows = Array.Empty<PranamProfileRow>();

        [SerializeField]
        private RectTransform _listContent;

        [SerializeField]
        private PranamListScroll _scroll;

        [SerializeField]
        private RoleFilterView[] _roleFilters = Array.Empty<RoleFilterView>();

        [Min(1.0f)]
        [SerializeField]
        private float _rowSpacing = 96.0f;

        private readonly List<PranamProfileRow> _listRows = new();

        private List<PranamData> _visibleWeapons = new();

        private PranamRole _activeRole = PranamRole.All;

        [SerializeField]
        private MetricRow[] _metrics = Array.Empty<MetricRow>();

        private readonly List<PranamData> _catalog = new();

        private readonly List<GraphicRaycaster> _suspendedRaycasters = new();

        private string _returnSceneName;

        private int _selectedIndex;

        private bool _isClosing;

        public bool CanInteract => isActiveAndEnabled &&
            !SceneManager.GetSceneByName("GamePlayModeUI").isLoaded &&
            (SettingsUIManager.Instance == null || !SettingsUIManager.Instance.IsOpen) && !_isClosing;

        /// <summary>
        /// データを検証し、遷移時に指定された兵装を初期選択します。
        /// </summary>
        private void Awake()
        {
            _returnSceneName = PranamProfileNavigation.ReturnSceneName;

            foreach (PranamData weapon in _weapons)
            {
                if (IsBrowsable(weapon) && !_catalog.Contains(weapon))
                {
                    _catalog.Add(weapon);
                }
            }

            PranamData requested = PranamProfileNavigation.RequestedWeapon;

            if (IsBrowsable(requested) && !_catalog.Contains(requested))
            {
                _catalog.Add(requested);
            }

            List<PranamData> ordered = PranamProfileCatalog.Order(_catalog);
            _catalog.Clear();
            _catalog.AddRange(ordered);
            PrepareInput();
            PrepareRows();
            ApplyFilter(requested);
        }

        /// <summary>
        /// 空スロット用データと表示名のないデータを一覧から除外します。
        /// </summary>
        private static bool IsBrowsable(PranamData weapon)
        {
            return weapon != null && !string.IsNullOrWhiteSpace(weapon.DisplayName) &&
                !string.Equals(weapon.DisplayName, "NONE", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 既存UI入力を使用し、背面のロビーとキャラクター画面への入力を停止します。
        /// </summary>
        private void PrepareInput()
        {
            if (UIManager.Instance == null)
            {
                new GameObject("Profile UI Manager", typeof(UIManager));
            }

            if (EventSystem.current == null)
            {
                var input = new GameObject("Profile Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                SceneManager.MoveGameObjectToScene(input, gameObject.scene);
            }

            foreach (string sceneName in new[] { "LobbyScene", "CharacterProfile" })
            {
                Scene scene = SceneManager.GetSceneByName(sceneName);

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (GraphicRaycaster raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true))
                    {
                        if (raycaster.enabled)
                        {
                            _suspendedRaycasters.Add(raycaster);
                            raycaster.enabled = false;
                        }
                    }
                }
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// 表示中のリスト行が有効な兵装に対応するか確認します。
        /// </summary>
        public bool CanSelectRow(int row)
        {
            return row >= 0 && row < _listRows.Count && row < _visibleWeapons.Count;
        }

        /// <summary>
        /// 表示中の行に対応する兵装へ説明を切り替えます。
        /// </summary>
        public void SelectRow(int row)
        {
            if (!CanInteract || !CanSelectRow(row))
            {
                return;
            }

            _selectedIndex = row;
            Refresh();
        }

        /// <summary>
        /// カタログ全体に必要な行を用意し、既存UIへの登録前に参照を設定します。
        /// </summary>
        private void PrepareRows()
        {
            _listRows.AddRange(_rows);

            if (_listRows.Count == 0)
            {
                Debug.LogError("兵装一覧の行が設定されていません。", this);
                return;
            }

            while (_listRows.Count < _catalog.Count)
            {
                PranamProfileRow row = Instantiate(_rows[0], _listContent);
                row.name = "Weapon Row " + (_listRows.Count + 1);
                _listRows.Add(row);
            }

            for (int index = 0; index < _listRows.Count; index++)
            {
                _listRows[index].Configure(this, index, _rowSpacing);
            }
        }

        /// <summary>
        /// ロールで絞り込み、同じロールの再クリックで全兵装表示へ戻します。
        /// </summary>
        public void SelectRole(PranamRole role)
        {
            if (!CanInteract || role < PranamRole.Attacker || role > PranamRole.Option)
            {
                return;
            }

            PranamData selected = _visibleWeapons.Count == 0 ? null : _visibleWeapons[_selectedIndex];
            _activeRole = _activeRole == role ? PranamRole.All : role;
            ApplyFilter(selected);
        }

        /// <summary>
        /// 該当する兵装へ一覧を置き換え、選択を維持できる場合は表示枠内へ移動します。
        /// </summary>
        private void ApplyFilter(PranamData preferred)
        {
            _visibleWeapons = PranamProfileCatalog.Filter(_catalog, _activeRole);
            _selectedIndex = Mathf.Max(0, _visibleWeapons.IndexOf(preferred));
            Refresh();
            float rowHeight = _listRows.Count == 0 ? _rowSpacing : _listRows[0].Rect.rect.height;
            float height = _visibleWeapons.Count == 0 ? 0.0f : (_visibleWeapons.Count - 1) * _rowSpacing + rowHeight;
            _scroll.SetContentHeight(height);

            if (_visibleWeapons.Count > 0 && _selectedIndex < _listRows.Count)
            {
                _scroll.EnsureVisible(_listRows[_selectedIndex].Rect);
            }

            foreach (RoleFilterView filter in _roleFilters)
            {
                filter.Show(_activeRole);
            }
        }

        /// <summary>
        /// 選択中の兵装に対応する情報と選択枠を描画します。
        /// </summary>
        private void Refresh()
        {
            PranamData weapon = _visibleWeapons.Count == 0 ? null : _visibleWeapons[_selectedIndex];
            PranamProfileMetrics metrics = weapon == null ? null : PranamProfileMetrics.Create(weapon);
            _name.text = weapon != null ? weapon.DisplayName : "該当する兵装はありません";
            _englishName.text = weapon != null ? weapon.ProfileEnglishName : string.Empty;
            _number.text = weapon != null ? $"WEAPON / {_catalog.IndexOf(weapon) + 1:00}" : "WEAPON / --";
            _category.text = metrics?.Category ?? "—";
            _summary.text = weapon != null ? weapon.ProfileSummary : string.Empty;
            _description.text = weapon != null && !string.IsNullOrWhiteSpace(weapon.Description)
                ? weapon.Description : "この兵装の説明は未登録です。";
            _artwork.sprite = weapon != null ? weapon.ProfileSprite : null;
            _artwork.enabled = _artwork.sprite != null;
            _listStatus.text = $"{_activeRole.ToString().ToUpperInvariant()} / {_visibleWeapons.Count:00}";

            for (int index = 0; index < _metrics.Length; index++)
            {
                float? value = metrics != null && index < metrics.Values.Count ? metrics.Values[index] : null;
                _metrics[index].Show(value);

                if (index == 2)
                {
                    _metrics[index].SetLabel(metrics?.PowerLabel ?? "攻撃力");
                }
            }

            for (int row = 0; row < _listRows.Count; row++)
            {
                _listRows[row].Show(row < _visibleWeapons.Count ? _visibleWeapons[row] : null, row == _selectedIndex);
            }
        }

        /// <summary>
        /// 遷移元のシーンを再利用し、兵装プロフィールだけを閉じます。
        /// </summary>
        public void Close()
        {
            if (!CanInteract)
            {
                return;
            }

            _isClosing = true;
            Scene target = SceneManager.GetSceneByName(_returnSceneName ?? "LobbyScene");

            if (!target.IsValid() || !target.isLoaded)
            {
                target = SceneManager.GetSceneByName("LobbyScene");
            }

            if (target.IsValid() && target.isLoaded)
            {
                SceneManager.SetActiveScene(target);
                SceneManager.UnloadSceneAsync(gameObject.scene);
            }
            else
            {
                SceneManager.LoadScene("LobbyScene");
            }
        }

        /// <summary>
        /// 自身が停止したレイキャスターだけを復元します。
        /// </summary>
        private void OnDestroy()
        {
            foreach (GraphicRaycaster raycaster in _suspendedRaycasters)
            {
                if (raycaster != null)
                {
                    raycaster.enabled = true;
                }
            }
        }

        /// <summary>
        /// ロール絞り込みの選択表示を保持します。
        /// </summary>
        [Serializable]
        private sealed class RoleFilterView
        {
            [SerializeField]
            private PranamRole _role;

            [SerializeField]
            private GameObject _selection;

            /// <summary>
            /// 選択中のロールだけを強調します。
            /// </summary>
            public void Show(PranamRole selected)
            {
                _selection.SetActive(_role == selected);
            }
        }

        /// <summary>
        /// 性能の数値と、表示上限に対するバーを保持します。
        /// </summary>
        [Serializable]
        private sealed class MetricRow
        {
            [SerializeField]
            private TMP_Text _label;

            [SerializeField]
            private TMP_Text _value;

            [SerializeField]
            private RectTransform _fill;

            [SerializeField]
            private string _suffix = string.Empty;

            [Min(0.01f)]
            [SerializeField]
            private float _maximum = 100.0f;

            /// <summary>
            /// 攻撃型と防御型に合わせて項目名を切り替えます。
            /// </summary>
            public void SetLabel(string label)
            {
                _label.text = label;
            }

            /// <summary>
            /// 未設定の値をゼロと区別し、不正な数値がバーへ渡らないよう補正します。
            /// </summary>
            public void Show(float? value)
            {
                bool valid = value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value);
                float number = valid ? Mathf.Max(0.0f, value.Value) : 0.0f;
                _value.text = valid ? number.ToString("0.#", CultureInfo.InvariantCulture) + _suffix : "—";
                float maximum = float.IsNaN(_maximum) || float.IsInfinity(_maximum) ? 1.0f : Mathf.Max(0.01f, _maximum);
                RectTransform track = (RectTransform)_fill.parent;
                _fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, track.rect.width * Mathf.Clamp01(number / maximum));
            }
        }
    }
}
