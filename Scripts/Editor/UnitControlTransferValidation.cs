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
    /// ソロ試合で連続撃破と操作引き継ぎ、残ったNPCの経路実行を検証します。
    /// </summary>
    public static class UnitControlTransferValidation
    {
        [MenuItem("Tools/Prana Tactics/Validate unit control transfer %#F11")]
        public static async void Validate()
        {
            if (!Application.isPlaying || GameFlowManager.Instance == null ||
                !GameFlowManager.Instance.IsSoloPlay)
            {
                Debug.LogWarning("検証用のソロ試合で実行してください。操作ユニットを撃破します。");

                return;
            }

            var report = new List<string>();
            var errors = new List<string>();
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error)
                {
                    errors.Add(message + "\n" + stack);
                }
            };

            Application.logMessageReceived += capture;

            try
            {
                UnitManager manager = UnitManager.Instance;
                int sessionId = manager.LocalPlayerId;
                int teamId = manager.LocalUnit.GetTeamId();
                int attackerId = manager.GetAllUnitBehaviors().First(unit => unit.GetTeamId() != teamId).PlayerId;

                foreach (UnitBehavior unit in manager.GetAllUnitBehaviors())
                {
                    if (manager.IsAIControlledUnit(unit.PlayerId))
                    {
                        manager.SetAIWeaponUsageEnabled(unit.PlayerId, false);
                    }
                }

                MatchManager.Instance.ChangeState(new MacroPhaseState());
                await WaitUntil(() => MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro);
                HUDManager.Instance.SetTimer(300);
                await WaitUntil(() => SoloAiDirector.IsMacroPlanningComplete);
                UnitBehavior[] bots = manager.GetAllUnitBehaviors().Where(unit => unit.GetTeamId() == teamId &&
                    manager.IsAIControlledUnit(unit.PlayerId) && unit.GetUnitStatus().IsAlive).ToArray();

                Require(bots.Length >= 2, "味方NPCが2体以上必要");
                MatchManager.Instance.ChangeState(new MicroPhaseState());
                await WaitUntil(() => MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Micro);
                HUDManager.Instance.SetTimer(300);
                await Awaitable.NextFrameAsync(Application.exitCancellationToken);
                UnitBehavior original = manager.LocalUnit;

                foreach (UnitBehavior bot in bots)
                {
                    bot.GetUnitStatus().SetHealth(bot.GetUnitStatus().MaxHealth * 0.35f);
                }

                original.ApplyDamage(attackerId, original.GetUnitStatus().MaxHealth * 2, true);
                UnitBehavior successor = manager.LocalUnit;

                Require(successor != original && bots.Contains(successor), "味方NPCへ切り替え");
                Require(
                    manager.LocalPlayerId == sessionId && manager.ControlledUnitId == successor.PlayerId,
                    "接続IDと操作IDを分離");
                Require(!manager.IsAIControlledUnit(successor.PlayerId), "引き継ぎ先のAI入力を解除");
                Require(original.GetUnitStatus().IsDead && !original.GetUnitStatus().IsAlive, "死亡確定");
                original.ApplySetDead();
                Require(manager.LocalUnit == successor, "重複死亡通知で再切り替えしない");
                Require(successor.GetMicroUnitBehavior().UnitCamera.gameObject.activeInHierarchy &&
                    successor.GetMicroUnitBehavior().UnitCamera.enabled, "新カメラ描画有効");
                Require(!original.GetMicroUnitBehavior().UnitCamera.enabled, "旧カメラ描画無効");
                Require(
                    !successor.GetMacroUnitBehavior().UnitCamera.gameObject.activeSelf,
                    "Micro中のMacroカメラ無効");
                report.Add("PASS transfer, identity, AI exclusion, death idempotency, cameras");
                await Awaitable.NextFrameAsync(Application.exitCancellationToken);
                ValidateHealthGauge(successor);
                report.Add("PASS damaged successor HP in both gauge images after animated lethal damage");
                UnitBehavior remaining = bots.First(bot => bot != successor);
                Vector3 position = remaining.GetMicroUnitBehavior().transform.position;

                await WaitUntil(
                    () => remaining == null ||
                    Vector3.Distance(
                    position,
                    remaining.GetMicroUnitBehavior().transform.position) > 0.1f);
                Require(remaining != null && remaining.GetUnitStatus().IsAlive, "残った味方NPCが移動を継続");
                report.Add("PASS remaining ally NPC movement");
                await Awaitable.WaitForSecondsAsync(3, Application.exitCancellationToken);
                ValidateHealthGauge(successor);
                successor.ApplyDamage(attackerId, successor.GetUnitStatus().MaxHealth * 2, false);
                Require(manager.LocalUnit != successor && manager.LocalUnit.GetTeamId() == teamId &&
                    manager.LocalUnit.GetUnitStatus().IsAlive,
                    "連続撃破でも次の味方へ切り替え");
                report.Add("PASS repeated transfer after bailout");
                await Awaitable.NextFrameAsync(Application.exitCancellationToken);
                ValidateHealthGauge(manager.LocalUnit);
                Require(errors.Count == 0, "実行時エラー: " + string.Join("\n", errors));
                report.Add("PASS no runtime exceptions");
                Debug.Log("Unit control transfer validation passed.");
            }
            catch (Exception exception)
            {
                report.Add("FAIL " + exception);
                report.AddRange(errors);
                Debug.LogException(exception);
            }
            finally
            {
                Application.logMessageReceived -= capture;
                File.WriteAllLines("Temp/UnitControlTransferValidation.txt", report);
            }
        }

        private static async Task WaitUntil(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 30;

            while (!predicate())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    throw new TimeoutException("AI計画または移動が完了しませんでした。");
                }

                await Awaitable.NextFrameAsync(Application.exitCancellationToken);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// 通常ゲージと遅延ダメージゲージが、引き継ぎ先のHP割合を表示していることを確認します。
        /// </summary>
        private static void ValidateHealthGauge(UnitBehavior unit)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var gauge = (HealthGauge)typeof(HUDManager).GetField("_healthGauge", flags).GetValue(
                HUDManager.Instance);
            float expected = unit.GetUnitStatus().CurrentHealth / unit.GetUnitStatus().MaxHealth;

            Require(Mathf.Approximately(gauge.CurrentRate, expected), "HPゲージの対象値");

            foreach (string name in new[] { "_healthImage", "_damageImage" })
            {
                var image = (UnityEngine.UI.Image)typeof(HealthGauge).GetField(name, flags).GetValue(gauge);

                Require(Mathf.Approximately(image.fillAmount, expected), name + "が引き継ぎ先のHPと一致");
            }
        }
    }
}
