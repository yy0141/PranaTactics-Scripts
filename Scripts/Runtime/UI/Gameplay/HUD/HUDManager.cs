using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

using PranaTactics.Board;
using PranaTactics.GameFlow;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.UI.Screens;
using PranaTactics.Unit;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 戦闘画面のタイマー、ターン情報、HPゲージ、
    /// Pranamスロットおよびミニマップ表示を管理します。
    /// </summary>
    /// <remarks>
    /// <para>現在のフェーズの残り秒数です。タイマー未設定時は0を返します。</para>
    /// <para>HUDManagerのシーン内インスタンスを取得します。</para>
    /// <para>タイマーが終了したときに発生します。</para>
    /// <para>タイマーが終了したときに発生します。
    ///
    ///
    /// 既存コードとの互換性維持用です。
    /// 新しいコードではTimeEndedを使用してください。</para>
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class HUDManager : MonoBehaviour
    {
        /// <summary>
        /// フェーズの残り時間を管理するタイマーです。
        /// </summary>
        private MatchTimer _timer;

        /// <summary>
        /// 残り時間の円周ゲージです。
        /// </summary>
        [SerializeField]
        private TacticalHudGraphic _timerRing;

        /// <summary>
        /// フェーズ開始時の制限時間です。
        /// </summary>
        private float _timerDuration;

        public float PhaseRemainingSeconds => _timer != null ? _timer.RemainingSeconds : 0.0f;

        /// <summary>
        /// MainのPranamスロットUI一覧です。
        /// </summary>
        private readonly List<PranamSlotUI> _mainPranamSlotUIs = new();

        /// <summary>
        /// SubのPranamスロットUI一覧です。
        /// </summary>
        private readonly List<PranamSlotUI> _subPranamSlotUIs = new();

        /// <summary>
        /// 残り時間を表示するUIです。
        /// </summary>
        [Header("Text")]
        [FormerlySerializedAs("_timerTextUI")]
        [SerializeField]
        private ChangeTextMeshProUGUI _timerText;

        /// <summary>
        /// 現在のターン数を表示するUIです。
        /// </summary>
        [FormerlySerializedAs("_turnTextUI")]
        [SerializeField]
        private ChangeTextMeshProUGUI _turnText;

        /// <summary>
        /// 現在の行動力を表示するUIです。
        /// </summary>
        [FormerlySerializedAs("_proactivenessTextUI")]
        [SerializeField]
        private ChangeTextMeshProUGUI _proactivenessText;

        /// <summary>
        /// プレイヤーユニットのHPを表示するゲージです。
        /// </summary>
        [Header("Gauge")]
        [FormerlySerializedAs("_healthGuageTextUI")]
        [SerializeField]
        private HealthGauge _healthGauge;

        /// <summary>
        /// HPバーが現在表示している操作ユニットです。
        /// </summary>
        private UnitBehavior _healthDisplayUnit;

        /// <summary>
        /// 戦闘HUD全体のルートオブジェクトです。
        /// </summary>
        [Header("HUD Objects")]
        [FormerlySerializedAs("_ui")]
        [SerializeField]
        private GameObject _hudRoot;

        /// <summary>
        /// ミニマップUIのルートオブジェクトです。
        /// </summary>
        [FormerlySerializedAs("_miniMap")]
        [SerializeField]
        private GameObject _miniMapRoot;

        /// <summary>
        /// ミニマップのRenderTextureを表示するRawImageです。
        /// </summary>
        [SerializeField]
        private RawImage _miniMapRawImage;

        /// <summary>
        /// 表示状態を外部から変更するHUDパネルです。
        /// </summary>
        [FormerlySerializedAs("_panel")]
        [SerializeField]
        private GameObject _panelRoot;

        /// <summary>
        /// デバッグ用ログを出力するかどうかを示します。
        /// </summary>
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLogging;

        public static HUDManager Instance
        {
            get; private set;
        }

        public event Action TimeEnded;

        [Obsolete("Use TimeEnded instead.")]
        public event Action OnTimeEnd
        {
            add
            {
                TimeEnded += value;
            }

            remove
            {
                TimeEnded -= value;
            }
        }

        /// <summary>
        /// シングルトンインスタンスを設定し、
        /// ミニマップ表示先を取得します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{nameof(HUDManager)}が複数存在するため、" + $"{gameObject.name}を破棄します。", this);
                Destroy(gameObject);

                return;
            }

            Instance = this;
            ResolveMiniMapRawImage();
            AddMacroVisibility(_healthGauge);
            AddMacroVisibility(_proactivenessText);
        }

        /// <summary>
        /// 情報表示の背後にあるマスを確認できるようにします。
        /// </summary>
        private static void AddMacroVisibility(Component display)
        {
            if (display != null && !display.TryGetComponent<MacroHudVisibility>(out _))
            {
                display.gameObject.AddComponent<MacroHudVisibility>();
            }
        }

        /// <summary>
        /// フェーズタイマーを毎フレーム更新します。
        /// </summary>
        private void Update()
        {
            if (_timer == null)
            {
                return;
            }

            _timer.Tick(Time.deltaTime);
            UpdateTimerText();
        }

        /// <summary>
        /// 操作対象の切り替えとHP変更を表示へ同期します。
        /// </summary>
        /// <remarks>
        /// 死亡通知やHUD再表示の順序に依存せず、引き継ぎ先の現在HPと最大HPから割合を求めます。
        /// 対象変更時は旧ユニットの被弾Tweenを停止し、通常・ダメージ両ゲージを即時更新します。
        /// 同じ対象でHP割合が一致する間は既存の被弾アニメーションを継続します。
        /// </remarks>
        private void LateUpdate()
        {
            UnitBehavior unit = UnitManager.Instance != null ? UnitManager.Instance.LocalUnit : null;
            UnitStatus status = unit != null ? unit.GetUnitStatus() : null;

            if (_healthGauge == null || status == null)
            {
                _healthDisplayUnit = null;

                return;
            }

            float rate = status.MaxHealth > 0.0f ? Mathf.Clamp01(status.CurrentHealth / status.MaxHealth) : 0.0f;

            if (_healthDisplayUnit != unit || !Mathf.Approximately(_healthGauge.CurrentRate, rate))
            {
                _healthDisplayUnit = unit;
                _healthGauge.SetGaugeImmediately(rate);
            }
        }

        /// <summary>
        /// タイマーのイベント購読を解除し、
        /// シングルトン参照を解放します。
        /// </summary>
        private void OnDestroy()
        {
            UnsubscribeTimer();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 指定された秒数でフェーズタイマーを開始します。
        /// </summary>
        /// <param name="durationSeconds">
        /// タイマーの初期秒数です。
        /// 0未満の値は0として扱われます。
        /// </param>
        public void SetTimer(int durationSeconds)
        {
            UnsubscribeTimer();
            GameFlowManager gameFlowManager = GameFlowManager.Instance;

            if (gameFlowManager != null)
            {
                gameFlowManager.SetTimeEnded(false);
            }

            int validDuration = Mathf.Max(0, durationSeconds);

            _timerDuration = validDuration;
            _timer = new MatchTimer(validDuration);
            _timer.TimeEnded += HandleTimerEnded;
            UpdateTimerText();
        }

        /// <summary>
        /// 現在動作しているタイマーを停止します。
        /// </summary>
        public void StopTimer()
        {
            UnsubscribeTimer();
        }

        /// <summary>
        /// HUDパネルの表示状態を変更します。
        /// </summary>
        /// <param name="enable">
        /// 表示する場合は<c>true</c>です。
        /// </param>
        public void EnablePanel(bool enable)
        {
            SetActiveIfNeeded(_panelRoot, enable);
        }

        /// <summary>
        /// HUD本体とミニマップの表示状態を変更します。
        /// </summary>
        /// <param name="enable">
        /// 表示する場合は<c>true</c>です。
        /// </param>
        public void EnableGUI(bool enable)
        {
            SetActiveIfNeeded(_hudRoot, enable);
            SetActiveIfNeeded(_miniMapRoot, enable);

            if (enable)
            {
                RefreshMiniMapTexture();
            }
        }

        /// <summary>
        /// ミニマップへ表示するRenderTextureを設定します。
        /// </summary>
        /// <param name="renderTexture">
        /// ミニマップカメラの出力先RenderTextureです。
        /// </param>
        public void SetMiniMapTexture(RenderTexture renderTexture)
        {
            ResolveMiniMapRawImage();

            if (_miniMapRawImage == null)
            {
                Debug.LogError(
                    $"{nameof(HUDManager)}: " + "ミニマップ用RawImageが設定されていないため、" +
                    "RenderTextureを設定できません。",
                    this);

                return;
            }

            if (renderTexture == null)
            {
                Debug.LogError($"{nameof(HUDManager)}: " + "設定するRenderTextureがnullです。", this);

                return;
            }

            if (!renderTexture.IsCreated() && !renderTexture.Create())
            {
                Debug.LogError(
                    $"{nameof(HUDManager)}: " + "ミニマップ用RenderTextureを生成できませんでした。" +
                    $" Texture={renderTexture.name}",
                    this);

                return;
            }

            _miniMapRawImage.texture = renderTexture;
            _miniMapRawImage.enabled = true;
            SetActiveIfNeeded(_miniMapRawImage.gameObject, true);
            SetGraphicAlpha(_miniMapRawImage, 1.0f);
            _miniMapRawImage.canvasRenderer.SetAlpha(1.0f);
            CanvasGroup canvasGroup = _miniMapRawImage.GetComponentInParent<CanvasGroup>(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1.0f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            SetActiveIfNeeded(_miniMapRoot, true);

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(HUDManager)}: " + "ミニマップ用RenderTextureを設定しました。" +
                    $" RawImage={GetHierarchyPath(_miniMapRawImage.transform)}," +
                    $" Texture={renderTexture.name}," +
                    $" Created={renderTexture.IsCreated()}," +
                    $" Active={_miniMapRawImage.gameObject.activeInHierarchy}," +
                    $" Enabled={_miniMapRawImage.enabled}",
                    this);
            }
        }

        /// <summary>
        /// 現在RawImageに設定されているRenderTextureを再適用します。
        /// </summary>
        public void RefreshMiniMapTexture()
        {
            ResolveMiniMapRawImage();

            if (_miniMapRawImage == null)
            {
                return;
            }

            RenderTexture renderTexture = _miniMapRawImage.texture as RenderTexture;

            if (renderTexture == null)
            {
                return;
            }

            SetMiniMapTexture(renderTexture);
        }

        /// <summary>
        /// ターン数表示を更新します。
        /// </summary>
        /// <param name="value">
        /// 表示するターン数です。
        /// </param>
        public void SetTurnText(int value)
        {
            _turnText?.SetText(value.ToString());
        }

        /// <summary>
        /// 行動力表示を更新します。
        /// </summary>
        /// <param name="value">
        /// 表示する行動力です。
        /// </param>
        public void SetProactiveness(int value)
        {
            _proactivenessText?.SetText(value.ToString());
        }

        /// <summary>
        /// HPゲージを更新します。
        /// </summary>
        /// <param name="value">
        /// 設定するHP割合です。
        /// </param>
        /// <param name="animation">
        /// アニメーションを再生する場合は<c>true</c>です。
        /// </param>
        public void SetHealthGauge(float value, bool animation = true)
        {
            _healthGauge?.SetGauge(value, animation);
        }

        /// <summary>
        /// MainのPranamスロットUIを登録します。
        /// </summary>
        /// <param name="slotUI">
        /// 登録するスロットUIです。
        /// </param>
        public void AddMainPranamSlotUI(PranamSlotUI slotUI)
        {
            AddPranamSlotUI(_mainPranamSlotUIs, slotUI, "Main");
        }

        /// <summary>
        /// SubのPranamスロットUIを登録します。
        /// </summary>
        /// <param name="slotUI">
        /// 登録するスロットUIです。
        /// </param>
        public void AddSubPranamSlotUI(PranamSlotUI slotUI)
        {
            AddPranamSlotUI(_subPranamSlotUIs, slotUI, "Sub");
        }

        /// <summary>
        /// MainのPranamスロットUIを初期化します。
        /// </summary>
        /// <param name="pranamRange">
        /// 選択中に表示するPranamの射程オブジェクトです。
        /// </param>
        /// <param name="icon">
        /// 表示するPranamアイコンです。
        /// </param>
        /// <param name="displayName">
        /// 表示するPranam名です。
        /// </param>
        /// <param name="index">
        /// 初期化するスロットのインデックスです。
        /// </param>
        /// <param name="actionPointCost">
        /// 兵装を1回使用する際の消費行動力です。
        /// </param>
        /// <param name="isDefault">
        /// 初期選択スロットの場合は<c>true</c>です。
        /// </param>
        public void InitializeMainPranamSlotUI(
            GameObject pranamRange,
            Sprite icon,
            string displayName,
            int actionPointCost,
            int index,
            bool isDefault)
        {
            if (!TryGetPranamSlotUI(_mainPranamSlotUIs, index, "Main", out PranamSlotUI slotUI))
            {
                return;
            }

            slotUI.Initialize(pranamRange, icon, displayName, actionPointCost, isDefault);
        }

        /// <summary>
        /// SubのPranamスロットUIを初期化します。
        /// </summary>
        /// <param name="pranamRange">
        /// 選択中に表示するPranamの射程オブジェクトです。
        /// </param>
        /// <param name="icon">
        /// 表示するPranamアイコンです。
        /// </param>
        /// <param name="displayName">
        /// 表示するPranam名です。
        /// </param>
        /// <param name="index">
        /// 初期化するスロットのインデックスです。
        /// </param>
        /// <param name="actionPointCost">
        /// 兵装を1回使用する際の消費行動力です。
        /// </param>
        /// <param name="isDefault">
        /// 初期選択スロットの場合は<c>true</c>です。
        /// </param>
        public void InitializeSubPranamSlotUI(
            GameObject pranamRange,
            Sprite icon,
            string displayName,
            int actionPointCost,
            int index,
            bool isDefault)
        {
            if (!TryGetPranamSlotUI(_subPranamSlotUIs, index, "Sub", out PranamSlotUI slotUI))
            {
                return;
            }

            slotUI.Initialize(pranamRange, icon, displayName, actionPointCost, isDefault);
        }

        /// <summary>
        /// 通常戦闘表示とマップ表示を切り替えます。
        /// </summary>
        /// <param name="enable">
        /// マップモードを有効にする場合は<c>true</c>です。
        /// </param>
        public void SetMapMode(bool enable)
        {
            EnableGUI(!enable);
            SetMapCameraActive(enable);
            SwitchCurrentUnitCamera(!enable);
        }

        /// <summary>
        /// タイマーの残り時間表示を更新します。
        /// </summary>
        private void UpdateTimerText()
        {
            _timerRing?.SetFill(_timer != null && _timerDuration > 0.0f
                ? _timer.RemainingSeconds / _timerDuration : 0.0f);

            if (_timer == null || _timerText == null)
            {
                return;
            }

            _timerText.SetText(_timer.RemainingTime.ToString());
        }

        /// <summary>
        /// タイマー終了通知を処理します。
        /// </summary>
        private void HandleTimerEnded()
        {
            if (_timer != null)
            {
                _timer.TimeEnded -= HandleTimerEnded;
                _timer = null;
            }

            _timerRing?.SetFill(0.0f);
            _timerText?.SetText("0");
            GameFlowManager gameFlowManager = GameFlowManager.Instance;

            if (gameFlowManager != null)
            {
                gameFlowManager.SetTimeEnded(true);
            }

            TimeEnded?.Invoke();
        }

        /// <summary>
        /// 現在のタイマーからイベント購読を解除します。
        /// </summary>
        private void UnsubscribeTimer()
        {
            if (_timer == null)
            {
                return;
            }

            _timer.TimeEnded -= HandleTimerEnded;
            _timer = null;
        }

        /// <summary>
        /// ミニマップ表示用RawImageを取得します。
        /// </summary>
        private void ResolveMiniMapRawImage()
        {
            if (_miniMapRawImage != null)
            {
                return;
            }

            if (_miniMapRoot != null)
            {
                _miniMapRawImage = _miniMapRoot.GetComponentInChildren<RawImage>(true);
            }

            if (_miniMapRawImage == null)
            {
                Debug.LogWarning(
                    $"{nameof(HUDManager)}: " + "ミニマップ用RawImageを取得できません。" +
                    " InspectorのMini Map Raw Imageへ" +
                    "対象のRawImageを設定してください。",
                    this);

                return;
            }

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(HUDManager)}: " + "ミニマップ用RawImageを取得しました。" +
                    $" Object={_miniMapRawImage.gameObject.name}," +
                    $" Path={GetHierarchyPath(_miniMapRawImage.transform)}",
                    this);
            }
        }

        /// <summary>
        /// ミニマップ用カメラオブジェクトの表示状態を変更します。
        /// </summary>
        /// <param name="isActive">
        /// カメラを有効にする場合は<c>true</c>です。
        /// </param>
        private void SetMapCameraActive(bool isActive)
        {
            BoardManager boardManager = BoardManager.Instance;

            if (boardManager == null)
            {
                Debug.LogWarning(
                    $"{nameof(HUDManager)}: " + $"{nameof(BoardManager)}が存在しないため、" +
                    "マップカメラを切り替えられません。",
                    this);

                return;
            }

            GameObject mapCameraObject = boardManager.GetMinimapCameraObject();

            if (mapCameraObject == null)
            {
                Debug.LogWarning($"{nameof(HUDManager)}: " + "ミニマップカメラのオブジェクトを取得できません。", this);

                return;
            }

            SetActiveIfNeeded(mapCameraObject, isActive);
        }

        /// <summary>
        /// 現在のフェーズに対応するユニットカメラを切り替えます。
        /// </summary>
        /// <param name="enable">
        /// ユニットカメラを有効にする場合は<c>true</c>です。
        /// </param>
        private void SwitchCurrentUnitCamera(bool enable)
        {
            MatchPhaseManager phaseManager = MatchPhaseManager.Instance;
            UnitManager unitManager = UnitManager.Instance;

            if (phaseManager == null || unitManager == null)
            {
                return;
            }

            switch (phaseManager.CurrentPhase)
            {
                case MatchPhase.Macro:
                    unitManager.LocalMacroUnitBehavior?.SwitchCamera(enable);
                    break;
                case MatchPhase.Micro:
                case MatchPhase.MicroTransition:
                case MatchPhase.MacroTransition:
                    unitManager.LocalMicroUnitBehavior?.SwitchCamera(enable);
                    break;
            }

            unitManager.LocalTacticalMapUnitBehavior?.SwitchCamera(enable);
        }

        /// <summary>
        /// 指定された一覧へPranamスロットUIを追加します。
        /// </summary>
        private void AddPranamSlotUI(List<PranamSlotUI> slotUIs, PranamSlotUI slotUI, string slotType)
        {
            if (slotUI == null)
            {
                Debug.LogWarning($"{nameof(HUDManager)}: " + $"nullの{slotType}スロットUIは登録できません。", this);

                return;
            }

            if (slotUIs.Contains(slotUI))
            {
                return;
            }

            slotUIs.Add(slotUI);

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(HUDManager)}: " + $"{slotType}スロットUIを登録しました。" +
                    $" Index={slotUIs.Count - 1}," +
                    $" Object={slotUI.gameObject.name}",
                    this);
            }
        }

        /// <summary>
        /// 指定された一覧からPranamスロットUIを取得します。
        /// </summary>
        private bool TryGetPranamSlotUI(
            List<PranamSlotUI> slotUIs,
            int index,
            string slotType,
            out PranamSlotUI slotUI)
        {
            slotUI = null;

            if (index < 0 || index >= slotUIs.Count)
            {
                Debug.LogWarning(
                    $"{nameof(HUDManager)}: " + $"{slotType}スロットのインデックスが範囲外です。" +
                    $" Index={index}, Count={slotUIs.Count}",
                    this);

                return false;
            }

            slotUI = slotUIs[index];

            if (slotUI != null)
            {
                return true;
            }

            Debug.LogWarning(
                $"{nameof(HUDManager)}: " + $"{slotType}スロットUIが設定されていません。" + $" Index={index}",
                this);

            return false;
        }

        /// <summary>
        /// UIオブジェクトの表示状態を必要な場合だけ変更します。
        /// </summary>
        private static void SetActiveIfNeeded(GameObject target, bool isActive)
        {
            if (target == null || target.activeSelf == isActive)
            {
                return;
            }

            target.SetActive(isActive);
        }

        /// <summary>
        /// UI要素のRGB値を維持したまま透明度を変更します。
        /// </summary>
        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            Color color = graphic.color;

            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }

        /// <summary>
        /// 指定されたTransformの階層パスを取得します。
        /// </summary>
        /// <param name="target">
        /// 階層パスを取得するTransformです。
        /// </param>
        /// <returns>
        /// シーンのルートから対象までの階層パスです。
        /// </returns>
        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "null";
            }

            string path = target.name;
            Transform current = target.parent;

            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }
    }
}
