using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// プラーナムによる攻撃後の硬直時間、
    /// 攻撃中の移動状態および対象ユニットを保持します。
    /// </summary>
    [Serializable]
    public sealed class PranamAttackContext
    {
        /// <summary>
        /// 攻撃後に適用する硬直時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _timer;

        /// <summary>
        /// 攻撃中にユニットが移動するかどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _isAttackMoving;

        /// <summary>
        /// 攻撃対象となるユニットまたはプレイヤーのID一覧です。
        /// </summary>
        [SerializeField]
        private List<int> _targetIds = new();

        public float Timer
        {
            get
            {
                return _timer;
            }
            set
            {
                _timer =
                    Mathf.Max(
                        0.0f,
                        value);
            }
        }

        public bool IsAttackMoving
        {
            get
            {
                return _isAttackMoving;
            }
            set
            {
                _isAttackMoving = value;
            }
        }

        public List<int> TargetIds
        {
            get
            {
                return _targetIds;
            }
            set
            {
                _targetIds =
                    value ??
                    new List<int>();
            }
        }

        public bool HasTargets
        {
            get
            {
                return _targetIds != null &&
                    _targetIds.Count > 0;
            }
        }

        /// <summary>
        /// 登録されている全ての攻撃対象IDを削除します。
        /// </summary>
        public void ClearTargets()
        {
            _targetIds?.Clear();
        }

        /// <summary>
        /// 指定されたIDを攻撃対象へ追加します。
        /// </summary>
        /// <param name="targetId">
        /// 追加する対象IDです。
        /// </param>
        /// <returns>
        /// 対象IDを新しく追加した場合は<c>true</c>です。
        /// 既に登録されている場合は<c>false</c>です。
        /// </returns>
        public bool AddTarget(
            int targetId)
        {
            _targetIds ??=
                new List<int>();

            if (_targetIds.Contains(
                    targetId))
            {
                return false;
            }

            _targetIds.Add(
                targetId);

            return true;
        }
    }
}
