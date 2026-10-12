using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// キャラクタープレビュー用モデルの生成位置を保存し、
    /// 指定されたスロットへキャラクターモデルを生成します。
    /// </summary>
    /// <remarks>
    /// このコンポーネントの直下に配置されたオブジェクトを
    /// プレビュー位置のマーカーとして使用します。
    ///
    /// 初期化時に各マーカーの位置、回転およびスケールを保存し、
    /// マーカーオブジェクトを削除します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterModelPreview : MonoBehaviour
    {
        /// <summary>
        /// 生成したキャラクターモデルを格納する親オブジェクトです。
        /// </summary>
        [FormerlySerializedAs("_models")]
        [SerializeField]
        private GameObject _modelContainer;

        /// <summary>
        /// キャラクターモデルの生成情報一覧です。
        /// </summary>
        private readonly List<PreviewTransformData>
            _previewTransforms = new();

        /// <summary>
        /// 各スロットへ生成されたキャラクターモデルです。
        /// </summary>
        private readonly List<GameObject>
            _generatedCharacters = new();

        /// <summary>
        /// プレビュー位置の初期化が完了しているかどうかを示します。
        /// </summary>
        private bool _isInitialized;

        public int SlotCount
        {
            get
            {
                return _previewTransforms.Count;
            }
        }

        /// <summary>
        /// プレビュー位置を保存し、
        /// 位置決め用のマーカーを削除します。
        /// </summary>
        private void Awake()
        {
            InitializePreviewSlots();
        }

        /// <summary>
        /// 指定されたプレビュースロットへ
        /// キャラクターモデルを生成します。
        /// </summary>
        /// <param name="characterPrefab">
        /// 生成するキャラクターモデルのPrefabです。
        /// </param>
        /// <param name="index">
        /// 生成先となるプレビュースロットのインデックスです。
        /// </param>
        /// <returns>
        /// 生成されたキャラクターモデルです。
        /// 生成できなかった場合は<c>null</c>です。
        /// </returns>
        public GameObject GenerateCharacter(
            GameObject characterPrefab,
            int index)
        {
            return CreateCharacter(
                characterPrefab,
                index);
        }

        /// <summary>
        /// 指定されたプレビュースロットへ
        /// キャラクターモデルを生成します。
        /// </summary>
        /// <param name="characterPrefab">
        /// 生成するキャラクターモデルのPrefabです。
        /// </param>
        /// <param name="index">
        /// 生成先となるプレビュースロットのインデックスです。
        /// </param>
        /// <returns>
        /// 生成されたキャラクターモデルです。
        /// 生成できなかった場合は<c>null</c>です。
        /// </returns>
        public GameObject CreateCharacter(
            GameObject characterPrefab,
            int index)
        {
            if (characterPrefab == null)
            {
                Debug.LogError(
                    $"{nameof(CharacterModelPreview)}: " +
                    "生成するキャラクターPrefabがnullです。",
                    this);

                return null;
            }

            if (!TryGetPreviewTransform(
                    index,
                    out PreviewTransformData previewTransform))
            {
                return null;
            }

            Transform containerTransform =
                GetModelContainerTransform();

            RemoveCharacter(
                index);

            GameObject character =
                Instantiate(
                    characterPrefab,
                    previewTransform.Position,
                    previewTransform.Rotation,
                    containerTransform);

            character.transform.localScale =
                CalculateLocalScale(
                    containerTransform,
                    previewTransform.WorldScale);

            _generatedCharacters[index] =
                character;

            return character;
        }

        /// <summary>
        /// 指定されたスロットに生成されている
        /// キャラクターモデルを取得します。
        /// </summary>
        /// <param name="index">
        /// 取得するプレビュースロットのインデックスです。
        /// </param>
        /// <param name="character">
        /// 取得されたキャラクターモデルです。
        /// </param>
        /// <returns>
        /// 有効なキャラクターモデルを取得できた場合は
        /// <c>true</c>です。
        /// </returns>
        public bool TryGetCharacter(
            int index,
            out GameObject character)
        {
            character =
                null;

            if (index < 0 ||
                index >= _generatedCharacters.Count)
            {
                return false;
            }

            character =
                _generatedCharacters[index];

            return character != null;
        }

        /// <summary>
        /// 指定されたスロットに生成されている
        /// キャラクターモデルを削除します。
        /// </summary>
        /// <param name="index">
        /// 削除するプレビュースロットのインデックスです。
        /// </param>
        public void RemoveCharacter(
            int index)
        {
            if (index < 0 ||
                index >= _generatedCharacters.Count)
            {
                return;
            }

            GameObject generatedCharacter =
                _generatedCharacters[index];

            if (generatedCharacter != null)
            {
                Destroy(
                    generatedCharacter);
            }

            _generatedCharacters[index] =
                null;
        }

        /// <summary>
        /// 生成されているすべてのキャラクターモデルを削除します。
        /// </summary>
        public void ClearCharacters()
        {
            for (int index = 0;
                index < _generatedCharacters.Count;
                index++)
            {
                RemoveCharacter(
                    index);
            }
        }

        /// <summary>
        /// 子オブジェクトからプレビュー位置を収集します。
        /// </summary>
        private void InitializePreviewSlots()
        {
            if (_isInitialized)
            {
                return;
            }

            _previewTransforms.Clear();
            _generatedCharacters.Clear();

            Transform modelContainerTransform =
                _modelContainer != null
                    ? _modelContainer.transform
                    : null;

            List<GameObject> markerObjects =
                new();

            foreach (Transform childTransform in
                    transform)
            {
                if (childTransform == null)
                {
                    continue;
                }

                if (childTransform ==
                    modelContainerTransform)
                {
                    continue;
                }

                _previewTransforms.Add(
                    new PreviewTransformData(
                        childTransform.position,
                        childTransform.rotation,
                        childTransform.lossyScale));

                _generatedCharacters.Add(
                    null);

                markerObjects.Add(
                    childTransform.gameObject);
            }

            foreach (GameObject markerObject in
                    markerObjects)
            {
                if (markerObject != null)
                {
                    Destroy(
                        markerObject);
                }
            }

            _isInitialized =
                true;

            if (_previewTransforms.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterModelPreview)}: " +
                    "キャラクタープレビュー用の位置マーカーがありません。",
                    this);
            }
        }

        /// <summary>
        /// 指定されたインデックスの生成情報を取得します。
        /// </summary>
        /// <param name="index">
        /// 取得するプレビュースロットのインデックスです。
        /// </param>
        /// <param name="previewTransform">
        /// 取得された生成情報です。
        /// </param>
        /// <returns>
        /// 有効な生成情報を取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetPreviewTransform(
            int index,
            out PreviewTransformData previewTransform)
        {
            previewTransform =
                default;

            if (!_isInitialized)
            {
                InitializePreviewSlots();
            }

            if (index < 0 ||
                index >= _previewTransforms.Count)
            {
                Debug.LogError(
                    $"{nameof(CharacterModelPreview)}: " +
                    "プレビュースロットのインデックスが範囲外です。" +
                    $" Index={index}, Count={_previewTransforms.Count}",
                    this);

                return false;
            }

            previewTransform =
                _previewTransforms[index];

            return true;
        }

        /// <summary>
        /// 生成したモデルを格納するTransformを取得します。
        /// </summary>
        /// <returns>
        /// モデル格納先として使用するTransformです。
        /// </returns>
        private Transform GetModelContainerTransform()
        {
            if (_modelContainer != null)
            {
                return _modelContainer.transform;
            }

            return transform;
        }

        /// <summary>
        /// 目標となるワールドスケールから、
        /// 指定された親の下で使用するローカルスケールを計算します。
        /// </summary>
        /// <param name="parent">
        /// 生成モデルの親Transformです。
        /// </param>
        /// <param name="worldScale">
        /// 適用するワールドスケールです。
        /// </param>
        /// <returns>
        /// 親のスケールを考慮したローカルスケールです。
        /// </returns>
        private static Vector3 CalculateLocalScale(
            Transform parent,
            Vector3 worldScale)
        {
            if (parent == null)
            {
                return worldScale;
            }

            Vector3 parentScale =
                parent.lossyScale;

            return new Vector3(
                SafeDivide(
                    worldScale.x,
                    parentScale.x),
                SafeDivide(
                    worldScale.y,
                    parentScale.y),
                SafeDivide(
                    worldScale.z,
                    parentScale.z));
        }

        /// <summary>
        /// 0除算を防止しながら除算します。
        /// </summary>
        /// <param name="value">
        /// 除算される値です。
        /// </param>
        /// <param name="divisor">
        /// 除数です。
        /// </param>
        /// <returns>
        /// 除算結果です。
        /// 除数が0に近い場合は元の値を返します。
        /// </returns>
        private static float SafeDivide(
            float value,
            float divisor)
        {
            if (Mathf.Approximately(
                    divisor,
                    0.0f))
            {
                return value;
            }

            return value /
                divisor;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定されたモデル格納先を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_modelContainer == null)
            {
                return;
            }

            if (_modelContainer ==
                gameObject)
            {
                return;
            }

            if (_modelContainer.transform.IsChildOf(
                    transform))
            {
                return;
            }

            Debug.LogWarning(
                $"{nameof(CharacterModelPreview)}: " +
                "モデル格納先がプレビューオブジェクトの階層外にあります。" +
                "位置は維持されますが、階層構造を確認してください。",
                this);
        }

#endif

        private readonly struct PreviewTransformData
        {
            public Vector3 Position
            {
                get;
            }

            public Quaternion Rotation
            {
                get;
            }

            public Vector3 WorldScale
            {
                get;
            }

            public PreviewTransformData(
    Vector3 position,
    Quaternion rotation,
    Vector3 worldScale)
            {
                Position =
                    position;

                Rotation =
                    rotation;

                WorldScale =
                    worldScale;
            }
        }
    }
}
