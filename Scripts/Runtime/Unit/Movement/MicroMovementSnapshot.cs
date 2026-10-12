using System;
using UnityEngine;

namespace PranaTactics.Unit.Movement
{
    /// <summary>
    /// ホストが確定した移動状態を同期します。
    /// </summary>
    [Serializable]
    public sealed class MicroMovementSnapshot
    {
        /// <summary>
        /// 対象ターンです。
        /// </summary>
        public int Turn;
        /// <summary>
        /// フェイズ終了時の最終確定状態かどうかです。
        /// </summary>
        public bool IsFinal;
        /// <summary>
        /// ホスト送信順です。
        /// </summary>
        public int Sequence;
        /// <summary>
        /// 確定した現在マスです。
        /// </summary>
        public Vector2Int Coordinate;
        /// <summary>
        /// 進行中の目標マスです。
        /// </summary>
        public Vector2Int Destination;
        /// <summary>
        /// 表示位置です。
        /// </summary>
        public Vector3 Position;
        /// <summary>
        /// 移動または帰還中かどうかです。
        /// </summary>
        public bool IsMoving;
        /// <summary>
        /// 移動のために消費した累積行動力です。
        /// </summary>
        public int MovementCost;
        /// <summary>
        /// 到着が確定した累積歩数です。
        /// </summary>
        public int CompletedSteps;
        /// <summary>
        /// ホスト交代時にも継続できる出発位置です。
        /// </summary>
        public Vector3 StartPosition;
        /// <summary>
        /// ホスト交代時にも継続できる目標位置です。
        /// </summary>
        public Vector3 TargetPosition;
        /// <summary>
        /// 1マス移動の進捗です。
        /// </summary>
        public float Progress;
        /// <summary>
        /// 水平、ジャンプなどの移動種別です。
        /// </summary>
        public int MovementKind;
        /// <summary>
        /// 待機再開時の行動力支払い済み状態です。
        /// </summary>
        public bool HasPaidDestination;
        /// <summary>
        /// 行動力支払い済みの目的地です。
        /// </summary>
        public Vector2Int PaidDestination;
        /// <summary>
        /// 攻撃による一時的なワールド移動中かどうかです。
        /// </summary>
        public bool IsDirectMoving;
        /// <summary>
        /// 一時的なワールド移動の目標位置です。
        /// </summary>
        public Vector3 DirectTargetPosition;
    }
}
