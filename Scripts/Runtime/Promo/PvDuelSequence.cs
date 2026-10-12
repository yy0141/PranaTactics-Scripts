#if UNITY_EDITOR || PV_CAPTURE
using System;
using System.Collections.Generic;

using PranaTactics.Prana;
using UnityEngine;

namespace PranaTactics.Promo
{
    /// <summary>
    /// 実弾の飛翔、防御、被弾を契機として三人の撮影シーケンスを進めます。
    /// </summary>
    public sealed class PvDuelSequence : IDisposable
    {
        private enum Stage
        {
            Opening,
            Flight,
            Orbit,
            Defense,
            FlankPreparing,
            Flank,
            Turn,
            Impact,
            Ending,
            Ascending,
            Complete
        }

        private readonly PvDuelDefinition _definition;

        private readonly PvActor _shooter;

        private readonly PvActor _defender;

        private readonly PvActor _flanker;

        private readonly PvCameraRig _rig;

        private readonly PvCaptureVisibility _visibility;

        private readonly Action<float> _setTimeScale;

        private readonly float _triggerDistance;

        private Stage _stage;

        private float _elapsed;

        private float _stageElapsed;

        private bool _hasFlankDamage;

        private float _ascentDuration;

        public bool IsComplete => _stage == Stage.Complete;

        public PvDuelSequence(
            PvScenario scenario,
            Dictionary<string, PvActor> actors,
            PvCameraRig rig,
            Action<float> setTimeScale)
        {
            _definition = scenario.Duel;
            _shooter = actors[_definition.Shooter];
            _defender = actors[_definition.Defender];
            _flanker = actors[_definition.Flanker];
            _rig = rig;
            _setTimeScale = setTimeScale;
            PvActorDefinition first = Array.Find(scenario.Actors, actor => actor.Id == _definition.Shooter);
            PvActorDefinition second = Array.Find(scenario.Actors, actor => actor.Id == _definition.Defender);
            int cells = PvDuelDefinition.GetCellDistance(first, second);

            if (cells <= _definition.TravelCells + 1)
            {
                throw new ArgumentException("Duel: 飛翔距離の切替地点より先に、防御までの距離を1マス以上確保してください。");
            }

            _triggerDistance = Vector3.Distance(_shooter.Position, _defender.Position) / cells * _definition.TravelCells;
            _visibility = new PvCaptureVisibility(rig.Camera);
            _shooter.Aim(_defender, 0, 0);
            _defender.Aim(_shooter, 0, 0);
            _flanker.Aim(_defender, 0, 0);
            _rig.SetFirstPerson(_shooter);
        }

