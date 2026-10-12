using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Animation
{
    /// <summary>
    /// Animatorステートに入ったとき、指定したIntegerパラメーターへ
    /// ランダムな整数を設定します。
    /// </summary>
    public sealed class RandomIntegerParameterSetter : StateMachineBehaviour
    {
        /// <summary>
        /// ランダムな整数を設定するAnimatorパラメーターの名前です。
        /// </summary>
        [SerializeField]
        private string _parameterName = string.Empty;

        /// <summary>
        /// 生成するランダム値の上限です。
        /// この値自体は生成結果に含まれません。
        /// </summary>
        [Min(1)]
        [SerializeField]
        private int _exclusiveMaximum = 1;

        /// <summary>
        /// Animatorステートに入ったとき、指定したIntegerパラメーターへ
        /// 0以上かつ上限未満のランダムな整数を設定します。
        /// </summary>
        /// <param name="animator">
        /// ステートを管理しているAnimatorです。
        /// </param>
        /// <param name="stateInfo">
        /// 遷移先ステートの情報です。
        /// </param>
        /// <param name="layerIndex">
        /// ステートが所属するAnimatorレイヤーのインデックスです。
        /// </param>
        public override void OnStateEnter(
            Animator animator,
            AnimatorStateInfo stateInfo,
            int layerIndex)
        {
            if (string.IsNullOrEmpty(_parameterName))
            {
                return;
            }

            if (_exclusiveMaximum <= 0)
            {
                return;
            }

            int randomValue = Random.Range(0, _exclusiveMaximum);

            animator.SetInteger(_parameterName, randomValue);
        }
    }
}
