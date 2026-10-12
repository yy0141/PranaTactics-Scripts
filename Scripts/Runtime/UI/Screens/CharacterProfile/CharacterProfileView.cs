using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using PranaTactics.Pranam;
using PranaTactics.UI.Screens.PranamProfile;
using PranaTactics.Unit;

namespace PranaTactics.UI.Screens.CharacterProfile
{
    /// <summary>
    /// シーンに配置されたUIへキャラクターデータを表示し、閲覧対象を切り替えます。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterProfileView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField]
        private UnitParams[] _characters = Array.Empty<UnitParams>();

        [SerializeField]
        private IconData _roleIcons;

        [SerializeField]
        private PranamData[] _pranamData = Array.Empty<PranamData>();

        [Header("Profile")]
        [SerializeField]
        private TMP_Text _name;

        [SerializeField]
        private TMP_Text _romanizedName;

        [SerializeField]
        private TMP_Text _number;

        [SerializeField]
        private TMP_Text _largeNumber;

        [SerializeField]
        private TMP_Text _role;

        [SerializeField]
        private TMP_Text _roleDescription;

        [SerializeField]
        private TMP_Text _story;

        [SerializeField]
        private Image _portrait;

        [SerializeField]
        private Image _background;

        [SerializeField]
        private AspectRatioFitter _backgroundAspect;

        [SerializeField]
        private AspectRatioFitter _portraitAspect;

        [SerializeField]
        private Image _roleIcon;

        [Header("Statistics")]
        [SerializeField]
        private StatView[] _stats = Array.Empty<StatView>();

        [Header("Pranam")]
        [SerializeField]
        private Image[] _pranamIcons = Array.Empty<Image>();

        [SerializeField]
        private TMP_Text[] _pranamNames = Array.Empty<TMP_Text>();

        [Header("Selection")]
        [SerializeField]
        private ThumbnailView[] _thumbnails = Array.Empty<ThumbnailView>();

        [SerializeField]
        private TMP_Text _selectionCount;

        private readonly List<UnitParams> _availableCharacters = new();

        private readonly Dictionary<string, Sprite> _pranamSprites = new(StringComparer.Ordinal);

        private readonly List<GraphicRaycaster> _suspendedRaycasters = new();

        private int _selectedIndex;

        private bool _isClosing;

        public bool CanInteract => isActiveAndEnabled &&
            !SceneManager.GetSceneByName("GamePlayModeUI").isLoaded &&
            (SettingsUIManager.Instance == null || !SettingsUIManager.Instance.IsOpen) && !_isClosing && !PranamProfileNavigation.IsOpenOrLoading;

