using UnityEngine;
using UnityEngine.UI;

using PranaTactics.Combat;
using PranaTactics.Match.Phases;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// ローカルプレイヤーのMicro視点に照準と敵への命中フィードバックを表示します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MicroReticleView : MonoBehaviour
    {
        /// <summary>
        /// 通常レティクルを表示する画像です。
        /// </summary>
        [SerializeField]
        private Image _image;

        /// <summary>
        /// レティクルと攻撃結果の表示設定です。
        /// </summary>
        [SerializeField]
        private MicroReticleProfile _profile;

        /// <summary>
        /// 現在表示する操作対象です。
        /// </summary>
        private UnitBehavior _unit;

        /// <summary>
        /// 操作対象のスコープ表示です。
        /// </summary>
        private SniperScopeController _scope;

        /// <summary>
        /// 命中色の表示期限です。
        /// </summary>
        private float _hitUntil;

        /// <summary>
        /// 発動元別の結果マーカーです。
        /// </summary>
        private readonly CombatFeedbackMarker[] _markers = new CombatFeedbackMarker[3];

        /// <summary>
        /// ダメージ確定通知を購読し、初期表示を隠します。
        /// </summary>
        private void OnEnable()
        {
            CreateFeedbackMarkers();
            CombatFeedbackEvents.ResultConfirmed += OnCombatResultConfirmed;
            _hitUntil = 0f;

            if (_image != null)
            {
                _image.enabled = false;
            }
        }

        /// <summary>
        /// シーンの終了時に購読と表示状態を解除します。
        /// </summary>
        private void OnDisable()
        {
            CombatFeedbackEvents.ResultConfirmed -= OnCombatResultConfirmed;
            ResetFeedbackMarkers();
            _unit = null;
            _scope = null;

            if (_image != null)
            {
                _image.enabled = false;
            }
        }

        /// <summary>
        /// 実際に表示しているMicroカメラとスナイパー照準に合わせて表示を更新します。
        /// </summary>
        private void LateUpdate()
        {
            if (_image == null)
            {
                return;
            }

            if (_profile == null)
            {
                _image.enabled = false;
                ResetFeedbackMarkers();

                return;
            }

            UnitManager manager = UnitManager.Instance;
            UnitBehavior localUnit = null;

            if (manager != null)
            {
                manager.TryGetUnit(manager.ControlledUnitId, out localUnit);
            }

            if (_unit != localUnit)
            {
                RefreshControlledUnit(localUnit);
            }

            MicroUnitBehavior micro = _unit != null ? _unit.GetMicroUnitBehavior() : null;
            Camera camera = micro != null ? micro.UnitCamera : null;
            bool isAiming = _scope != null && _scope.IsAiming;
            bool isVisible = camera != null && (camera.isActiveAndEnabled || isAiming) &&
                MatchPhaseManager.Instance != null && MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Micro &&
                _unit.GetTacticalMapUnitBehavior() != null && !_unit.GetTacticalMapUnitBehavior().IsMapMode &&
                _unit.GetUnitStatus() != null && _unit.GetUnitStatus().CurrentHealth > 0f;

            _image.enabled = isVisible && !isAiming;
            _image.raycastTarget = false;
            _image.sprite = _profile.Sprite;
            _image.preserveAspect = true;
            _image.rectTransform.sizeDelta = _profile.Size;
            _image.color = Time.unscaledTime < _hitUntil ? _profile.HitColor : _profile.NormalColor;

            for (int index = 0; index < _markers.Length; index++)
            {
                if (_markers[index] != null)
                {
                    float offset = index == (int)CombatFeedbackSide.Main ? -_profile.FeedbackSideOffset :
                        index == (int)CombatFeedbackSide.Sub ? _profile.FeedbackSideOffset : 0.0f;
                    _markers[index].rectTransform.anchoredPosition = new Vector2(offset, 0.0f);
                    _markers[index].RefreshVisibility(isVisible);
                }
            }

            if (!isVisible)
            {
                ResetFeedbackMarkers();
                _hitUntil = 0.0f;
            }
        }

        /// <summary>
        /// 公開可能な確定結果を、対応する発動元の照準へ表示します。
        /// </summary>
        private void OnCombatResultConfirmed(CombatFeedbackResult result)
        {
            if (_profile == null || !CombatFeedbackVisibility.CanPresent(result))
            {
                return;
            }

            UnitManager.Instance.TryGetUnit(result.AttackerId, out UnitBehavior attacker);
            if (_unit != attacker)
            {
                RefreshControlledUnit(attacker);
            }

            if (result.Kind == CombatFeedbackKind.UnitHit || result.Kind == CombatFeedbackKind.Elimination)
            {
                _hitUntil = Time.unscaledTime + _profile.HitDuration;
            }

            int index = (int)result.Side;
            if (index >= 0 && index < _markers.Length && _markers[index] != null)
            {
                _markers[index].ShowResult(result.Kind, _profile);
            }
        }

        /// <summary>
        /// 画像の子として非操作型の結果マーカーを作成します。
        /// </summary>
        private void CreateFeedbackMarkers()
        {
            if (_image == null)
            {
                return;
            }

            for (int index = 0; index < _markers.Length; index++)
            {
                if (_markers[index] != null)
                {
                    continue;
                }

                var markerObject = new GameObject(
                    $"Feedback_{(CombatFeedbackSide)index}", typeof(RectTransform));
                markerObject.transform.SetParent(_image.transform, false);
                Canvas canvas = markerObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 150;
                CombatFeedbackMarker marker = markerObject.AddComponent<CombatFeedbackMarker>();
                marker.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                marker.rectTransform.sizeDelta = new Vector2(28.0f, 28.0f);
                marker.raycastTarget = false;
                marker.ResetResult();
                _markers[index] = marker;
            }
        }

        /// <summary>
        /// 操作対象とスコープ参照を更新して古い結果を消去します。
        /// </summary>
        private void RefreshControlledUnit(UnitBehavior unit)
        {
            _unit = unit;
            MicroUnitBehavior micro = unit != null ? unit.GetMicroUnitBehavior() : null;
            _scope = micro != null ? micro.GetComponentInChildren<SniperScopeController>(true) : null;
            _hitUntil = 0.0f;
            ResetFeedbackMarkers();
        }

        /// <summary>
        /// 全発動元のマーカーを解除します。
        /// </summary>
        private void ResetFeedbackMarkers()
        {
            foreach (CombatFeedbackMarker marker in _markers)
            {
                marker?.ResetResult();
            }
        }
    }
}
