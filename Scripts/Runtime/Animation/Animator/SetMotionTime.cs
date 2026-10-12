using UnityEngine;

namespace PranaTactics.Animation
{
    /// <summary>
    /// アニメーションステートの再生終了時にAnimatorを一時停止し、
    /// 指定された時間の経過後に再生を再開します。
    /// </summary>
    public sealed class SetMotionTime : StateMachineBehaviour
    {
        /// <summary>
        /// アニメーションステートの再生終了を示す正規化時間です。
        /// </summary>
        private const float k_StateEndNormalizedTime = 1.0f;

        /// <summary>
        /// Animatorの通常再生速度です。
        /// </summary>
        private const float k_NormalPlaybackSpeed = 1.0f;

        /// <summary>
        /// アニメーションを停止する時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _holdDuration = 1.0f;

        /// <summary>
        /// アニメーションの再開までに残っている時間です。
        /// </summary>
        private float _remainingHoldTime;

        /// <summary>
        /// アニメーションが一時停止中かどうかを示します。
        /// </summary>
        private bool _isHolding;

        /// <summary>
        /// アニメーションステートに入ったとき、一時停止処理の状態を初期化します。
        /// </summary>
        /// <param name="animator">
        /// ステートを管理しているAnimatorです。
        /// </param>
        /// <param name="stateInfo">
        /// 遷移先のアニメーションステート情報です。
        /// </param>
        /// <param name="layerIndex">
        /// ステートが所属するAnimatorレイヤーのインデックスです。
        /// </param>
        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            _remainingHoldTime = _holdDuration;
            _isHolding = false;
        }

        /// <summary>
        /// アニメーションステートの再生終了を検出してAnimatorを一時停止し、
        /// 指定された時間の経過後に再生を再開します。
        /// </summary>
        /// <param name="animator">
        /// ステートを管理しているAnimatorです。
        /// </param>
        /// <param name="stateInfo">
        /// 現在のアニメーションステート情報です。
        /// </param>
        /// <param name="layerIndex">
        /// ステートが所属するAnimatorレイヤーのインデックスです。
        /// </param>
        public override void OnStateUpdate(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            if (stateInfo.normalizedTime >= k_StateEndNormalizedTime &&
                !_isHolding)
            {
                _isHolding = true;
                animator.speed = 0.0f;
            }

            if (!_isHolding)
            {
                return;
            }

            _remainingHoldTime -= Time.deltaTime;

            if (_remainingHoldTime > 0.0f)
            {
                return;
            }

            animator.speed = k_NormalPlaybackSpeed;
            _isHolding = false;
        }

        /// <summary>
        /// アニメーションステートから退出したとき、
        /// Animatorの再生速度を通常の速度へ戻します。
        /// </summary>
        /// <param name="animator">
        /// ステートを管理しているAnimatorです。
        /// </param>
        /// <param name="stateInfo">
        /// 退出するアニメーションステート情報です。
        /// </param>
        /// <param name="layerIndex">
        /// ステートが所属するAnimatorレイヤーのインデックスです。
        /// </param>
        public override void OnStateExit(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            animator.speed = k_NormalPlaybackSpeed;
            _isHolding = false;
        }
    }
}
