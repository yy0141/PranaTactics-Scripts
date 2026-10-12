using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

using PranaTactics.GameFlow;
using PranaTactics.GameFlow.States;
using PranaTactics.Match.Phases;
using PranaTactics.Tutorial;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;

namespace PranaTactics.Editor
{
    /// <summary>
    /// 実際の訓練シーンで経路編集・ボタンの可否・操作待機から戦闘への遷移を検証します。
    /// </summary>
    public static class TutorialPlanningValidation
    {
        private const BindingFlags k_Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Tools/Prana Tactics/Validate tutorial planning")]
        public static async void Validate()
        {
            if (!Application.isPlaying || GameFlowManager.Instance == null)
            {
                Debug.LogWarning("再生中に実行してください。訓練シーンへ移動します。");

                return;
            }

            string stage = "load";

            try
            {
                GameFlowManager.Instance.ChangeState(new TutorialState());
                await WaitUntil(
                    () => TutorialSession.Instance != null &&
                    Read<bool>(
                    TutorialSession.Instance,
                    "_isReady"));
                TutorialSession session = TutorialSession.Instance;

                Invoke(session, "GoTo", TutorialStep.Plan);
                MacroUnitBehavior macro = UnitManager.Instance.LocalUnit.GetMacroUnitBehavior();
                TutorialLessonContent lesson = TutorialLessonContent.Load(TutorialStep.Plan);

                for (int index = 0; index < lesson.sequence.Length; index++)
                {
                    TutorialPage page = lesson.sequence[index];

                    stage = index + ":" + page.waitFor;
                    File.WriteAllText("Temp/TutorialPlanningValidation.txt", "RUNNING: " + stage);
                    await WaitUntil(() => Read<int>(session, "_explanationPage") == index &&
                        (page.waitFor == "confirm" || !TutorialSession.BlocksInput));

                    if (page.waitFor == "confirm")
                    {
                        Invoke(session, "AdvanceExplanation");
                        continue;
                    }

                    await WaitUntil(() => macro.CanEditPath);

                    switch (page.waitFor)
                    {
                        case "planTarget":
                            await WaitUntil(
                                () => macro.TryAppendRoute(
                                new Vector2Int(
                                page.targetX,
                                page.targetY)));
                            break;
                        case "blockedClimb":
                            if (TutorialSession.AllowsPlanningClick(
                                macro,
                                new Vector2Int(
                                page.targetX,
                                page.targetY),
                                false))
                            {
                                throw new InvalidOperationException("段差実習で移動が許可されました。");
                            }
                            break;
                        case "routeShortest":
                            Click(MacroPlanningAction.ShortestRoute);
                            break;
                        case "routeBalanced":
                            Click(MacroPlanningAction.BalancedRoute);
                            break;
                        case "routeSafest":
                            Click(MacroPlanningAction.SafestRoute);
                            break;
                        case "pathUndo":
                            Click(MacroPlanningAction.Undo);
                            break;
                        case "pathRedo":
                            Click(MacroPlanningAction.Redo);
                            break;
                        case "pathReset":
                            Click(MacroPlanningAction.Reset);
                            await Awaitable.NextFrameAsync();
                            if (Read<int>(session, "_explanationPage") != index)
                            {
                                throw new InvalidOperationException("確認画面を開いただけでページが進みました。");
                            }
                            Click(MacroPlanningAction.AcceptConfirmation);
                            break;
                        case "planningConfirmed":
                            Click(MacroPlanningAction.Confirm);
                            await Awaitable.NextFrameAsync();
                            Click(MacroPlanningAction.AcceptConfirmation);
                            break;
                        default:
                            throw new InvalidOperationException("未対応の検証ページ: " + stage);
                    }

                    await WaitUntil(
                        () => Read<int>(session, "_explanationPage") != index ||
                        !Read<bool>(
                        session,
                        "_isExplaining"));
                }

                await WaitUntil(() => MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Micro);
                Invoke(session, "GoTo", TutorialStep.Plan);
                await WaitUntil(() => Read<int>(session, "_explanationPage") == 0);
                string result = "PASS: tutorial route append, height wait, all policies, click undo/redo, confirmed reset, explicit decision and Micro transition; retry initialized.";

                File.WriteAllText("Temp/TutorialPlanningValidation.txt", result);
                Debug.Log(result);
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/TutorialPlanningValidation.txt", stage + "\n" + exception);
                Debug.LogException(exception);
            }
        }

        private static void Click(MacroPlanningAction action)
        {
            MacroPlanningButton button = UnityEngine.Object.FindObjectsByType<MacroPlanningButton>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
                .First(b => Read<MacroPlanningAction>(b, "_action") == action);

            if (!button.gameObject.activeInHierarchy || !button.CanClick())
            {
                throw new InvalidOperationException("ボタンを操作できません: " + action);
            }

            button.SendMessage("OnClickEnter", SendMessageOptions.RequireReceiver);
        }

        private static T Read<T>(object target, string field)
        {
            return (T)target.GetType().GetField(field, k_Private).GetValue(target);
        }

        private static void Invoke(object target, string method, params object[] args)
        {
            target.GetType().GetMethod(method, k_Private).Invoke(target, args);
        }

        private static async Task WaitUntil(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 40;

            while (!predicate())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    throw new TimeoutException("チュートリアル操作が完了しませんでした。");
                }

                await Awaitable.NextFrameAsync(Application.exitCancellationToken);
            }
        }
    }
}
