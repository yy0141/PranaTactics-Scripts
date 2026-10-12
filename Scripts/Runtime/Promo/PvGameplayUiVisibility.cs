#if UNITY_EDITOR || PV_CAPTURE
using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Pranam;
using PranaTactics.UI.Gameplay;

namespace PranaTactics.Promo
{
    /// <summary>
    /// 通常UIの有効状態を変更せず、撮影用CanvasGroupで表示とクリック受付を抑制します。
    /// </summary>
    public sealed class PvGameplayUiVisibility : IDisposable
    {
        private readonly Dictionary<Transform, CanvasGroup> _groups = new Dictionary<Transform, CanvasGroup>();

        public bool IsVisible { get; private set; } = true;

        public void SetVisible(bool visible)
        {
            IsVisible = visible;

            if (visible)
            {
                Restore();
            }
            else
            {
                Refresh();
            }
        }

        /// <summary>
        /// フェイズ切替や遅延ロードで増えたCanvasにも非表示設定を適用します。
        /// </summary>
        public void Refresh()
        {
            if (IsVisible)
            {
                return;
            }

            var preserved = new HashSet<Transform>();

            foreach (MicroReticleView reticle in UnityEngine.Object.FindObjectsByType<MicroReticleView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                preserved.Add(reticle.transform);
            }

            foreach (SniperScopeController scope in UnityEngine.Object.FindObjectsByType<SniperScopeController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (scope.CaptureAdsRoot != null)
                {
                    preserved.Add(scope.CaptureAdsRoot.transform);
                }

                if (scope.CaptureScopeRoot != null)
                {
                    preserved.Add(scope.CaptureScopeRoot.transform);
                }
            }

            var ancestors = new HashSet<Transform>();

            foreach (Transform root in preserved)
            {
                for (Transform parent = root.parent; parent != null; parent = parent.parent)
                {
                    ancestors.Add(parent);
                }
            }

            var hidden = new HashSet<Transform>();

            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas)
                {
                    CollectHiddenBranches(canvas.transform, preserved, ancestors, hidden);
                }
            }

            var removed = new List<Transform>();

            foreach (var entry in _groups)
            {
                if (!hidden.Contains(entry.Key) || entry.Key == null)
                {
                    RestoreGroup(entry.Value);
                    removed.Add(entry.Key);
                }
            }

            foreach (Transform root in removed)
            {
                _groups.Remove(root);
            }

            foreach (Transform root in hidden)
            {
                if (!_groups.TryGetValue(root, out CanvasGroup group) || group == null)
                {
                    group = root.gameObject.AddComponent<CanvasGroup>();
                    _groups[root] = group;
                }

                group.alpha = 0;
                group.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 照準UIとその祖先を隠さず、同じCanvas内の通常HUDを枝ごとに分離します。
        /// </summary>
        private static void CollectHiddenBranches(Transform root, HashSet<Transform> preserved,
            HashSet<Transform> ancestors, HashSet<Transform> hidden)
        {
            if (preserved.Contains(root))
            {
                return;
            }

            if (!ancestors.Contains(root))
            {
                hidden.Add(root);

                return;
            }

            foreach (Transform child in root)
            {
                CollectHiddenBranches(child, preserved, ancestors, hidden);
            }
        }

        private static void RestoreGroup(CanvasGroup group)
        {
            if (group != null)
            {
                group.alpha = 1;
                group.blocksRaycasts = true;
                UnityEngine.Object.Destroy(group);
            }
        }

        private void Restore()
        {
            foreach (CanvasGroup group in _groups.Values)
            {
                RestoreGroup(group);
            }

            _groups.Clear();
        }

        public void Dispose()
        {
            Restore();
        }
    }
}
#endif
