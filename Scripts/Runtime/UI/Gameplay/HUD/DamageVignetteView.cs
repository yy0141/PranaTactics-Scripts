using PranaTactics.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 自分が敵から被弾したとき、画面の縁に赤いグラデーションを表示します。
    /// </summary>
    public sealed class DamageVignetteView : MaskableGraphic
    {
        [SerializeField]
        private DamageVignetteProfile _profile;

        private float _hitTime;

        private float _intensity;

        private UnitBehavior _damagedUnit;

        /// <summary>
        /// ダメージ通知を購読し、入力を遮らない透明な初期状態にします。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            _intensity = 0f;
            UnitBehavior.DamageApplied += OnDamageApplied;
            SetVerticesDirty();
        }

        /// <summary>
        /// シーン終了時に購読と被弾状態を解除します。
        /// </summary>
        protected override void OnDisable()
        {
            UnitBehavior.DamageApplied -= OnDamageApplied;
            _damagedUnit = null;
            _intensity = 0f;
            base.OnDisable();
        }

        /// <summary>
        /// ローカルプレイヤーへの敵の攻撃だけを表示し、連続被弾では表示時間を更新します。
        /// </summary>
        private void OnDamageApplied(UnitBehavior target, int attackerId)
        {
            UnitManager manager = UnitManager.Instance;

            if (_profile == null || manager == null || attackerId < 0 || manager.ControlledUnitId < 0 ||
                !manager.TryGetUnit(manager.ControlledUnitId, out UnitBehavior localUnit) || target != localUnit ||
                !manager.TryGetUnit(attackerId, out UnitBehavior attacker) || attacker == target ||
                attacker.GetTeamId() < 0 || target.GetTeamId() < 0 || attacker.GetTeamId() == target.GetTeamId())
            {
                return;
            }

            _damagedUnit = target;
            _hitTime = Time.unscaledTime;
            _intensity = 1f;
            SetVerticesDirty();
        }

        /// <summary>
        /// ポーズ中でも時間経過に応じて縁の赤色を滑らかに消します。
        /// </summary>
        private void Update()
        {
            if (_intensity <= 0f)
            {
                return;
            }

            UnitManager manager = UnitManager.Instance;

            if (_profile == null)
            {
                _intensity = 0f;
                SetVerticesDirty();

                return;
            }

            bool isStillLocalUnit = manager != null && _damagedUnit != null &&
                manager.TryGetUnit(manager.ControlledUnitId, out UnitBehavior localUnit) && localUnit == _damagedUnit;
            float elapsed = Time.unscaledTime - _hitTime - _profile.HoldDuration;

            _intensity = isStillLocalUnit ? 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(
                elapsed / _profile.FadeDuration)) : 0f;
            SetVerticesDirty();
        }

        /// <summary>
        /// 中央を透明に保ち、画面端から内側へ薄くなる四辺の帯を描画します。
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();

            if (_profile == null || _intensity <= 0f)
            {
                return;
            }

            Rect rect = rectTransform.rect;
            const int k_RingCount = 8;

            for (int ring = 0; ring < k_RingCount; ring++)
            {
                float outerRatio = (float)ring / k_RingCount;
                float innerRatio = (float)(ring + 1) / k_RingCount;

                for (int side = 0; side < 4; side++)
                {
                    int start = vertices.currentVertCount;

                    AddCorner(vertices, rect, side, outerRatio);
                    AddCorner(vertices, rect, (side + 1) % 4, outerRatio);
                    AddCorner(vertices, rect, (side + 1) % 4, innerRatio);
                    AddCorner(vertices, rect, side, innerRatio);
                    vertices.AddTriangle(start, start + 1, start + 2);
                    vertices.AddTriangle(start, start + 2, start + 3);
                }
            }
        }

        /// <summary>
        /// 画面端からの距離に応じた位置と透明度で頂点を追加します。
        /// </summary>
        private void AddCorner(VertexHelper vertices, Rect rect, int corner, float ratio)
        {
            float inset = Mathf.Min(rect.width, rect.height) * _profile.EdgeWidth * ratio;
            float x = corner == 0 || corner == 3 ? rect.xMin + inset : rect.xMax - inset;
            float y = corner < 2 ? rect.yMin + inset : rect.yMax - inset;
            Color tint = _profile.Color;

            tint.a *= _intensity * (1f - ratio) * (1f - ratio);
            vertices.AddVert(new Vector3(x, y, 0f), tint, Vector2.zero);
        }
    }
}
