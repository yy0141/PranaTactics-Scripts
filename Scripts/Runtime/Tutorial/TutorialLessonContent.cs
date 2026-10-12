using System;
using UnityEngine;

namespace PranaTactics.Tutorial
{
    /// <summary>
    /// 説明文と、そのページを進めるための操作・達成条件を保持します。
    /// </summary>
    /// <remarks>
    /// publicフィールド名は既存のJSONキーと一致させ、読み込み互換性を維持します。
    /// </remarks>
    [Serializable]
    public sealed class TutorialPage
    {
        public string text;

        public string waitFor = "confirm";

        public string key;

        public string[] keys;

        public int cells;

        public int targetX;

        public int targetY;
    }

    /// <summary>
    /// Resources/Tutorial/Lessons内のレッスン別JSONです。
    /// </summary>
    /// <remarks>
    /// publicフィールド名は既存のJSONキーを維持します。
    /// </remarks>
    [Serializable]
    public sealed class TutorialLessonContent
    {
        public string title;

        public string[] pages;

        public string[] pagesWhenToggle;

        public string objective;

        public TutorialPage[] sequence;

        public bool completeOnSequenceEnd;

        public int planningStartX;

        public int planningStartY;

        /// <summary>
        /// 操作待機シーケンスを優先し、未設定なら移動入力方式に対応する説明を返します。
        /// </summary>
        public TutorialPage[] GetPages(bool holdMovement)
        {
            if (sequence != null && sequence.Length > 0)
            {
                return sequence;
            }

            string[] source = !holdMovement && pagesWhenToggle != null && pagesWhenToggle.Length > 0 ? pagesWhenToggle : pages;

            return Array.ConvertAll(source, text => new TutorialPage { text = text, waitFor = "confirm" });
        }

        /// <summary>
        /// 待機条件名、必要マス数、キー指定を検証します。
        /// </summary>
        /// <param name="path">
        /// 読み込み失敗時のメッセージに表示するリソースパスです。
        /// </param>
        public void ValidateSequence(string path)
        {
            if (sequence == null || sequence.Length == 0)
            {
                return;
            }

            foreach (TutorialPage page in sequence)
            {
                if (page == null)
                {
                    throw new InvalidOperationException("空の説明ページ: " + path);
                }

                switch (page.waitFor)
                {
                    case "cameraPan":
                    case "cameraReset":
                    case "macroUiHidden":
                    case "macroUiShown":
                    case "targetConfirmed":
                    case "scrollDown":
                    case "scrollUp":
                    case "confirm":
                    case "mapOpen":
                    case "mapClosed":
                    case "perspectiveChanged":
                    case "mainWeaponChanged":
                    case "subWeaponChanged":
                    case "walking":
                    case "stopping":
                    case "normalMovement":
                    case "planningFinished":
                    case "routeShortest":
                    case "routeBalanced":
                    case "routeSafest":
                    case "pathUndo":
                    case "pathRedo":
                    case "pathReset":
                    case "planningConfirmed":
                    case "documentOpen":
                    case "documentClosed":
                    case "primaryFire":
                    case "secondaryFire":
                        break;
                    case "targetCandidate":
                    case "planTarget":
                    case "blockedClimb":
                        if (page.targetX <= 0 || page.targetY <= 0)
                        {
                            throw new InvalidOperationException("段差実習の移動先座標を指定してください: " + path);
                        }

                        break;
                    case "plannedCells":
                    case "moveCells":
                        if (page.cells <= 0)
                        {
                            throw new InvalidOperationException("cellsには正の移動マス数を指定してください: " + path);
                        }

                        break;
                    case "key":
                        if (!Enum.TryParse(page.key, out KeyCode key) || key == KeyCode.None)
                        {
                            throw new InvalidOperationException("不明な待機キー: " + path);
                        }

                        break;
                    case "keysAll":
                        if (page.keys == null || page.keys.Length == 0)
                        {
                            throw new InvalidOperationException("keysには待機するキーを指定してください: " + path);
                        }

                        var uniqueKeys = new System.Collections.Generic.HashSet<KeyCode>();
                        foreach (string name in page.keys)
                        {
                            if (!Enum.TryParse(name, out KeyCode requiredKey) || requiredKey == KeyCode.None ||
                                !Enum.IsDefined(
                                typeof(KeyCode),
                                requiredKey) ||
                                !uniqueKeys.Add(
                                requiredKey))
                            {
                                throw new InvalidOperationException("不明または重複した待機キー: " + name + " / " + path);
                            }
                        }

                        break;
                    default:
                        throw new InvalidOperationException("不明なwaitFor: " + page.waitFor + " / " + path);
                }
            }
        }

        /// <summary>
        /// 課題に対応するJSONを読み込み、必須項目と待機条件を検証します。
        /// </summary>
        public static TutorialLessonContent Load(TutorialStep step)
        {
            string path = "Tutorial/Lessons/" + step;
            TextAsset asset = Resources.Load<TextAsset>(path);

            if (asset == null)
            {
                throw new InvalidOperationException("説明JSONがありません: " + path);
            }

            TutorialLessonContent content = JsonUtility.FromJson<TutorialLessonContent>(asset.text);

            if (content == null || string.IsNullOrWhiteSpace(content.title) ||
                ((content.sequence == null ||
                content.sequence.Length == 0) &&
                (content.pages == null ||
                content.pages.Length == 0 ||
                Array.Exists(
                content.pages,
                string.IsNullOrWhiteSpace))) ||
                string.IsNullOrWhiteSpace(
                content.objective) ||
                (content.pagesWhenToggle != null &&
                Array.Exists(
                content.pagesWhenToggle,
                string.IsNullOrWhiteSpace)))
            {
                throw new InvalidOperationException("説明JSONのtitle/pages/objectiveを確認してください: " + path);
            }

            content.ValidateSequence(path);

            return content;
        }
    }
}
