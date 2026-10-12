using UnityEngine;

using PranaTactics.AI.Solo;
using PranaTactics.Unit;

namespace PranaTactics.Controls.AI
{
    /// <summary>
    /// CPUユニットの入力状態を提供します。
    /// </summary>
    public sealed class AIUnitInput : IUnitInput
    {
        private readonly SoloAiAgent _agent;

        private UnitInputState _currentState;

        private int _lastUpdatedFrame = -1;

        public AIUnitInput(UnitBehavior self)
        {
            SoloAiDirector director =
                SoloAiDirector.EnsureExists();

            _agent = director.Register(self);
            _currentState = new UnitInputState();
        }

        public UnitInputState CurrentState
        {
            get
            {
                if (_lastUpdatedFrame == Time.frameCount)
                {
                    return _currentState;
                }

                _lastUpdatedFrame = Time.frameCount;

                _currentState = _agent != null
                    ? _agent.GetInputState()
                    : new UnitInputState();

                return _currentState;
            }
        }
    }
}
