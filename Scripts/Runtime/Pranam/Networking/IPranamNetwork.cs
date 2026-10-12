using UnityEngine;

using PranaTactics.Audio;

namespace PranaTactics.Pranam.Networking
{
    public interface IPranamNetwork
    {
        /// <summary>
        /// 指定されたPhotonオブジェクトを
        /// プラーナ格納オブジェクトの子へ設定します。
        /// </summary>
        /// <param name="viewId">
        /// 親設定を行うPhoton View IDです。
        /// </param>
        /// <param name="isMountRoot">
        /// <c>true</c>の場合はMountRoot、
        /// <c>false</c>の場合はWorldRootへ設定します。
        /// </param>
        /// <remarks>
        /// Photon以外の通信方式では、この値を使用しない場合があります。
        /// </remarks>
        void SetPranaParent(
            int viewId,
            bool isMountRoot);

        /// <summary>
        /// 指定された設定を使用してプラーナを生成します。
        /// </summary>
        /// <param name="prefabPath">
        /// ResourcesフォルダからのPrefab相対パスです。
        /// </param>
        /// <param name="position">
        /// プラーナを生成するワールド座標です。
        /// </param>
        /// <param name="rotation">
        /// プラーナを生成する回転です。
        /// </param>
        /// <param name="context">
        /// 所有者、攻撃力、対象などの生成情報です。
        /// </param>
        void InstantiatePrana(
            string prefabPath,
            Vector3 position,
            Quaternion rotation,
            PranamContext context);

        /// <summary>
        /// 指定された対象ユニットへダメージを適用します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// 攻撃を行ったプレイヤーのIDです。
        /// </param>
        /// <param name="targetUnitId">
        /// ダメージを受ける対象ユニットの識別子です。
        /// </param>
        /// <param name="damage">
        /// 対象へ適用するダメージ量です。
        /// </param>
        /// <param name="hitSlowdownRatio">
        /// 通常・歩行速度を減らす割合です。0から1で指定します。
        /// </param>
        /// <param name="hitSlowdownDurationSeconds">
        /// 減速が持続するゲーム内秒数です。
        /// </param>
        /// <param name="sourcePranamSlot">
        /// 発射時の兵装スロットです。不明な場合は-1です。
        /// </param>
        void HitUnit(
            int attackerPlayerId,
            int targetUnitId,
            float damage,
            float hitSlowdownRatio = 0.0f,
            float hitSlowdownDurationSeconds = 0.0f,
            int sourcePranamSlot = -1);

        /// <summary>
        /// 指定されたプラーナを破棄します。
        /// </summary>
        /// <param name="pranaId">
        /// 破棄するプラーナの識別子です。
        /// </param>
        void DestroyPrana(int pranaId);

        /// <summary>
        /// プラーナムに対応する効果音を再生します。
        /// </summary>
        /// <param name="soundProfileId">
        /// 使用するサウンドプロファイルの識別子です。
        /// </param>
        /// <param name="soundEvent">
        /// 再生するプラーナムサウンドイベントです。
        /// </param>
        /// <param name="position">
        /// 効果音を再生するワールド座標です。
        /// </param>
        /// <param name="forward">
        /// 3Dサウンドの向きとして使用する前方ベクトルです。
        /// </param>
        void PlayPranamSound(
            string soundProfileId,
            PranamSoundEvent soundEvent,
            Vector3 position,
            Vector3 forward);
    }
}
