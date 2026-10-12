using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Combat;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 命中・防御・撃破を別の線形状で表示します。
    /// </summary>
    public sealed class CombatFeedbackMarker : MaskableGraphic
    {
        /// <summary>
        /// 現在表示している結果の種別です。
        /// </summary>
        private CombatFeedbackKind _kind;

        /// <summary>
        /// マーカーの表示期限です。
        /// </summary>
        private float _until;

        /// <summary>
        /// 最後に結果を更新したフレームです。
        /// </summary>
        private int _resultFrame = -1;

        /// <summary>
        /// 確定した結果を表示し、撃破の表示期限を保護します。
        /// </summary>
        public void ShowResult(CombatFeedbackKind kind, MicroReticleProfile profile)
        {
            if (Time.unscaledTime < _until && kind < _kind &&
                (_kind == CombatFeedbackKind.Elimination || _resultFrame == Time.frameCount))
            {
                return;
            }

            _kind = kind;
            _resultFrame = Time.frameCount;
            _until = Time.unscaledTime + profile.GetFeedbackDuration(kind);
            color = profile.GetFeedbackColor(kind);
            raycastTarget = false;
            SetVerticesDirty();
        }

        /// <summary>
        /// Micro表示の可否と結果の期限に応じて表示を切り替えます。
        /// </summary>
        public void RefreshVisibility(bool canShow)
        {
            enabled = canShow && Time.unscaledTime < _until;
        }

        /// <summary>
        /// 操作対象の変更時に表示を解除します。
        /// </summary>
        public void ResetResult()
        {
            _until = 0.0f;
            _resultFrame = -1;
            enabled = false;
        }

        /// <summary>
        /// 結果に対応するX字、菱形、撃破紋章を描画します。
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (_kind == CombatFeedbackKind.DefenseHit)
            {
                AddLine(vertices, new Vector2(0, 10), new Vector2(10, 0));
                AddLine(vertices, new Vector2(10, 0), new Vector2(0, -10));
                AddLine(vertices, new Vector2(0, -10), new Vector2(-10, 0));
                AddLine(vertices, new Vector2(-10, 0), new Vector2(0, 10));
                return;
            }

            float outer = _kind == CombatFeedbackKind.Elimination ? 12.0f : 8.0f;
            AddLine(vertices, new Vector2(-outer, -outer), new Vector2(-3, -3));
            AddLine(vertices, new Vector2(outer, -outer), new Vector2(3, -3));
            AddLine(vertices, new Vector2(-outer, outer), new Vector2(-3, 3));
            AddLine(vertices, new Vector2(outer, outer), new Vector2(3, 3));

            if (_kind == CombatFeedbackKind.Elimination)
            {
                AddLine(vertices, new Vector2(-7, 1), new Vector2(0, -6));
                AddLine(vertices, new Vector2(0, -6), new Vector2(7, 1));
            }
        }

        /// <summary>
        /// 2点を結ぶ一定幅の線を追加します。
        /// </summary>
        private void AddLine(VertexHelper vertices, Vector2 start, Vector2 end)
        {
            Vector2 normal = new Vector2(-(end - start).y, (end - start).x).normalized;
            normal *= _kind == CombatFeedbackKind.Elimination ? 1.5f : 1.0f;
            int first = vertices.currentVertCount;
            vertices.AddVert(start - normal, color, Vector2.zero);
            vertices.AddVert(start + normal, color, Vector2.zero);
            vertices.AddVert(end + normal, color, Vector2.zero);
            vertices.AddVert(end - normal, color, Vector2.zero);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first, first + 2, first + 3);
        }
    }
}
