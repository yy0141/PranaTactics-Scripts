using UnityEngine;

using PranaTactics.Match.Phases;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 所有ユニットに接近している脅威を検出し、
    /// 最も危険な方向へ局所シールドを自動展開します。
    /// </summary>
    public sealed class LocalizedShieldBehavior : ShieldBase
    {
        /// <summary>
        /// サブ側のシールド生成距離へ適用する倍率です。
        /// </summary>
        private const float k_SubShieldDistanceMultiplier = 1.5f;

        /// <summary>
        /// 方向ベクトルを有効と判断する最小二乗距離です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude = 0.0001f;

        /// <summary>
        /// 所有ユニットに対する攻撃予測コンポーネントです。
        /// </summary>
        private AttackPrediction _attackPrediction;

        /// <summary>
        /// 所有ユニットからシールドを生成する基本距離です。
        /// </summary>
        [Header("Shield Placement")]
        [Min(0.0f)]
        [SerializeField]
        private float _shieldDistance = 5.0f;

        /// <summary>
        /// 継続コストを最後に消費してからの経過時間です。
        /// </summary>
        private float _consumptionTimer;

        public override bool RequiresInput
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// 最も危険な脅威の方向へ局所シールドを生成します。
        /// </summary>
        public override void UsePranam()
        {
            if (!CanDeployShield())
            {
                return;
            }

            AttackPrediction.ThreatCluster threatCluster =
                _attackPrediction.MostDangerousCluster;

            if (threatCluster == null)
            {
                return;
            }

            Vector3 ownerPosition =
                MicroUnitBehavior.transform.position;

            Vector3 directionToThreat =
                threatCluster.CenterPosition -
                ownerPosition;

            if (directionToThreat.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return;
            }

            directionToThreat.Normalize();

            float generateDistance =
                IsMain
                    ? _shieldDistance
                    : _shieldDistance *
                    k_SubShieldDistanceMultiplier;

            Vector3 generatePosition =
                ownerPosition +
                directionToThreat *
                generateDistance;

            Quaternion generateRotation =
                Quaternion.LookRotation(
                    directionToThreat,
                    Vector3.up);

            CreateShieldObject(
                _playerId,
                IsMain,
                false,
                generatePosition,
                generateRotation);

            _pranamBehavior.ConsumeCosts(
                _playerId,
                0,
                ShieldData.PranaCost);

            _pranamBehavior.SetRecoveryTime(
                IsMain,
                Number,
                ShieldData.RecoveryTime);

            _time = 0.0f;
        }

        /// <summary>
        /// コンポーネントが有効になったとき、
        /// 継続コスト用の時間を初期化します。
        /// </summary>
        protected override void OnEnableInternal()
        {
            base.OnEnableInternal();

            _consumptionTimer = 0.0f;
            _attackPrediction = null;
        }

        /// <summary>
        /// シールドの自動展開と継続コストの消費を更新します。
        /// </summary>
        protected override void Update()
        {
            if (!_isInitialized)
            {
                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null ||
                !unitManager.CanExecuteUnitLogic(
                    _playerId))
            {
                return;
            }

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null ||
                phaseManager.CurrentPhase !=
                MatchPhase.Micro)
            {
                return;
            }

            if (!TryResolveAttackPrediction())
            {
                return;
            }

            _time += Time.deltaTime;
            _consumptionTimer += Time.deltaTime;

            UsePranam();
            UpdateConsumptionCost();
        }

        /// <summary>
        /// 現在の状態で局所シールドを生成できるか判定します。
        /// </summary>
        /// <returns>
        /// 必要な参照、クールダウンおよびコスト条件を
        /// 満たす場合は<c>true</c>です。
        /// </returns>
        private bool CanDeployShield()
        {
            if (_attackPrediction == null ||
                ShieldData == null ||
                MicroUnitBehavior == null ||
                _pranamBehavior == null)
            {
                return false;
            }

            if (_time <
                ShieldData.RecoveryTime)
            {
                return false;
            }

            return _pranamBehavior.CanUsePranam(
                ShieldData.ActionPointCost,
                ShieldData.PranaCost);
        }

        /// <summary>
        /// 所有ユニットから攻撃予測コンポーネントを取得します。
        /// </summary>
        /// <returns>
        /// 有効な攻撃予測コンポーネントを取得できた場合は
        /// <c>true</c>です。
        /// </returns>
        private bool TryResolveAttackPrediction()
        {
            if (_attackPrediction != null)
            {
                return true;
            }

            if (MicroUnitBehavior == null)
            {
                return false;
            }

            _attackPrediction =
                MicroUnitBehavior.GetAttackPrediction();

            if (_attackPrediction != null)
            {
                return true;
            }

            Debug.LogWarning(
                $"{nameof(LocalizedShieldBehavior)}: " +
                $"{nameof(AttackPrediction)}を取得できません。",
                this);

            return false;
        }

        /// <summary>
        /// 設定された時間間隔ごとに行動力コストを消費します。
        /// </summary>
        private void UpdateConsumptionCost()
        {
            if (ShieldData == null ||
                _pranamBehavior == null)
            {
                return;
            }

            float consumptionInterval =
                ShieldData.ConsumptionInterval;

            if (consumptionInterval <= 0.0f ||
                _consumptionTimer <
                consumptionInterval)
            {
                return;
            }

            _consumptionTimer = 0.0f;

            int actionPointCost =
                ShieldData.ActionPointCost;

            if (actionPointCost <= 0)
            {
                return;
            }

            if (!_pranamBehavior.CanUsePranam(
                    actionPointCost,
                    0.0f))
            {
                return;
            }

            _pranamBehavior.ConsumeCosts(
                _playerId,
                actionPointCost,
                0.0f);
        }
    }
}
