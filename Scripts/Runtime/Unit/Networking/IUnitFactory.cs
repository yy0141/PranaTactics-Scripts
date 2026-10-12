using PranaTactics.Unit;

namespace PranaTactics.Unit.Networking
{
    public interface IUnitFactory
    {
        UnitBehavior Create(UnitInitializeData data);
    }
}
