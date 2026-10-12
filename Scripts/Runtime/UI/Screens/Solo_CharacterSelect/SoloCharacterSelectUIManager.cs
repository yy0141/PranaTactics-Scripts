using System;
using System.Collections;
using UnityEngine;

using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// ソロプレイ用キャラクター選択画面の
    /// UIおよび選択状態を管理します。
    /// </summary>
    /// <remarks>
    /// オンラインサービスは使用せず、
    /// ローカルプレイヤー1人分のキャラクターを
    /// UIスロットのインデックス0へ表示します。
    ///
    /// 選択されたキャラクター名は
    /// <see cref="SoloCharacterSelectionContext"/>へ保存されます。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SoloCharacterSelectUIManager :
        CharacterSelectUIManagerBase
    {
        /// <summary>
        /// ローカルプレイヤー用UIスロットの
        /// インデックスです。
        /// </summary>
        private const int k_LocalCharacterSlotIndex = 0;

        /// <summary>
        /// キャラクタータイプのアイコンを管理するUIです。
        /// </summary>
        [Header("Character UI")]
        [SerializeField]
        private SetImage _characterTypeImage;

        /// <summary>
        /// キャラクター名を管理するUIです。
        /// </summary>
        [SerializeField]
        private SetTextMeshProUGUI _characterNameText;

        /// <summary>
        /// キャラクターの3Dプレビューモデルを管理します。
        /// </summary>
        [SerializeField]
        private CharacterModelPreview _characterModelPreview;

        /// <summary>
        /// メインプラーナムタイプのアイコンを管理するUIです。
        /// </summary>
        [SerializeField]
        private SetImage _mainPranamTypeImage;

        /// <summary>
        /// メインプラーナム名を管理するUIです。
        /// </summary>
        [SerializeField]
        private SetTextMeshProUGUI _mainPranamNameText;

        /// <summary>
        /// サブプラーナムタイプのアイコンを管理するUIです。
        /// </summary>
        [SerializeField]
        private SetImage _subPranamTypeImage;

        /// <summary>
        /// サブプラーナム名を管理するUIです。
        /// </summary>
        [SerializeField]
        private SetTextMeshProUGUI _subPranamNameText;

        /// <summary>
        /// 保存済みのキャラクター選択を
        /// 画面表示時に復元するかどうかです。
        /// </summary>
        [Header("Selection")]
        [SerializeField]
        private bool _restorePreviousSelection = true;

        /// <summary>
        /// 状態更新の詳細をログへ出力するかどうかです。
        /// </summary>
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLogging;

        /// <summary>
        /// 現在選択されているキャラクターデータです。
        /// </summary>
        private UnitParams _selectedCharacterData;

        /// <summary>
        /// 現在生成されているキャラクターモデルです。
        /// </summary>
        private GameObject _selectedCharacterModel;

        /// <summary>
        /// 保存済みキャラクターの復元処理です。
        /// </summary>
        private Coroutine _restoreSelectionCoroutine;

        public static SoloCharacterSelectUIManager Instance
        {
            get;
            private set;
        }

        public override bool CanStartGame
        {
            get
            {
                return IsCharacterSelected &&
                    _selectedCharacterData != null &&
                    SoloCharacterSelectionContext.HasSelection;
            }
        }

        public UnitParams SelectedCharacterData
        {
            get
            {
                return _selectedCharacterData;
            }
        }

        public GameObject SelectedCharacterModel
        {
            get
            {
                return _selectedCharacterModel;
            }
        }

        public event Action<UnitParams> CharacterChanged;

        /// <summary>
        /// 基底クラスとソロ用シングルトン参照を初期化します。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (!IsPrimaryInstance)
            {
                return;
            }

            Instance =
                this;
        }

        /// <summary>
        /// 準備完了状態を初期化し、
        /// 保存済みキャラクターの復元を開始します。
        /// </summary>
        private void OnEnable()
        {
            if (!IsPrimaryInstance)
            {
                return;
            }

            SetReadySelectionLocked(
                false);

            StartRestoreSelection();
        }

        /// <summary>
        /// 実行中の復元処理を停止します。
        /// </summary>
        private void OnDisable()
        {
            StopRestoreSelection();
        }

        /// <summary>
        /// 復元処理を停止し、
        /// シングルトン参照を解放します。
        /// </summary>
        protected override void OnDestroy()
        {
            StopRestoreSelection();

            if (Instance == this)
            {
                Instance =
                    null;
            }

            base.OnDestroy();
        }

        /// <summary>
        /// 現在のキャラクター選択を確定し、
        /// 準備完了状態にします。
        /// </summary>
        /// <remarks>
        /// ソロプレイ用の準備完了ボタンから呼び出します。
        /// </remarks>
        public void ConfirmSelection()
        {
            if (!CanStartGame)
            {
                Debug.LogWarning(
                    $"{nameof(SoloCharacterSelectUIManager)}: " +
                    "キャラクターが選択されていないため、" +
                    "選択を確定できません。",
                    this);

                return;
            }

            SetReadySelectionLocked(
                true);
        }

        /// <summary>
        /// 準備完了状態を解除します。
        /// </summary>
        public void CancelConfirmation()
        {
            SetReadySelectionLocked(
                false);
        }

        /// <summary>
        /// ソロ用キャラクター選択を
        /// ローカル状態へ反映します。
        /// </summary>
        protected override bool TryApplyCharacterSelection(
            UnitParams characterData)
        {
            if (characterData == null)
            {
                return false;
            }

            ApplyCharacterSelection(
                characterData,
                true);

            return true;
        }

        /// <summary>
        /// キャラクター名を使用して
        /// ソロ用キャラクター選択を反映します。
        /// </summary>
        /// <param name="characterName">
        /// 選択するキャラクター名です。
        /// </param>
        public void SendSelectCharacter(
            string characterName)
        {
            string validCharacterName =
                characterName?.Trim() ??
                string.Empty;

            if (string.IsNullOrEmpty(
                    validCharacterName))
            {
                Debug.LogWarning(
                    $"{nameof(SoloCharacterSelectUIManager)}: " +
                    "キャラクター名が空のため選択できません。",
                    this);

                return;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null)
            {
                Debug.LogWarning(
                    $"{nameof(SoloCharacterSelectUIManager)}: " +
                    $"{nameof(GameDataRepository)}が初期化されていません。",
                    this);

                return;
            }

            UnitParams characterData =
                repository.GetCharacterData(
                    validCharacterName);

            if (characterData == null)
            {
                Debug.LogWarning(
                    $"{nameof(SoloCharacterSelectUIManager)}: " +
                    "キャラクターデータが見つかりません。" +
                    $" Character={validCharacterName}",
                    this);

                return;
            }

            if (TryApplyCharacterSelection(
                    characterData))
            {
                SetCharacterSelected(
                    true);
            }
        }

        /// <summary>
        /// 指定されたキャラクターを
        /// ローカルプレイヤー用UIへ表示します。
        /// </summary>
        /// <param name="characterData">
        /// 表示するキャラクターデータです。
        /// </param>
        /// <returns>
        /// 生成されたプレビューモデルです。
        /// </returns>
        public GameObject SetCharacterUI(
            UnitParams characterData)
        {
            if (characterData == null)
            {
                Debug.LogWarning(
                    $"{nameof(SoloCharacterSelectUIManager)}: " +
                    "表示するキャラクターデータがnullです。",
                    this);

                return null;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            Sprite typeIcon =
                repository != null
                    ? repository.GetIconSprite(
                        characterData.Type.ToString())
                    : null;

            _characterTypeImage?.SetSprite(
                typeIcon,
                k_LocalCharacterSlotIndex);

            _characterNameText?.SetText(
                characterData.UnitName,
                k_LocalCharacterSlotIndex);

            BindPranamSlots(characterData, repository);

            if (_characterModelPreview == null)
            {
                return null;
            }

            return _characterModelPreview.GenerateCharacter(
                characterData.ModelPrefab,
                k_LocalCharacterSlotIndex);
        }

        /// <summary>
        /// 欠損データがあってもスロット位置を維持して装備を表示します。
        /// </summary>
        private void BindPranamSlots(UnitParams character, GameDataRepository repository)
        {
            for (int slot = 0; slot < 8; slot++)
            {
                bool isMain = slot < 4;
                int index = slot % 4;
                PranamData pranam = null;
                if (character != null && repository != null &&
                    character.TryGetPranamName(isMain, index, out string name) &&
                    !string.IsNullOrWhiteSpace(name) && !string.Equals(name, "NONE", StringComparison.OrdinalIgnoreCase))
                {
                    pranam = repository.GetPranamData(name);
                }

                SetImage image = isMain ? _mainPranamTypeImage : _subPranamTypeImage;
                SetTextMeshProUGUI text = isMain ? _mainPranamNameText : _subPranamNameText;
                image?.SetSprite(pranam != null ? pranam.Icon : null, index);
                text?.SetText(pranam != null ? pranam.DisplayName : "—", index);
            }
        }

        /// <summary>
        /// 現在選択されているキャラクターデータの
        /// 取得を試みます。
        /// </summary>
        /// <param name="characterData">
        /// 選択されているキャラクターデータです。
        /// </param>
        /// <returns>
        /// キャラクターが選択されている場合は<c>true</c>です。
        /// </returns>
        public bool TryGetSelectedCharacter(
            out UnitParams characterData)
        {
            characterData =
                _selectedCharacterData;

            return characterData != null;
        }

        /// <summary>
        /// ソロ用キャラクター選択の表示と
        /// 保存情報を消去します。
        /// </summary>
        protected override void OnSelectionCleared()
        {
            _selectedCharacterData =
                null;

            _selectedCharacterModel =
                null;

            _characterTypeImage?.HideImage(
                k_LocalCharacterSlotIndex);

            _characterNameText?.HideText(
                k_LocalCharacterSlotIndex);

            BindPranamSlots(null, null);

            _characterModelPreview?.ClearCharacters();

            SoloCharacterSelectionContext.Clear();

            CharacterChanged?.Invoke(
                null);
        }

        /// <summary>
        /// 指定されたキャラクターをUIへ反映し、
        /// ローカル選択状態へ保存します。
        /// </summary>
        /// <param name="characterData">
        /// 適用するキャラクターデータです。
        /// </param>
        /// <param name="notifyChange">
        /// 選択変更イベントを発生させる場合は
        /// <c>true</c>です。
        /// </param>
        private void ApplyCharacterSelection(
            UnitParams characterData,
            bool notifyChange)
        {
            if (characterData == null)
            {
                return;
            }

            _selectedCharacterModel =
                SetCharacterUI(
                    characterData);

            _selectedCharacterData =
                characterData;

            SoloCharacterSelectionContext.SetCharacter(
                characterData.UnitName);

            SetCharacterSelected(
                true);

            if (notifyChange)
            {
                CharacterChanged?.Invoke(
                    characterData);
            }

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(SoloCharacterSelectUIManager)}: " +
                    "ローカルキャラクターを選択しました。" +
                    $" Character={characterData.UnitName}",
                    this);
            }
        }

        /// <summary>
        /// 保存済みキャラクター選択の復元を開始します。
        /// </summary>
        private void StartRestoreSelection()
        {
            if (!_restorePreviousSelection ||
                !SoloCharacterSelectionContext.HasSelection ||
                _restoreSelectionCoroutine != null)
            {
                return;
            }

            _restoreSelectionCoroutine =
                StartCoroutine(
                    RestoreSelectionWhenReady());
        }

        /// <summary>
        /// 保存済みキャラクター選択の復元を停止します。
        /// </summary>
        private void StopRestoreSelection()
        {
            if (_restoreSelectionCoroutine == null)
            {
                return;
            }

            StopCoroutine(
                _restoreSelectionCoroutine);

            _restoreSelectionCoroutine =
                null;
        }

        /// <summary>
        /// ゲームデータが利用可能になるまで待機し、
        /// 保存済みのキャラクター選択を復元します。
        /// </summary>
        private IEnumerator RestoreSelectionWhenReady()
        {
            while (isActiveAndEnabled &&
                GameDataRepository.Instance == null)
            {
                yield return null;
            }

            _restoreSelectionCoroutine =
                null;

            if (!isActiveAndEnabled ||
                !SoloCharacterSelectionContext.HasSelection)
            {
                yield break;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null)
            {
                yield break;
            }

            string characterName =
                SoloCharacterSelectionContext.CharacterName;

            UnitParams characterData =
                repository.GetCharacterData(
                    characterName);

            if (characterData == null)
            {
                Debug.LogWarning(
                    $"{nameof(SoloCharacterSelectUIManager)}: " +
                    "保存されていたキャラクターデータが" +
                    "見つかりません。" +
                    $" Character={characterName}",
                    this);

                SoloCharacterSelectionContext.Clear();

                yield break;
            }

            ApplyCharacterSelection(
                characterData,
                true);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定された参照を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_characterTypeImage == null)
            {
                Debug.LogWarning(
                    $"{nameof(SoloCharacterSelectUIManager)}: " +
                    $"{nameof(_characterTypeImage)}が設定されていません。",
                    this);
            }

            if (_characterNameText == null)
            {
                Debug.LogWarning(
                    $"{nameof(SoloCharacterSelectUIManager)}: " +
                    $"{nameof(_characterNameText)}が設定されていません。",
                    this);
            }

            if (_characterModelPreview == null)
            {
                Debug.LogWarning(
                    $"{nameof(SoloCharacterSelectUIManager)}: " +
                    $"{nameof(_characterModelPreview)}が" +
                    "設定されていません。",
                    this);
            }
        }
#endif
    }
}
