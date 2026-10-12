using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Controls;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.Unit;
using PranaTactics.Pranam;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 1体のCPUについて、公開情報からMicro入力を生成します。
    /// 射程と方向はMapUnit、直接視認はMicroUnitで判定します。
    /// </summary>
    public sealed class SoloAiAgent
    {
        private enum MovementPace
        {
            Cruise,
            Caution,
            ShortHold
        }

        private const float k_SwitchUtilityMargin = 2.5f;

        private const float k_MainFireInterval = 0.12f;

        private const float k_SubFireInterval = 0.16f;

        /// <summary>
        /// 交戦機会や被弾の後、攻撃用スロットを維持する時間です。
        /// </summary>
        private const float k_EngagementContinuationSeconds = 6.0f;

        /// <summary>
        /// 観測済みの接近攻撃です。
        /// </summary>
        private AttackPrediction _attackPrediction;

        /// <summary>
        /// 敵を一瞬見失っても隠密へ戻らないための交戦期限です。
        /// </summary>
        private float _engagementUntil = float.NegativeInfinity;

        /// <summary>
        /// 前回の入力更新で交戦状態だったかどうかです。
        /// </summary>
        private bool _wasEngaged;

        /// <summary>
        /// 初動または交戦終了時の抽選によって隠密を許可するかどうかです。
        /// </summary>
        private bool _useStealthOutsideCombat = true;

        /// <summary>
        /// 攻防の周期をずらすための交戦経過時間です。
        /// </summary>
        private float _combatSeconds;

        /// <summary>
        /// 現在の攻防方針です。
        /// </summary>
        private AiCombatStance _combatStance = AiCombatStance.Conceal;

        /// <summary>
        /// 初動と交戦終了時の抽選に応じて隠密用スロットを維持します。
        /// </summary>
        private bool _shouldMaintainStealth = true;

        /// <summary>
        /// 攻撃開始後の短い発射待ちを隠密への切り替えで中断しないための時刻です。
        /// </summary>
        private float _lastReadyAttackTime = float.NegativeInfinity;

        /// <summary>
        /// 隠密を維持したまま準備する攻撃用の視点です。
        /// </summary>
        private bool _concealedPerspectiveMain = true;

        /// <summary>
        /// 防御候補の方向数です。
        /// </summary>
        private int _threatDirectionCount;

        /// <summary>
        /// 弾が消えた直後の防御切り替えを抑える脅威の保持期限です。
        /// </summary>
        private float _threatMemoryUntil = float.NegativeInfinity;

        /// <summary>
        /// 直近で観測した接近弾数です。
        /// </summary>
        private int _recentThreatCount;

        /// <summary>
        /// 直近で観測した攻撃方向数です。
        /// </summary>
        private int _recentThreatDirectionCount;

        private readonly UnitBehavior _self;

        private readonly AiWeaponSelector _weaponSelector;

        private readonly AiWorldStateProvider _worldState;

        private readonly AiDifficultyProfile _difficulty;

        private UnitBehavior _target;

        private string _teamTaskId = AiTeamTaskIds.EngagePrimary;

        private List<AiWeaponProfile> _mainWeapons;

        private List<AiWeaponProfile> _subWeapons;

        private AiWeaponProfile _selectedMain;

        private AiWeaponProfile _selectedSub;

        private float _selectedMainUtility = float.NegativeInfinity;

        private float _selectedSubUtility = float.NegativeInfinity;

        private float _weaponDecisionTimer;

        private float _targetDecisionTimer;

        private bool _hasAttackOpportunity;

        private float _mainFireTimer;

        private float _subFireTimer;

        /// <summary>
        /// 現在のMain武器で攻撃入力を継続した時間です。
        /// </summary>
        private float _mainAttackSeconds;

        /// <summary>
        /// 現在のSub武器で攻撃入力を継続した時間です。
        /// </summary>
        private float _subAttackSeconds;

        private float _reactionTimer;

        private float _mainAimError;

        private float _subAimError;

        private float _movementPaceTimer;

        private int _decisionSequence;

        private int _movementDecisionSequence;

        private int _microSequence;

        private bool _wasMainHolding;

        private bool _wasSubHolding;

        private bool _isReadyForCoordinatedStrike;

        private bool _isCoordinatedFireAuthorized;

        private AiUnitTacticDirective _tacticalDirective;

        private MovementPace _movementPace = MovementPace.Cruise;

        public SoloAiAgent(UnitBehavior self, AiWorldStateProvider worldState, AiDifficultyProfile difficulty)
        {
            _self = self;
            _worldState = worldState ?? new AiWorldStateProvider();
            _difficulty = difficulty ?? AiDifficultyProfile.Create(AiDifficulty.Normal);
            _weaponSelector = new AiWeaponSelector();
        }

        public SoloAiAgent(UnitBehavior self) : this(
            self,
            new AiWorldStateProvider(),
            AiDifficultyProfile.Create(
            AiDifficulty.Normal))
        {
        }

        public UnitBehavior Unit => _self;

        public string RoleId => AiRoleResolver.ResolveRoleId(_self);

        public AiRoleDefinition RoleDefinition => AiDefinitionRegistry.GetRole(RoleId);

        public AiRoleProfile RoleProfile => AiRoleProfile.Create(RoleId);

        public AiUnitRole Role => RoleDefinition.LegacyRole;

        public UnitBehavior Target => _target;

        public string TeamTaskId => _teamTaskId;

        public AiUnitTacticDirective TacticalDirective => _tacticalDirective != null ? _tacticalDirective.Clone() : null;

        public bool IsReadyForCoordinatedStrike => _isReadyForCoordinatedStrike;

        public int MinimumCoordinatedParticipants => _tacticalDirective != null ? Mathf.Max(
            2,
            _tacticalDirective.MinimumCoordinatedParticipants) : 2;

        public void SetTarget(UnitBehavior target)
        {
            int oldId = _target != null ? _target.PlayerId : -1;
            int newId = target != null ? target.PlayerId : -1;

            _target = target;

            if (oldId != newId)
            {
                _hasAttackOpportunity = false;
                _reactionTimer = _difficulty.ReactionTime;
                _weaponDecisionTimer = 0.0f;
            }
        }

        public void SetTeamTask(string taskId)
        {
            _teamTaskId = string.IsNullOrWhiteSpace(taskId) ? AiTeamTaskIds.SelfPreserve : taskId;
        }

        public void ApplyTacticalDirective(AiUnitTacticDirective directive)
        {
            _targetDecisionTimer = 0.0f;
            _tacticalDirective = directive != null ? directive.Clone() : null;
            SetTeamTask(_tacticalDirective != null ? _tacticalDirective.TaskId : AiTeamTaskIds.SelfPreserve);
            SetTarget(_tacticalDirective != null ? _tacticalDirective.Target : null);
            _isReadyForCoordinatedStrike = false;
            _isCoordinatedFireAuthorized = false;
        }

        public void SetCoordinatedFireAuthorization(bool authorized)
        {
            _isCoordinatedFireAuthorized = authorized;
        }

        public void OnMicroPhaseEntered()
        {
            UpdatePostCombatStealth(false);
            _engagementUntil = float.NegativeInfinity;
            _combatSeconds = 0.0f;
            _combatStance = AiCombatStance.Conceal;
            _shouldMaintainStealth = _useStealthOutsideCombat;
            _lastReadyAttackTime = float.NegativeInfinity;
            _attackPrediction = _self != null ? _self.GetMicroUnitBehavior()?.GetAttackPrediction() : null;
            _threatMemoryUntil = float.NegativeInfinity;
            _recentThreatCount = 0;
            _recentThreatDirectionCount = 0;
            _targetDecisionTimer = 0.0f;
            _hasAttackOpportunity = false;
            _weaponDecisionTimer = 0.0f;
            _mainFireTimer = 0.0f;
            _subFireTimer = 0.0f;
            _mainAttackSeconds = 0.0f;
            _subAttackSeconds = 0.0f;
            _reactionTimer = _difficulty.ReactionTime;
            _selectedMainUtility = float.NegativeInfinity;
            _selectedSubUtility = float.NegativeInfinity;
            _selectedMain = null;
            _selectedSub = null;
            _wasMainHolding = false;
            _wasSubHolding = false;
            _isReadyForCoordinatedStrike = false;
            _isCoordinatedFireAuthorized = false;
            _mainWeapons = null;
            _subWeapons = null;
            _decisionSequence = 0;
            _movementDecisionSequence = 0;
            _movementPace = MovementPace.Cruise;
            _movementPaceTimer = 0.0f;
            _microSequence++;
            RebuildWeaponProfiles();
            RefreshAimError();
            if (_self != null && UnitManager.Instance != null &&
                UnitManager.Instance.IsAIWeaponUsageEnabled(_self.PlayerId) &&
                MatchPhaseManager.Instance != null && MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Micro)
            {
                SelectWeapons(0.0f, null);
            }
        }

        public UnitInputState GetInputState()
        {
            UnitInputState state = UnitInputState.CreateDefault();

            if (_self == null || _self.GetUnitStatus() == null || MatchPhaseManager.Instance == null ||
                MatchPhaseManager.Instance.CurrentPhase != MatchPhase.Micro)
            {
                _isReadyForCoordinatedStrike = false;
                ApplyReleases(ref state, false, false);

                return state;
            }

            UnitManager unitManager = UnitManager.Instance;
            float deltaTime = Time.deltaTime;

            _targetDecisionTimer -= deltaTime;
            UpdateMovementPace(deltaTime);
            ApplyMovementPace(ref state);

            if (unitManager == null || !unitManager.IsAIWeaponUsageEnabled(_self.PlayerId))
            {
                _hasAttackOpportunity = false;
                ApplyMovementPace(ref state);
                _isReadyForCoordinatedStrike = false;
                ApplyReleases(ref state, false, false);

                return state;
            }

            _weaponDecisionTimer -= deltaTime;
            _mainFireTimer -= deltaTime;
            _subFireTimer -= deltaTime;
            _reactionTimer -= deltaTime;

            MicroUnitBehavior micro = _self.GetMicroUnitBehavior();

            if (micro != null && micro.IsSniperAimLocked)
            {
                state.IsStopping = true;
                state.IsWalking = false;
                _isReadyForCoordinatedStrike = false;
                ApplyReleases(ref state, false, false);

                return state;
            }

            AiWorldSnapshot snapshot = _worldState.BuildSnapshot(_self.GetTeamId());
            UnitBehavior validTarget = ResolveValidTarget(_target, snapshot);

            if (validTarget != _target)
            {
                SetTarget(validTarget);
            }

            if (_target == null || _self.GetMicroUnitBehavior() == null)
            {
                UpdateSurvivalStance(false, deltaTime);
                SelectWeapons(0.0f, null);
                _hasAttackOpportunity = false;
                ApplyMovementPace(ref state);
                _isReadyForCoordinatedStrike = false;
                ApplyMainFire(ref state, CanUseDefense(_selectedMain));
                ApplySubFire(ref state, CanUseDefense(_selectedSub));

                return state;
            }

            float distance = GetTargetMapDistance();
            AiPublicUnitState targetState = snapshot.FindEnemy(_target.PlayerId);
            bool directlyVisible = targetState != null &&
                targetState.ContactState >= AiContactState.MapTracked &&
                AiVisibilityEvaluator.CanDirectlySee(
                _self,
                _target);
            AiWeaponSelectionContext selectionContext = new AiWeaponSelectionContext(
                targetState != null ? targetState.ContactState : AiContactState.Unknown,
                directlyVisible,
                targetState != null &&
                targetState.IsMoving,
                targetState != null ? targetState.ObservedSpeed : 0.0f);

            bool canAttackTarget = CanAttackTargetWithWeapons(
                _mainWeapons, targetState, distance, directlyVisible, _self.GetUnitStatus().CurrentHealth) ||
                CanAttackTargetWithWeapons(
                    _subWeapons, targetState, distance, directlyVisible, _self.GetUnitStatus().CurrentHealth);
            UpdateSurvivalStance(canAttackTarget, deltaTime);
            if (_weaponDecisionTimer <= 0.0f || _shouldMaintainStealth)
            {
                bool shouldRefreshAimError = _weaponDecisionTimer <= 0.0f;
                SelectWeapons(distance, selectionContext, targetState);
                if (shouldRefreshAimError)
                {
                    RefreshAimError();
                    _weaponDecisionTimer = _difficulty.DecisionInterval;
                }
            }

            float mainTargetAngle = GetSignedMapAngleToTarget(
                _selectedMain?.RuntimePranam != null
                    ? _selectedMain.RuntimePranam.transform
                    : micro.GetMainPranamTransform());
            float subTargetAngle = GetSignedMapAngleToTarget(
                _selectedSub?.RuntimePranam != null
                    ? _selectedSub.RuntimePranam.transform
                    : micro.GetSubPranamTransform());
            float mainAngle = mainTargetAngle + ClampWeaponAimError(_selectedMain, _mainAimError);
            float subAngle = subTargetAngle + ClampWeaponAimError(_selectedSub, _subAimError);
            float pitchAngle = GetCameraPitchAngleToTarget(micro);

            float pitchInput = Mathf.Clamp(pitchAngle / 10.0f, -1.0f, 1.0f);

            state.PrimaryAxis = new Vector2(
                Mathf.Clamp(mainAngle / 10.0f, -1.0f, 1.0f),
                pitchInput);
            state.SecondaryAxis = new Vector2(
                Mathf.Clamp(subAngle / 10.0f, -1.0f, 1.0f),
                pitchInput);
            ApplyAttackPerspective(ref state, micro, distance);
            bool reactionComplete = _reactionTimer <= 0.0f;

            _hasAttackOpportunity = canAttackTarget;
            ApplyMovementPace(ref state);
            bool canMainFire = CanUseDefense(_selectedMain) ||
                (reactionComplete && IsWeaponAimAligned(_selectedMain, mainTargetAngle) &&
                CanUseWeapon(
                _selectedMain,
                distance,
                targetState,
                directlyVisible));
            bool canSubFire = CanUseDefense(_selectedSub) ||
                (reactionComplete && IsWeaponAimAligned(_selectedSub, subTargetAngle) &&
                CanUseWeapon(
                _selectedSub,
                distance,
                targetState,
                directlyVisible));

            if (_shouldMaintainStealth || _self.IsStealthActive)
            {
                canMainFire = false;
                canSubFire = false;
            }

            if (state.ShouldSwitchPerspective)
            {
                canMainFire &= _selectedMain != null && _selectedMain.UseMode == AiWeaponUseMode.Defense;
                canSubFire &= _selectedSub != null && _selectedSub.UseMode == AiWeaponUseMode.Defense;
            }

            bool isCoordinatedStrike = _teamTaskId == AiTeamTaskIds.CoordinatedStrike;

            _isReadyForCoordinatedStrike = isCoordinatedStrike &&
                ((_shouldMaintainStealth && _isReadyForCoordinatedStrike) ||
                    (canMainFire && _selectedMain?.UseMode == AiWeaponUseMode.Attack) ||
                    (canSubFire && _selectedSub?.UseMode == AiWeaponUseMode.Attack));

            if ((isCoordinatedStrike && !_isCoordinatedFireAuthorized) ||
                (_tacticalDirective != null && _tacticalDirective.SuppressWeaponUse))
            {
                canMainFire &= _selectedMain != null && _selectedMain.UseMode == AiWeaponUseMode.Defense;
                canSubFire &= _selectedSub != null && _selectedSub.UseMode == AiWeaponUseMode.Defense;
            }

            ApplyMainFire(ref state, canMainFire);
            ApplySubFire(ref state, canSubFire);
            ApplyStationaryAttackHold(
                ref state,
                CanUseWeapon(_selectedMain, distance, targetState, directlyVisible),
                CanUseWeapon(_selectedSub, distance, targetState, directlyVisible));
            _mainAttackSeconds += canMainFire && _selectedMain?.UseMode == AiWeaponUseMode.Attack ? deltaTime : 0.0f;
            _subAttackSeconds += canSubFire && _selectedSub?.UseMode == AiWeaponUseMode.Attack ? deltaTime : 0.0f;

            return state;
        }

        /// <summary>
        /// 観測済みの接近弾と外部からの被弾に基づいて攻防方針を更新します。
        /// </summary>
        private void UpdateSurvivalStance(bool canAttack, float deltaTime)
        {
            int imminentThreatCount = 0;
            _threatDirectionCount = 0;
            if (_attackPrediction == null)
            {
                _attackPrediction = _self.GetMicroUnitBehavior()?.GetAttackPrediction();
            }

            if (_attackPrediction != null)
            {
                foreach (AttackPrediction.ThreatCluster cluster in _attackPrediction.CurrentClusters)
                {
                    if (cluster.NearestTimeToImpact <= 1.5f)
                    {
                        imminentThreatCount += cluster.Count;
                        _threatDirectionCount++;
                    }
                }
            }

            if (Time.time >= _threatMemoryUntil)
            {
                _recentThreatCount = 0;
                _recentThreatDirectionCount = 0;
            }

            if (imminentThreatCount > 0)
            {
                _threatMemoryUntil = Time.time + 0.4f;
                _recentThreatCount = Mathf.Max(_recentThreatCount, imminentThreatCount);
                _recentThreatDirectionCount = Mathf.Max(_recentThreatDirectionCount, _threatDirectionCount);
            }

            imminentThreatCount = Mathf.Max(imminentThreatCount, _recentThreatCount);
            _threatDirectionCount = Mathf.Max(_threatDirectionCount, _recentThreatDirectionCount);
            bool recentlyHit = Time.time - _self.LastIncomingDamageTime <= 1.5f;
            if (canAttack || imminentThreatCount > 0 || recentlyHit)
            {
                _engagementUntil = Time.time + k_EngagementContinuationSeconds;
            }

            bool engaged = Time.time < _engagementUntil;
            UpdatePostCombatStealth(engaged);
            _combatSeconds = engaged ? _combatSeconds + Mathf.Max(0.0f, deltaTime) : 0.0f;
            UnitStatus status = _self.GetUnitStatus();
            AiCombatStance stance = AiSurvivalPolicy.Evaluate(
                engaged, canAttack, imminentThreatCount, _threatDirectionCount, recentlyHit,
                status.CurrentHealth / Mathf.Max(1.0f, status.MaxHealth),
                _combatSeconds + (_self.PlayerId % 7) * 0.37f);
            if (stance != _combatStance)
            {
                _combatStance = stance;
                _weaponDecisionTimer = 0.0f;
            }

        }

        /// <summary>
        /// 交戦終了の遷移時だけ抽選し、索敵中の再抽選や周期的な解除を防ぎます。
        /// </summary>
        private void UpdatePostCombatStealth(bool isEngaged)
        {
            if (_wasEngaged && !isEngaged)
            {
                _useStealthOutsideCombat = AiSurvivalPolicy.ShouldUseStealthAfterCombat(UnityEngine.Random.value);
                _weaponDecisionTimer = 0.0f;
            }

            _wasEngaged = isEngaged;
        }

        /// <summary>
        /// 攻撃または両側防御の実行時にオブスクラの自動再発動を抑止します。
        /// </summary>
        private static void SetStealthActivation(IReadOnlyList<AiWeaponProfile> weapons, bool allowed)
        {
            if (weapons == null)
            {
                return;
            }

            foreach (AiWeaponProfile weapon in weapons)
            {
                if (weapon?.RuntimePranam is Obscura obscura)
                {
                    obscura.SetAutomaticActivationAllowed(allowed);
                }
            }
        }

        /// <summary>
        /// 攻撃候補を現在の隠密・片側防御・両側防御に合わせて組み合わせます。
        /// </summary>
        private void ApplySurvivalCandidates(
            ref AiWeaponProfile main, ref AiWeaponProfile sub, ref float mainUtility, ref float subUtility)
        {
            if (_combatStance == AiCombatStance.Conceal)
            {
                ApplyConcealmentCandidates(ref main, ref sub);
                return;
            }

            UnitStatus status = _self.GetUnitStatus();
            AiWeaponProfile mainDefense = _weaponSelector.SelectDefense(
                _mainWeapons, status.CurrentHealth, status.Proactiveness, _threatDirectionCount);
            AiWeaponProfile subDefense = _weaponSelector.SelectDefense(
                _subWeapons, status.CurrentHealth, status.Proactiveness, _threatDirectionCount);
            if (_combatStance == AiCombatStance.DoubleDefense && mainDefense != null && subDefense != null &&
                mainDefense.RequiredActionPointCost + subDefense.RequiredActionPointCost <= status.Proactiveness &&
                mainDefense.PranaCost + subDefense.PranaCost < status.CurrentHealth)
            {
                main = mainDefense;
                sub = subDefense;
            }
            else if (_combatStance != AiCombatStance.DoubleAttack)
            {
                bool defendMain = mainDefense != null && (subDefense == null || main == null ||
                    (sub != null && subUtility > mainUtility));
                if (defendMain)
                {
                    main = mainDefense;
                }
                else if (subDefense != null)
                {
                    sub = subDefense;
                }
            }

            main ??= _weaponSelector.SelectNonCombatSlot(_mainWeapons, false);
            sub ??= _weaponSelector.SelectNonCombatSlot(_subWeapons, false);
        }

        /// <summary>
        /// オブスクラと待機スロットを組み合わせ、移動中の隠密を維持します。
        /// </summary>
        private void ApplyConcealmentCandidates(ref AiWeaponProfile main, ref AiWeaponProfile sub)
        {
            AiWeaponProfile subStealth = _weaponSelector.SelectNonCombatSlot(_subWeapons, true);
            bool subHasStealth = subStealth?.RuntimePranam is Obscura;
            main = _weaponSelector.SelectNonCombatSlot(_mainWeapons, !subHasStealth) ?? main;
            sub = subStealth ?? sub;
        }

        /// <summary>
        /// 初動と交戦終了時の抽選結果に従い、交戦中は攻撃用スロットを維持します。
        /// </summary>
        private void PrepareStealthCandidates(ref AiWeaponProfile main, ref AiWeaponProfile sub, bool attackReady)
        {
            bool hasStealth = _weaponSelector.SelectNonCombatSlot(_mainWeapons, true)?.RuntimePranam is Obscura ||
                _weaponSelector.SelectNonCombatSlot(_subWeapons, true)?.RuntimePranam is Obscura;
            _shouldMaintainStealth = AiSurvivalPolicy.ShouldMaintainStealth(
                hasStealth && _useStealthOutsideCombat,
                attackReady || Time.time < _engagementUntil ||
                    AiSurvivalPolicy.ShouldRemainRevealed(Time.time - _self.LastAttackTime),
                _combatStance == AiCombatStance.DoubleDefense);
            SetStealthActivation(_mainWeapons, _shouldMaintainStealth);
            SetStealthActivation(_subWeapons, _shouldMaintainStealth);
            if (_shouldMaintainStealth)
            {
                ApplyConcealmentCandidates(ref main, ref sub);
            }
        }

        /// <summary>
        /// 武装の自動更新を待たず、選択済みのオブスクラを発動します。
        /// </summary>
        private static void ActivateSelectedStealth(AiWeaponProfile weapon)
        {
            if (weapon?.RuntimePranam is Obscura obscura && obscura.isActiveAndEnabled)
            {
                obscura.UsePranam();
            }
        }

        /// <summary>
        /// 照準、反応時間、使用コスト、硬直、射撃間隔が発射可能か確認します。
        /// </summary>
        private bool IsAttackReadyToReveal(
            AiWeaponProfile weapon, bool isMain, float distance,
            AiWeaponSelectionContext context, AiPublicUnitState targetState)
        {
            if (context == null || _target == null || _reactionTimer > 0.0f ||
                (_tacticalDirective != null && _tacticalDirective.SuppressWeaponUse) ||
                !CanUseWeapon(weapon, distance, targetState, context.IsDirectlyVisible))
            {
                return false;
            }

            MicroUnitBehavior micro = _self.GetMicroUnitBehavior();
            Transform origin = weapon.RuntimePranam != null ? weapon.RuntimePranam.transform :
                (isMain ? micro.GetMainPranamTransform() : micro.GetSubPranamTransform());
            bool aligned = IsWeaponAimAligned(weapon, GetSignedMapAngleToTarget(origin));
            if (_teamTaskId == AiTeamTaskIds.CoordinatedStrike && aligned)
            {
                _isReadyForCoordinatedStrike = true;
                if (!_isCoordinatedFireAuthorized)
                {
                    return false;
                }
            }

            PranamManager manager = micro.GetComponentInChildren<PranamManager>(true);
            PranamBehavior behavior = isMain ? manager?.MainPranamBehavior : manager?.SubPranamBehavior;
            float exitCost = GetActiveStealthExitCost(_mainWeapons) + GetActiveStealthExitCost(_subWeapons);
            return aligned && behavior != null && !behavior.IsRecovering &&
                _self.GetUnitStatus().CurrentHealth >= weapon.PranaCost + exitCost &&
                (RequiresHoldInput(weapon) || (isMain ? _mainFireTimer : _subFireTimer) <= 0.0f);
        }

        /// <summary>
        /// 発射直前の資源確認用に、発動中のオブスクラの解除コストを取得します。
        /// </summary>
        private static float GetActiveStealthExitCost(IReadOnlyList<AiWeaponProfile> weapons)
        {
            float cost = 0.0f;
            if (weapons != null)
            {
                foreach (AiWeaponProfile weapon in weapons)
                {
                    if (weapon?.RuntimePranam is Obscura obscura && obscura.IsStealthActive)
                    {
                        cost += weapon.PranaCost;
                    }
                }
            }

            return cost;
        }

        /// <summary>
        /// 手動防御は敵の照準合わせや反応待ちに依存せず入力します。
        /// 自動防御はスロットの選択だけで動作します。
        /// </summary>
        private bool CanUseDefense(AiWeaponProfile weapon)
        {
            return !_shouldMaintainStealth && _combatStance != AiCombatStance.Conceal && weapon != null && weapon.IsUsable &&
                weapon.UseMode == AiWeaponUseMode.Defense && weapon.RuntimePranam != null &&
                weapon.RuntimePranam.RequiresInput && !_self.IsStealthActive &&
                weapon.PranaCost < _self.GetUnitStatus().CurrentHealth &&
                weapon.RequiredActionPointCost <= _self.GetUnitStatus().Proactiveness;
        }

        /// <summary>
        /// 狙撃武器の所有側へ視点を合わせ、Sub側でも照準を開始できるようにします。
        /// </summary>
        private void ApplyAttackPerspective(ref UnitInputState state, MicroUnitBehavior micro, float distance)
        {
            bool mainSniper = IsSniperWithinRange(_selectedMain, distance);
            bool subSniper = IsSniperWithinRange(_selectedSub, distance);
            bool shouldUseMain = mainSniper || !subSniper;

            if (!mainSniper && !subSniper && _selectedMain == null && _selectedSub != null)
            {
                shouldUseMain = false;
            }

            state.ShouldSwitchPerspective = micro.IsMainPranamPerspective != shouldUseMain;
            if (_shouldMaintainStealth)
            {
                state.ShouldSwitchPerspective = micro.IsMainPranamPerspective != _concealedPerspectiveMain;
            }
        }

        /// <summary>
        /// 選択した武器が射程内の狙撃武器か判定します。
        /// </summary>
        private bool IsSniperWithinRange(AiWeaponProfile weapon, float distance)
        {
            return weapon != null && weapon.IsUsable &&
                AiId.Equals(weapon.WeaponRoleId, AiRoleIds.Sniper) &&
                _self.GetUnitStatus().CanAffordProactiveness(weapon.RequiredActionPointCost) &&
                _self.GetUnitStatus().CurrentHealth >= weapon.PranaCost &&
                distance >= weapon.MinimumRange && distance <= weapon.MaximumRange;
        }

        /// <summary>
        /// 高低差のある敵にもカメラを向けるため、現在視線からの仰角差を求めます。
        /// </summary>
        private float GetCameraPitchAngleToTarget(MicroUnitBehavior micro)
        {
            Camera camera = micro.GetCamera();
            MicroUnitBehavior targetMicro = _target.GetMicroUnitBehavior();

            if (camera == null || targetMicro == null)
            {
                return 0.0f;
            }

            Camera targetCamera = targetMicro.GetCamera();
            Vector3 targetPosition = targetCamera != null ? targetCamera.transform.position :
                targetMicro.transform.position + Vector3.up * 1.5f;
            Vector3 direction = targetPosition - camera.transform.position;
            float horizontalDistance = new Vector2(direction.x, direction.z).magnitude;
            float targetElevation = Mathf.Atan2(direction.y, horizontalDistance) * Mathf.Rad2Deg;
            Vector3 forward = camera.transform.forward;
            float currentElevation = Mathf.Atan2(forward.y, new Vector2(forward.x, forward.z).magnitude) * Mathf.Rad2Deg;

            return targetElevation - currentElevation;
        }

        /// <summary>
        /// ロール定義の移動率を基に，通常移動・警戒移動・短時間停止を
        /// 決定論的に切り替えます。
        /// </summary>
        private void UpdateMovementPace(float deltaTime)
        {
            _movementPaceTimer -= Mathf.Max(0.0f, deltaTime);

            if (_movementPaceTimer > 0.0f)
            {
                return;
            }

            AiRoleProfile profile = RoleProfile;
            float movementDutyRatio = profile != null ? profile.MovementDutyRatio : 0.88f;
            float stopChance = Mathf.Clamp01(1.0f - movementDutyRatio);
            float cautionChance = Mathf.Lerp(0.42f, 0.18f, movementDutyRatio);
            float selection = SampleMovement01(11);
            float durationSample = SampleMovement01(12);

            if (selection < stopChance)
            {
                _movementPace = MovementPace.ShortHold;
                float maximumStopDuration = Mathf.Lerp(2.20f, 0.35f, movementDutyRatio);

                _movementPaceTimer = Mathf.Lerp(0.15f, maximumStopDuration, durationSample);
            }
            else if (selection < stopChance + cautionChance)
            {
                _movementPace = MovementPace.Caution;
                _movementPaceTimer = Mathf.Lerp(0.65f, 1.60f, durationSample);
            }
            else
            {
                _movementPace = MovementPace.Cruise;
                _movementPaceTimer = Mathf.Lerp(1.75f, 4.50f, durationSample);
            }

            _movementDecisionSequence++;
        }

        /// <summary>
        /// 移動速度の強弱を既存の停止・歩行入力へ変換します。
        /// 通常攻撃中もこの入力は維持されます。
        /// </summary>
        private void ApplyMovementPace(ref UnitInputState state)
        {
            if (AiCombatDecisionPolicy.ShouldKeepMoving(
                _hasAttackOpportunity,
                _tacticalDirective != null &&
                _tacticalDirective.ForceReposition,
                _tacticalDirective != null &&
                _tacticalDirective.SuppressWeaponUse))
            {
                state.IsStopping = false;
                state.IsWalking = false;

                return;
            }

            state.IsStopping = _movementPace == MovementPace.ShortHold;
            state.IsWalking = !state.IsStopping && _movementPace == MovementPace.Caution;
        }

        /// <summary>
        /// 高硬直武器、または停止を重視するロールは、
        /// 攻撃機会を得た時点から照準のために停止します。
        /// </summary>
        private void ApplyStationaryAttackHold(ref UnitInputState state, bool canMainFire, bool canSubFire)
        {
            bool mainRequiresHold = canMainFire && RequiresStationaryAttack(_selectedMain);
            bool subRequiresHold = canSubFire && RequiresStationaryAttack(_selectedSub);

            if (!mainRequiresHold && !subRequiresHold)
            {
                return;
            }

            state.IsStopping = true;
            state.IsWalking = false;
        }

        private bool RequiresStationaryAttack(AiWeaponProfile weapon)
        {
            if (weapon != null && weapon.HasAbilityTag(AiAbilityTags.HighCommitment))
            {
                return true;
            }

            AiRoleProfile profile = RoleProfile;

            return profile != null && profile.MovementDutyRatio < 0.70f;
        }

        private float SampleMovement01(int channel)
        {
            float signedValue = SampleSigned(
                _self != null ? _self.PlayerId : 0,
                _microSequence,
                _movementDecisionSequence,
                channel);

            return Mathf.Clamp01(signedValue * 0.5f + 0.5f);
        }

        private void RebuildWeaponProfiles()
        {
            if (_self == null || _self.GetTacticalMapUnitBehavior() == null)
            {
                return;
            }

            Vector2Int coordinate = _self.GetTacticalMapUnitBehavior().GetCurrentCoordinate();
            float spacing = 1.0f;

            try
            {
                spacing = new AiBoardAdapter().GetCellSpacing(coordinate);
            }
            catch
            {
                spacing = 1.0f;
            }

            _mainWeapons = _weaponSelector.BuildProfiles(_self, true, spacing);
            _subWeapons = _weaponSelector.BuildProfiles(_self, false, spacing);
        }

        private void SelectWeapons(
            float distance, AiWeaponSelectionContext selectionContext, AiPublicUnitState targetState = null)
        {
            if (_mainWeapons == null || _subWeapons == null)
            {
                RebuildWeaponProfiles();
            }

            if (_self == null || _self.GetUnitStatus() == null)
            {
                return;
            }

            float currentPrana = _self.GetUnitStatus().CurrentHealth;
            float maximumPrana = _self.GetUnitStatus().MaxHealth;
            string roleId = RoleId;

            _selectedMainUtility = _selectedMain != null ? _weaponSelector.Evaluate(
                roleId,
                _selectedMain,
                distance,
                currentPrana,
                maximumPrana,
                selectionContext) : float.NegativeInfinity;
            _selectedSubUtility = _selectedSub != null ? _weaponSelector.Evaluate(
                roleId,
                _selectedSub,
                distance,
                currentPrana,
                maximumPrana,
                selectionContext) : float.NegativeInfinity;
            _selectedMainUtility -= AiCombatDecisionPolicy.EvaluateWeaponRepetitionPenalty(_mainAttackSeconds);
            _selectedSubUtility -= AiCombatDecisionPolicy.EvaluateWeaponRepetitionPenalty(_subAttackSeconds);
            AiWeaponProfile mainCandidate = _weaponSelector.SelectBest(
                roleId,
                _mainWeapons,
                distance,
                currentPrana,
                maximumPrana,
                selectionContext,
                out float mainUtility,
                _selectedMain != null ? _selectedMain.Slot : -1,
                _mainAttackSeconds,
                _self.GetUnitStatus().Proactiveness);
            AiWeaponProfile subCandidate = _weaponSelector.SelectBest(
                roleId,
                _subWeapons,
                distance,
                currentPrana,
                maximumPrana,
                selectionContext,
                out float subUtility,
                _selectedSub != null ? _selectedSub.Slot : -1,
                _subAttackSeconds,
                _self.GetUnitStatus().Proactiveness);

            ApplySurvivalCandidates(ref mainCandidate, ref subCandidate, ref mainUtility, ref subUtility);
            _isReadyForCoordinatedStrike = false;
            _concealedPerspectiveMain = IsSniperWithinRange(mainCandidate, distance) ||
                !IsSniperWithinRange(subCandidate, distance);
            bool attackReady = IsAttackReadyToReveal(mainCandidate, true, distance, selectionContext, targetState) ||
                IsAttackReadyToReveal(subCandidate, false, distance, selectionContext, targetState);
            attackReady &= _self.GetMicroUnitBehavior() != null &&
                _self.GetMicroUnitBehavior().IsMainPranamPerspective == _concealedPerspectiveMain;
            if (attackReady)
            {
                _lastReadyAttackTime = Time.time;
            }

            bool attackAuthorized = _reactionTimer <= 0.0f &&
                (_tacticalDirective == null || !_tacticalDirective.SuppressWeaponUse) &&
                (_teamTaskId != AiTeamTaskIds.CoordinatedStrike || _isCoordinatedFireAuthorized);
            bool canContinueAttack = AiSurvivalPolicy.CanContinueAttack(
                !_shouldMaintainStealth && !_self.IsStealthActive,
                CanUseWeapon(mainCandidate, distance, targetState, selectionContext?.IsDirectlyVisible == true) ||
                    CanUseWeapon(subCandidate, distance, targetState, selectionContext?.IsDirectlyVisible == true),
                attackAuthorized,
                Time.time - _lastReadyAttackTime);
            PrepareStealthCandidates(ref mainCandidate, ref subCandidate, attackReady || canContinueAttack);
            int previousMainSlot = _selectedMain != null ? _selectedMain.Slot : -1;
            int previousSubSlot = _selectedSub != null ? _selectedSub.Slot : -1;

            SelectChannelWeapon(
                true,
                mainCandidate,
                mainUtility,
                distance,
                selectionContext,
                ref _selectedMain,
                ref _selectedMainUtility);
            SelectChannelWeapon(
                false,
                subCandidate,
                subUtility,
                distance,
                selectionContext,
                ref _selectedSub,
                ref _selectedSubUtility);

            if (_selectedMain != null && _selectedMain.Slot != previousMainSlot)
            {
                _mainAttackSeconds = 0.0f;
            }

            if (_selectedSub != null && _selectedSub.Slot != previousSubSlot)
            {
                _subAttackSeconds = 0.0f;
            }

            if (_shouldMaintainStealth)
            {
                ActivateSelectedStealth(_selectedMain);
                ActivateSelectedStealth(_selectedSub);
            }
        }

        /// <remarks>
        /// <para>視認状態または対象の移動状態に対して、候補の兵装種別が</para>
        /// <para>現在兵装より適している場合はUtility差を待たず切り替えます。</para>
        /// <para>これにより、直接視認後もアルクスを保持し続けることを</para>
        /// <para>防止します。</para>
        /// </remarks>
        private void SelectChannelWeapon(
            bool isMain,
            AiWeaponProfile candidate,
            float candidateUtility,
            float distance,
            AiWeaponSelectionContext selectionContext,
            ref AiWeaponProfile selected,
            ref float selectedUtility)
        {
            if (candidate == null)
            {
                return;
            }

            if (selected != null && candidate.Slot == selected.Slot)
            {
                selected = candidate;
                selectedUtility = candidateUtility;

                return;
            }

            if (!candidate.IsUsable || candidate.UseMode != AiWeaponUseMode.Attack ||
                (selected != null && selected.UseMode != AiWeaponUseMode.Attack))
            {
                if (AiSlotSwitcher.TrySelect(_self, isMain, candidate.Slot))
                {
                    selected = candidate;
                    selectedUtility = candidateUtility;
                }

                return;
            }

            bool currentOutOfRange = selected == null || distance < selected.MinimumRange ||
                distance > selected.MaximumRange;
            bool currentInvalid = selected == null || !selected.IsUsable ||
                !_self.GetUnitStatus().CanAffordProactiveness(selected.RequiredActionPointCost) ||
                float.IsNegativeInfinity(
                selectedUtility);
            int candidateSituationPriority = AiWeaponSelector.GetSituationPriority(
                candidate,
                selectionContext);
            int selectedSituationPriority = AiWeaponSelector.GetSituationPriority(selected, selectionContext);
            bool isSituationUpgrade = candidateSituationPriority > selectedSituationPriority;
            bool isSituationDowngrade = candidateSituationPriority < selectedSituationPriority;

            if (!currentInvalid && !currentOutOfRange && isSituationDowngrade)
            {
                return;
            }

            if (!currentInvalid && !currentOutOfRange && !isSituationUpgrade &&
                candidateUtility < selectedUtility +
                k_SwitchUtilityMargin)
            {
                return;
            }

            if (AiSlotSwitcher.TrySelect(_self, isMain, candidate.Slot))
            {
                selected = candidate;
                selectedUtility = candidateUtility;
            }
        }

        private bool CanUseWeapon(
            AiWeaponProfile weapon,
            float distance,
            AiPublicUnitState targetState,
            bool directlyVisible)
        {
            if (weapon == null || !weapon.IsUsable || weapon.UseMode != AiWeaponUseMode.Attack ||
                _combatStance == AiCombatStance.Conceal || targetState == null || distance < weapon.MinimumRange ||
                distance > weapon.MaximumRange)
            {
                return false;
            }

            if (targetState.ContactState == AiContactState.LastKnown ||
                targetState.ContactState == AiContactState.Unknown)
            {
                return false;
            }

            if (weapon.VisibilityRequirement == AiVisibilityRequirement.DirectVisible && !directlyVisible)
            {
                return false;
            }

            if (weapon.VisibilityRequirement == AiVisibilityRequirement.MapTracked &&
                targetState.ContactState < AiContactState.MapTracked)
            {
                return false;
            }

            float currentPrana = _self.GetUnitStatus().CurrentHealth;

            return currentPrana + 0.001f >= weapon.PranaCost &&
                _self.GetUnitStatus().CanAffordProactiveness(weapon.RequiredActionPointCost);
        }

        private void ApplyMainFire(ref UnitInputState state, bool canFire)
        {
            bool shouldHold = canFire && RequiresHoldInput(_selectedMain);

            state.IsPrimaryHeld = shouldHold;

            if (canFire && !shouldHold && _mainFireTimer <= 0.0f)
            {
                state.IsPrimaryPressed = true;
                _mainFireTimer = k_MainFireInterval * _difficulty.FireIntervalMultiplier;
            }

            state.IsPrimaryReleased = _wasMainHolding && !shouldHold;
            _wasMainHolding = shouldHold;
        }

        private void ApplySubFire(ref UnitInputState state, bool canFire)
        {
            bool shouldHold = canFire && RequiresHoldInput(_selectedSub);

            state.IsSecondaryHeld = shouldHold;

            if (canFire && !shouldHold && _subFireTimer <= 0.0f)
            {
                state.IsSecondaryPressed = true;
                _subFireTimer = k_SubFireInterval * _difficulty.FireIntervalMultiplier;
            }

            state.IsSecondaryReleased = _wasSubHolding && !shouldHold;
            _wasSubHolding = shouldHold;
        }

        /// <summary>
        /// AIが継続入力を送る必要がある兵装か判定します。
        /// UsesHoldの設定ミスだけで単発兵装へHeld入力を送らないよう、
        /// RapidFireタグとの両方を必須にします。
        /// </summary>
        private static bool RequiresHoldInput(AiWeaponProfile weapon)
        {
            return weapon != null && weapon.UsesHold && weapon.HasAbilityTag(AiAbilityTags.RapidFire);
        }

        private void ApplyReleases(ref UnitInputState state, bool mainHolding, bool subHolding)
        {
            state.IsPrimaryReleased = _wasMainHolding && !mainHolding;
            state.IsSecondaryReleased = _wasSubHolding && !subHolding;
            _wasMainHolding = mainHolding;
            _wasSubHolding = subHolding;
        }

        /// <remarks>
        /// <para>連携攻撃でも担当対象を攻撃できない場合は、射撃可能な敵を優先します。</para>
        /// <para>一時的に見失った場合も、再観測時には担当対象へ復帰します。</para>
        /// <para>標的が見つからない場合も、難易度の判断間隔で再走査します。</para>
        /// <para>有望でない候補は射線判定を省き、同点はIDで安定させます。</para>
        /// <para>遮蔽中の担当敵をMapTrackedだけで固定せず、現在視認できて</para>
        /// <para>射程内にいる敵へ切り替えます。</para>
        /// </remarks>
        private UnitBehavior ResolveValidTarget(UnitBehavior current, AiWorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            AiPublicUnitState currentState = current != null ? snapshot.FindEnemy(current.PlayerId) : null;
            bool isCurrentValid = currentState != null && currentState.IsAlive && currentState.Unit != null &&
                currentState.ContactState >= AiContactState.MapTracked;

            if (_targetDecisionTimer > 0.0f)
            {
                return isCurrentValid ? currentState.Unit : null;
            }

            _targetDecisionTimer = Mathf.Max(0.05f, _difficulty.DecisionInterval);

            if (_mainWeapons == null || _subWeapons == null)
            {
                RebuildWeaponProfiles();
            }

            AiPublicUnitState bestAttackable = null;
            float bestAttackableUtility = float.NegativeInfinity;
            AiPublicUnitState best = null;
            float bestDistance = float.MaxValue;

            if (!TryGetMapPosition(_self, out Vector3 selfPosition))
            {
                return null;
            }

            float rangeScale = Mathf.Max(
                1.0f,
                Mathf.Max(
                GetMaximumAttackRange(_mainWeapons),
                GetMaximumAttackRange(
                _subWeapons)));

            foreach (AiPublicUnitState enemy in snapshot.Enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy.Unit == null ||
                    enemy.ContactState < AiContactState.MapTracked)
                {
                    continue;
                }

                Vector3 difference = enemy.MapWorldPosition - selfPosition;

                difference.y = 0.0f;
                float distance = difference.sqrMagnitude;
                float mapDistance = Mathf.Sqrt(distance);
                float utility = AiCombatDecisionPolicy.EvaluateTarget(
                    mapDistance / rangeScale,
                    enemy.EstimatedPranaRatio,
                    enemy.PranaEstimateConfidence,
                    _tacticalDirective != null &&
                    _tacticalDirective.Target != null &&
                    _tacticalDirective.Target.PlayerId == enemy.PlayerId,
                    isCurrentValid &&
                    currentState.PlayerId == enemy.PlayerId);

                if ((utility > bestAttackableUtility ||
                    (utility == bestAttackableUtility &&
                    bestAttackable != null &&
                    enemy.PlayerId < bestAttackable.PlayerId)) &&
                    CanAttackTargetWithAnyWeapon(
                    enemy,
                    mapDistance))
                {
                    bestAttackableUtility = utility;
                    bestAttackable = enemy;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }

            if (bestAttackable != null)
            {
                return bestAttackable.Unit;
            }

            if (isCurrentValid)
            {
                return currentState.Unit;
            }

            return best != null ? best.Unit : null;
        }

        private static float GetMaximumAttackRange(IReadOnlyList<AiWeaponProfile> weapons)
        {
            float range = 0.0f;

            if (weapons == null)
            {
                return range;
            }

            foreach (AiWeaponProfile weapon in weapons)
            {
                if (weapon != null && weapon.IsUsable && weapon.UseMode == AiWeaponUseMode.Attack)
                {
                    range = Mathf.Max(range, weapon.MaximumRange);
                }
            }

            return range;
        }

        /// <summary>
        /// Main/Subのいずれかで対象を現在攻撃できるか判定します。
        /// 距離はMapUnit、直接視認はMicroUnitを使用します。
        /// </summary>
        /// <remarks>
        /// <para>間接攻撃が可能なら視線判定は不要です。</para>
        /// <para>射程外・コスト不足の敵にもPhysics判定を行わないようにします。</para>
        /// </remarks>
        private bool CanAttackTargetWithAnyWeapon(AiPublicUnitState targetState, float mapDistance)
        {
            if (targetState == null || targetState.Unit == null || !targetState.IsAlive ||
                targetState.ContactState < AiContactState.MapTracked ||
                _self == null ||
                _self.GetUnitStatus() == null)
            {
                return false;
            }

            float currentPrana = _self.GetUnitStatus().CurrentHealth;

            if (CanAttackTargetWithWeapons(_mainWeapons, targetState, mapDistance, false, currentPrana) ||
                CanAttackTargetWithWeapons(
                _subWeapons,
                targetState,
                mapDistance,
                false,
                currentPrana))
            {
                return true;
            }

            bool canAttackIfVisible = CanAttackTargetWithWeapons(
                _mainWeapons,
                targetState,
                mapDistance,
                true,
                currentPrana) ||
                CanAttackTargetWithWeapons(
                _subWeapons,
                targetState,
                mapDistance,
                true,
                currentPrana);

            return canAttackIfVisible && AiVisibilityEvaluator.CanDirectlySee(_self, targetState.Unit);
        }

        private bool CanAttackTargetWithWeapons(
            IReadOnlyList<AiWeaponProfile> weapons,
            AiPublicUnitState targetState,
            float mapDistance,
            bool directlyVisible,
            float currentPrana)
        {
            if (weapons == null)
            {
                return false;
            }

            foreach (AiWeaponProfile weapon in weapons)
            {
                if (weapon == null || !weapon.IsUsable || weapon.UseMode != AiWeaponUseMode.Attack ||
                    !_self.GetUnitStatus().CanAffordProactiveness(weapon.RequiredActionPointCost) ||
                    currentPrana +
                    0.001f < weapon.PranaCost ||
                    mapDistance < weapon.MinimumRange ||
                    mapDistance > weapon.MaximumRange)
                {
                    continue;
                }

                if (weapon.VisibilityRequirement == AiVisibilityRequirement.DirectVisible && !directlyVisible)
                {
                    continue;
                }

                if (weapon.VisibilityRequirement == AiVisibilityRequirement.MapTracked &&
                    targetState.ContactState < AiContactState.MapTracked)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private float GetTargetMapDistance()
        {
            if (!TryGetMapPosition(_self, out Vector3 selfPosition) ||
                !TryGetMapPosition(
                _target,
                out Vector3 targetPosition))
            {
                return float.MaxValue;
            }

            Vector2 self = new Vector2(selfPosition.x, selfPosition.z);
            Vector2 target = new Vector2(targetPosition.x, targetPosition.z);

            return Vector2.Distance(self, target);
        }

        private float GetSignedMapAngleToTarget(Transform triggerTransform)
        {
            if (_target == null || triggerTransform == null ||
                !TryGetMapPosition(
                _self,
                out Vector3 selfPosition) ||
                !TryGetMapPosition(
                _target,
                out Vector3 targetPosition))
            {
                return 0.0f;
            }

            Vector3 direction = targetPosition - selfPosition;

            direction.y = 0.0f;
            Vector3 forward = triggerTransform.forward;

            forward.y = 0.0f;

            if (direction.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f)
            {
                return 0.0f;
            }

            return Vector3.SignedAngle(forward.normalized, direction.normalized, Vector3.up);
        }

        private static bool TryGetMapPosition(UnitBehavior unit, out Vector3 position)
        {
            position = Vector3.zero;

            if (unit == null || unit.GetTacticalMapUnitBehavior() == null)
            {
                return false;
            }

            position = unit.GetTacticalMapUnitBehavior().transform.position;

            return true;
        }

        private void RefreshAimError()
        {
            _mainAimError = SampleSigned(
                _self != null ? _self.PlayerId : 0,
                _microSequence,
                _decisionSequence,
                1) * _difficulty.AimErrorDegrees;
            _subAimError = SampleSigned(
                _self != null ? _self.PlayerId : 0,
                _microSequence,
                _decisionSequence,
                2) * _difficulty.AimErrorDegrees;
            _decisionSequence++;
        }

        /// <summary>
        /// 狭い射界の兵装でも敵を射界内へ捉えられるよう、照準誤差を制限します。
        /// </summary>
        private static float ClampWeaponAimError(AiWeaponProfile weapon, float aimError)
        {
            float limit = AiCombatDecisionPolicy.ResolveAimLimit(
                Mathf.Abs(aimError),
                weapon != null ? weapon.AttackArcDegrees * 0.5f : 360.0f);

            return Mathf.Sign(aimError) * limit;
        }

        /// <summary>
        /// 誤差を加える前の実際の武器方向から、射界内で発射できるか判定します。
        /// </summary>
        private bool IsWeaponAimAligned(AiWeaponProfile weapon, float targetAngle)
        {
            return weapon != null && Mathf.Abs(targetAngle) <=
                AiCombatDecisionPolicy.ResolveAimLimit(_difficulty.AimToleranceDegrees, weapon.AttackArcDegrees);
        }

        private static float SampleSigned(int playerId, int microSequence, int decisionSequence, int channel)
        {
            unchecked
            {
                uint value = (uint)(playerId + 1) * 747796405u;

                value ^= (uint)(microSequence + 1) * 2891336453u;
                value ^= (uint)(decisionSequence + 1) * 277803737u;
                value ^= (uint)channel * 1402946737u;
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                float normalized = (value & 0x00FFFFFFu) / 16777215.0f;

                return normalized * 2.0f - 1.0f;
            }
        }
    }
}
