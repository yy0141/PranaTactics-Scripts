using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

using PranaTactics.AI.Solo;
using PranaTactics.GameFlow;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;

namespace PranaTactics.Editor
{
    /// <summary>
    /// ソロ試合のMacro経路追加・履歴復元を実際のローカル同期と補間移動で検証します。
    /// </summary>
    public static class MacroRouteValidation
    {
        [MenuItem("Tools/Prana Tactics/Validate Macro route assistance %#F9")]
        public static async void Validate()
        {
            if (!Application.isPlaying || GameFlowManager.Instance == null ||
                !GameFlowManager.Instance.IsSoloPlay ||
                UnitManager.Instance?.LocalUnit == null)
            {
                Debug.LogWarning("ソロ試合の再生中に実行してください。検証用ターンを開始し、最後に経路をリセットします。");

                return;
            }

            try
            {
                if (MatchPhaseManager.Instance.CurrentPhase != MatchPhase.Macro)
                {
                    MatchManager.Instance.ChangeState(new MacroPhaseState());
                }

                await WaitUntil(() => MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro);
                HUDManager.Instance.SetTimer(300);
                MacroUnitBehavior macro = UnitManager.Instance.LocalUnit.GetMacroUnitBehavior();
                UnitStatus status = UnitManager.Instance.LocalUnit.GetUnitStatus();

                await WaitUntil(() => macro.CanEditPath);

                if (macro.CanResetPath)
                {
                    macro.ResetPath();
                    await WaitUntil(() => macro.CanEditPath);
                }

                Vector2Int origin = macro.GetCurrentCoordinate();
                int originalBalance = status.Proactiveness;
                float originalSpeed = status.CurrentMoveSpeed;

                status.SetCurrentMoveSpeed(0.0f);
                var board = new AiBoardAdapter();
                Vector2Int destination = FindDestination(board, origin, macro.MaxStepHeight);

                macro.SetRoutePolicy(MacroRoutePolicy.Shortest);
                await WaitUntil(() => macro.TryAppendRoute(destination));
                await WaitUntil(() => macro.CanEditPath);
                Vector2Int[] firstPath = status.MigrationPath.ToArray();
                int firstBalance = status.Proactiveness;

                Check(firstPath.Length >= 3 && macro.GetCurrentCoordinate() == destination, "遠いマスへの追加と再生");
                Check(
                    firstBalance == originalBalance - firstPath.Length * status.MoveProactivenessCost,
                    "歩数分だけ消費");
                macro.SetRoutePolicy(MacroRoutePolicy.Safest);
                Check(status.MigrationPath.SequenceEqual(firstPath), "方針変更は既存経路を変更しない");
                Vector2Int secondDestination = board.GetNeighbors(destination).First(
                    c => board.CanStep(
                    destination,
                    c,
                    macro.MaxStepHeight));

                await WaitUntil(() => macro.TryAppendRoute(secondDestination));
                await WaitUntil(() => macro.CanEditPath);
                Check(status.MigrationPath.Count == firstPath.Length + 1, "隣接マスは一マスで追加");
                macro.StepPath(false);
                await WaitUntil(() => macro.CanEditPath);
                Check(
                    status.MigrationPath.SequenceEqual(firstPath) && status.Proactiveness == firstBalance,
                    "二回目のクリックだけ取消");
                macro.StepPath(false);
                await WaitUntil(() => macro.CanEditPath);
                Check(
                    status.MigrationPath.Count == 0 && status.Proactiveness == originalBalance &&
                    macro.GetCurrentCoordinate() == origin,
                    "一回目のクリック全体を取消");
                macro.StepPath(true);
                await WaitUntil(() => macro.CanEditPath);
                Check(
                    status.MigrationPath.SequenceEqual(firstPath) && status.Proactiveness == firstBalance &&
                    macro.GetCurrentCoordinate() == destination,
                    "経路全体のやり直し");
                macro.SetRoutePolicy(MacroRoutePolicy.Balanced);
                await WaitUntil(() => macro.TryAppendRoute(secondDestination));
                await WaitUntil(() => macro.CanEditPath);
                Check(!macro.CanRedoPath, "新しいクリックは旧やり直し履歴を破棄");
                macro.ResetPath();
                await WaitUntil(() => macro.CanEditPath);
                Check(
                    status.Proactiveness == originalBalance && status.MigrationPath.Count == 0 &&
                    macro.GetCurrentCoordinate() == origin,
                    "全リセット");
                macro.SetRoutePolicy(MacroRoutePolicy.Shortest);
                Check(status.CurrentMoveSpeed == 0.0f, "経路再生はミクロの停止速度を書き換えない");
                status.SetCurrentMoveSpeed(originalSpeed);
                string result = "PASS: runtime multi-cell append, exact movement cost, policy switching, click undo/redo, branch/reset and local snapshot playback with stopped Micro movement speed.";

                File.WriteAllText("Temp/MacroRouteValidation.txt", result);
                Debug.Log(result);
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/MacroRouteValidation.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }

        private static Vector2Int FindDestination(AiBoardAdapter board, Vector2Int origin, int maximumClimb)
        {
            var open = new Queue<Vector2Int>();
            var steps = new Dictionary<Vector2Int, int> { { origin, 0 } };

            open.Enqueue(origin);

            while (open.Count > 0)
            {
                Vector2Int current = open.Dequeue();

                if (steps[current] == 3)
                {
                    return current;
                }

                foreach (Vector2Int next in board.GetNeighbors(current))
                {
                    if (!steps.ContainsKey(next) && board.CanStep(current, next, maximumClimb))
                    {
                        steps[next] = steps[current] + 1;
                        open.Enqueue(next);
                    }
                }
            }

            throw new InvalidOperationException("3マス以上離れた到達可能マスがありません。");
        }

        private static async Task WaitUntil(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 30.0f;

            while (!predicate())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    throw new TimeoutException("経路編集または補間移動が完了しませんでした。");
                }

                await Awaitable.NextFrameAsync(Application.exitCancellationToken);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Macro経路検証失敗: " + message);
            }
        }
    }
}