        public void Tick(float gameTime, float unscaledDelta)
        {
            if (IsComplete)
            {
                return;
            }

            _elapsed += unscaledDelta;
            _stageElapsed += unscaledDelta;

            if (_elapsed > _definition.TimeoutSeconds)
            {
                throw new TimeoutException($"PV Duel: {_stage}でタイムアウトしました。射程、装備、射線を確認してください。");
            }

            float openingDistance = 0;
            float flankDistance = 0;
            int flankProjectileCount = 0;

            foreach (Gerade projectile in UnityEngine.Object.FindObjectsByType<Gerade>(
                FindObjectsSortMode.None))
            {
                if (!projectile.IsInitialized)
                {
                    continue;
                }

                if (projectile.OwnerPlayerId == _shooter.Unit.PlayerId && projectile.IsMain)
                {
                    projectile.AimForCapture(_defender.Position);
                    openingDistance = Mathf.Max(openingDistance, projectile.CaptureFlightDistance);
                }

                if (projectile.OwnerPlayerId == _flanker.Unit.PlayerId && !projectile.IsMain)
                {
                    flankProjectileCount++;
                    projectile.AimForCapture(_defender.Position);
                    flankDistance = Mathf.Max(flankDistance, projectile.CaptureFlightDistance);
                }
            }

            switch (_stage)
            {
                case Stage.Opening:
                    if (_stageElapsed >= _definition.FireDelay)
                    {
                        Fire(_shooter, _definition.OpeningSlot, true, gameTime);
                        Advance(Stage.Flight);
                    }
                    break;
                case Stage.Flight:
                    if (openingDistance >= _triggerDistance)
                    {
                        _setTimeScale(_definition.SlowScale);
                        _rig.StartOrbit(
                            _shooter,
                            _defender,
                            _definition.OrbitSeconds,
                            _definition.DefenderViewHeight);
                        Advance(Stage.Orbit);
                    }
                    break;
                case Stage.Orbit:
                    if (_stageElapsed >= _definition.OrbitSeconds)
                    {
                        _rig.SetFirstPerson(
                            _defender,
                            impactShake: 0.35f,
                            heightOffset: _definition.DefenderViewHeight);
                        _defender.SelectWeapon(
                            new PvCue { Actor = _definition.Defender, IsMain = false, Slot = _definition.ShieldSlot });
                        _setTimeScale(1);
                        Advance(Stage.Defense);
                    }
                    break;
                case Stage.Defense:
                    if (_stageElapsed >= _definition.DefenseSeconds)
                    {
                        _flanker.SelectWeapon(
                            new PvCue { IsMain = false, Slot = _definition.FlankSlot, Actor = _definition.Flanker });
                        Advance(Stage.FlankPreparing);
                    }
                    break;
                case Stage.FlankPreparing:
                    if (_flanker.TryFireAt(_defender, _definition.FlankSlot, false))
                    {
                        Advance(Stage.Flank);
                    }
                    else if (_stageElapsed >= 2)
                    {
                        throw new InvalidOperationException(
                            "PV Duel: Cのサブゲラーデを生成できません。装備の有効状態・初期化・硬直・HPを確認してください。");
                    }
                    break;
                case Stage.Flank:
                    if (flankDistance > 0)
                    {
                        _defender.Aim(_flanker, 0, 0);
                        _rig.SetFirstPerson(
                            _defender,
                            _definition.TurnSeconds,
                            0.35f,
                            _definition.DefenderViewHeight);
                        _setTimeScale(0.12f);
                        Advance(Stage.Turn);
                    }
                    break;
                case Stage.Turn:
                    if (_stageElapsed >= _definition.TurnSeconds)
                    {
                        _setTimeScale(0.65f);
                        Advance(Stage.Impact);
                    }
                    break;
                case Stage.Impact:
                    if (_hasFlankDamage)
                    {
                        _defender.CheckDamage(true, _flanker);
                        _setTimeScale(1);
                        Advance(Stage.Ending);
                    }
                    break;
                case Stage.Ending:
                    if (_stageElapsed >= 0.8f && flankProjectileCount == 0)
                    {
                        _ascentDuration = PranaTactics.Match.Phases.MatchPhaseManager.Instance.MacroTransitionDuration;
                        Transform fullMapCamera = PranaTactics.Board.BoardManager.Instance.GetMinimapCameraObject().transform;

                        _rig.StartOverhead(
                            _defender,
                            fullMapCamera,
                            _ascentDuration,
                            _definition.OverheadHeightRatio);
                        Advance(Stage.Ascending);
                    }
                    break;
                case Stage.Ascending:
                    if (_stageElapsed >= _ascentDuration + 0.5f)
                    {
                        Advance(Stage.Complete);
                        Debug.Log("PV Duel: 撮影演出が完了し、Cのサブゲラーデによる被弾を確認しました。");
                    }
                    break;
            }
        }

        public void ApplyVisibility()
        {
            _visibility.Apply(_shooter, _defender, _flanker,
                _stage < Stage.Orbit || _stage == Stage.Orbit && _stageElapsed < 0.25f,
                _stage >= Stage.Defense && _stage < Stage.Ascending ||
                    _stage == Stage.Ascending && _stageElapsed < 0.2f ||
                    _stage == Stage.Orbit && _stageElapsed >= _definition.OrbitSeconds * 0.85f,
                _stage < Stage.Turn, _stage == Stage.Orbit && _definition.IsolateOrbit,
                _stage >= Stage.Defense ||
                    _stage == Stage.Orbit &&
                    _stageElapsed >= _definition.OrbitSeconds * 0.85f,
                _stage == Stage.Orbit && _stageElapsed < _definition.OrbitSeconds * 0.95f);
        }

        public void RecordDamage(PranaTactics.Unit.UnitBehavior unit, int attackerId)
        {
            if (unit == _defender.Unit && attackerId == _flanker.Unit.PlayerId)
            {
                _hasFlankDamage = true;
            }
        }

        private static void Fire(PvActor actor, int slot, bool isMain, float gameTime)
        {
            actor.Fire(new PvCue { Time = gameTime, Duration = 0.1f, IsMain = isMain, Slot = slot });
        }

        private void Advance(Stage stage)
        {
            _stage = stage;
            _stageElapsed = 0;
            Debug.Log($"PV Duel: {stage}へ進行（実時間 {_elapsed:0.00}秒）。");
        }

        public void Dispose()
        {
            _visibility.Dispose();
        }
    }
}
#endif
