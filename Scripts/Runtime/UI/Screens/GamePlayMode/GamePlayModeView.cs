using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using PranaTactics.Match.Modes;
using PranaTactics.Settings;
using PranaTactics.UI.Interaction;
using PranaTactics.UI.Screens.Header;

namespace PranaTactics.UI.Screens.GamePlayMode
{
    /// <summary>
    /// モード選択画面を表示し、背面の画面への入力を一時停止します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePlayModeView : MonoBehaviour
    {
        /// <summary>
        /// ローカル区分のカード生成に使用するシーン内のテンプレートです。
        /// </summary>
        [SerializeField]
        private GameObject _localCardTemplate;

        /// <summary>
        /// オンライン区分のカード生成に使用するシーン内のテンプレートです。
        /// </summary>
        [SerializeField]
        private GameObject _onlineCardTemplate;

        /// <summary>
        /// 選択状態と接続できない場合の案内を表示します。
        /// </summary>
        [SerializeField]
        private TMP_Text _status;

        /// <summary>
        /// 画面を閉じた際に入力を復元する背面のレイキャスターです。
        /// </summary>
        private readonly List<GraphicRaycaster> _suspendedRaycasters = new();

        /// <summary>
        /// 生成したカードの選択枠とモード情報です。
        /// </summary>
        private readonly List<ModeCard> _cards = new();

        /// <summary>
        /// 画面遷移中の重複操作を防ぎます。
        /// </summary>
        private bool _isClosing;

        public string ReturnSceneName { get; private set; }

        public bool CanInteract => isActiveAndEnabled && !_isClosing &&
            SceneManager.GetActiveScene() == gameObject.scene &&
            (SettingsUIManager.Instance == null || !SettingsUIManager.Instance.IsOpen) &&
            (LobbyUIManager.Instance == null || !LobbyUIManager.Instance.IsMatchmaking);

        /// <summary>
        /// 入力管理と戻り先を準備し、背面のレイキャスターを停止します。
        /// </summary>
        private void Awake()
        {
            ReturnSceneName = SceneManager.GetActiveScene().name;
            if (UIManager.Instance == null)
            {
                new GameObject("UIManager").AddComponent<UIManager>();
            }

            if (EventSystem.current == null)
            {
                var input = new GameObject("Mode Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                SceneManager.MoveGameObjectToScene(input, gameObject.scene);
            }

            foreach (string name in new[] { "LobbyScene", "CharacterProfile", "PranamProfile" })
            {
                Scene scene = SceneManager.GetSceneByName(name);
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

            BuildCategory(_localCardTemplate, PlayCategory.Local, "ローカル");
            BuildCategory(_onlineCardTemplate, PlayCategory.Online, "オンライン");
            RefreshSelection();
        }

        /// <summary>
        /// カタログの候補を区分別のスクロール領域へ生成します。
        /// </summary>
        private void BuildCategory(GameObject template, PlayCategory category, string categoryName)
        {
            if (template == null || _status == null)
            {
                Debug.LogError("モード画面のテンプレートまたは案内文が未設定です。", this);
                return;
            }

            RectTransform templateRect = template.GetComponent<RectTransform>();
            TMP_Text categoryLabel = CreateCategoryLabel(template, categoryName);
            RectTransform viewport = CreateCategoryViewport(templateRect, categoryName);
            int count = CreateModeList(template, viewport, category);
            template.SetActive(false);
            if (count == 0)
            {
                categoryLabel.text = categoryName + "（このビルドでは利用できません）";
            }
        }

        /// <summary>
        /// 接続区分の見出しをカード一覧の上へ生成します。
        /// </summary>
        private TMP_Text CreateCategoryLabel(GameObject template, string categoryName)
        {
            RectTransform templateRect = template.GetComponent<RectTransform>();
            TMP_Text categoryLabel = Instantiate(template.transform.Find("English Label").GetComponent<TMP_Text>(), template.transform.parent);
            categoryLabel.text = categoryName;
            categoryLabel.rectTransform.anchoredPosition = templateRect.anchoredPosition + new Vector2(0.0f, 36.0f);
            categoryLabel.rectTransform.sizeDelta = new Vector2(templateRect.sizeDelta.x, 30.0f);
            categoryLabel.raycastTarget = false;
            return categoryLabel;
        }

        /// <summary>
        /// 一つの区分のカード表示領域とスクロール入力を生成します。
        /// </summary>
        private static RectTransform CreateCategoryViewport(RectTransform templateRect, string categoryName)
        {
            var viewportObject = new GameObject(categoryName + " Modes", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(SettingsScrollRect));
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.SetParent(templateRect.parent, false);
            viewport.anchorMin = templateRect.anchorMin;
            viewport.anchorMax = templateRect.anchorMax;
            viewport.pivot = templateRect.pivot;
            viewport.anchoredPosition = templateRect.anchoredPosition;
            viewport.sizeDelta = templateRect.sizeDelta;
            viewportObject.GetComponent<Image>().color = Color.clear;
            return viewport;
        }

        /// <summary>
        /// 区分に対応するモードを並べ、件数に合わせてスクロール範囲を設定します。
        /// </summary>
        private int CreateModeList(GameObject template, RectTransform viewport, PlayCategory category)
        {
            RectTransform templateRect = template.GetComponent<RectTransform>();
            var contentObject = new GameObject("Mode List", typeof(RectTransform));
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0.0f, 1.0f);
            content.anchorMax = new Vector2(0.0f, 1.0f);
            content.pivot = new Vector2(0.0f, 1.0f);

            int count = 0;
            foreach (GameMode mode in GameModeRegistry.Catalog.Modes)
            {
                if (!mode.Supports(category) || (category == PlayCategory.Online && GameBuildSettings.IsSoloOnly))
                {
                    continue;
                }

                CreateCard(template, content, category, mode, count);
                count++;
            }

            content.sizeDelta = new Vector2(templateRect.sizeDelta.x,
                Mathf.Max(templateRect.sizeDelta.y, count * (templateRect.sizeDelta.y + 24.0f) - 24.0f));
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40.0f;
            return count;
        }

        /// <summary>
        /// 一つのモードカードを生成し、既存の入力処理へ接続します。
        /// </summary>
        private void CreateCard(GameObject template, RectTransform content, PlayCategory category, GameMode mode, int index)
        {
            GameObject card = Instantiate(template, content);
            card.name = category + " " + mode.Id;
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.0f, 1.0f);
            rect.anchorMax = new Vector2(0.0f, 1.0f);
            rect.pivot = new Vector2(0.0f, 1.0f);
            rect.anchoredPosition = new Vector2(0.0f, -index * (rect.sizeDelta.y + 24.0f));
            card.transform.Find("Index").GetComponent<TMP_Text>().text = (index + 1).ToString("D2");
            card.transform.Find("English Label").GetComponent<TMP_Text>().text = mode.Id.ToUpperInvariant();
            card.transform.Find("Title").GetComponent<TMP_Text>().text = mode.DisplayName;
            card.transform.Find("Description").GetComponent<TMP_Text>().text = mode.Description;
            card.GetComponent<GamePlayModeClick>().Initialize(this, category, mode.Id);
            _cards.Add(new ModeCard(category, mode.Id, card.transform.Find("Selected Frame").gameObject));
            card.SetActive(true);
        }

        /// <summary>
        /// 選択を保存し、接続できない場合は画面内に理由を表示します。
        /// </summary>
        public void SelectMode(PlayCategory category, string gameModeId)
        {
            if (!CanInteract)
            {
                return;
            }

            HeaderNavigationView navigation = FindFirstObjectByType<HeaderNavigationView>();
            if (navigation == null || !navigation.CanNavigate(HeaderDestination.Play))
            {
                return;
            }

            bool isCurrentMode = category == PlayModeSelection.Category && gameModeId == PlayModeSelection.GameModeId;
            if (!isCurrentMode && !PlayModeSelection.TrySelect(category, gameModeId))
            {
                _status.text = "オンラインサービスに接続できません。接続後にもう一度選択してください。";
                if (category == PlayCategory.Online)
                {
                    PlayModeSelection.TryReconnect();
                }

                return;
            }

            RefreshLobbyButtons(category == PlayCategory.Local);
            _isClosing = true;
            navigation.Navigate(HeaderDestination.Play);
        }

        /// <summary>
        /// 非表示の切り替えUIも含め、既存ロビーのボタン表示を即時更新します。
        /// </summary>
        private static void RefreshLobbyButtons(bool isSoloPlay)
        {
            Scene lobby = SceneManager.GetSceneByName("LobbyScene");
            if (!lobby.IsValid() || !lobby.isLoaded)
            {
                return;
            }

            foreach (GameObject root in lobby.GetRootGameObjects())
            {
                foreach (SwitchPlayMode display in root.GetComponentsInChildren<SwitchPlayMode>(true))
                {
                    display.RefreshDisplay(isSoloPlay);
                }
            }
        }

        /// <summary>
        /// 現在のモードを選択枠と案内文へ反映します。
        /// </summary>
        private void RefreshSelection()
        {
            bool isSoloPlay = PlayModeSelection.IsSoloPlay;
            foreach (ModeCard card in _cards)
            {
                card.Selection.SetActive(card.Category == PlayModeSelection.Category && card.Id == PlayModeSelection.GameModeId);
            }

            if (_status != null && GameModeRegistry.Catalog.TryGet(PlayModeSelection.GameModeId, PlayModeSelection.Category, out GameMode mode))
            {
                _status.text = (isSoloPlay ? "ローカル" : "オンライン") + " / " + mode.DisplayName +
                    "を選択中です。一覧はマウスホイールでスクロールできます。";
            }
        }

        /// <summary>
        /// この画面が停止した入力だけを復元します。
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
        /// 一つのカードの選択表示に必要な情報を保持します。
        /// </summary>
        private sealed class ModeCard
        {
            public PlayCategory Category { get; }
            public string Id { get; }
            public GameObject Selection { get; }

            public ModeCard(PlayCategory category, string id, GameObject selection)
            {
                Category = category;
                Id = id;
                Selection = selection;
            }
        }
    }
}
