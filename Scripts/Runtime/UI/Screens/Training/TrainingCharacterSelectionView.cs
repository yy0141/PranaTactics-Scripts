using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using PranaTactics.Core;
using PranaTactics.Unit;
using PranaTactics.UI.Interaction;

namespace PranaTactics.UI.Screens.Training
{
    /// <summary>
    /// 射撃訓練の入場時とF2でキャラクター選択画面を表示します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingCharacterSelectionView : MonoBehaviour
    {
        [SerializeField]
        private TMP_FontAsset _font;

        private GameObject _panel;
        private TMP_Text _status;
        private bool _isChanging;
        private float _previousTimeScale;
        private bool _previousCursorVisible;
        private CursorLockMode _previousCursorLock;
        private readonly List<GraphicRaycaster> _suspendedRaycasters = new();
        private static TrainingCharacterSelectionView s_instance;

        public static bool IsOpen => s_instance != null && s_instance._panel != null && s_instance._panel.activeSelf;
        public bool CanSelect => IsOpen && !_isChanging;

        /// <summary>
        /// データから選択カードを生成し、最初の選択を開始します。
        /// </summary>
        private void Start()
        {
            s_instance = this;
            if (UIManager.Instance == null)
            {
                new GameObject("UIManager").AddComponent<UIManager>();
            }

            if (EventSystem.current == null)
            {
                gameObject.AddComponent<EventSystem>();
                gameObject.AddComponent<StandaloneInputModule>();
            }

            BuildView();
            Open();
        }

        /// <summary>
        /// 設定画面が閉じているときF2で再選択を開始します。
        /// </summary>
        private void Update()
        {
            if (!IsOpen && Input.GetKeyDown(KeyCode.F2) &&
                (SettingsUIManager.Instance == null || !SettingsUIManager.Instance.IsOpen))
            {
                Open();
            }
        }

        /// <summary>
        /// 選択中はカーソルを選択UIで使用します。
        /// </summary>
        private void LateUpdate()
        {
            if (IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>
        /// シーン終了時にも入力と時間を復元します。
        /// </summary>
        private void OnDestroy()
        {
            if (s_instance == this)
            {
                if (IsOpen)
                {
                    Close();
                }

                s_instance = null;
            }
        }

        /// <summary>
        /// 背面のUI入力と訓練の進行を停止します。
        /// </summary>
        private void Open()
        {
            _status.text = "使用するキャラクターを選択してください。訓練中は F2 で変更できます。";
            _previousTimeScale = Time.timeScale;
            _previousCursorVisible = Cursor.visible;
            _previousCursorLock = Cursor.lockState;
            foreach (GraphicRaycaster raycaster in FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None))
            {
                if (raycaster.gameObject.scene != gameObject.scene && raycaster.enabled)
                {
                    _suspendedRaycasters.Add(raycaster);
                    raycaster.enabled = false;
                }
            }

            Time.timeScale = 0.0f;
            _panel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// 選択画面を閉じ、訓練を再開します。
        /// </summary>
        private void Close()
        {
            _panel.SetActive(false);
            Time.timeScale = _previousTimeScale;
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
            foreach (GraphicRaycaster raycaster in _suspendedRaycasters)
            {
                if (raycaster != null)
                {
                    raycaster.enabled = true;
                }
            }

            _suspendedRaycasters.Clear();
        }

        /// <summary>
        /// 選択したキャラクターへの再生成を予約します。
        /// </summary>
        public void SelectCharacter(string characterName)
        {
            UnitManager manager = UnitManager.Instance;
            if (!CanSelect || manager == null)
            {
                return;
            }

            if (!manager.RequestLocalRespawn(manager.LocalPlayerId, characterName))
            {
                _status.text = "キャラクターを変更できませんでした。再度選択してください。";
                return;
            }

            _isChanging = true;
            StartCoroutine(FinishSelection());
        }

        /// <summary>
        /// 選択クリックを訓練へ伝えず、次のフレームで訓練へ戻します。
        /// </summary>
        private IEnumerator FinishSelection()
        {
            yield return null;
            Close();
            _isChanging = false;
        }

        /// <summary>
        /// 既存のクリック・マウスオーバー処理を使って専用UIを構築します。
        /// </summary>
        private void BuildView()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();
            RectTransform panel = CreateRect("Character Selection", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _panel = panel.gameObject;
            panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.065f, 0.98f);
            CreateText("Title", panel, "射撃訓練 / CHARACTER SELECT", new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.94f), 42);
            _status = CreateText("Help", panel, "使用するキャラクターを選択してください。訓練中は F2 で変更できます。",
                new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.14f), 24);
            List<UnitParams> characters = GameDataRepository.Instance.GetPlayerCharactersData();
            int columns = Mathf.Min(4, Mathf.Max(1, characters.Count));
            int rows = Mathf.Max(1, Mathf.CeilToInt(characters.Count / (float)columns));
            for (int index = 0; index < characters.Count; index++)
            {
                UnitParams character = characters[index];
                float width = 0.84f / columns;
                float height = 0.64f / rows;
                Vector2 minimum = new Vector2(0.08f + index % columns * width, 0.17f + (rows - 1 - index / columns) * height);
                RectTransform card = CreateRect(character.UnitName, panel, minimum, minimum + new Vector2(width, height), new Vector2(8, 8), new Vector2(-8, -8));
                card.gameObject.tag = "UI";
                card.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.20f, 0.25f);
                TrainingCharacterSelectionClick click = card.gameObject.AddComponent<TrainingCharacterSelectionClick>();
                click.Initialize(this, character.UnitName);
                card.gameObject.AddComponent<MouseOverAlphaChanger>();
                RectTransform portrait = CreateRect("Portrait", card, new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);
                Image image = portrait.gameObject.AddComponent<Image>();
                image.sprite = character.ProfileSelectionSprite != null ? character.ProfileSelectionSprite : character.PortraitSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.enabled = image.sprite != null;
                CreateText("Name", card, character.UnitName, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.21f), 28);
            }

            gameObject.AddComponent<RegisterUIGroup>();
            _panel.SetActive(false);
        }

        /// <summary>
        /// アンカー指定のUI領域を作成します。
        /// </summary>
        private static RectTransform CreateRect(string name, Transform parent, Vector2 minimum, Vector2 maximum, Vector2 offsetMinimum, Vector2 offsetMaximum)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = offsetMinimum;
            rect.offsetMax = offsetMaximum;
            return rect;
        }

        /// <summary>
        /// 日本語フォントで案内文字を配置します。
        /// </summary>
        private TMP_Text CreateText(string name, Transform parent, string value, Vector2 minimum, Vector2 maximum, float size)
        {
            RectTransform rect = CreateRect(name, parent, minimum, maximum, Vector2.zero, Vector2.zero);
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
