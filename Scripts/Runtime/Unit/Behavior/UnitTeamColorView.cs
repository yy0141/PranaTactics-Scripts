using UnityEngine;

using PranaTactics.Match;
using PranaTactics.Settings;
using PranaTactics.UI.Gameplay;

namespace PranaTactics.Unit
{
    /// <summary>
    /// 保存したチームカラーを操作盤面とマップのモデルへ反映します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitBehavior))]
    public sealed class UnitTeamColorView : MonoBehaviour
    {
        private UnitBehavior _unit;
        private MaterialPropertyBlock _properties;
        private bool _needsRefresh = true;
        private int _lastLocalTeamId = -1;
        private int _lastTeamId = -1;
        private int _lastRosterSignature = -1;

        /// <summary>
        /// 表示対象を取得します。
        /// </summary>
        private void Awake()
        {
            _unit = GetComponent<UnitBehavior>();
            _properties = new MaterialPropertyBlock();
        }

        /// <summary>
        /// 設定変更を購読します。
        /// </summary>
        private void OnEnable()
        {
            SettingsManager.SettingsChanged += Invalidate;
            _needsRefresh = true;
        }

        /// <summary>
        /// 次回の描画前に保存色を反映します。
        /// </summary>
        public void Invalidate()
        {
            _needsRefresh = true;
        }

        /// <summary>
        /// チームの確定と設定変更を検出してモデルの色を更新します。
        /// </summary>
        private void LateUpdate()
        {
#if UNITY_EDITOR || PV_CAPTURE
            if (MatchManager.Instance != null && MatchManager.Instance.CaptureScenario != null)
            {
                return;
            }
#endif
            UnitManager units = UnitManager.Instance;
            if (_unit == null || units == null || units.LocalPlayerId < 0)
            {
                return;
            }

            int localTeam = units.GetTeamId(units.LocalPlayerId);
            int team = _unit.GetTeamId();
            int rosterSignature = 0;
            foreach (UnitBehavior unit in units.GetAllUnitBehaviors())
            {
                if (unit != null)
                {
                    rosterSignature = unchecked(rosterSignature + (unit.PlayerId * 397 ^ unit.GetTeamId()));
                }
            }

            if (team < 0 || localTeam < 0)
            {
                return;
            }

            if (!_needsRefresh && _lastLocalTeamId == localTeam && _lastTeamId == team && _lastRosterSignature == rosterSignature)
            {
                return;
            }

            Color color = GameplayColorSettings.GetTeamColor(team);
            ApplyColor(_unit.GetMacroUnitBehavior()?.GetModel(), color);
            ApplyColor(_unit.GetTacticalMapUnitBehavior()?.GetModel(), color);
            _lastLocalTeamId = localTeam;
            _lastTeamId = team;
            _lastRosterSignature = rosterSignature;
            _needsRefresh = false;
        }

        /// <summary>
        /// 共有マテリアルを変更せず、モデルの基本色を設定します。
        /// </summary>
        private void ApplyColor(GameObject model, Color color)
        {
            if (model == null)
            {
                return;
            }

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(_properties);
                _properties.SetColor("_BaseColor", color);
                _properties.SetColor("_Color", color);
                renderer.SetPropertyBlock(_properties);
                _properties.Clear();
            }
        }

        /// <summary>
        /// 設定変更の購読を解除します。
        /// </summary>
        private void OnDisable()
        {
            SettingsManager.SettingsChanged -= Invalidate;
        }
    }
}
