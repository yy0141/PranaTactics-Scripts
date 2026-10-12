using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

using PranaTactics.Unit.Presentation;

namespace PranaTactics.Editor
{
    /// <summary>
    /// 設定アセットからSceneビューで撃破演出をプレビューします。
    /// </summary>
    [CustomEditor(typeof(UnitEliminationProfile))]
    public sealed class UnitEliminationProfileEditor : UnityEditor.Editor
    {
        /// <summary>
        /// プレビュー全体の周囲に確保する表示余白です。
        /// </summary>
        private const float k_PreviewFramingMargin = 1.15f;

        /// <summary>
        /// 保存しないプレビュー用オブジェクトです。
        /// </summary>
        private UnitEliminationEffect _preview;
        /// <summary>
        /// プレビュー開始時刻です。
        /// </summary>
        private double _startedAt;
        /// <summary>
        /// 最大サイズで形状を確認する停止プレビューかどうかです。
        /// </summary>
        private bool _isPreviewPaused;

        /// <summary>
        /// 設定項目とプレビューボタンを表示します。
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Rim Widthで縁の幅を調整できます。色のAは透明度です。\nプレビュー開始時に球体と上下の線が収まる距離へ自動調整します。", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Sceneビューでプレビュー"))
                {
                    StartPreview(false);
                }

                if (GUILayout.Button("形状確認（最大サイズで停止）"))
                {
                    StartPreview(true);
                }

                if (_preview != null && GUILayout.Button("プレビュー全体を表示"))
                {
                    FramePreview();
                }

                if (_preview != null && GUILayout.Button("プレビューを終了"))
                {
                    StopPreview();
                }
            }
        }

        /// <summary>
        /// 編集中のシーンを汚さない一時プレビューを開始します。
        /// </summary>
        private void StartPreview(bool isPaused)
        {
            StopPreview();
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 center = view != null ? view.pivot : Vector3.zero;
            _preview = UnitEliminationEffect.Spawn((UnitEliminationProfile)target,
                center, SceneManager.GetActiveScene());
            if (_preview == null)
            {
                return;
            }

            foreach (Transform child in _preview.GetComponentsInChildren<Transform>())
            {
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            FramePreview();
            _startedAt = EditorApplication.timeSinceStartup;
            _isPreviewPaused = isPaused;
            EditorApplication.update += UpdatePreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>
        /// 最大サイズの球体と上下の線が収まるようSceneビューを即座に移動します。
        /// </summary>
        private void FramePreview()
        {
            if (_preview == null)
            {
                return;
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                view = EditorWindow.GetWindow<SceneView>();
            }

            var profile = (UnitEliminationProfile)target;
            float lineHalfWidth = profile.LineWidth * 0.5f;
            float halfExtent = Mathf.Max(1.0f, profile.ExpandedRadius,
                profile.LineLength + lineHalfWidth,
                profile.LightningAmplitude + lineHalfWidth);
            var bounds = new Bounds(_preview.transform.position,
                Vector3.one * (halfExtent * 2.0f * k_PreviewFramingMargin));
            view.Frame(bounds, true);
            view.Repaint();
        }

        /// <summary>
        /// Editorの時刻で再生し、終了時に後始末を行います。
        /// </summary>
        private void UpdatePreview()
        {
            double elapsed = _isPreviewPaused
                ? ((UnitEliminationProfile)target).ExpansionDuration
                : EditorApplication.timeSinceStartup - _startedAt;
            if (_preview == null || !_preview.RenderAtTime(elapsed))
            {
                StopPreview();
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Playモード移行前にプレビューを破棄します。
        /// </summary>
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            StopPreview();
        }

        /// <summary>
        /// Inspectorの終了や再コンパイル時に購読を解除します。
        /// </summary>
        private void OnDisable()
        {
            StopPreview();
        }

        /// <summary>
        /// 一時オブジェクトとEditorイベント購読を解放します。
        /// </summary>
        private void StopPreview()
        {
            EditorApplication.update -= UpdatePreview;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (_preview != null)
            {
                DestroyImmediate(_preview.gameObject);
                _preview = null;
            }
        }
    }
}
