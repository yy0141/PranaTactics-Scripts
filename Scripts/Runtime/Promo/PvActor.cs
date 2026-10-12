#if UNITY_EDITOR || PV_CAPTURE
using System;
using System.Collections.Generic;

using PranaTactics.Controls;
using PranaTactics.Unit;
using UnityEngine;

namespace PranaTactics.Promo
{
    /// <summary>
    /// 撮影コマンドを既存ユニットの移動・照準・武器入力へ変換します。
    /// </summary>
    public sealed class PvActor : IUnitInput
    {
        private float _mainUntil = -1;

        private float _subUntil = -1;

        private bool _wasMainHeld;

        private bool _wasSubHeld;

        private PvActor _target;

        private Vector3 _lastPosition;

        private readonly float _initialHealth;

        private readonly int _playerId;

        private readonly HashSet<int> _damageSources = new HashSet<int>();

        public UnitBehavior Unit
        {
            get;
        }

        public UnitInputState CurrentState { get; private set; } = UnitInputState.CreateDefault();

        public Vector3 Position => Unit != null ? Unit.MicroUnitBehavior.transform.position : _lastPosition;

        public PvActor(UnitBehavior unit, float speed)
        {
            Unit = unit;
            _lastPosition = Position;
            _initialHealth = unit.Status.CurrentHealth;
            _playerId = unit.PlayerId;
            unit.MicroUnitBehavior.SetUnitInput(this);
            unit.Status.SetActionPoint(100000);
            unit.MicroUnitBehavior.SetProactiveness(100000);
            SetSpeed(speed);
        }

        public void SetSpeed(float speed)
        {
            Unit.Status.SetMoveSpeed(speed);
            Unit.Status.SetCurrentMoveSpeed(speed);
            Unit.Status.SetWalkMoveSpeed(speed);
            Unit.Status.SetDashMoveSpeed(speed);
        }

        public void Aim(PvActor target, float yaw, float pitch)
        {
            _target = target;
            Unit.MicroUnitBehavior.AlignPerspectiveAndPranamYaw(yaw);
            Unit.MicroUnitBehavior.SetPitch(pitch);
        }

        public void SelectWeapon(PvCue cue)
        {
            if (!Unit.MicroUnitBehavior.TrySelectPranam(cue.IsMain, cue.Slot))
            {
                throw new InvalidOperationException(
                    $"{cue.Actor}: 武器スロット {cue.Slot} を選択できません。硬直や装備を確認してください。");
            }
        }

        public void RecordDamage(int attackerId)
        {
            _damageSources.Add(attackerId);
        }

        public void CheckDamage(bool shouldHaveDamage, PvActor attacker)
        {
            bool hasDamage = attacker != null
                ? _damageSources.Contains(attacker._playerId)
                : Unit == null || Unit.Status.CurrentHealth < _initialHealth;

            if (hasDamage != shouldHaveDamage)
            {
                throw new InvalidOperationException(shouldHaveDamage
                    ? "PVの被弾確認に失敗しました。装備・射線・射程・シールドの回復時間を確認してください。"
                    : "PVの防御確認に失敗しました。先行する攻撃でHPが減少しています。");
            }
        }

        public void Fire(PvCue cue)
        {
            SelectWeapon(cue);

            if (cue.IsMain)
            {
                _mainUntil = cue.Time + cue.Duration;
            }
            else
            {
                _subUntil = cue.Time + cue.Duration;
            }
        }

        /// <summary>
        /// 撮影用の対象指定で装備中のShooterを発射し、入力の取りこぼしと視認キャッシュへの依存を避けます。
        /// </summary>
        public bool TryFireAt(PvActor target, int slot, bool isMain)
        {
            foreach (PranaTactics.Pranam.ShooterBehavior weapon in Unit.TacticalMapUnitBehavior.GetComponentsInChildren<PranaTactics.Pranam.ShooterBehavior>(
                true))
            {
                if (weapon.IsMain == isMain && weapon.Number == slot && weapon.isActiveAndEnabled)
                {
                    return weapon.TryFireForCapture(target.Unit);
                }
            }

            return false;
        }

        public void Tick(float time)
        {
            if (Unit == null)
            {
                return;
            }

            _lastPosition = Position;

            if (!Unit.Status.IsAlive)
            {
                CurrentState = UnitInputState.CreateDefault();

                return;
            }

            if (_target != null && _target.Unit != null)
            {
                Vector3 direction = _target.Position - Position;

                Unit.MicroUnitBehavior.AlignPerspectiveAndPranamYaw(
                    Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
                Unit.MicroUnitBehavior.SetPitch(
                    -Mathf.Atan2(
                    direction.y,
                    new Vector2(
                    direction.x,
                    direction.z).magnitude) * Mathf.Rad2Deg);
            }

            bool main = time < _mainUntil;
            bool sub = time < _subUntil;
            UnitInputState state = UnitInputState.CreateDefault();

            state.IsPrimaryHeld = main;
            state.IsPrimaryPressed = main && !_wasMainHeld;
            state.IsPrimaryReleased = !main && _wasMainHeld;
            state.IsSecondaryHeld = sub;
            state.IsSecondaryPressed = sub && !_wasSubHeld;
            state.IsSecondaryReleased = !sub && _wasSubHeld;
            CurrentState = state;
            _wasMainHeld = main;
            _wasSubHeld = sub;
        }
    }
}
#endif
