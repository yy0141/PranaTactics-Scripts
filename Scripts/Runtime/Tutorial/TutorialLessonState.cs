using PranaTactics.Core;

namespace PranaTactics.Tutorial
{
    public enum TutorialStep
    {
        Briefing,
        Plan,
        Move,
        Perspective,
        Map,
        Weapons,

        Aim,
        Exercise,
        Complete
    }

    /// <summary>
    /// 各実地課題の開始・観測・終了を担当します。
    /// </summary>
    public abstract class TutorialLessonState : IState
    {
        protected readonly TutorialSession _session;

        public abstract TutorialStep Step
        {
            get;
        }

        protected TutorialLessonState(TutorialSession session)
        {
            _session = session;
        }

        public abstract void Enter();

        public virtual void UpdateState()
        {
        }

        public virtual void Exit()
        {
        }
    }
}
