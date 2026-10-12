using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

using PranaTactics.Core;
using PranaTactics.Unit;

namespace PranaTactics.UI
{
    /// <summary>
    /// ユニット画像をUIの画像スロットへ設定します。
    /// </summary>
    /// <remarks>
    /// <see cref="GameDataRepository"/>から
    /// プレイヤーが使用可能なキャラクターデータを取得し，
    /// 登録されている画像スロットへ順番に反映します。
    ///
    /// キャラクター数より多い画像スロットは非表示にします。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SetImage : MonoBehaviour
    {
        /// <summary>
        /// ユニット画像を表示するUI画像の一覧です。
        /// </summary>
        [Header("Unit Images")]
        [SerializeField]
        private List<Image> _images = new();

        /// <summary>
        /// コンポーネントの開始時に，
        /// ユニット画像を自動設定するかどうかを示します。
        /// </summary>
        [FormerlySerializedAs("_isInit")]
        [SerializeField]
        private bool _initializeOnStart;

        /// <summary>
        /// 初期設定処理が完了しているかどうかを示します。
        /// </summary>
        private bool _isInitialized;

        public bool IsInitialized
        {
            get
            {
                return _isInitialized;
            }
        }

        /// <summary>
        /// 必要に応じてデータリポジトリの生成を待ち，
        /// ユニット画像を設定します。
        /// </summary>
        private IEnumerator Start()
        {
            if (!_initializeOnStart)
            {
                yield break;
            }

            yield return InitializeAsync();
        }

        /// <summary>
        /// <see cref="GameDataRepository"/>の生成と
        /// データ読み込み完了を待ち，ユニット画像を設定します。
        /// </summary>
        /// <returns>
        /// 初期化処理を表すコルーチンです。
        /// </returns>
        public IEnumerator InitializeAsync()
        {
            while (GameDataRepository.Instance == null)
            {
                yield return null;
            }

            while (!GameDataRepository.Instance.IsLoaded)
            {
                yield return null;
            }

            SetUnitImages();
        }

        /// <summary>
        /// プレイヤーが使用可能なキャラクター情報を取得し，
        /// 画像スロットへ反映します。
        /// </summary>
        public void SetUnitImages()
        {
            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null)
            {
                Debug.LogWarning(
                    $"{nameof(SetImage)}: " +
                    $"{nameof(GameDataRepository)}が存在しないため，" +
                    "ユニット画像を設定できません。",
                    this);

                _isInitialized = false;

                return;
            }

            if (!repository.IsLoaded)
            {
                Debug.LogWarning(
                    $"{nameof(SetImage)}: " +
                    $"{nameof(GameDataRepository)}のデータ読み込みが" +
                    "完了していません。",
                    this);

                _isInitialized = false;

                return;
            }

            List<UnitParams> characters =
                repository.GetPlayerCharactersData();

            if (characters == null)
            {
                Debug.LogWarning(
                    $"{nameof(SetImage)}: " +
                    "キャラクターデータを取得できませんでした。",
                    this);

                HideAllImageSlots();
                _isInitialized = false;

                return;
            }

            for (int index = 0;
                index < _images.Count;
                index++)
            {
                Image targetImage =
                    _images[index];

                if (targetImage == null)
                {
                    Debug.LogWarning(
                        $"{nameof(SetImage)}: " +
                        "画像スロットが設定されていません。" +
                        $" Index={index}",
                        this);

                    continue;
                }

                if (index >= characters.Count ||
                    characters[index] == null)
                {
                    SetImageSlotActive(
                        targetImage,
                        false);

                    continue;
                }

                UnitParams character =
                    characters[index];

                targetImage.sprite =
                    character.GameplaySprite;

                targetImage.gameObject.name =
                    character.UnitName;

                SetImageSlotActive(
                    targetImage,
                    true);
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 指定された画像スロットのSpriteを変更します。
        /// </summary>
        /// <param name="sprite">
        /// 設定するSpriteです。
        /// <c>null</c>を指定した場合は画像を非表示にします。
        /// </param>
        /// <param name="index">
        /// 変更する画像スロットのインデックスです。
        /// </param>
        public void SetSprite(
            Sprite sprite,
            int index)
        {
            if (!TryGetImage(
                    index,
                    out Image targetImage))
            {
                return;
            }

            targetImage.sprite =
                sprite;

            if (sprite == null)
            {
                SetImageSlotActive(
                    targetImage,
                    false);

                Debug.Log(
                    $"{nameof(SetImage)}: Spriteを更新しました。" +
                    $" Index={index}," +
                    " Sprite=null," +
                    " Active=False",
                    targetImage);

                return;
            }

            SetImageSlotActive(
                targetImage,
                true);

            Debug.Log(
                $"{nameof(SetImage)}: Spriteを更新しました。" +
                $" Index={index}," +
                $" Sprite={sprite.name}," +
                $" ActiveSelf={targetImage.gameObject.activeSelf}," +
                $" ActiveInHierarchy={targetImage.gameObject.activeInHierarchy}," +
                $" Enabled={targetImage.enabled}",
                targetImage);
        }

        /// <summary>
        /// 指定された画像スロットを非表示にします。
        /// </summary>
        /// <param name="index">
        /// 非表示にする画像スロットのインデックスです。
        /// </param>
        public void HideImage(
            int index)
        {
            if (!TryGetImage(
                    index,
                    out Image targetImage))
            {
                return;
            }

            SetImageSlotActive(
                targetImage,
                false);
        }

        /// <summary>
        /// 指定されたインデックスの画像を取得します。
        /// </summary>
        /// <param name="index">
        /// 取得する画像スロットのインデックスです。
        /// </param>
        /// <param name="targetImage">
        /// 取得された画像です。
        /// </param>
        /// <returns>
        /// 有効な画像を取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetImage(
            int index,
            out Image targetImage)
        {
            targetImage = null;

            if (index < 0 ||
                index >= _images.Count)
            {
                Debug.LogWarning(
                    $"{nameof(SetImage)}: " +
                    "画像スロットのインデックスが範囲外です。" +
                    $" Index={index}, Count={_images.Count}",
                    this);

                return false;
            }

            targetImage =
                _images[index];

            if (targetImage == null)
            {
                Debug.LogWarning(
                    $"{nameof(SetImage)}: " +
                    "指定された画像スロットがnullです。" +
                    $" Index={index}",
                    this);

                return false;
            }

            return true;
        }

        /// <summary>
        /// すべての画像スロットを非表示にします。
        /// </summary>
        private void HideAllImageSlots()
        {
            foreach (Image targetImage in
                    _images)
            {
                if (targetImage == null)
                {
                    continue;
                }

                SetImageSlotActive(
                    targetImage,
                    false);
            }
        }

        /// <summary>
        /// 画像自身を表示または非表示にします。
        /// </summary>
        /// <param name="targetImage">
        /// 表示状態を変更する画像です。
        /// </param>
        /// <param name="isActive">
        /// 表示する場合は<c>true</c>です。
        /// </param>
        /// <remarks>
        /// Imageが設定されているGameObject自身の
        /// Active状態のみを変更します。
        /// </remarks>
        private static void SetImageSlotActive(
            Image targetImage,
            bool isActive)
        {
            if (targetImage == null)
            {
                return;
            }

            GameObject imageObject =
                targetImage.gameObject;

            if (imageObject.activeSelf ==
                isActive)
            {
                return;
            }

            imageObject.SetActive(
                isActive);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定された画像一覧を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_images == null)
            {
                _images =
                    new List<Image>();
            }
        }
#endif
    }
}
