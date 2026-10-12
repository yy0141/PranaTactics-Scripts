using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Pranam.Networking;
using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 共通の対象選択ルールで選ばれた敵に対して、
    /// 対象への接近を伴う近接攻撃を実行します。
    /// </summary>
    public sealed class AttackerBehavior : AttackerBase
    {
        /// <summary>
        /// 指定されたユニットを近接攻撃の対象として
        /// 選択できるかどうかを判定します。
        /// </summary>
        /// <param name="targetUnit">
        /// 判定対象のユニットです。
        /// </param>
        /// <returns>
        /// 対象が視認可能であり、攻撃可能距離内に存在する場合は
        /// <c>true</c>です。
        /// </returns>
        protected override bool CanSelectAttackTarget(
            UnitBehavior targetUnit)
        {
            if (!base.CanSelectAttackTarget(targetUnit) ||
                AttackerData == null ||
                OwnerMicroUnit == null)
            {
                return false;
            }

            MicroUnitBehavior targetMicroUnit =
                targetUnit?.GetMicroUnitBehavior();

            if (targetMicroUnit == null)
            {
                return false;
            }

            float attackRange =
                _diameter;

            float squaredDistance =
                (targetMicroUnit.transform.position -
                OwnerMicroUnit.transform.position).sqrMagnitude;

            return squaredDistance <=
                attackRange *
                attackRange;
        }

        /// <summary>
        /// 選択された攻撃対象へ接近し、
        /// 近接攻撃処理を開始します。
        /// </summary>
        public override void UsePranam()
        {
            if (AttackerData == null ||
                OwnerMicroUnit == null ||
                OwnerPranamBehavior == null)
            {
                return;
            }

            if (!OwnerPranamBehavior.CanUsePranam(
                    AttackerData.ActionPointCost,
                    AttackerData.PranaCost))
            {
                return;
            }

            UnitBehavior targetUnit =
                GetAttackTarget();

            if (targetUnit == null)
            {
                return;
            }

            MicroUnitBehavior targetMicroUnit =
                targetUnit.GetMicroUnitBehavior();

            if (targetMicroUnit == null)
            {
                return;
            }

            bool hasStartedAttack =
                BeginAttackMovement(
                    targetMicroUnit.gameObject);

            if (!hasStartedAttack)
            {
                return;
            }

            PranamAttackContext attackContext =
                new()
                {
                    Timer =
                        AttackerData.RecoveryTime,

                    IsAttackMoving = true,

                    TargetIds =
                        new List<int>
                        {
                            targetUnit.PlayerId
                        }
                };

            OwnerPranamBehavior.ConsumeCosts(
                OwnerPlayerId,
                AttackerData.ActionPointCost,
                AttackerData.PranaCost);

            OwnerPranamBehavior.ApplyAttack(
                this,
                attackContext);

            ResetElapsedTime();
        }
    }
}
