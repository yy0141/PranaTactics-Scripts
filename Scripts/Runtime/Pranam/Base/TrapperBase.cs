using UnityEngine;

using PranaTactics.Board;
using PranaTactics.Match.Phases;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 罠兵装の初期化、設置要求と設置成功時のリソース消費を担当します。
    /// </summary>
    public abstract class TrapperBase : PranamBase
    {
        /// <summary>
        /// この兵装の罠設定です。
        /// </summary>
        private TrapperPranamData _trapData;

        /// <summary>
        /// 設置中のユニットのミクロ表現です。
        /// </summary>
        private MicroUnitBehavior _microUnit;

        /// <summary>
        /// 設置要求の確定待ちであるかどうかです。
        /// </summary>
        private bool _isPlacementPending;

        public TrapperPranamData TrapData => _trapData;
        public bool IsPlacementPending => _isPlacementPending;

        /// <summary>
        /// 所有ユニットと罠データを検証し、対象検出用の扇形を無効化します。
        /// </summary>
        public override void Initialize(int playerId, bool isMain, int number,
            UnitBehavior unitBehavior, PranamBehavior pranamBehavior, PranamData pranamData, int startAngle)
        {
            _isInitialized = false;
            _isPlacementPending = false;
            if (unitBehavior == null || pranamBehavior == null ||
                pranamData is not TrapperPranamData trapData || unitBehavior.GetMicroUnitBehavior() == null)
            {
                Debug.LogError("Trapperの初期化に必要なUnit、PranamBehaviorまたはTrapperPranamDataがありません。", this);
                return;
            }

            _playerId = playerId;
            _isMain = isMain;
            _number = number;
            _unitBehavior = unitBehavior;
            _mapUnitBehavior = unitBehavior.GetTacticalMapUnitBehavior();
            _microUnit = unitBehavior.GetMicroUnitBehavior();
            _pranamBehavior = pranamBehavior;
            _trapData = trapData;
            GetComponent<SphereCollider>().enabled = false;
            _isInitialized = true;
        }

        /// <summary>
        /// 攻撃対象の物理走査を行わず、ボードのマス進入判定に委譲します。
        /// </summary>
        protected override void Update()
        {
        }

        /// <summary>
        /// 現在の状態、コストとボード上の設置可否を確認します。
        /// </summary>
        public bool CanPlaceTrap()
        {
            return _isInitialized && _trapData != null && _microUnit != null && !_microUnit.IsDead &&
                _trapData.ActionPointCost >= 0 && _trapData.PranaCost >= 0 &&
                _pranamBehavior != null && MatchPhaseManager.Instance != null &&
                MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Micro &&
                _pranamBehavior.CanUsePranam(_trapData.ActionPointCost, _trapData.PranaCost) &&
                BoardManager.Instance != null && BoardManager.Instance.Traps != null &&
                BoardManager.Instance.Traps.CanPlaceAt(_microUnit.GetCurrentCoordinate());
        }

        /// <summary>
        /// 現在地への設置をボードの裁定処理へ要求します。
        /// </summary>
        protected void RequestTrapPlacement()
        {
            if (_isPlacementPending || !CanPlaceTrap())
            {
                return;
            }

            _isPlacementPending = true;
            if (!BoardManager.Instance.Traps.RequestPlacement(this, _playerId, _microUnit.GetCurrentCoordinate()))
            {
                _isPlacementPending = false;
            }
        }

        /// <summary>
        /// 設置確定時だけコストを消費し、発動を待たずに硬直を開始します。
        /// </summary>
        internal void ApplyPlacementResult(bool accepted)
        {
            _isPlacementPending = false;
            if (!accepted || !_isInitialized || _pranamBehavior == null)
            {
                return;
            }

            _pranamBehavior.ConsumeCosts(_playerId, _trapData.ActionPointCost, _trapData.PranaCost);
            _pranamBehavior.SetRecoveryTime(IsMain, Number, _trapData.RecoveryTime);
            _unitBehavior.ApplyLockMovement(_trapData.RecoveryTime);
        }
    }
}
