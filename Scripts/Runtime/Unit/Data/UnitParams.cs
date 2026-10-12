using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Unit
{
    /// <summary>
    /// キャラクター固有の能力値、Prefab、画像および
    /// Main・SubのPranam構成を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "UnitParams",
    menuName = "Prana Tactics/Data/Unit Parameters",
    order = 1)]
    public sealed class UnitParams : ScriptableObject
    {
        /// <summary>
        /// MainまたはSubに装備できるPranamのスロット数です。
        /// </summary>
        private const int k_PranamSlotCount = 4;

        /// <summary>
        /// Pranamが設定されていないことを表す名称です。
        /// </summary>
        private const string k_NonePranamName = "NONE";

        /// <summary>
        /// キャラクターデータの検索やユニット生成に使用する内部名です。
        /// </summary>
        [Header("Identity")]
        [SerializeField]
        private string _unitName = string.Empty;

        /// <summary>
        /// プロフィール画面に表示する英字名です。
        /// </summary>
        [SerializeField]
        private string _romanizedName = string.Empty;

        /// <summary>
        /// キャラクターの戦闘上の役割を説明します。
        /// </summary>
        [TextArea(2, 4)]
        [SerializeField]
        private string _roleDescription = string.Empty;

        /// <summary>
        /// キャラクターの戦闘タイプです。
        /// </summary>
        [SerializeField]
        private CharacterType _characterType;

        /// <summary>
        /// キャラクターのストーリー説明です。
        /// </summary>
        [TextArea(3, 10)]
        [SerializeField]
        private string _story = string.Empty;

        /// <summary>
        /// プレイヤーがこのキャラクターを使用できるかどうかです。
        /// </summary>
        [SerializeField]
        private bool _canUsePlayer = true;

        /// <summary>
        /// ユニットの初期積極性です。
        /// </summary>
        [Header("Parameters")]
        [Min(0)]
        [SerializeField]
        private int _initialProactiveness;

        /// <summary>
        /// ユニットが保持できるプラーナの最大量です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _pranaCapacity;

        /// <summary>
        /// ユニットの基本移動速度です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _moveSpeed;

        /// <summary>
        /// ユニット全体を生成するPrefabです。
        /// </summary>
        [Header("Prefabs")]
        [SerializeField]
        private GameObject _unitPrefab;

        /// <summary>
        /// キャラクターの3DモデルPrefabです。
        /// </summary>
        [SerializeField]
        private GameObject _modelPrefab;

        /// <summary>
        /// キャラクター選択画面などで使用する大きな画像です。
        /// </summary>
        [Header("Images")]
        [SerializeField]
        private Sprite _portraitSprite;

        /// <summary>
        /// プレイ画面やHUDで使用するキャラクター画像です。
        /// </summary>
        [SerializeField]
        private Sprite _gameplaySprite;

        /// <summary>
        /// プロフィールのPortrait Viewportへ配置する背景画像です。
        /// </summary>
        [Header("Character Profile Images")]
        [SerializeField]
        private Sprite _profileBackgroundSprite;

        /// <summary>
        /// プロフィールのPortrait Viewportへ重ねるキャラクター画像です。
        /// </summary>
        [SerializeField]
        private Sprite _profileCharacterSprite;

        /// <summary>
        /// プロフィールのCharacter Thumbnailsへ表示する選択画像です。
        /// </summary>
        [SerializeField]
        private Sprite _profileSelectionSprite;

        /// <summary>
        /// ソロキャラクター選択画面の全面に配置する1920×1080の背景画像です。
        /// </summary>
        [Header("Solo Play Images")]
        [Tooltip("ソロキャラクター選択画面用の背景画像（1920×1080）を設定します。")]
        [SerializeField]
        private Sprite _soloSelectionBackgroundSprite;

        public Sprite SoloSelectionBackgroundSprite => _soloSelectionBackgroundSprite;

        public Sprite ProfileBackgroundSprite => _profileBackgroundSprite;

        public Sprite ProfileCharacterSprite => _profileCharacterSprite;

        public Sprite ProfileSelectionSprite => _profileSelectionSprite;

        /// <summary>
        /// Main側に装備するPranam構成です。
        /// </summary>
        [Header("Pranam")]
        [SerializeField]
        private PranamSet _mainPranamSet = new();

        /// <summary>
        /// Sub側に装備するPranam構成です。
        /// </summary>
        [SerializeField]
        private PranamSet _subPranamSet = new();

        public string UnitName
        {
            get
            {
                return _unitName;
            }
        }

        public CharacterType Type
        {
            get
            {
                return _characterType;
            }
        }

        public string RomanizedName => _romanizedName;

        public string RoleDescription => _roleDescription;

        public string Story
        {
            get
            {
                return _story;
            }
        }

        public bool CanUsePlayer
        {
            get
            {
                return _canUsePlayer;
            }
        }

        public int InitialProactiveness
        {
            get
            {
                return _initialProactiveness;
            }
        }

        public float PranaCapacity
        {
            get
            {
                return _pranaCapacity;
            }
        }

        public float MoveSpeed
        {
            get
            {
                return _moveSpeed;
            }
        }

        public GameObject UnitPrefab
        {
            get
            {
                return _unitPrefab;
            }
        }

        public GameObject ModelPrefab
        {
            get
            {
                return _modelPrefab;
            }
        }

        public Sprite PortraitSprite
        {
            get
            {
                return _portraitSprite;
            }
        }

        public Sprite GameplaySprite
        {
            get
            {
                return _gameplaySprite;
            }
        }

        public PranamSet MainPranamSet
        {
            get
            {
                return _mainPranamSet;
            }
        }

        public PranamSet SubPranamSet
        {
            get
            {
                return _subPranamSet;
            }
        }

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(
                        _unitName) &&
                    _unitPrefab != null;
            }
        }

        /// <summary>
        /// ScriptableObject読み込み時に内部データを初期化します。
        /// </summary>
        private void OnEnable()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// MainまたはSubの指定スロットに設定された
        /// Pranam名を取得します。
        /// </summary>
        /// <param name="isMain">
        /// Main側を取得する場合は<c>true</c>です。
        /// </param>
        /// <param name="slotIndex">
        /// 取得するスロット番号です。
        /// </param>
        /// <param name="pranamName">
        /// 取得されたPranam名です。
        /// </param>
        /// <returns>
        /// 有効なスロットを取得できた場合は<c>true</c>です。
        /// </returns>
        public bool TryGetPranamName(
            bool isMain,
            int slotIndex,
            out string pranamName)
        {
            PranamSet pranamSet =
                isMain
                    ? _mainPranamSet
                    : _subPranamSet;

            return pranamSet.TryGetPranamName(
                slotIndex,
                out pranamName);
        }

        /// <summary>
        /// MainまたはSubの指定スロットへPranam名を設定します。
        /// </summary>
        /// <param name="isMain">
        /// Main側へ設定する場合は<c>true</c>です。
        /// </param>
        /// <param name="slotIndex">
        /// 設定するスロット番号です。
        /// </param>
        /// <param name="pranamName">
        /// 設定するPranam名です。
        /// </param>
        /// <returns>
        /// 設定できた場合は<c>true</c>です。
        /// </returns>
        public bool SetPranamName(
            bool isMain,
            int slotIndex,
            string pranamName)
        {
            PranamSet pranamSet =
                isMain
                    ? _mainPranamSet
                    : _subPranamSet;

            return pranamSet.SetPranamName(
                slotIndex,
                pranamName);
        }

        /// <summary>
        /// 文字列とPranam構成を有効な状態へ初期化します。
        /// </summary>
        private void EnsureInitialized()
        {
            _unitName ??=
                string.Empty;

            _story ??=
                string.Empty;

            _mainPranamSet ??=
                new PranamSet();

            _subPranamSet ??=
                new PranamSet();

            _mainPranamSet.EnsureSlotCount(
                k_PranamSlotCount);

            _subPranamSet.EnsureSlotCount(
                k_PranamSlotCount);
        }

        /// <summary>
        /// MainまたはSubに装備するPranam名の一覧を保持します。
        /// </summary>
        [Serializable]
        public sealed class PranamSet
        {
            /// <summary>
            /// 各スロットに設定されたPranam名です。
            /// </summary>
            [SerializeField]
            private List<string> _pranamNames =
                CreateDefaultPranamNames();

            public IReadOnlyList<string> PranamNames
            {
                get
                {
                    return _pranamNames;
                }
            }

            public int Count
            {
                get
                {
                    return _pranamNames?.Count ??
                        0;
                }
            }

            /// <summary>
            /// 指定したスロットに設定されたPranam名を取得します。
            /// </summary>
            /// <param name="slotIndex">
            /// 取得するスロット番号です。
            /// </param>
            /// <param name="pranamName">
            /// 取得されたPranam名です。
            /// </param>
            /// <returns>
            /// 有効なスロットを取得できた場合は<c>true</c>です。
            /// </returns>
            public bool TryGetPranamName(
                int slotIndex,
                out string pranamName)
            {
                pranamName =
                    string.Empty;

                if (_pranamNames == null ||
                    slotIndex < 0 ||
                    slotIndex >= _pranamNames.Count)
                {
                    return false;
                }

                pranamName =
                    NormalizePranamName(
                        _pranamNames[slotIndex]);

                return true;
            }

            /// <summary>
            /// 指定したスロットへPranam名を設定します。
            /// </summary>
            /// <param name="slotIndex">
            /// 設定するスロット番号です。
            /// </param>
            /// <param name="pranamName">
            /// 設定するPranam名です。
            /// </param>
            /// <returns>
            /// 設定できた場合は<c>true</c>です。
            /// </returns>
            public bool SetPranamName(
                int slotIndex,
                string pranamName)
            {
                if (_pranamNames == null ||
                    slotIndex < 0 ||
                    slotIndex >= _pranamNames.Count)
                {
                    return false;
                }

                _pranamNames[slotIndex] =
                    NormalizePranamName(
                        pranamName);

                return true;
            }

            /// <summary>
            /// 指定したスロットを空スロットへ変更します。
            /// </summary>
            /// <param name="slotIndex">
            /// 空にするスロット番号です。
            /// </param>
            /// <returns>
            /// 設定できた場合は<c>true</c>です。
            /// </returns>
            public bool ClearPranam(
                int slotIndex)
            {
                return SetPranamName(
                    slotIndex,
                    k_NonePranamName);
            }

            /// <summary>
            /// 指定したスロットが空かどうかを確認します。
            /// </summary>
            /// <param name="slotIndex">
            /// 確認するスロット番号です。
            /// </param>
            /// <returns>
            /// 空スロットの場合は<c>true</c>です。
            /// </returns>
            public bool IsEmpty(
                int slotIndex)
            {
                if (!TryGetPranamName(
                        slotIndex,
                        out string pranamName))
                {
                    return true;
                }

                return string.Equals(
                    pranamName,
                    k_NonePranamName,
                    StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Pranamスロット数を指定された数へ調整します。
            /// </summary>
            /// <param name="slotCount">
            /// 必要なスロット数です。
            /// </param>
            public void EnsureSlotCount(
                int slotCount)
            {
                int validSlotCount =
                    Mathf.Max(
                        0,
                        slotCount);

                _pranamNames ??=
                    new List<string>();

                while (_pranamNames.Count <
                    validSlotCount)
                {
                    _pranamNames.Add(
                        k_NonePranamName);
                }

                if (_pranamNames.Count >
                    validSlotCount)
                {
                    _pranamNames.RemoveRange(
                        validSlotCount,
                        _pranamNames.Count -
                        validSlotCount);
                }

                for (int index = 0;
                    index < _pranamNames.Count;
                    index++)
                {
                    _pranamNames[index] =
                        NormalizePranamName(
                            _pranamNames[index]);
                }
            }

            /// <summary>
            /// Pranam名を有効なスロット値へ補正します。
            /// </summary>
            private static string NormalizePranamName(
                string pranamName)
            {
                return string.IsNullOrWhiteSpace(
                        pranamName)
                    ? k_NonePranamName
                    : pranamName.Trim();
            }

            /// <summary>
            /// 4つの空Pranamスロットを生成します。
            /// </summary>
            private static List<string> CreateDefaultPranamNames()
            {
                List<string> pranamNames =
                    new(
                        k_PranamSlotCount);

                for (int index = 0;
                    index < k_PranamSlotCount;
                    index++)
                {
                    pranamNames.Add(
                        k_NonePranamName);
                }

                return pranamNames;
            }
        }

        public enum CharacterType
        {
            Attacker,

            Shooter,

            Gunner,

            Sniper,

            Supporter
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の値とPranamスロットを検証します。
        /// </summary>
        private void OnValidate()
        {
            EnsureInitialized();

            _unitName =
                _unitName.Trim();

            _story =
                _story.Trim();

            _initialProactiveness =
                Mathf.Max(
                    0,
                    _initialProactiveness);

            _pranaCapacity =
                Mathf.Max(
                    0.0f,
                    _pranaCapacity);

            _moveSpeed =
                Mathf.Max(
                    0.0f,
                    _moveSpeed);
        }
#endif
    }
}
