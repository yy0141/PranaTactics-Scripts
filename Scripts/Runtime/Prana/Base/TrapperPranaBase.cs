using UnityEngine;

using PranaTactics.Board;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.Prana
{
    /// <summary>
    /// ボードが保持する罠の効果と演出の拡張点を提供します。自動消滅や物理接触検知は行いません。
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class TrapperPranaBase : PranaBase
    {
        /// <summary>
        /// 設置時に固定された罠の情報です。
        /// </summary>
        private BoardTrapMessage _placement;

        /// <summary>
        /// 効果を適用済みであるかどうかです。
        /// </summary>
        private bool _hasActivated;

        /// <summary>
        /// 所有者が消滅した後も参照できる兵装設定です。派生データで固有の性能を追加できます。
        /// </summary>
        private TrapperPranamData _trapData;

        public string TrapId => _placement?.TrapId;
        public Vector2Int PlacementCoordinate => _placement != null ? new Vector2Int(_placement.X, _placement.Y) : default;
        protected int OwnerTeamId => _placement != null ? _placement.OwnerTeamId : -1;
        protected internal TrapperPranamData TrapData => _trapData;
        protected virtual bool RequiresLivingEnemyTarget => true;

        /// <summary>
        /// 設置セルの床面を生成位置として保持し、設置者が撃破されても罠を維持します。
        /// </summary>
        internal void InitializePlacement(BoardTrapMessage placement, TrapperPranamData data)
        {
            _placement = placement;
            _trapData = data;
            InitializeIndependentPrana(new PranamContext
            {
                PlayerId = placement.OwnerId,
                Id = -1,
                IsMain = placement.IsMain,
                PranamNumber = placement.Slot,
                GeneratePosition = transform.position,
                SoundProfileId = placement.SoundProfileId
            });
        }

        /// <summary>
        /// 裁定側が確定した発動について、各クライアントで演出とローカル状態を一度適用します。
        /// </summary>
        internal void Activate(UnitBehavior target)
        {
            if (!IsInitialized || _hasActivated)
            {
                return;
            }

            _hasActivated = true;
            try
            {
                OnTrapActivated(target);
            }
            finally
            {
                if (!RequiresLivingEnemyTarget ||
                    (target != null && target.GetUnitStatus() != null &&
                    target.GetUnitStatus().IsAlive && target.GetTeamId() != OwnerTeamId))
                {
                    ApplyTrapEffect(target);
                }
            }
        }

        /// <summary>
        /// 全クライアントで発動時の表示を再生します。対象が撃破済みの場合はnullになることがあります。
        /// </summary>
        protected virtual void OnTrapActivated(UnitBehavior target)
        {
        }

        /// <summary>
        /// 確定した罠の効果を各クライアントで一度適用します。
        /// </summary>
        /// <remarks>
        /// ボードが発動を同期するため、ApplyLockMovementなど受信適用用のAPIを使用し、
        /// このメソッドから同じ効果をネットワークへ再要求しません。
        /// </remarks>
        protected abstract void ApplyTrapEffect(UnitBehavior target);
    }
}
