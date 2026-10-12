using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

using PranaTactics.Board;
using PranaTactics.GameFlow;
using PranaTactics.Pranam;
using PranaTactics.Tutorial;
using PranaTactics.UI;
using PranaTactics.Unit;

namespace PranaTactics.Core
{
    /// <summary>
    /// ゲーム内で使用するキャラクター、兵装、アイコン、
    /// ボードおよびゲーム設定データを読み込み、管理します。
    /// </summary>
    public sealed class GameDataRepository : MonoBehaviour
    {
        /// <summary>
        /// キャラクターデータに設定されているAddressablesラベルです。
        /// </summary>
        private const string k_CharacterAddressableLabel = "Character";

        /// <summary>
        /// アイコンデータに設定されているAddressablesラベルです。
        /// </summary>
        private const string k_IconAddressableLabel = "Icon";

        /// <summary>
        /// 兵装データに設定されているAddressablesラベルです。
        /// </summary>
        private const string k_PranamAddressableLabel = "Trigger";

        /// <summary>
        /// ゲーム設定データに設定されているAddressablesアドレスです。
        /// </summary>
        private const string k_GameDataAddress = "Game";

        /// <summary>
        /// チュートリアルUIデータに設定されているAddressablesアドレスです。
        /// </summary>
        private const string k_TutorialUiDataAddress = "Tutorial";

        /// <summary>
        /// ユニット設定データに設定されているAddressablesアドレスです。
        /// </summary>
        private const string k_UnitDataAddress = "Unit";

        /// <summary>
        /// キャラクター名から対応するユニットパラメータを取得する辞書です。
        /// </summary>
        private readonly Dictionary<string, UnitParams>
            _characterDataByName =
                new Dictionary<string, UnitParams>(
                    StringComparer.Ordinal);

        /// <summary>
        /// アイコン名から対応するスプライトを取得する辞書です。
        /// </summary>
        private readonly Dictionary<string, Sprite>
            _iconSpriteByName =
                new Dictionary<string, Sprite>(
                    StringComparer.Ordinal);

        /// <summary>
        /// 兵装名から対応する兵装データを取得する辞書です。
        /// </summary>
        private readonly Dictionary<string, PranamData>
            _pranamDataByName =
                new Dictionary<string, PranamData>(
                    StringComparer.Ordinal);

        /// <summary>
        /// ボード上の各セルに設定されている高さを保持します。
        /// </summary>
        private readonly List<List<int>> _hexagonMap = new();

        /// <summary>
        /// キャラクターデータのAddressablesロードハンドルです。
        /// </summary>
        private AsyncOperationHandle<IList<UnitParams>>
            _characterDataHandle;

        /// <summary>
        /// アイコンデータのAddressablesロードハンドルです。
        /// </summary>
        private AsyncOperationHandle<IList<IconData>>
            _iconDataHandle;

        /// <summary>
        /// 兵装データのAddressablesロードハンドルです。
        /// </summary>
        private AsyncOperationHandle<IList<PranamData>>
            _pranamDataHandle;

        /// <summary>
        /// ゲーム設定データのAddressablesロードハンドルです。
        /// </summary>
        private AsyncOperationHandle<GameData> _gameDataHandle;

        /// <summary>
        /// チュートリアルUIデータのAddressablesロードハンドルです。
        /// </summary>
        private AsyncOperationHandle<TutorialUiResources> _tutorialUiHandle;

        /// <summary>
        /// ユニット設定データのAddressablesロードハンドルです。
        /// </summary>
        private AsyncOperationHandle<UnitData> _unitDataHandle;

        /// <summary>
        /// Addressablesから読み込まれたゲーム設定データです。
        /// </summary>
        private GameData _gameData;

        /// <summary>
        /// Addressablesから読み込まれたチュートリアルUIデータです。
        /// </summary>
        private TutorialUiResources _tutorialUiData;

