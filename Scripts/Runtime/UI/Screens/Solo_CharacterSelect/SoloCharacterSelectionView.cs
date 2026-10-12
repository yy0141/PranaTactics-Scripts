using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.GameFlow.States;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// ソロ専用画面のキャラクター紹介、兵装タブ、能力値と一覧送りを表示します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoloCharacterSelectionView : MonoBehaviour
    {
        [SerializeField]
        private SoloCharacterSelectUIManager _selection;

        [SerializeField]
        private SoloCharacterModelViewport _modelViewport;

        /// <summary>
        /// キャラクターデータのソロ用背景を全面表示する画像です。
        /// </summary>
        [SerializeField]
        private Image _background;

        [SerializeField]
        private TMP_Text _role;

        [SerializeField]
        private TMP_Text _romanizedName;

        [SerializeField]
        private TMP_Text _introduction;

        [SerializeField]
        private CharacterSelectIcon[] _icons = Array.Empty<CharacterSelectIcon>();

        [SerializeField]
        private GameObject[] _panels = Array.Empty<GameObject>();

        [SerializeField]
        private Image[] _tabs = Array.Empty<Image>();

        /// <summary>
        /// 兵装一覧の各スロットへ表示する消費行動力です。
        /// </summary>
        [SerializeField]
        private TMP_Text[] _weaponActionCosts = Array.Empty<TMP_Text>();

        [SerializeField]
        private TMP_Text[] _equipmentCounts = Array.Empty<TMP_Text>();

        /// <summary>
        /// 兵装をマウスオーバーしている間だけ左側に表示する詳細パネルです。
        /// </summary>
        [SerializeField]
        private GameObject _weaponDetails;

        [SerializeField]
        private Image _weaponIcon;

        [SerializeField]
        private TMP_Text _weaponName;

        [SerializeField]
        private TMP_Text _weaponSummary;

        [SerializeField]
        private TMP_Text _weaponDescription;

        [SerializeField]
        private TMP_Text _weaponCost;

        [SerializeField]
        private TMP_Text _characterDescription;

        [SerializeField]
        private StatView[] _stats = Array.Empty<StatView>();

        private readonly List<UnitParams> _roster = new();
        private readonly PranamData[] _weapons = new PranamData[8];
        private GameDataRepository _repository;
        private Coroutine _initializeCoroutine;
        private int _hoveredWeaponSlot = -1;
        private int _currentTab;
        private bool _isClosing;

        public bool CanInteract => _selection != null && _selection.CanSelectCharacter && !_isClosing &&
            (SettingsUIManager.Instance == null || !SettingsUIManager.Instance.IsOpen);

        /// <summary>
        /// 選択通知を購読し、データの読み込み後に一覧を初期化します。
        /// </summary>
        private void OnEnable()
        {
            if (_selection == null)
            {
                Debug.LogError("ソロ選択画面の管理コンポーネントが設定されていません。", this);
                return;
            }

            _isClosing = false;
            _selection.CharacterChanged += OnCharacterChanged;
            SetTab(0);
            _initializeCoroutine = StartCoroutine(InitializeRoster());
        }

        /// <summary>
        /// 画面を離れるときに購読と初期化待機を停止します。
        /// </summary>
        private void OnDisable()
        {
            HideWeaponDetails();
            if (_selection != null)
            {
                _selection.CharacterChanged -= OnCharacterChanged;
            }

            if (_initializeCoroutine != null)
            {
                StopCoroutine(_initializeCoroutine);
                _initializeCoroutine = null;
            }
        }

        /// <summary>
        /// データ取得完了後、既存アイコンと同じ順序で一覧と初期選択を設定します。
        /// </summary>
        private IEnumerator InitializeRoster()
        {
            while (GameDataRepository.Instance == null || !GameDataRepository.Instance.IsLoaded)
            {
                yield return null;
            }

            _repository = GameDataRepository.Instance;
            _roster.Clear();
            _roster.AddRange(_repository.GetPlayerCharactersData());

            yield return null;
            _initializeCoroutine = null;
            if (_selection.SelectedCharacterData != null)
            {
                OnCharacterChanged(_selection.SelectedCharacterData);
            }
            else if (_roster.Count > 0 && _icons.Length > 0)
            {
                _selection.TrySelectCharacter(_icons[0], _roster[0]);
            }
        }

        /// <summary>
        /// 選択されたキャラクターの紹介、プレビューと装備を更新します。
        /// </summary>
        private void OnCharacterChanged(UnitParams character)
        {
            _repository = _repository != null ? _repository : GameDataRepository.Instance;
            _role.text = character != null ? character.Type.ToString().ToUpperInvariant() : "UNIT";
            _romanizedName.text = character != null ? character.RomanizedName : string.Empty;
            _introduction.text = character != null ? character.RoleDescription : "キャラクターを選択してください。";
            _characterDescription.text = character != null && !string.IsNullOrWhiteSpace(character.Story)
                ? character.Story
                : _introduction.text;
            _background.sprite = character != null ? character.SoloSelectionBackgroundSprite : null;
            _background.enabled = _background.sprite != null;
            _modelViewport.Show(_selection.SelectedCharacterModel);
            float[] values = character != null
                ? new[] { character.PranaCapacity, character.MoveSpeed, character.InitialProactiveness }
                : new float[3];
            for (int index = 0; index < _stats.Length && index < values.Length; index++)
            {
                _stats[index].SetValue(values[index]);
            }

            HideWeaponDetails();
            BindWeapons(character);
        }

        /// <summary>
        /// MainとSubの8スロットへ、消費行動力と装備数を反映します。
        /// </summary>
        private void BindWeapons(UnitParams character)
        {
            int mainCount = 0;
            int subCount = 0;
            for (int slot = 0; slot < _weapons.Length; slot++)
            {
                PranamData weapon = null;
                if (character != null && _repository != null &&
                    character.TryGetPranamName(slot < 4, slot % 4, out string name) &&
                    !string.IsNullOrWhiteSpace(name) && !string.Equals(name, "NONE", StringComparison.OrdinalIgnoreCase))
                {
                    weapon = _repository.GetPranamData(name);
                }

                _weapons[slot] = weapon;
                _weaponActionCosts[slot].text = weapon != null ? $"消費行動力  {weapon.ActionPointCost}" : string.Empty;
                if (weapon != null)
                {
                    if (slot < 4)
                    {
                        mainCount++;
                    }
                    else
                    {
                        subCount++;
                    }
                }
            }

            _equipmentCounts[0].text = $"{mainCount} / 4";
            _equipmentCounts[1].text = $"{subCount} / 4";
        }

        /// <summary>
        /// 左側の詳細パネルへ表示する兵装の概要を取得します。
        /// </summary>
        private static string GetSummary(PranamData weapon)
        {
            return !string.IsNullOrWhiteSpace(weapon.ProfileSummary)
                ? weapon.ProfileSummary.Replace("\n", " ")
                : weapon.Description;
        }

        /// <summary>
        /// マウスオーバーしている有効な兵装の詳細を左側へ表示します。
        /// </summary>
        /// <param name="slot">
        /// Mainの0～3またはSubの4～7を表す装備スロットです。
        /// </param>
        public void ShowWeaponDetails(int slot)
        {
            if (!CanInteract || _currentTab != 0 || slot < 0 || slot >= _weapons.Length || _weapons[slot] == null)
            {
                HideWeaponDetails();
                return;
            }

            if (_hoveredWeaponSlot == slot && _weaponDetails.activeSelf)
            {
                return;
            }

            PranamData weapon = _weapons[slot];
            _hoveredWeaponSlot = slot;
            _weaponIcon.sprite = weapon.Icon;
            _weaponIcon.enabled = weapon.Icon != null;
            _weaponName.text = weapon.DisplayName;
            _weaponSummary.text = GetSummary(weapon);
            _weaponDescription.text = weapon.Description;
            _weaponCost.text = $"消費行動力  {weapon.ActionPointCost}\n消費プラーナ  {weapon.PranaCost}";
            _weaponDetails.SetActive(true);
        }

        /// <summary>
        /// 終了したスロットの詳細だけを閉じ、別の兵装への移動を妨げないようにします。
        /// </summary>
        public void HideWeaponDetails(int slot)
        {
            if (_hoveredWeaponSlot == slot)
            {
                HideWeaponDetails();
            }
        }

        /// <summary>
        /// 詳細表示を閉じてマウスオーバー中の兵装を解除します。
        /// </summary>
        private void HideWeaponDetails()
        {
            _hoveredWeaponSlot = -1;
            if (_weaponDetails != null)
            {
                _weaponDetails.SetActive(false);
            }
        }

        /// <summary>
        /// 選択確定や設定画面の表示中は兵装の詳細を閉じます。
        /// </summary>
        private void LateUpdate()
        {
            if (_hoveredWeaponSlot >= 0 && !CanInteract)
            {
                HideWeaponDetails();
            }
        }

        /// <summary>
        /// タブとキャラクター一覧の操作を実行します。
        /// </summary>
        /// <param name="action">
        /// 0はタブ、2と3は一覧送り、4は戻る操作です。
        /// </param>
        /// <param name="index">
        /// 兵装一覧は0、ステータスは1です。
        /// </param>
        public void Execute(int action, int index)
        {
            if (!CanInteract)
            {
                return;
            }

            switch (action)
            {
                case 0:
                    SetTab(index);
                    break;
                case 2:
                    SelectRelative(-1);
                    break;
                case 3:
                    SelectRelative(1);
                    break;
                case 4:
                    ReturnToStageSelection();
                    break;
            }
        }

        /// <summary>
        /// 有効なタブだけを表示し、選択中の見出しをシアンで強調します。
        /// </summary>
        private void SetTab(int index)
        {
            if (index < 0 || index >= _panels.Length)
            {
                return;
            }

            HideWeaponDetails();
            _currentTab = index;
            for (int tab = 0; tab < _panels.Length; tab++)
            {
                _panels[tab].SetActive(tab == _currentTab);
                _tabs[tab].color = tab == _currentTab
                    ? new Color(0.055f, 0.27f, 0.36f, 0.96f)
                    : new Color(0.025f, 0.075f, 0.1f, 0.94f);
            }
        }

        /// <summary>
        /// 既存アイコンの選択APIを使用して、前後のキャラクターへ循環します。
        /// </summary>
        private void SelectRelative(int direction)
        {
            int count = Mathf.Min(_roster.Count, _icons.Length);
            if (count == 0)
            {
                return;
            }

            int current = _roster.IndexOf(_selection.SelectedCharacterData);
            int next = (current + direction + count) % count;
            _selection.TrySelectCharacter(_icons[next], _roster[next]);
        }

        /// <summary>
        /// 準備状態を解除し、既存のゲームフロー経由でステージ選択へ戻ります。
        /// </summary>
        private void ReturnToStageSelection()
        {
            _isClosing = true;
            _selection.CancelConfirmation();
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.ChangeState(new SoloStageSelectState());
            }
            else
            {
                SceneManager.LoadScene("SoloStageSelectScene");
            }
        }

        /// <summary>
        /// 能力値を実数と水平バーで表示します。
        /// </summary>
        [Serializable]
        private sealed class StatView
        {
            [SerializeField]
            private TMP_Text _value;

            [SerializeField]
            private Image _fill;

            [Min(0.01f)]
            [SerializeField]
            private float _maximum = 100.0f;

            /// <summary>
            /// 不正値を補正して能力値の表示を更新します。
            /// </summary>
            public void SetValue(float value)
            {
                float safeValue = float.IsNaN(value) || float.IsInfinity(value) ? 0.0f : Mathf.Max(0.0f, value);
                _value.text = safeValue.ToString("0.#", CultureInfo.InvariantCulture);
                _fill.fillAmount = Mathf.Clamp01(safeValue / Mathf.Max(0.01f, _maximum));
            }
        }
    }
}
