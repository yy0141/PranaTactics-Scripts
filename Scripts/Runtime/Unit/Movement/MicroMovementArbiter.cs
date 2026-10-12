using System;
using System.Collections.Generic;

namespace PranaTactics.Unit.Movement
{
    /// <summary>
    /// 同一時刻の盤面占有と実際の到着順に基づいて移動を確定します。
    /// </summary>
    /// <remarks>
    /// 各更新で現在速度から残り時間を取り直します。
    /// 出発マスは到着するまで占有し、同着時は裁定側でランダムに選びます。
    /// </remarks>
    public sealed class MicroMovementArbiter
    {
        /// <summary>
        /// 同着と見なす秒数です。
        /// </summary>
        private const float k_TimeTolerance = 0.000001f;

        /// <summary>
        /// 更新中のマス占有数です。
        /// </summary>
        private readonly Dictionary<long, int> _occupancy = new();

        /// <summary>
        /// 列挙順に依存しない処理順です。
        /// </summary>
        private readonly List<IMicroMovementParticipant> _participants = new();

        /// <summary>
        /// 同着抽選に使用する乱数です。
        /// </summary>
        private readonly Random _random;

        /// <summary>
        /// 必要に応じて検証用の乱数シードを指定します。
        /// </summary>
        public MicroMovementArbiter(int? randomSeed = null)
        {
            _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        }

        /// <summary>
        /// 全Unitを共通の時間軸で進め、フレーム内の到着も時刻順に確定します。
        /// </summary>
        public void Tick(IEnumerable<IMicroMovementParticipant> participants, float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0.0f)
            {
                return;
            }

            _participants.Clear();
            _occupancy.Clear();
            foreach (IMicroMovementParticipant participant in participants)
            {
                if (participant != null && participant.IsMovementAlive)
                {
                    _participants.Add(participant);
                    AddOccupant(participant.OccupiedCell);
                }
            }

            ShuffleParticipants();
            float remaining = seconds;
            for (int iteration = 0; iteration <= _participants.Count && remaining > 0.0f; iteration++)
            {
                float interval = remaining;
                foreach (IMicroMovementParticipant participant in _participants)
                {
                    if (!participant.IsAdvancing)
                    {
                        continue;
                    }

                    if (IsOccupiedByAnother(participant))
                    {
                        participant.BlockMovement();
                        continue;
                    }

                    interval = Math.Min(interval, Math.Max(0.0f, participant.SecondsToArrival));
                }

                foreach (IMicroMovementParticipant participant in _participants)
                {
                    participant.AdvanceMovement(interval);
                }

                bool resolved = false;
                foreach (IMicroMovementParticipant participant in _participants)
                {
                    if (!participant.IsAdvancing || participant.SecondsToArrival > k_TimeTolerance)
                    {
                        continue;
                    }

                    resolved = true;
                    if (IsOccupiedByAnother(participant))
                    {
                        participant.BlockMovement();
                        continue;
                    }

                    RemoveOccupant(participant.OccupiedCell);
                    AddOccupant(participant.DestinationCell);
                    participant.CompleteMovement();
                }

                remaining = Math.Max(0.0f, remaining - interval);
                if (!resolved && interval <= 0.0f)
                {
                    break;
                }
            }

            foreach (IMicroMovementParticipant participant in _participants)
            {
                if (participant.IsAdvancing && IsOccupiedByAnother(participant))
                {
                    participant.BlockMovement();
                }
            }
        }

        /// <summary>
        /// 同着時の処理順を偏りなく抽選します。
        /// </summary>
        private void ShuffleParticipants()
        {
            for (int index = _participants.Count - 1; index > 0; index--)
            {
                int other = _random.Next(index + 1);
                IMicroMovementParticipant participant = _participants[index];
                _participants[index] = _participants[other];
                _participants[other] = participant;
            }
        }

        /// <summary>
        /// 自身の出発マス以外の占有を判定します。
        /// </summary>
        private bool IsOccupiedByAnother(IMicroMovementParticipant participant)
        {
            return _occupancy.TryGetValue(participant.DestinationCell, out int count) &&
                count > (participant.DestinationCell == participant.OccupiedCell ? 1 : 0);
        }

        /// <summary>
        /// 占有数を追加します。
        /// </summary>
        private void AddOccupant(long cell)
        {
            _occupancy.TryGetValue(cell, out int count);
            _occupancy[cell] = count + 1;
        }

        /// <summary>
        /// 出発マスの占有を解除します。
        /// </summary>
        private void RemoveOccupant(long cell)
        {
            if (_occupancy[cell] <= 1)
            {
                _occupancy.Remove(cell);
            }
            else
            {
                _occupancy[cell]--;
            }
        }
    }
}
