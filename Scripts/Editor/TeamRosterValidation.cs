using System;
using System.IO;

using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.Editor
{
    /// <summary>
    /// 実際のアイコンPrefabで画像、陣営枠、HP透過、死亡と復帰の表示を検証します。
    /// </summary>
    public static class TeamRosterValidation
    {
        [MenuItem("Tools/Prana Tactics/Validate team roster %#F7")]
        public static void Validate()
        {
            var root = new GameObject("Roster validation", typeof(RectTransform));

            root.SetActive(false);

            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/TeamRosterIcon.prefab");
                UnitParams character = AssetDatabase.LoadAssetAtPath<UnitParams>(
                    "Assets/Datas/CharacterAssets/Attacker.asset");
                GameObject instance = UnityEngine.Object.Instantiate(prefab, root.transform);
                TeamRosterIconView icon = instance.GetComponent<TeamRosterIconView>();
                Image portrait = instance.transform.Find("Character Portrait").GetComponent<Image>();
                Image frame = instance.GetComponent<Image>();
                GameObject deathMark = instance.transform.Find("Death Cross").gameObject;
                var status = new UnitStatus(7, 1, "Validation", 100, 100.0f, 1.0f, 1.0f, 1.0f, 1, character);

                icon.Bind(status);
                icon.Refresh(true, Color.blue, true);
                Require(
                    portrait.sprite == character.GameplaySprite && portrait.sprite != null,
                    "Selection icon sprite");
                Require(
                    frame.color == Color.blue && portrait.color.a == 1.0f && !deathMark.activeSelf,
                    "Healthy ally");
                status.SetHealth(50.0f);
                icon.Refresh(true, Color.blue, true);
                Require(portrait.color.a == 0.75f, "Half HP ally opacity");
                icon.Refresh(false, Color.red, true);
                Require(frame.color == Color.red && portrait.color.a == 1.0f, "Enemy HP stays concealed");
                status.SetHealth(0.0f);
                status.SetDead(true);

                foreach (bool isAlly in new[] { true, false })
                {
                    icon.Refresh(isAlly, isAlly ? Color.blue : Color.red, true);
                    Require(portrait.color.a == 0.5f && deathMark.activeSelf, "Dead icon and cross");
                }

                status.SetDead(false);
                status.SetHealth(100.0f);
                icon.Refresh(true, Color.blue, true);
                Require(portrait.color.a == 1.0f && !deathMark.activeSelf, "Revival clears death marker");

                foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true))
                {
                    Require(!graphic.raycastTarget, "Roster must not intercept gameplay input");
                }

                string report = "PASS: prefab references, character selection icon, team frames, allied HP alpha, enemy HP concealment, death markers, revival, input passthrough.";

                File.WriteAllText("Temp/TeamRosterValidation.txt", report);
                Debug.Log(report);
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/TeamRosterValidation.txt", "FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 表示契約の違反を検出します。
        /// </summary>
        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
