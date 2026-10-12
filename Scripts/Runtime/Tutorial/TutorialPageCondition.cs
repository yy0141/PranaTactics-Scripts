namespace PranaTactics.Tutorial
{
    public struct TutorialPageObservation
    {
        public int CameraPanInputs;
        public int CameraResetInputs;
        public int MacroHudChanges;
        public bool MacroHudHidden;
        public bool HasTargetCandidate;
        public int TargetConfirmations;
        public bool IsExpectedTargetConfirmed;

        public int CameraZoomOutInputs;

        public int CameraZoomInInputs;

        public int Steps;

        public int PlannedSteps;

        public int MainSlot;

        public int SubSlot;

        public bool PlanningFinished;

        public bool HasPerformedPlanningAction;

        public bool IsPlanningIdle;

        public bool DocumentOpen;

        public int PrimaryUses;

        public int SecondaryUses;

        public bool PrimaryBusy;

        public bool SecondaryBusy;

        public int RequiredKeyCount;

        public int PressedKeyCount;

        public bool Moving;

        public bool Walking;

        public bool Stopping;

        public bool MapOpen;

        public bool MainPerspective;

        public bool Turning;

        public bool KeyPressed;

        public bool HasClickedBlockedClimb;

        public bool IsAtPlanningTarget;
    }

    /// <summary>
    /// 説明ページ開始時と現在の観測結果から、待機条件の達成を判定します。
    /// </summary>
    public static class TutorialPageCondition
    {
        /// <summary>
        /// 指定された操作・移動・兵装使用の待機条件を判定します。
        /// </summary>
        /// <remarks>
        /// 兵装使用は使用回数の増加と両兵装の動作終了を確認します。
        /// 未対応の条件名は達成と見なしません。
        /// </remarks>
        public static bool IsSatisfied(
            string waitFor,
            int cells,
            TutorialPageObservation before,
            TutorialPageObservation now)
        {
            switch (waitFor)
            {
                case "cameraPan":
                    return now.CameraPanInputs > before.CameraPanInputs;
                case "cameraReset":
                    return now.CameraResetInputs > before.CameraResetInputs;
                case "macroUiHidden":
                    return now.MacroHudChanges > before.MacroHudChanges && now.MacroHudHidden;
                case "macroUiShown":
                    return now.MacroHudChanges > before.MacroHudChanges && !now.MacroHudHidden;
                case "targetCandidate":
                    return now.HasTargetCandidate;
                case "targetConfirmed":
                    return now.TargetConfirmations > before.TargetConfirmations && now.IsExpectedTargetConfirmed;
                case "scrollDown":
                    return now.CameraZoomOutInputs > before.CameraZoomOutInputs;
                case "scrollUp":
                    return now.CameraZoomInInputs > before.CameraZoomInInputs;
                case "routeShortest":
                case "routeBalanced":
                case "routeSafest":
                case "pathUndo":
                case "pathRedo":
                    return now.HasPerformedPlanningAction && now.IsPlanningIdle && !now.Moving;
                case "pathReset":
                    return now.HasPerformedPlanningAction && now.IsPlanningIdle && !now.Moving &&
                        now.PlannedSteps == 0;
                case "planningConfirmed":
                    return now.HasPerformedPlanningAction && now.PlanningFinished && !now.Moving;
                case "blockedClimb":
                    return now.HasClickedBlockedClimb;
                case "planTarget":
                    return now.IsAtPlanningTarget && now.PlannedSteps > before.PlannedSteps && !now.Moving;
                case "key":
                    return now.KeyPressed;
                case "keysAll":
                    return now.RequiredKeyCount > 0 && now.PressedKeyCount == now.RequiredKeyCount;
                case "primaryFire":
                    return now.PrimaryUses > before.PrimaryUses && !now.PrimaryBusy && !now.SecondaryBusy;
                case "secondaryFire":
                    return now.SecondaryUses > before.SecondaryUses && !now.PrimaryBusy && !now.SecondaryBusy;
                case "moveCells":
                    return now.Steps - before.Steps >= cells && !now.Moving;
                case "plannedCells":
                    return now.PlannedSteps - before.PlannedSteps >= cells && !now.Moving;
                case "planningFinished":
                    return now.PlanningFinished;
                case "documentOpen":
                    return now.DocumentOpen;
                case "documentClosed":
                    return !now.DocumentOpen;
                case "mapOpen":
                    return now.MapOpen;
                case "mapClosed":
                    return !now.MapOpen;
                case "perspectiveChanged":
                    return !now.Turning && now.MainPerspective != before.MainPerspective;
                case "mainWeaponChanged":
                    return before.MainSlot >= 0 && now.MainSlot >= 0 && now.MainSlot != before.MainSlot;
                case "subWeaponChanged":
                    return before.SubSlot >= 0 && now.SubSlot >= 0 && now.SubSlot != before.SubSlot;
                case "walking":
                    return now.KeyPressed && now.Walking;
                case "stopping":
                    return now.KeyPressed && now.Stopping;
                case "normalMovement":
                    return !now.Walking && !now.Stopping;
                default:
                    return false;
            }
        }
    }
}
