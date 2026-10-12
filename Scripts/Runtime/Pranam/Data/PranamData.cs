using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Audio;

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
#endif

namespace PranaTactics.Pranam
{
    /// <summary>
    /// プラーナムの表示情報、消費リソース、Prefabおよび
    /// サウンド設定を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "PranamData",
    menuName = "Prana Tactics/Pranam/Pranam Data",
    order = 0)]
    public class PranamData : ScriptableObject
    {
#if UNITY_EDITOR

        /// <summary>
        /// UnityのResourcesフォルダを表すパス文字列です。
        /// </summary>
        private const string k_ResourcesDirectoryName = "Resources/";

        /// <summary>
        /// Prefabファイルの拡張子です。
        /// </summary>
        private const string k_PrefabExtension = ".prefab";

#endif

        /// <summary>
        /// プラーナムの射程分類を表す文字列です。
        /// </summary>
        [HideInInspector]
        [SerializeField]
        private string _range = "None";

        /// <summary>
        /// 使用時に生成するプラーナムのPrefabです。
        /// </summary>
        [SerializeField]
        private GameObject _pranamPrefab;

        /// <summary>
        /// Resources.Loadで使用するPrefabの相対パスです。
        /// </summary>
        [HideInInspector]
        [SerializeField]
        private string _prefabResourcePath = string.Empty;

        /// <summary>
        /// UIへ表示するプラーナム名です。
        /// </summary>
        [SerializeField]
        private string _displayName = string.Empty;

        /// <summary>
        /// AiDefinitionCatalog内の武器定義IDです。
        /// </summary>
        [WeaponAiDefinitionId]
        [SerializeField]
        private string _weaponAiDefinitionId = string.Empty;

        /// <summary>
        /// UIへ表示するプラーナムのアイコンです。
        /// </summary>
        [SerializeField]
        private Sprite _icon;

        /// <summary>
        /// 兵装プロフィールに表示する英字名です。
        /// </summary>
        [Header("Profile")]
        [SerializeField]
        private string _profileEnglishName = string.Empty;

        /// <summary>
        /// 兵装の分類と役割を短く説明します。
        /// </summary>
        [TextArea(2, 4)]
        [SerializeField]
        private string _profileSummary = string.Empty;

        /// <summary>
        /// プロフィールの拡大画像です。未設定の場合はアイコンを使用します。
        /// </summary>
        [SerializeField]
        private Sprite _profileSprite;

        public string ProfileEnglishName => _profileEnglishName;

        public string ProfileSummary => _profileSummary;

        public Sprite ProfileSprite => _profileSprite != null ? _profileSprite : _icon;

        /// <summary>
        /// プラーナム使用時のサウンド設定です。
        /// </summary>
        [Header("Sound")]
        [SerializeField]
        private PranamSoundProfile _soundProfile;

        /// <summary>
        /// UIへ表示するプラーナムの説明文です。
        /// </summary>
        [Header("Gameplay")]
        [TextArea]
        [SerializeField]
        private string _description = string.Empty;

        /// <summary>
        /// プラーナムの使用時に消費する行動力です。
        /// </summary>
        [SerializeField]
        private int _actionPointCost;

        /// <summary>
        /// プラーナムの使用時に消費するプラーナ量です。
        /// </summary>
        [SerializeField]
        private int _pranaCost;

        protected virtual string RuntimeBehaviorName => null;

        public string Range
        {
            get
            {
                return RuntimeBehaviorName ?? _range;
            }
        }

        public GameObject PranamPrefab
        {
            get
            {
                return _pranamPrefab;
            }
        }

        public string PrefabResourcePath
        {
            get
            {
                return _prefabResourcePath;
            }
        }

        public string DisplayName
        {
            get
            {
                return _displayName;
            }
        }

        public string WeaponAiDefinitionId
        {
            get
            {
                return _weaponAiDefinitionId;
            }
        }

        public Sprite Icon
        {
            get
            {
                return _icon;
            }
        }

        public PranamSoundProfile SoundProfile
        {
            get
            {
                return _soundProfile;
            }
        }

        public string Description
        {
            get
            {
                return _description;
            }
        }

        public int ActionPointCost
        {
            get
            {
                return _actionPointCost;
            }
        }

        public int PranaCost
        {
            get
            {
                return _pranaCost;
            }
        }

#if UNITY_EDITOR

        /// <summary>
        /// Inspector上でPrefabが変更されたとき、
        /// Resources.Loadで使用する相対パスを更新します。
        /// </summary>
        private void OnValidate()
        {
            UpdatePrefabResourcePath();
        }

        /// <summary>
        /// 設定されているPrefabからResources相対パスを生成します。
        /// </summary>
        private void UpdatePrefabResourcePath()
        {
            if (_pranamPrefab == null)
            {
                _prefabResourcePath = string.Empty;

                return;
            }

            string assetPath =
                AssetDatabase.GetAssetPath(_pranamPrefab);

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                _prefabResourcePath = string.Empty;

                Debug.LogWarning(
                    $"{nameof(PranamData)}: " +
                    $"{_pranamPrefab.name}のアセットパスを取得できません。" +
                    "Project上のPrefabを指定してください。",
                    this);

                return;
            }

            if (!string.Equals(
                    Path.GetExtension(assetPath),
                    k_PrefabExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                _prefabResourcePath = string.Empty;

                Debug.LogWarning(
                    $"{nameof(PranamData)}: " +
                    $"{_pranamPrefab.name}はPrefabアセットではありません。" +
                    $"Path={assetPath}",
                    this);

                return;
            }

            int resourcesDirectoryIndex =
                assetPath.LastIndexOf(
                    k_ResourcesDirectoryName,
                    StringComparison.Ordinal);

            if (resourcesDirectoryIndex < 0)
            {
                _prefabResourcePath = string.Empty;

                Debug.LogWarning(
                    $"{nameof(PranamData)}: " +
                    $"{_pranamPrefab.name}はResourcesフォルダ内にありません。" +
                    $"Path={assetPath}",
                    this);

                return;
            }

            int relativePathStartIndex =
                resourcesDirectoryIndex +
                k_ResourcesDirectoryName.Length;

            string relativePath =
                assetPath.Substring(relativePathStartIndex);

            _prefabResourcePath =
                Path.ChangeExtension(
                    relativePath,
                    null);
        }

#endif
    }
}
