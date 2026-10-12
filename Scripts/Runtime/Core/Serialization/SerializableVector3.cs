using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Core
{
    [Serializable]
    public struct SerializableVector3
    {
        /// <summary>
        /// ベクトルのX成分です。
        /// </summary>
        public float X;

        /// <summary>
        /// ベクトルのY成分です。
        /// </summary>
        public float Y;

        /// <summary>
        /// ベクトルのZ成分です。
        /// </summary>
        public float Z;

        public SerializableVector3(
    float x,
    float y,
    float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public SerializableVector3(Vector3 vector)
        {
            X = vector.x;
            Y = vector.y;
            Z = vector.z;
        }

        /// <summary>
        /// この値をUnityの<see cref="Vector3"/>へ変換します。
        /// </summary>
        /// <returns>
        /// 各軸の値を保持する<see cref="Vector3"/>です。
        /// </returns>
        public Vector3 ToVector3()
        {
            return new Vector3(
                X,
                Y,
                Z);
        }

        public static implicit operator Vector3(
    SerializableVector3 value)
        {
            return value.ToVector3();
        }

        public static implicit operator SerializableVector3(
    Vector3 value)
        {
            return new SerializableVector3(value);
        }
    }
}
