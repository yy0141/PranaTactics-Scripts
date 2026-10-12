using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Match.Phases;
using PranaTactics.Unit;
using PranaTactics.UI.Interaction;
using PranaTactics.UI.Screens;

namespace PranaTactics.UI.Gameplay
{
    public enum MacroPlanningAction
    {
        Undo,
        Redo,
        Confirm,
        Reset,
        AcceptConfirmation,
        CancelConfirmation,
        ShortestRoute,
        BalancedRoute,
        SafestRoute
    }

    /// <summary>
    /// 既存UIManagerの繰り返しクリックと可否判定を経路操作へ接続します。
    /// </summary>
    [RequireComponent(typeof(Image), typeof(MacroPlanningButtonColor))]
    public sealed class MacroPlanningButton : RepeatClickBehaviorBase, IClickableCondition
    {
        [SerializeField]
        private MacroPlanningAction _action;

        [SerializeField]
        private MacroPlanningUiView _view;

        private UIManager _uiManager;

        public bool IsSelected
        {
            get
            {
                MacroUnitBehavior macro = UnitManager.Instance?.LocalUnit?.GetMacroUnitBehavior();

                return macro != null && _action >= MacroPlanningAction.ShortestRoute &&
                    (int)macro.RoutePolicy == (int)_action - (int)MacroPlanningAction.ShortestRoute;
            }
        }

        private void Start()
        {
            _uiManager = UIManager.Instance;
            _uiManager?.RegisterUIGroup(gameObject, "MacroPlanning");
        }

        public bool CanClick()
        {
            if (!PranaTactics.Tutorial.TutorialSession.AllowsPlanningAction(_action) ||
                (SettingsUIManager.Instance != null && SettingsUIManager.Instance.IsOpen))
            {
                return false;
            }

            MacroUnitBehavior macro = UnitManager.Instance?.LocalUnit?.GetMacroUnitBehavior();

            if (macro == null)
            {
                return false;
            }

            bool isConfirmationAction = _action == MacroPlanningAction.AcceptConfirmation ||
                _action == MacroPlanningAction.CancelConfirmation;

            if (_view == null || _view.IsConfirmationOpen != isConfirmationAction)
            {
                return false;
            }

            return _action switch
            {
                MacroPlanningAction.Undo => macro.CanUndoPath,
                MacroPlanningAction.Redo => macro.CanRedoPath,
                MacroPlanningAction.Confirm => macro.CanEditPath,
                MacroPlanningAction.Reset => macro.CanResetPath,
                MacroPlanningAction.AcceptConfirmation => _view.CanAcceptConfirmation,
                MacroPlanningAction.CancelConfirmation => true,
                MacroPlanningAction.ShortestRoute => macro.CanEditPath,
                MacroPlanningAction.BalancedRoute => macro.CanEditPath,
                MacroPlanningAction.SafestRoute => macro.CanEditPath,
                _ => false
            };
        }

        protected override void OnClickEnter()
        {
            GetComponent<MacroPlanningButtonColor>().ShowPressed();
            MacroUnitBehavior macro = UnitManager.Instance.LocalUnit.GetMacroUnitBehavior();

            switch (_action)
            {
                case MacroPlanningAction.Undo:
                    macro.StepPath(false);
                    break;
                case MacroPlanningAction.Redo:
                    macro.StepPath(true);
                    break;
                case MacroPlanningAction.Confirm:
                    _view.OpenConfirmation(false);
                    break;
                case MacroPlanningAction.Reset:
                    _view.OpenConfirmation(true);
                    break;
                case MacroPlanningAction.AcceptConfirmation:
                    _view.AcceptConfirmation();
                    break;
                case MacroPlanningAction.CancelConfirmation:
                    _view.CloseConfirmation();
                    break;
                case MacroPlanningAction.ShortestRoute:
                case MacroPlanningAction.BalancedRoute:
                case MacroPlanningAction.SafestRoute:
                    macro.SetRoutePolicy(
                        (MacroRoutePolicy)((int)_action - (int)MacroPlanningAction.ShortestRoute));
                    macro.BlockPlanningInputForCurrentFrame();
                    break;
            }

            PranaTactics.Tutorial.TutorialSession.NotifyPlanningAction(_action);
        }

        private void OnDestroy()
        {
            _uiManager?.UnregisterUI(gameObject);
        }
    }
}
