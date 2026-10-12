using System.Collections.Generic;

namespace PranaTactics.Networking
{
    /// <summary>
    /// パーティを分割せず、二つのチームの定員内に収める組み合わせを求めます。
    /// </summary>
    public static class PartyTeamPlanner
    {
        /// <summary>
        /// 到達可能人数を記録し、貪欲法では見逃す組み合わせも探索します。
        /// </summary>
        public static bool TryAssign(IReadOnlyList<int> sizes, int capacityA, int capacityB, out bool[] teamA)
        {
            teamA = null;
            if (sizes == null || capacityA < 0 || capacityB < 0)
            {
                return false;
            }

            int total = 0;
            var reachable = new bool[sizes.Count + 1, capacityA + 1];
            reachable[0, 0] = true;
            for (int i = 0; i < sizes.Count; i++)
            {
                int size = sizes[i];
                if (size <= 0 || size > System.Math.Max(capacityA, capacityB) ||
                    size > capacityA + capacityB - total)
                {
                    return false;
                }

                total += size;
                for (int count = 0; count <= capacityA; count++)
                {
                    reachable[i + 1, count] = reachable[i, count] ||
                        (count >= size && reachable[i, count - size]);
                }
            }

            for (int count = System.Math.Min(capacityA, total); count >= 0; count--)
            {
                if (!reachable[sizes.Count, count] || total - count > capacityB)
                {
                    continue;
                }

                teamA = new bool[sizes.Count];
                for (int i = sizes.Count - 1; i >= 0; i--)
                {
                    if (!reachable[i, count])
                    {
                        teamA[i] = true;
                        count -= sizes[i];
                    }
                }

                return true;
            }

            return false;
        }
    }
}
