namespace PranaTactics.Tutorial
{
    public sealed class TutorialBriefingState : TutorialLessonState
    {
        public TutorialBriefingState(TutorialSession session) : base(session)
        {
        }

        public override TutorialStep Step => TutorialStep.Briefing;

        public override void Enter()
        {
            _session.PresentLesson(Step);
        }
    }

    public sealed class TutorialPlanState : TutorialLessonState
    {
        public TutorialPlanState(TutorialSession session) : base(session)
        {
        }

        public override TutorialStep Step => TutorialStep.Plan;

        public override void Enter()
        {
            _session.ResetArena();
            _session.BeginPlanning();
            _session.PresentLesson(Step);
        }

        public override void UpdateState()
        {
            if (_session.PlanningFinished)
            {
                _session.MarkObjectiveComplete();
            }
        }
    }

    public sealed class TutorialMoveState : TutorialLessonState
    {
        private bool _isChangedSpeed;

        public TutorialMoveState(TutorialSession session) : base(session)
        {
        }

        public override TutorialStep Step => TutorialStep.Move;

        public override void Enter()
        {
            _session.BeginCombat();
            _session.PresentLesson(Step);
        }

        public override void UpdateState()
        {
            _isChangedSpeed |= _session.IsWalkingOrStopping;

            if (_isChangedSpeed && !_session.IsWalkingOrStopping && _session.ExecutedSteps > 0)
            {
                _session.MarkObjectiveComplete();
            }
        }
    }

    public sealed class TutorialPerspectiveState : TutorialLessonState
    {
        private bool _wasInitiallyMainPerspective;

        private bool _isSwitched;

        public TutorialPerspectiveState(TutorialSession session) : base(session)
        {
        }

        public override TutorialStep Step => TutorialStep.Perspective;

        public override void Enter()
        {
            _isSwitched = false;
            _session.BeginCombat();
            _wasInitiallyMainPerspective = _session.IsMainPerspective;
            _session.PresentLesson(Step);
        }

        public override void UpdateState()
        {
            if (_session.IsPerspectiveTransitioning)
            {
                return;
            }

            _isSwitched |= _session.IsMainPerspective != _wasInitiallyMainPerspective;

            if (_isSwitched && _session.IsMainPerspective == _wasInitiallyMainPerspective)
            {
                _session.MarkObjectiveComplete();
            }
        }
    }

    public sealed class TutorialMapState : TutorialLessonState
    {
        private bool _isOpened;

        public TutorialMapState(TutorialSession session) : base(session)
        {
        }

        public override TutorialStep Step => TutorialStep.Map;

        public override void Enter()
        {
            _isOpened = false;
            _session.BeginCombat();
            _session.CloseMap();
            _session.PresentLesson(Step);
        }

        public override void UpdateState()
        {
            _isOpened |= _session.IsMapOpen;

            if (_isOpened && !_session.IsMapOpen)
            {
                _session.MarkObjectiveComplete();
            }
        }

        public override void Exit()
        {
            _session.CloseMap();
        }
    }

    public sealed class TutorialWeaponsState : TutorialLessonState
    {
        private int _main;

        private int _sub;

        private bool _isMainChanged;

        private bool _isSubChanged;

        public TutorialWeaponsState(TutorialSession session) : base(session)
        {
        }

        public override TutorialStep Step => TutorialStep.Weapons;

        public override void Enter()
        {
            _session.BeginCombat();
            _main = _session.MainWeaponSlot;
            _sub = _session.SubWeaponSlot;
            _isMainChanged = _isSubChanged = false;
            _session.PresentLesson(Step);
        }

        public override void UpdateState()
        {
            if (_main < 0)
            {
                _main = _session.MainWeaponSlot;
            }

            if (_sub < 0)
            {
                _sub = _session.SubWeaponSlot;
            }

            _isMainChanged |= _main >= 0 && _session.MainWeaponSlot >= 0 && _session.MainWeaponSlot != _main;
            _isSubChanged |= _sub >= 0 && _session.SubWeaponSlot >= 0 && _session.SubWeaponSlot != _sub;

            if (_isMainChanged && _isSubChanged)
            {
                _session.MarkObjectiveComplete();
            }
        }
    }

    public sealed class TutorialAimState : TutorialLessonState
    {
        private bool _isFired;

        public TutorialAimState(TutorialSession session) : base(session)
        {
        }

        public override TutorialStep Step => TutorialStep.Aim;

        public override void Enter()
        {
            _session.PrepareTarget();
            _session.BeginCombat();
            _session.PresentLesson(Step);
        }

        public override void UpdateState()
        {
            _isFired |= _session.IsFiring;

            if (_isFired && _session.TargetWasHit)
            {
                _session.MarkObjectiveComplete();
            }
        }
    }

    public sealed class TutorialExerciseState : TutorialLessonState
    {
        private bool _isPlanned;

        private bool _isMoved;

        private bool _isFired;

        public TutorialExerciseState(TutorialSession session) : base(session)
        {
        }

        public override TutorialStep Step => TutorialStep.Exercise;

        public override void Enter()
        {
            _isPlanned = _isMoved = _isFired = false;
            _session.ResetArena();
            _session.BeginPlanning();
            _session.PresentLesson(Step);
            _session.SetExerciseChecklist(false, false, false);
        }

        public override void UpdateState()
        {
            _isPlanned |= _session.PlanningFinished;

            if (!_isMoved && _session.ExecutedSteps >= 2)
            {
                _isMoved = true;
                _session.ResetHitObservation();
            }

            _isFired |= _isMoved && _session.IsFiring;
            bool hit = _isMoved && _isFired && _session.TargetWasHit;

            _session.SetExerciseChecklist(_isPlanned, _isMoved, hit);

            if (hit)
            {
                _session.MarkObjectiveComplete();
            }
        }
    }

    public sealed class TutorialCompleteState : TutorialLessonState
    {
        public TutorialCompleteState(TutorialSession session) : base(session)
        {
        }

        public override TutorialStep Step => TutorialStep.Complete;

        public override void Enter()
        {
            _session.PresentLesson(Step);
            _session.MarkObjectiveComplete();
        }
    }
}