        /// <summary>
        /// 現在使用しているボード設定データです。
        /// </summary>
        private BoardData _boardData;

        /// <summary>
        /// Addressablesから読み込まれたユニット設定データです。
        /// </summary>
        private UnitData _unitData;

        public static GameDataRepository Instance
        {
            get; private set;
        }

        public bool IsLoaded
        {
            get; private set;
        }

        public Exception InitializationError
        {
            get; private set;
        }

        /// <summary>
        /// シングルトンインスタンスを設定し、
        /// Addressablesからゲームデータを非同期で読み込みます。
        /// </summary>
        private async void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            try
            {
                await LoadDataAsync();
                IsLoaded = true;
            }
            catch (Exception exception)
            {
                IsLoaded = false;
                InitializationError = exception;

                Debug.LogError(
                    $"{nameof(GameDataRepository)}の初期化に失敗しました。",
                    this);

                Debug.LogException(
                    exception,
                    this);

                ClearLoadedData();
                ReleaseAddressableHandles();
            }
        }

        /// <summary>
        /// このインスタンスが破棄されたとき、
        /// シングルトン参照とAddressablesのロードハンドルを解放します。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Instance = null;
            IsLoaded = false;

            ClearLoadedData();
            ReleaseAddressableHandles();
        }

        /// <summary>
        /// ゲーム内で使用するすべてのデータをAddressablesから読み込みます。
        /// </summary>
        /// <returns>
        /// すべてのデータの読み込みが完了したことを表すタスクです。
        /// </returns>
        private async Task LoadDataAsync()
        {
            await LoadPranamDataAsync();
            await LoadCharacterDataAsync();
            await LoadGameDataAsync();
            await LoadTutorialUiDataAsync();
            await LoadUnitDataAsync();
            await LoadIconDataAsync();
        }

        /// <summary>
        /// キャラクターデータをAddressablesから読み込み、
        /// キャラクター名をキーとして辞書へ登録します。
        /// </summary>
        /// <returns>
        /// キャラクターデータの読み込み完了を表すタスクです。
        /// </returns>
        private async Task LoadCharacterDataAsync()
        {
            _characterDataByName.Clear();

            _characterDataHandle =
                Addressables.LoadAssetsAsync<UnitParams>(
                    k_CharacterAddressableLabel,
                    null);

            IList<UnitParams> characterDataList =
                await _characterDataHandle.Task;

            ThrowIfLoadFailed(
                _characterDataHandle,
                k_CharacterAddressableLabel);

            if (characterDataList == null)
            {
                return;
            }

            foreach (UnitParams unitParams in characterDataList)
            {
                if (unitParams == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(unitParams.UnitName))
                {
                    Debug.LogWarning(
                        "名前が設定されていないキャラクターデータがあります。",
                        unitParams);

                    continue;
                }

                if (_characterDataByName.ContainsKey(unitParams.UnitName))
                {
                    Debug.LogWarning(
                        $"キャラクター名が重複しています: {unitParams.UnitName}",
                        unitParams);

                    continue;
                }

                _characterDataByName.Add(
                    unitParams.UnitName,
                    unitParams);
            }
        }

        /// <summary>
        /// アイコンデータをAddressablesから読み込み、
        /// アイコン名をキーとしてスプライトを辞書へ登録します。
        /// </summary>
        /// <returns>
        /// アイコンデータの読み込み完了を表すタスクです。
        /// </returns>
        private async Task LoadIconDataAsync()
        {
            _iconSpriteByName.Clear();

            _iconDataHandle =
                Addressables.LoadAssetsAsync<IconData>(
                    k_IconAddressableLabel,
                    null);

            IList<IconData> iconDataList =
                await _iconDataHandle.Task;

            ThrowIfLoadFailed(
                _iconDataHandle,
                k_IconAddressableLabel);

            if (iconDataList == null)
            {
                return;
            }

            foreach (IconData iconData in iconDataList)
            {
                if (iconData == null ||
                    iconData.Icons == null)
                {
                    continue;
                }

                foreach (IconData.Icon icon in iconData.Icons)
                {
                    if (icon == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(icon.Name))
                    {
                        Debug.LogWarning(
                            "名前が設定されていないアイコンデータがあります。",
                            iconData);

                        continue;
                    }

                    if (icon.Sprite == null)
                    {
                        Debug.LogWarning(
                            $"{icon.Name}にSpriteが設定されていません。",
                            iconData);

                        continue;
                    }

                    if (_iconSpriteByName.ContainsKey(icon.Name))
                    {
                        Debug.LogWarning(
                            $"アイコン名が重複しています: {icon.Name}",
                            iconData);

                        continue;
                    }

                    _iconSpriteByName.Add(
                        icon.Name,
                        icon.Sprite);
                }
            }
        }

        /// <summary>
        /// 兵装データをAddressablesから読み込み、
        /// 兵装名をキーとして辞書へ登録します。
        /// </summary>
        /// <returns>
        /// 兵装データの読み込み完了を表すタスクです。
        /// </returns>
        private async Task LoadPranamDataAsync()
        {
            _pranamDataByName.Clear();

            _pranamDataHandle =
                Addressables.LoadAssetsAsync<PranamData>(
                    k_PranamAddressableLabel,
                    null);

            IList<PranamData> pranamDataList =
                await _pranamDataHandle.Task;

            ThrowIfLoadFailed(
                _pranamDataHandle,
                k_PranamAddressableLabel);

            if (pranamDataList == null)
            {
                return;
            }

            foreach (PranamData pranamData in pranamDataList)
            {
                if (pranamData == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(pranamData.DisplayName))
                {
                    Debug.LogWarning(
                        "名前が設定されていない兵装データがあります。",
                        pranamData);

                    continue;
                }

                if (_pranamDataByName.ContainsKey(pranamData.DisplayName))
                {
                    Debug.LogWarning(
                        $"兵装名が重複しています: {pranamData.DisplayName}",
                        pranamData);

                    continue;
                }

                _pranamDataByName.Add(
                    pranamData.DisplayName,
                    pranamData);
            }
        }

        /// <summary>
        /// ゲーム設定データをAddressablesから読み込みます。
        /// </summary>
        /// <returns>
        /// ゲーム設定データの読み込み完了を表すタスクです。
        /// </returns>
        private async Task LoadGameDataAsync()
        {
            _gameDataHandle =
                Addressables.LoadAssetAsync<GameData>(
                    k_GameDataAddress);

            _gameData = await _gameDataHandle.Task;

            ThrowIfLoadFailed(
                _gameDataHandle,
                k_GameDataAddress);

            if (_gameData == null)
            {
                throw new InvalidOperationException(
                    $"{k_GameDataAddress}から" +
                    $"{nameof(GameData)}を読み込めませんでした。");
            }
        }

        /// <summary>
        /// ゲーム設定データをAddressablesから読み込みます。
        /// </summary>
        /// <returns>
        /// ゲーム設定データの読み込み完了を表すタスクです。
        /// </returns>
        private async Task LoadTutorialUiDataAsync()
        {
            _tutorialUiHandle =
                Addressables.LoadAssetAsync<TutorialUiResources>(
                    k_TutorialUiDataAddress);

            _tutorialUiData = await _tutorialUiHandle.Task;

            ThrowIfLoadFailed(
                _tutorialUiHandle,
                k_TutorialUiDataAddress);

            if (_tutorialUiData == null)
            {
                throw new InvalidOperationException(
                    $"{k_TutorialUiDataAddress}から" +
                    $"{nameof(TutorialUiResources)}を読み込めませんでした。");
            }
        }

        /// <summary>
        /// ユニット設定データをAddressablesから読み込みます。
        /// </summary>
        /// <returns>
        /// ユニット設定データの読み込み完了を表すタスクです。
        /// </returns>
        private async Task LoadUnitDataAsync()
        {
            _unitDataHandle =
                Addressables.LoadAssetAsync<UnitData>(
                    k_UnitDataAddress);

            _unitData = await _unitDataHandle.Task;

            ThrowIfLoadFailed(
                _unitDataHandle,
                k_UnitDataAddress);

            if (_unitData == null)
            {
                throw new InvalidOperationException(
                    $"{k_UnitDataAddress}から" +
                    $"{nameof(UnitData)}を読み込めませんでした。");
            }
        }

        /// <summary>
        /// Addressablesのロード処理が正常に完了したか確認します。
        /// </summary>
        /// <typeparam name="TObject">
        /// Addressablesから読み込むデータの型です。
        /// </typeparam>
        /// <param name="handle">
        /// 確認するAddressablesロードハンドルです。
        /// </param>
        /// <param name="address">
        /// ロードに使用したアドレスまたはラベルです。
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Addressablesのロード処理に失敗した場合に発生します。
        /// </exception>
        private static void ThrowIfLoadFailed<TObject>(
            AsyncOperationHandle<TObject> handle,
            string address)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Addressablesの読み込みに失敗しました: {address}",
                handle.OperationException);
        }

        /// <summary>
        /// 現在使用するボードデータを設定し、
        /// JSONファイルからヘックスマップを読み込みます。
        /// </summary>
        /// <param name="boardData">
        /// 使用するボード設定データです。
        /// </param>
        public void SetBoardData(BoardData boardData)
        {
            if (boardData == null)
            {
                Debug.LogError(
                    $"{nameof(BoardData)}が指定されていません。",
                    this);

                _boardData = null;
                _hexagonMap.Clear();

                return;
            }

            _boardData = boardData;

            LoadHexagonMap();
        }

        /// <summary>
        /// BoardDataに設定されたJSONファイルを解析し、
        /// ボード上の各セルの高さを読み込みます。
        /// </summary>
        private void LoadHexagonMap()
        {
            _hexagonMap.Clear();

            if (_boardData == null)
            {
                Debug.LogError(
                    $"{nameof(BoardData)}が設定されていません。",
                    this);

                return;
            }

            if (_boardData.BoardJson == null)
            {
                Debug.LogError(
                    $"{_boardData.name}にボードJSONが設定されていません。",
                    _boardData);

                return;
            }

            try
            {
                JObject boardObject =
                    JObject.Parse(_boardData.BoardJson.text);

                List<List<int>> parsedMap =
                    new List<List<int>>(boardObject.Count);

                for (
                    int rowNumber = 1;
                    rowNumber <= boardObject.Count;
                    rowNumber++)
                {
                    string rowKey = rowNumber.ToString();

                    if (!boardObject.TryGetValue(
                            rowKey,
                            out JToken rowToken))
                    {
                        throw new JsonException(
                            $"ボードJSONに行「{rowKey}」が存在しません。");
                    }

                    if (!(rowToken is JArray rowArray))
                    {
                        throw new JsonException(
                            $"ボードJSONの行「{rowKey}」が配列ではありません。");
                    }

                    List<int> row =
                        new List<int>(rowArray.Count);

                    foreach (JToken valueToken in rowArray)
                    {
                        if (!int.TryParse(
                                valueToken.ToString(),
                                out int floor))
                        {
                            throw new JsonException(
                                $"行「{rowKey}」に整数ではない値があります: " +
                                $"{valueToken}");
                        }

                        row.Add(floor);
                    }

                    parsedMap.Add(row);
                }

                _hexagonMap.AddRange(parsedMap);
            }
            catch (JsonException exception)
            {
                _hexagonMap.Clear();

                Debug.LogError(
                    $"{_boardData.name}のボードJSONを解析できませんでした。",
                    _boardData);

                Debug.LogException(
                    exception,
                    _boardData);
            }
        }

        /// <summary>
        /// 登録されているすべてのキャラクターデータを取得します。
        /// </summary>
        /// <returns>
        /// 登録されているキャラクターデータの新しい一覧です。
        /// </returns>
        public List<UnitParams> GetCharactersData()
        {
            return new List<UnitParams>(
                _characterDataByName.Values);
        }

        /// <summary>
        /// プレイヤーが使用可能なキャラクターデータを取得します。
        /// </summary>
        /// <returns>
        /// プレイヤーが使用可能なキャラクターデータの一覧です。
        /// </returns>
        public List<UnitParams> GetPlayerCharactersData()
        {
            List<UnitParams> playerCharacters = new();

            foreach (
                UnitParams characterData
                in _characterDataByName.Values)
            {
                if (characterData != null &&
                    characterData.CanUsePlayer)
                {
                    playerCharacters.Add(characterData);
                }
            }

            return playerCharacters;
        }

        /// <summary>
        /// 指定された名前に対応するキャラクターデータを取得します。
        /// </summary>
        /// <param name="characterName">
        /// 取得するキャラクターの名前です。
        /// </param>
        /// <returns>
        /// 指定された名前に対応するキャラクターデータです。
        /// 存在しない場合は<c>null</c>です。
        /// </returns>
        public UnitParams GetCharacterData(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
            {
                Debug.LogWarning(
                    $"{nameof(GetCharacterData)}: " +
                    "キャラクター名が指定されていません。",
                    this);

                return null;
            }

            if (_characterDataByName.TryGetValue(
                    characterName,
                    out UnitParams unitParams))
            {
                return unitParams;
            }

            Debug.LogWarning(
                $"{nameof(GetCharacterData)}: " +
                $"キャラクターが登録されていません: {characterName}",
                this);

            return null;
        }

        /// <summary>
        /// 指定された名前に対応するアイコンスプライトを取得します。
        /// </summary>
        /// <param name="iconName">
        /// 取得するアイコンの名前です。
        /// </param>
        /// <returns>
        /// 指定された名前に対応するスプライトです。
        /// 存在しない場合は<c>null</c>です。
        /// </returns>
        public Sprite GetIconSprite(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
            {
                return null;
            }

            _iconSpriteByName.TryGetValue(
                iconName,
                out Sprite iconSprite);

            return iconSprite;
        }

        /// <summary>
        /// 現在読み込まれているヘックスマップを取得します。
        /// </summary>
        /// <returns>
        /// ヘックスマップのディープコピーです。
        /// </returns>
        public List<List<int>> GetHexagonMap()
        {
            List<List<int>> copiedMap =
                new List<List<int>>(_hexagonMap.Count);

            foreach (List<int> row in _hexagonMap)
            {
                copiedMap.Add(new List<int>(row));
            }

            return copiedMap;
        }

        /// <summary>
        /// 読み込まれているゲーム設定データを取得します。
        /// </summary>
        /// <returns>
        /// ゲーム設定データです。
        /// 読み込まれていない場合は<c>null</c>です。
        /// </returns>
        public GameData GetGameData()
        {
            return _gameData;
        }

        /// <summary>
        /// 読み込まれているチュートリアルUIデータを取得します。
        /// </summary>
        /// <returns>
        /// チュートリアルUIデータです。
        /// 読み込まれていない場合は<c>null</c>です。
        /// </returns>
        public TutorialUiResources GetTutorialUiData()
        {
            return _tutorialUiData;
        }

        /// <summary>
        /// 現在設定されているボードデータを取得します。
        /// </summary>
        /// <returns>
        /// 現在使用しているボードデータです。
        /// 設定されていない場合は<c>null</c>です。
        /// </returns>
        public BoardData GetBoardData()
        {
            return _boardData;
        }

        /// <summary>
        /// 読み込まれているユニット設定データを取得します。
        /// </summary>
        /// <returns>
        /// ユニット設定データです。
        /// 読み込まれていない場合は<c>null</c>です。
        /// </returns>
        public UnitData GetUnitData()
        {
            return _unitData;
        }

        /// <summary>
        /// 指定されたキャラクターに設定された
        /// メイン兵装とサブ兵装のデータを取得します。
        /// </summary>
        /// <param name="characterName">
        /// 兵装構成を取得するキャラクターの名前です。
        /// </param>
        /// <returns>
        /// 先頭にメイン兵装、2番目にサブ兵装を格納した一覧です。
        /// キャラクターが存在しない場合は空の一覧を2つ返します。
        /// </returns>
        public List<List<PranamData>> GetPranams(
            string characterName)
        {
            UnitParams unitParams =
                GetCharacterData(characterName);

            if (unitParams == null)
            {
                return new List<List<PranamData>>
                {
                    new List<PranamData>(),
                    new List<PranamData>()
                };
            }

            List<PranamData> mainPranamData =
                GetPranamDataList(
                    unitParams.MainPranamSet.PranamNames);

            List<PranamData> subPranamData =
                GetPranamDataList(
                    unitParams.SubPranamSet.PranamNames);

            return new List<List<PranamData>>
            {
                mainPranamData,
                subPranamData
            };
        }

        /// <summary>
        /// 指定された兵装名の一覧を兵装データの一覧へ変換します。
        /// </summary>
        /// <param name="pranamNames">
        /// 取得する兵装名の一覧です。
        /// </param>
        /// <returns>
        /// 登録されている兵装データの一覧です。
        /// </returns>
        private List<PranamData> GetPranamDataList(
            IEnumerable<string> pranamNames)
        {
            List<PranamData> pranamDataList = new();

            if (pranamNames == null)
            {
                return pranamDataList;
            }

            foreach (string pranamName in pranamNames)
            {
                PranamData pranamData =
                    GetPranamData(pranamName);

                if (pranamData != null)
                {
                    pranamDataList.Add(pranamData);
                }
            }

            return pranamDataList;
        }

        /// <summary>
        /// 指定された名前に対応する兵装データを取得します。
        /// </summary>
        /// <param name="pranamName">
        /// 取得する兵装の名前です。
        /// </param>
        /// <returns>
        /// 指定された名前に対応する兵装データです。
        /// 存在しない場合は<c>null</c>です。
        /// </returns>
        public PranamData GetPranamData(string pranamName)
        {
            if (string.IsNullOrEmpty(pranamName))
            {
                return null;
            }

            if (_pranamDataByName.TryGetValue(
                    pranamName,
                    out PranamData pranamData))
            {
                return pranamData;
            }

            Debug.LogWarning(
                $"兵装データが登録されていません: {pranamName}",
                this);

            return null;
        }

        /// <summary>
        /// 読み込まれているデータと辞書を初期状態へ戻します。
        /// </summary>
        private void ClearLoadedData()
        {
            _characterDataByName.Clear();
            _iconSpriteByName.Clear();
            _pranamDataByName.Clear();
            _hexagonMap.Clear();

            _gameData = null;
            _boardData = null;
            _unitData = null;
        }

        /// <summary>
        /// 保持しているAddressablesのロードハンドルを解放します。
        /// </summary>
        private void ReleaseAddressableHandles()
        {
            if (_characterDataHandle.IsValid())
            {
                Addressables.Release(_characterDataHandle);
            }

            if (_iconDataHandle.IsValid())
            {
                Addressables.Release(_iconDataHandle);
            }

            if (_pranamDataHandle.IsValid())
            {
                Addressables.Release(_pranamDataHandle);
            }

            if (_gameDataHandle.IsValid())
            {
                Addressables.Release(_gameDataHandle);
            }

            if (_unitDataHandle.IsValid())
            {
                Addressables.Release(_unitDataHandle);
            }
        }
    }
}
