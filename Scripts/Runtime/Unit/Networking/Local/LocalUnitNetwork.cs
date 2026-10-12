using UnityEngine;

using PranaTactics.Unit;

namespace PranaTactics.Unit.Networking.Local
{
    /// <summary>
    /// ローカルゲームにおけるユニット操作を、
    /// 同一クライアント上の<see cref="UnitBehavior"/>へ適用します。
    /// </summary>
    /// <remarks>
    /// PhotonやEOSのような通信処理は行わず、
    /// <see cref="IUnitNetwork"/>から受け取った命令を即座に実行します。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitBehavior))]
    public sealed class LocalUnitNetwork :
        MonoBehaviour,
        IUnitNetwork
    {
        /// <summary>
        /// ネットワーク命令の適用対象となるユニットです。
        /// </summary>
        private UnitBehavior _unitBehavior;

        private UnitBehavior UnitBehavior
        {
            get
            {
                if (_unitBehavior == null)
                {
                    _unitBehavior =
                        GetComponent<UnitBehavior>();
                }

                if (_unitBehavior == null)
                {
                    throw new MissingComponentException(
                        $"{nameof(LocalUnitNetwork)}には、" +
                        $"同じGameObject上の{nameof(UnitBehavior)}が必要です。");
                }

                return _unitBehavior;
            }
        }

        /// <summary>
        /// 経路編集結果をローカルのMacro Unitへ適用します。
        /// </summary>
        public void SetMacroPath(string snapshot)
        {
            UnitBehavior.GetMacroUnitBehavior()?.ApplyPathSnapshot(snapshot);
        }

        /// <summary>
        /// 必要なコンポーネントを取得します。
        /// </summary>
        private void Awake()
        {
            _unitBehavior =
                GetComponent<UnitBehavior>();

            if (_unitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(LocalUnitNetwork)}: " +
                    $"{nameof(UnitBehavior)}が見つかりません。",
                    this);
            }
        }

        /// <summary>
        /// ユニットの初期化情報を適用します。
        /// </summary>
        /// <param name="data">
        /// 所有者、チーム、キャラクター、初期座標および
        /// 回転を含む初期化情報です。
        /// </param>
        public void Instantiate(
            UnitInitializeData data)
        {
            UnitBehavior.ApplyInstantiate(
                data);
        }

        /// <summary>
        /// ユニットを撃破状態へ変更します。
        /// </summary>
        public void SetDead()
        {
            UnitBehavior.ApplySetDead();
        }

        /// <summary>
        /// 指定されたチームへスコアを加算します。
        /// </summary>
        /// <param name="teamId">
        /// スコアを加算するチームIDです。
        /// </param>
        public void AddTeamScore(
            int teamId)
        {
            UnitBehavior.ApplyAddTeamScore(
                teamId);
        }

        /// <summary>
        /// ユニットの移動経路へ座標を追加します。
        /// </summary>
        /// <param name="x">
        /// 追加するボード座標のX値です。
        /// </param>
        /// <param name="y">
        /// 追加するボード座標のY値です。
        /// </param>
        public void AddMigrationCoordinate(
            int x,
            int y)
        {
            UnitBehavior.ApplyAddMigrationCoordinate(
                x,
                y);
        }

        /// <summary>
        /// ユニットの基本移動速度を設定します。
        /// </summary>
        /// <param name="value">
        /// 設定する移動速度です。
        /// </param>
        public void SetMoveSpeed(
            float value)
        {
            UnitBehavior.ApplySetMoveSpeed(
                value);
        }

        /// <summary>
        /// 指定時間だけユニットの移動を制限します。
        /// </summary>
        /// <param name="duration">
        /// 移動を制限する秒数です。
        /// </param>
        public void LockMovement(
            float duration)
        {
            UnitBehavior.ApplyLockMovement(
                duration);
        }

        /// <summary>
        /// 指定された座標へのテレポート移動を
        /// ローカルユニットへ直接適用します。
        /// </summary>
        /// <param name="x">
        /// 移動先のX座標です。
        /// </param>
        /// <param name="y">
        /// 移動先のY座標です。
        /// </param>
        public void TeleportMove(
            int x,
            int y)
        {
            UnitBehavior?.ApplyTeleportMoveToCoordinate(
                x,
                y);
        }

        /// <summary>
        /// ユニットを指定されたボード座標へ補間移動させます。
        /// </summary>
        /// <param name="teamId">
        /// 移動を要求したチームIDです。
        /// </param>
        /// <param name="x">
        /// 移動先のボード座標のX値です。
        /// </param>
        /// <param name="y">
        /// 移動先のボード座標のY値です。
        /// </param>
        public void LerpMove(
            int teamId,
            int x,
            int y)
        {
            UnitBehavior.ApplyLerpMoveToCoordinate(
                teamId,
                x,
                y);
        }

        /// <summary>
        /// ユニットを指定されたワールド座標へ補間移動させます。
        /// </summary>
        /// <param name="position">
        /// 移動先のワールド座標です。
        /// </param>
        public void LerpMove(
            Vector3 position)
        {
            UnitBehavior.ApplyLerpMoveToPosition(
                position);
        }

        /// <summary>
        /// ユニットのレイヤーを変更します。
        /// </summary>
        /// <param name="layerName">
        /// 変更先のUnityレイヤー名です。
        /// </param>
        public void ChangeLayer(
            string layerName)
        {
            UnitBehavior.ApplyChangeLayer(
                layerName);
        }

        /// <summary>
        /// 指定されたチームから見たユニットの
        /// ステルス状態を切り替えます。
        /// </summary>
        /// <param name="teamId">
        /// ステルス表示判定に使用するチームIDです。
        /// </param>
        /// <param name="isStealth">
        /// ステルス状態へ変更する場合は<c>true</c>です。
        /// </param>
        public void SwitchStealth(
            int teamId,
            bool isStealth)
        {
            UnitBehavior.ApplySwitchStealth(
                teamId,
                isStealth);
        }

        /// <summary>
        /// 直接視認した敵のマップ公開をローカルで適用します。
        /// </summary>
        public void ReportStealthDetection(int targetPlayerId)
        {
            UnitBehavior.ApplyStealthDetection(targetPlayerId);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorでコンポーネントを追加したときに、
        /// 対象となるユニットを自動取得します。
        /// </summary>
        private void Reset()
        {
            _unitBehavior =
                GetComponent<UnitBehavior>();
        }
#endif
    }
}
