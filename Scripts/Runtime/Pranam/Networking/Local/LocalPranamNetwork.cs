using UnityEngine;

using PranaTactics.Audio;

namespace PranaTactics.Pranam.Networking.Local
{
    /// <summary>
    /// ネットワーク通信を行わず、プラーナの生成、命中、
    /// 破棄および効果音再生をローカル環境へ直接適用します。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PranamBehavior))]
    public sealed class LocalPranamNetwork :
    MonoBehaviour,
    IPranamNetwork
    {
        /// <summary>
        /// プラーナの生成や命中処理を適用する
        /// プラーナム管理コンポーネントです。
        /// </summary>
        private PranamBehavior _pranamBehavior;

        /// <summary>
        /// 必要なコンポーネントへの参照を取得します。
        /// </summary>
        private void Awake()
        {
            CacheReferences();
        }

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
        /// ローカルモードでは通常Photon View IDを使用しませんが、
        /// 共通インターフェースとの互換性のため処理を転送します。
        /// </remarks>
        public void SetPranaParent(
            int viewId,
            bool isMountRoot)
        {
            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplySetPranaParent(
                viewId,
                isMountRoot);
        }

        /// <summary>
        /// 指定された情報を使用してプラーナをローカルに生成します。
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
        /// プラーナの所有者、性能および対象情報です。
        /// </param>
        public void InstantiatePrana(
            string prefabPath,
            Vector3 position,
            Quaternion rotation,
            PranamContext context)
        {
            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    prefabPath))
            {
                Debug.LogError(
                    $"{nameof(LocalPranamNetwork)}: " +
                    "プラーナのPrefabパスが設定されていません。",
                    this);

                return;
            }

            if (context == null)
            {
                Debug.LogError(
                    $"{nameof(LocalPranamNetwork)}: " +
                    $"{nameof(PranamContext)}が設定されていません。",
                    this);

                return;
            }

            pranamBehavior.ApplyInstantiate(
                prefabPath,
                position,
                rotation,
                context);
        }

        /// <summary>
        /// 指定された対象ユニットへダメージをローカルに適用します。
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
        public void HitUnit(
            int attackerPlayerId,
            int targetUnitId,
            float damage,
            float hitSlowdownRatio = 0.0f,
            float hitSlowdownDurationSeconds = 0.0f,
            int sourcePranamSlot = -1)
        {
            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplyHit(
                attackerPlayerId,
                targetUnitId,
                Mathf.Max(
                    0.0f,
                    damage),
                hitSlowdownRatio,
                hitSlowdownDurationSeconds,
                sourcePranamSlot);
        }

        /// <summary>
        /// 指定されたプラーナをローカルに破棄します。
        /// </summary>
        /// <param name="pranaId">
        /// 破棄するプラーナの識別子です。
        /// </param>
        public void DestroyPrana(int pranaId)
        {
            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            pranamBehavior.ApplyDestroyPrana(
                pranaId);
        }

        /// <summary>
        /// 指定されたプラーナム効果音をローカルに再生します。
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
        public void PlayPranamSound(
            string soundProfileId,
            PranamSoundEvent soundEvent,
            Vector3 position,
            Vector3 forward)
        {
            if (!TryGetPranamBehavior(
                    out PranamBehavior pranamBehavior))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(
                    soundProfileId))
            {
                return;
            }

            pranamBehavior.ApplyPranamSound(
                soundProfileId,
                soundEvent,
                position,
                forward);
        }

        /// <summary>
        /// 必要なコンポーネントへの参照を取得します。
        /// </summary>
        private void CacheReferences()
        {
            _pranamBehavior =
                GetComponent<PranamBehavior>();

            if (_pranamBehavior != null)
            {
                return;
            }

            Debug.LogError(
                $"{nameof(LocalPranamNetwork)}: " +
                $"{nameof(PranamBehavior)}を取得できません。",
                this);
        }

        /// <summary>
        /// 有効な<see cref="PranamBehavior"/>の取得を試みます。
        /// </summary>
        /// <param name="pranamBehavior">
        /// 取得されたプラーナム管理コンポーネントです。
        /// </param>
        /// <returns>
        /// 有効なコンポーネントを取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetPranamBehavior(
            out PranamBehavior pranamBehavior)
        {
            if (_pranamBehavior == null)
            {
                CacheReferences();
            }

            pranamBehavior =
                _pranamBehavior;

            return pranamBehavior != null;
        }
    }
}
