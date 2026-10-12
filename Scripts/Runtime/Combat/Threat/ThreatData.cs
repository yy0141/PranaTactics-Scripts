using System;
using UnityEngine;

namespace PranaTactics.Combat
{
    [Serializable]
    public struct ThreatData
    {
        /// <summary>
        /// 攻撃を行ったプレイヤーの識別子です。
        /// </summary>
        public int AttackerPlayerId;

        /// <summary>
        /// 脅威情報を識別するための識別子です。
        /// </summary>
        public int ThreatId;

        /// <summary>
        /// メイン兵装による攻撃かどうかを示します。
        /// </summary>
        public bool IsMainWeapon;

        /// <summary>
        /// 脅威が検出された位置です。
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// 攻撃が進行する方向です。
        /// </summary>
        public Vector3 Direction;

        /// <summary>
        /// 攻撃によって与えられるダメージ量です。
        /// </summary>
        public float Damage;

        /// <summary>
        /// 攻撃オブジェクトの移動速度です。
        /// </summary>
        public float Speed;

        /// <summary>
        /// 攻撃が対象を追尾する誘導攻撃かどうかを示します。
        /// </summary>
        public bool IsHoming;

        /// <summary>
        /// 攻撃方向が対象に合わせて継続的に更新されるかどうかを示します。
        /// </summary>
        public bool IsTracking;

        /// <summary>
        /// 脅威情報に関連付けられた時刻です。
        /// </summary>
        public float Timestamp;
    }
}
