using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Core;
using PranaTactics.Unit;

namespace PranaTactics.UI.Screens
{
    /// <summary>
    /// プレイヤーが使用可能なキャラクターを
    /// ドロップダウンへ表示し、選択されたキャラクターを取得します。
    /// </summary>
    /// <remarks>
    /// <see cref="GameDataRepository"/>の読み込み完了後に、
    /// 使用可能なキャラクター一覧を構築します。
    ///
    /// 選択されたキャラクターを使用して、
    /// ローカルプレイヤーのキャラクターを再生成することもできます。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CharacterDropdown : MonoBehaviour
    {
        /// <summary>
        /// キャラクター名を表示するドロップダウンです。
        /// </summary>
        [FormerlySerializedAs("_dropdown")]
        [SerializeField]
        private TMP_Dropdown _characterDropdown;

        /// <summary>
        /// ドロップダウンの初期化が完了しているかどうかを示します。
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
        /// データリポジトリの読み込み完了を待ち、
        /// キャラクター一覧を初期化します。
        /// </summary>
        private IEnumerator Start()
        {
            while (isActiveAndEnabled &&
                !IsRepositoryReady())
            {
                yield return null;
            }

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            Initialize();
        }

        /// <summary>
        /// プレイヤーが使用可能なキャラクターを取得し、
        /// ドロップダウンの選択肢として設定します。
        /// </summary>
        public void Initialize()
        {
            _isInitialized =
                false;

            if (!TryGetDropdown(
                    out TMP_Dropdown dropdown))
            {
                return;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null ||
                !repository.IsLoaded)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterDropdown)}: " +
                    $"{nameof(GameDataRepository)}の読み込みが" +
                    "完了していないため、初期化できません。",
                    this);

                return;
            }

            List<UnitParams> characters =
                repository.GetPlayerCharactersData();

            dropdown.ClearOptions();

            if (characters == null ||
                characters.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterDropdown)}: " +
                    "プレイヤーが使用可能なキャラクターがありません。",
                    this);

                dropdown.interactable =
                    false;

                return;
            }

            List<string> options =
                CreateCharacterOptions(
                    characters);

            if (options.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterDropdown)}: " +
                    "ドロップダウンへ追加できるキャラクターがありません。",
                    this);

                dropdown.interactable =
                    false;

                return;
            }

            dropdown.AddOptions(
                options);

            dropdown.value =
                0;

            dropdown.RefreshShownValue();

            dropdown.interactable =
                true;

            _isInitialized =
                true;
        }

        /// <summary>
        /// 現在選択されているキャラクター名を取得します。
        /// </summary>
        /// <returns>
        /// 選択されているキャラクター名です。
        /// 有効な選択肢がない場合は空文字列です。
        /// </returns>
        public string GetSelectedCharacterName()
        {
            if (!TryGetSelectedCharacterName(
                    out string characterName))
            {
                return string.Empty;
            }

            return characterName;
        }

        /// <summary>
        /// 現在選択されているキャラクターデータを取得します。
        /// </summary>
        /// <returns>
        /// 選択されているキャラクターデータです。
        /// 取得できなかった場合は<c>null</c>です。
        /// </returns>
        public UnitParams GetSelectedCharacter()
        {
            if (!TryGetSelectedCharacter(
                    out UnitParams characterData))
            {
                return null;
            }

            return characterData;
        }

        /// <summary>
        /// 現在選択されているキャラクターデータを取得します。
        /// </summary>
        /// <param name="characterData">
        /// 取得されたキャラクターデータです。
        /// </param>
        /// <returns>
        /// キャラクターデータを取得できた場合は<c>true</c>です。
        /// </returns>
        public bool TryGetSelectedCharacter(
            out UnitParams characterData)
        {
            characterData =
                null;

            if (!TryGetSelectedCharacterName(
                    out string characterName))
            {
                return false;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterDropdown)}: " +
                    $"{nameof(GameDataRepository)}が存在しません。",
                    this);

                return false;
            }

            characterData =
                repository.GetCharacterData(
                    characterName);

            if (characterData != null)
            {
                return true;
            }

            Debug.LogWarning(
                $"{nameof(CharacterDropdown)}: " +
                "選択されたキャラクターデータが見つかりません。" +
                $" CharacterName={characterName}",
                this);

            return false;
        }

        /// <summary>
        /// ドロップダウンで選択されているキャラクターへ、
        /// ローカルプレイヤーのキャラクターを変更します。
        /// </summary>
        public void ChangeCharacter()
        {
            if (!TryGetSelectedCharacterName(
                    out string characterName))
            {
                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterDropdown)}: " +
                    $"{nameof(UnitManager)}が存在しないため、" +
                    "キャラクターを変更できません。",
                    this);

                return;
            }

            int playerId =
                unitManager.LocalPlayerId;

            if (playerId == -1)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterDropdown)}: " +
                    "有効なローカルプレイヤーIDを取得できません。" +
                    $" PlayerId={playerId}",
                    this);

                return;
            }

            unitManager.RequestLocalRespawn(
                playerId,
                characterName);
        }

        /// <summary>
        /// 現在選択されているキャラクター名を安全に取得します。
        /// </summary>
        /// <param name="characterName">
        /// 取得されたキャラクター名です。
        /// </param>
        /// <returns>
        /// 有効なキャラクター名を取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetSelectedCharacterName(
            out string characterName)
        {
            characterName =
                string.Empty;

            if (!TryGetDropdown(
                    out TMP_Dropdown dropdown))
            {
                return false;
            }

            if (!_isInitialized ||
                dropdown.options == null ||
                dropdown.options.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterDropdown)}: " +
                    "キャラクターの選択肢が初期化されていません。",
                    this);

                return false;
            }

            int selectedIndex =
                dropdown.value;

            if (selectedIndex < 0 ||
                selectedIndex >= dropdown.options.Count)
            {
                Debug.LogWarning(
                    $"{nameof(CharacterDropdown)}: " +
                    "選択中のインデックスが範囲外です。" +
                    $" Index={selectedIndex}," +
                    $" Count={dropdown.options.Count}",
                    this);

                return false;
            }

            TMP_Dropdown.OptionData option =
                dropdown.options[selectedIndex];

            if (option == null ||
                string.IsNullOrWhiteSpace(
                    option.text))
            {
                Debug.LogWarning(
                    $"{nameof(CharacterDropdown)}: " +
                    "選択されたキャラクター名が空です。",
                    this);

                return false;
            }

            characterName =
                option.text.Trim();

            return true;
        }

        /// <summary>
        /// 使用可能なキャラクターから、
        /// ドロップダウンへ表示する名前一覧を生成します。
        /// </summary>
        /// <param name="characters">
        /// キャラクターデータ一覧です。
        /// </param>
        /// <returns>
        /// 重複と空文字列を除いたキャラクター名一覧です。
        /// </returns>
        private static List<string> CreateCharacterOptions(
            List<UnitParams> characters)
        {
            List<string> options =
                new();

            HashSet<string> registeredNames =
                new();

            foreach (UnitParams character in
                    characters)
            {
                if (character == null ||
                    !character.CanUsePlayer ||
                    string.IsNullOrWhiteSpace(
                        character.UnitName))
                {
                    continue;
                }

                string characterName =
                    character.UnitName.Trim();

                if (!registeredNames.Add(
                        characterName))
                {
                    continue;
                }

                options.Add(
                    characterName);
            }

            return options;
        }

        /// <summary>
        /// GameDataRepositoryの利用準備が完了しているか確認します。
        /// </summary>
        /// <returns>
        /// インスタンスが存在し、読み込み済みの場合は<c>true</c>です。
        /// </returns>
        private static bool IsRepositoryReady()
        {
            GameDataRepository repository =
                GameDataRepository.Instance;

            return repository != null &&
                repository.IsLoaded;
        }

        /// <summary>
        /// キャラクタードロップダウンを取得します。
        /// </summary>
        /// <param name="dropdown">
        /// 取得されたドロップダウンです。
        /// </param>
        /// <returns>
        /// ドロップダウンを取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetDropdown(
            out TMP_Dropdown dropdown)
        {
            if (_characterDropdown == null)
            {
                _characterDropdown =
                    GetComponent<TMP_Dropdown>();
            }

            dropdown =
                _characterDropdown;

            if (dropdown != null)
            {
                return true;
            }

            Debug.LogError(
                $"{nameof(CharacterDropdown)}: " +
                $"{nameof(TMP_Dropdown)}が設定されていません。",
                this);

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上でドロップダウン参照を自動設定します。
        /// </summary>
        private void OnValidate()
        {
            if (_characterDropdown == null)
            {
                _characterDropdown =
                    GetComponent<TMP_Dropdown>();
            }
        }
#endif
    }
}
