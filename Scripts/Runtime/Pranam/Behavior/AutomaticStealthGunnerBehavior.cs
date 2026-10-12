using UnityEngine;

using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// 使用ボタンを押している間、MapUnit基準の扇形範囲内にいる
    /// マップ上で追跡できる最も近い敵へ一定間隔で射撃を続けます。
    /// </summary>
    /// <remarks>
    /// 攻撃範囲と発射対象はMapUnitから判定します。
    /// ハイライトは通常のPranamと同様に、所有MicroUnitの
    /// VisionSensorによる現在の視認結果だけを使用します。
    /// </remarks>
    public sealed class AutomaticStealthGunnerBehavior :
        AutomaticGunnerBehavior
    {

        /// <summary>
        /// マップ上で追跡できる候補を攻撃対象として選択します。
        /// </summary>
        protected override bool CanSelectAttackTarget(UnitBehavior targetUnit)
        {
            return CanTargetMapUnit(targetUnit);
        }

        /// <summary>
        /// マップ上の水平距離を共通の対象選択ルールへ提供します。
        /// </summary>
        protected override bool TryGetTargetSquaredDistance(UnitBehavior targetUnit, out float squaredDistance)
        {
            return TryGetMapTargetSquaredDistance(targetUnit, out squaredDistance);
        }

    }
}
