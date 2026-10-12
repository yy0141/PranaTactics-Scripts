using System.Collections.Generic;
using TMPro;
using UnityEngine;

using PranaTactics.AI.Solo;
using PranaTactics.Match.Phases;
using PranaTactics.Settings;
using PranaTactics.Unit;

using Outline = PranaTactics.Rendering.Outlines.Outline;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// Macro Phase専用UIの表示と決定待ちの案内を制御します。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class MacroPlanningUiView : MonoBehaviour
    {
        [SerializeField]
        private GameObject _panel;

        [SerializeField]
        private TMP_Text _statusText;

        [SerializeField]
        private GameObject _confirmationPanel;

        [SerializeField]
        private TMP_Text _confirmationText;

        [SerializeField]
        private TMP_Text _acceptLabel;

        private MacroPlanningSession _confirmationSession;

        /// <summary>
        /// ターゲット指定と観測情報を提供するAI管理です。
        /// </summary>
        private SoloAiDirector _aiDirector;

        private bool _isResetConfirmation;

        /// <summary>
        /// 確認画面を開いた時のターゲット候補です。
        /// </summary>
        private int _pendingTargetPlayerId = -1;

        /// <summary>
        /// 現在、ターゲット指定の枠線を表示しているユニットです。
        /// </summary>
        private UnitBehavior _outlinedTarget;

        private readonly List<Outline> _targetOutlines = new List<Outline>();

        /// <summary>
        /// 確認中のMacroモデルを薄く表示する描画状態です。
        /// </summary>
        private MacroTargetPreview _targetPreview;

        /// <summary>
        /// 確認中のMacroモデルの表示濃度です。
        /// </summary>
        [SerializeField, Range(0.0f, 1.0f)]
        private float _candidateOpacity = 0.5f;

        /// <summary>
        /// ターゲットを示す枠線のピクセル幅です。
        /// </summary>
        [Range(1.0f, 40.0f)]
        [SerializeField]
        private float _targetOutlineWidth = 6.0f;

        [SerializeField]
        private TMP_Text _routePreviewText;

        public int PendingTargetPlayerId => _pendingTargetPlayerId;
        public int TargetConfirmationCount { get; private set; }

        public bool IsConfirmationOpen => _confirmationSession != null;

        public bool CanAcceptConfirmation
        {
            get
            {
                MacroUnitBehavior macro = UnitManager.Instance?.LocalUnit?.GetMacroUnitBehavior();

                if (_pendingTargetPlayerId >= 0)
                {
                    return IsConfirmationOpen && _aiDirector != null && _confirmationSession == MatchPhaseManager.Instance?.MacroPlanning &&
                        !_confirmationSession.IsLocalConfirmed &&
                        _aiDirector.TryGetMacroTarget(_pendingTargetPlayerId, out UnitBehavior target);
                }

                return IsConfirmationOpen && macro != null &&
                    (_isResetConfirmation ? macro.CanResetPath : macro.CanEditPath);
            }
        }

        private void Awake()
        {
            _aiDirector = SoloAiDirector.EnsureExists();
            _panel.SetActive(false);
            _confirmationPanel.SetActive(false);
        }

        public void OpenConfirmation(bool isReset)
        {
            MacroPlanningSession session = MatchPhaseManager.Instance?.MacroPlanning;

            if (session == null || session.IsLocalConfirmed)
            {
                return;
            }

            _pendingTargetPlayerId = -1;
            _confirmationSession = session;
            _isResetConfirmation = isReset;
            session.IsPlanningConfirmationOpen = true;
            _confirmationText.text = isReset
                ? "移動経路をすべて削除して\n最初の位置に戻りますか？"
                : "この移動経路で決定しますか？\n決定後は経路を変更できません。";
            _acceptLabel.text = isReset ? "リセットする" : "決定する";
            _confirmationPanel.SetActive(true);
        }

        /// <summary>
        /// 右クリックした敵のターゲット指定を確認します。経路の変更は確定後に行います。
        /// </summary>
        public void OpenTargetConfirmation(UnitBehavior target)
        {
            MacroPlanningSession session = MatchPhaseManager.Instance?.MacroPlanning;
            if (_aiDirector == null || target == null || session == null || session.IsLocalConfirmed || IsConfirmationOpen ||
                !_aiDirector.TryGetMacroTarget(target.PlayerId, out UnitBehavior visibleTarget))
            {
                return;
            }

            _confirmationSession = session;
            _pendingTargetPlayerId = visibleTarget.PlayerId;
            _isResetConfirmation = false;
            session.IsPlanningConfirmationOpen = true;
            _confirmationText.text = $"敵 {_pendingTargetPlayerId} をターゲットに指定しますか？";
            _acceptLabel.text = "決定する";
            _confirmationPanel.SetActive(true);
            RefreshTargetOutline();
        }

        public void AcceptConfirmation()
        {
            if (!CanAcceptConfirmation)
            {
                return;
            }

            if (_pendingTargetPlayerId >= 0)
            {
                int targetPlayerId = _pendingTargetPlayerId;
                CloseConfirmation();
                if (_aiDirector.TryOrderMacroTarget(targetPlayerId))
                {
                    TargetConfirmationCount++;
                }
                RefreshTargetOutline();
                return;
            }

            if (_isResetConfirmation)
            {
                UnitManager.Instance.LocalUnit.GetMacroUnitBehavior().ResetPath();
            }
            else
            {
                _confirmationSession.Confirm();
            }

            CloseConfirmation();
        }

        public void CloseConfirmation()
        {
            if (_confirmationSession != null)
            {
                UnitManager.Instance?.LocalUnit?.GetMacroUnitBehavior()?.BlockPlanningInputForCurrentFrame();
                _confirmationSession.IsPlanningConfirmationOpen = false;
                _confirmationSession = null;
            }

            _pendingTargetPlayerId = -1;
            _confirmationPanel.SetActive(false);
            RefreshTargetOutline();
        }

        private void OnDisable()
        {
            CloseConfirmation();
            ClearTargetOutline();
        }

        private void Update()
        {
            MatchPhaseManager manager = MatchPhaseManager.Instance;
            bool isVisible = manager != null && manager.CurrentPhase == MatchPhase.Macro &&
                manager.MacroPlanning != null;

            _panel.SetActive(isVisible);

            if (IsConfirmationOpen && (!isVisible || manager.MacroPlanning != _confirmationSession ||
                manager.MacroPlanning.IsLocalConfirmed ||
                (_pendingTargetPlayerId >= 0 && (_aiDirector == null || !_aiDirector.TryGetMacroTarget(
                    _pendingTargetPlayerId, out UnitBehavior pendingTarget)))))
            {
                CloseConfirmation();
            }

            RefreshTargetOutline();

            if (isVisible && _statusText != null)
            {
                _statusText.text = manager.MacroPlanning.IsLocalConfirmed
                    ? "決定済み：他のプレイヤー・AIを待っています"
                    : "移動経路を選んで決定してください";
            }

            if (isVisible && _routePreviewText != null)
            {
                MacroUnitBehavior macro = UnitManager.Instance?.LocalUnit?.GetMacroUnitBehavior();

                _routePreviewText.text = macro != null && !string.IsNullOrEmpty(macro.RoutePreviewStatus)
                    ? macro.RoutePreviewStatus
                    : "マスに重ねて経路を確認・クリックで追加";
                if (!manager.MacroPlanning.IsLocalConfirmed)
                {
                    string targetStatus = _aiDirector != null ? _aiDirector.TargetOrderStatus : string.Empty;
                    _routePreviewText.text += "\n" + (string.IsNullOrEmpty(targetStatus)
                        ? "敵のマスを右クリックでAI目標指定"
                        : targetStatus);
                }
            }
        }
        /// <summary>
        /// 確認中はMacroモデルを透過し、確定済みのMacroとMapだけに枠線を適用します。
        /// </summary>
        private void RefreshTargetOutline()
        {
            MatchPhaseManager phases = MatchPhaseManager.Instance;
            UnitManager units = UnitManager.Instance;
            if (_aiDirector == null || phases == null || units == null ||
                (phases.CurrentPhase != MatchPhase.Macro && phases.CurrentPhase != MatchPhase.Micro))
            {
                ClearTargetOutline();
                return;
            }

            UnitBehavior pending = phases.CurrentPhase == MatchPhase.Macro
                ? GetVisibleTarget(_pendingTargetPlayerId, units) : null;
            GetTargetPreview().SetModel(pending?.GetMacroUnitBehavior()?.GetModel(), _candidateOpacity);
            UnitBehavior target = GetVisibleTarget(_aiDirector.OrderedTargetPlayerId, units);
            if (_outlinedTarget != target)
            {
                ClearConfirmedOutline();
                _outlinedTarget = target;
                if (target != null)
                {
                    AddTargetOutline(target.GetMacroUnitBehavior()?.GetModel());
                    AddTargetOutline(target.GetTacticalMapUnitBehavior()?.GetModel());
                }
            }

            ColorUtility.TryParseHtmlString(SettingsManager.Current.Appearance.ConfirmedTargetColor, out Color color);
            foreach (Outline outline in _targetOutlines)
            {
                if (outline != null)
                {
                    outline.SetTargetHighlight(true, color, _targetOutlineWidth);
                }
            }
        }

        /// <summary>
        /// 生存し、観測できる敵だけをターゲット表示対象として取得します。
        /// </summary>
        private UnitBehavior GetVisibleTarget(int targetId, UnitManager units)
        {
            if (units.TryGetUnit(targetId, out UnitBehavior candidate) && candidate != null &&
                candidate.GetUnitStatus() != null && candidate.GetUnitStatus().IsAlive &&
                _aiDirector.WorldState.GetContactState(
                    units.GetTeamId(units.LocalPlayerId), targetId) >= AiContactState.MapTracked)
            {
                return candidate;
            }

            return null;
        }

        /// <summary>
        /// スクリプト再読み込み後でも候補表示の描画状態を必要なときに生成します。
        /// </summary>
        private MacroTargetPreview GetTargetPreview()
        {
            _targetPreview ??= new MacroTargetPreview();
            return _targetPreview;
        }

        /// <summary>
        /// チーム色の更新後にも候補の透過度を維持します。
        /// </summary>
        private void LateUpdate()
        {
            _targetPreview?.ApplyOpacity(_candidateOpacity);
        }

        /// <summary>
        /// モデルに存在する枠線を取得し、未設定の場合は描画用コンポーネントを補います。
        /// </summary>
        private void AddTargetOutline(GameObject model)
        {
            if (model == null)
            {
                return;
            }

            Outline outline = model.GetComponentInChildren<Outline>(true);
            if (outline == null)
            {
                outline = model.AddComponent<Outline>();
                outline.enabled = false;
            }

            if (!_targetOutlines.Contains(outline))
            {
                _targetOutlines.Add(outline);
            }
        }

        /// <summary>
        /// ターゲット指定の枠線だけを解除し、通常のハイライト設定へ戻します。
        /// </summary>
        private void ClearTargetOutline()
        {
            _targetPreview?.Clear();
            ClearConfirmedOutline();
        }

        /// <summary>
        /// 確定枠線を解除し、通常のハイライトに戻します。
        /// </summary>
        private void ClearConfirmedOutline()
        {
            foreach (Outline outline in _targetOutlines)
            {
                if (outline != null)
                {
                    outline.SetTargetHighlight(false, Color.clear, 0.0f);
                }
            }

            _targetOutlines.Clear();
            _outlinedTarget = null;
        }
    }
}