        /// <summary>
        /// 閲覧対象と装備画像を検証し、直接シーンを再生する場合の入力を用意します。
        /// </summary>
        private void Awake()
        {
            SuspendLobbyInput();

            if (UIManager.Instance == null)
            {
                new GameObject("Profile UI Manager", typeof(UIManager));
            }

            foreach (UnitParams character in _characters)
            {
                if (character != null && character.CanUsePlayer && !_availableCharacters.Contains(character))
                {
                    _availableCharacters.Add(character);
                }
            }

            foreach (PranamData pranam in _pranamData)
            {
                if (pranam != null && !string.IsNullOrWhiteSpace(pranam.DisplayName))
                {
                    _pranamSprites[pranam.DisplayName] = pranam.Icon;
                }
            }

            if (EventSystem.current == null)
            {
                var input = new GameObject("Profile Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                SceneManager.MoveGameObjectToScene(input, gameObject.scene);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// 背面ロビーの独自クリック処理へ入力が透過しないようにします。
        /// </summary>
        private void SuspendLobbyInput()
        {
            Scene lobby = SceneManager.GetSceneByName("LobbyScene");

            if (!lobby.IsValid() || !lobby.isLoaded)
            {
                return;
            }

            foreach (GameObject root in lobby.GetRootGameObjects())
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

        /// <summary>
        /// プロフィール終了時に、自身が停止したロビー入力だけを復元します。
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
        /// シーン内のUIへ初期表示を反映します。
        /// </summary>
        private void OnEnable()
        {
            ShowSelectedCharacter();
        }

        /// <summary>
        /// キーボードによるキャラクター切り替えを処理します。
        /// </summary>
        private void Update()
        {
            if (!CanInteract)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Previous();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Next();
            }
        }

        /// <summary>
        /// 前のキャラクターへ循環して切り替えます。
        /// </summary>
        public void Previous()
        {
            SelectCharacter(_selectedIndex - 1);
        }

        /// <summary>
        /// 次のキャラクターへ循環して切り替えます。
        /// </summary>
        public void Next()
        {
            SelectCharacter(_selectedIndex + 1);
        }

        /// <summary>
        /// 表示中のページにあるサムネイルを選択します。
        /// </summary>
        public void SelectThumbnail(int slot)
        {
            if (!CanInteract || _thumbnails.Length == 0 || slot < 0 || slot >= _thumbnails.Length)
            {
                return;
            }

            int pageStart = _selectedIndex / _thumbnails.Length * _thumbnails.Length;
            if (pageStart + slot < _availableCharacters.Count)
            {
                SelectCharacter(pageStart + slot);
            }
        }

        /// <summary>
        /// キャラクター一覧内でインデックスを循環させ、UIを更新します。
        /// </summary>
        private void SelectCharacter(int index)
        {
            if (!CanInteract || _availableCharacters.Count == 0)
            {
                return;
            }

            _selectedIndex = (index % _availableCharacters.Count + _availableCharacters.Count) % _availableCharacters.Count;
            ShowSelectedCharacter();
        }

        /// <summary>
        /// シーン内の表示へ選択したUnitParamsを反映します。
        /// </summary>
        private void ShowSelectedCharacter()
        {
            if (_availableCharacters.Count == 0)
            {
                _name.text = "キャラクター未登録";
                _romanizedName.text = string.Empty;
                _role.text = string.Empty;
                _roleDescription.text = string.Empty;
                _story.text = "CharacterProfileView の Characters に UnitParams を設定してください。";
                _number.text = "UNIT / --";
                _largeNumber.text = "--";
                _portrait.enabled = false;
                _background.enabled = false;
                _roleIcon.enabled = false;
                _selectionCount.text = "0 / 0";

                foreach (StatView stat in _stats)
                {
                    stat.SetValue(0.0f);
                }

                BindPranam(null);
                BindThumbnails();
                return;
            }

            UnitParams character = _availableCharacters[_selectedIndex];
            _name.text = character.UnitName;
            _romanizedName.text = character.RomanizedName;
            _number.text = $"UNIT / {_selectedIndex + 1:00}";
            _largeNumber.text = $"{_selectedIndex + 1:00}";
            _role.text = character.Type.ToString().ToUpperInvariant();
            _roleDescription.text = character.RoleDescription;
            _story.text = string.IsNullOrWhiteSpace(character.Story) ? "プロフィールは未登録です。" : character.Story;
            SetSprite(_portrait, character.ProfileCharacterSprite);
            SetSprite(_background, character.ProfileBackgroundSprite);

            if (_background.sprite != null)
            {
                _backgroundAspect.aspectRatio = _background.sprite.rect.width / _background.sprite.rect.height;
            }

            if (_portrait.sprite != null)
            {
                _portraitAspect.aspectRatio = _portrait.sprite.rect.width / _portrait.sprite.rect.height;
            }

            SetSprite(_roleIcon, _roleIcons != null ? _roleIcons.GetSprite(character.Type.ToString()) : null);
            float[] values = { character.PranaCapacity, character.MoveSpeed, character.InitialProactiveness };

            for (int index = 0; index < _stats.Length && index < values.Length; index++)
            {
                _stats[index].SetValue(values[index]);
            }

            _selectionCount.text = $"{_selectedIndex + 1:00} / {_availableCharacters.Count:00}";
            BindPranam(character);
            BindThumbnails();
        }

        /// <summary>
        /// MainとSubの装備を名前で検索し、画像と名称を更新します。
        /// </summary>
        private void BindPranam(UnitParams character)
        {
            for (int index = 0; index < _pranamIcons.Length; index++)
            {
                string name = "NONE";

                if (character != null)
                {
                    character.TryGetPranamName(index < 4, index % 4, out name);
                }

                bool hasPranam = !string.IsNullOrWhiteSpace(name) && name != "NONE";
                Sprite sprite = null;

                if (hasPranam)
                {
                    _pranamSprites.TryGetValue(name, out sprite);
                }

                SetSprite(_pranamIcons[index], sprite);

                if (index < _pranamNames.Length)
                {
                    _pranamNames[index].text = hasPranam ? name : "—";
                }
            }
        }

        /// <summary>
        /// 表示中キャラクターの装備スロットに対応する兵装データを取得します。
        /// </summary>
        /// <param name="slot">
        /// Mainの0～3、Subの4～7を表すスロット番号です。
        /// </param>
        /// <param name="pranam">
        /// 登録済みの兵装データです。
        /// </param>
        public bool TryGetPranam(int slot, out PranamData pranam)
        {
            pranam = null;

            if (slot < 0 || slot >= 8 || _availableCharacters.Count == 0 ||
                !_availableCharacters[_selectedIndex].TryGetPranamName(slot < 4, slot % 4, out string name) ||
                string.IsNullOrWhiteSpace(name) || string.Equals(name, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (PranamData candidate in _pranamData)
            {
                if (candidate != null && string.Equals(candidate.DisplayName, name, StringComparison.Ordinal))
                {
                    pranam = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// サムネイルをページ単位で表示し、選択中の枠を強調します。
        /// </summary>
        private void BindThumbnails()
        {
            if (_thumbnails.Length == 0)
            {
                return;
            }

            int pageStart = _selectedIndex / _thumbnails.Length * _thumbnails.Length;

            for (int slot = 0; slot < _thumbnails.Length; slot++)
            {
                int index = pageStart + slot;
                ThumbnailView thumbnail = _thumbnails[slot];
                thumbnail.Click.gameObject.SetActive(index < _availableCharacters.Count);

                if (index >= _availableCharacters.Count)
                {
                    continue;
                }

                UnitParams character = _availableCharacters[index];
                SetSprite(thumbnail.Image, character.ProfileSelectionSprite);
                thumbnail.Selection.SetActive(index == _selectedIndex);
            }
        }

        /// <summary>
        /// 未設定の画像を白い矩形にせず非表示にします。
        /// </summary>
        private static void SetSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        /// <summary>
        /// ロビーへ戻り、プロフィールのシーンだけを解放します。
        /// </summary>
        public void Close()
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;
            Scene lobby = SceneManager.GetSceneByName("LobbyScene");

            if (lobby.IsValid() && lobby.isLoaded)
            {
                SceneManager.SetActiveScene(lobby);
                SceneManager.UnloadSceneAsync(gameObject.scene);
            }
            else
            {
                SceneManager.LoadScene("LobbyScene");
            }
        }

        /// <summary>
        /// 能力値の文字と、Inspectorで指定する上限に対するバーを保持します。
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
            /// 不正な値を補正し、数値とバーを更新します。
            /// </summary>
            public void SetValue(float value)
            {
                float safeValue = float.IsNaN(value) || float.IsInfinity(value) ? 0.0f : Mathf.Max(0.0f, value);
                _value.text = safeValue.ToString("0.#", CultureInfo.InvariantCulture);
                float ratio = Mathf.Clamp01(safeValue / Mathf.Max(0.01f, _maximum));
                RectTransform track = (RectTransform)_fill.transform.parent;
                _fill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, track.rect.width * ratio);
            }
        }

        /// <summary>
        /// サムネイルのクリック処理、画像、選択表示をまとめます。
        /// </summary>
        [Serializable]
        private sealed class ThumbnailView
        {
            public CharacterProfileClick Click;
            public Image Image;
            public GameObject Selection;
        }
    }
}
