using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Board.Networking;
using PranaTactics.Core;
using PranaTactics.UI.Gameplay;

namespace PranaTactics.Board
{
    /// <summary>
    /// ボードを構成するヘックスセルの生成、保持、検索および
    /// ミニマップカメラの配置を管理します。
    /// </summary>
    public sealed class BoardManager : MonoBehaviour
    {
        /// <summary>
        /// マクロ・フェイズのボードを配置するワールド座標のX方向オフセットです。
        /// </summary>
        private const float k_MacroPhaseBoardXOffset = -3000.0f;

        /// <summary>
        /// マップ用ボードを配置するワールド座標のX方向オフセットです。
        /// </summary>
        private const float k_MapBoardXOffset = 3000.0f;

        /// <summary>
        /// ヘックスセル間の水平方向の間隔に適用する倍率です。
        /// </summary>
        private const float k_HorizontalSpacingMultiplier = 0.95f;

        /// <summary>
        /// ヘックスセル間の垂直方向の間隔に適用する倍率です。
        /// </summary>
        private const float k_VerticalSpacingMultiplier = 1.8f;

        /// <summary>
        /// ヘックスセルの配置間隔を計算するために使用する角度です。
        /// </summary>
        private const float k_HexagonAngleDegrees = 60.0f;

        /// <summary>
        /// 生成するヘックスセルのY軸回転角度です。
        /// </summary>
        private const float k_HexagonRotationY = 90.0f;

        /// <summary>
        /// マクロ・フェイズ用ヘックスセルのY方向スケールです。
        /// </summary>
        private const float k_MacroPhaseCellHeightScale = 0.001f;

        /// <summary>
        /// マップ用ヘックスセルのY方向スケールです。
        /// </summary>
        private const float k_MapCellHeightScale = 5.0f;

        /// <summary>
        /// ミニマップカメラのX軸回転角度です。
        /// </summary>
        private const float k_MinimapCameraRotationX = 90.0f;

        /// <summary>
        /// ミニマップ全体を画面内に収めるための余白倍率です。
        /// </summary>
        private const float k_MinimapCameraMarginMultiplier = 1.05f;

        /// <summary>
        /// マクロ・フェイズおよびミクロ・フェイズのセル名に使用する区切り文字です。
        /// </summary>
        private const string k_PhaseCellNameSeparator = "/";

        /// <summary>
        /// マップ用セル名に使用する区切り文字です。
        /// </summary>
        private const string k_MapCellNameSeparator = "|";

        /// <summary>
        /// 各ヘックスセルから隣接する6方向への座標差分です。
        /// </summary>
        private static readonly Vector2Int[] s_AdjacentCoordinateOffsets =
        {
            new Vector2Int(0, -2),
            new Vector2Int(-1, -1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(1, 1),
            new Vector2Int(0, 2)
        };

        /// <summary>
        /// ボードの構成情報とヘックスセルのPrefabを保持するデータです。
        /// </summary>
        [SerializeField]
        private BoardData _boardDataAsset;

        /// <summary>
        /// マクロ・フェイズ用ヘックスセルの親オブジェクトです。
        /// </summary>
        [SerializeField]
        private GameObject _macroPhaseHexagonRoot;

        /// <summary>
        /// ミクロ・フェイズ用ヘックスセルの親オブジェクトです。
        /// </summary>
        [SerializeField]
        private GameObject _microPhaseHexagonRoot;

        /// <summary>
        /// マップ用ヘックスセルの親オブジェクトです。
        /// </summary>
        [SerializeField]
        private GameObject _mapHexagonRoot;

        /// <summary>
        /// マップ用ヘックスセルとして生成するPrefabです。
        /// </summary>
        [SerializeField]
        private GameObject _mapHexagonPrefab;

        /// <summary>
        /// ミニマップの描画に使用するカメラオブジェクトです。
        /// </summary>
        [SerializeField]
        private GameObject _minimapCameraObject;

        /// <summary>
        /// ボード生成処理を他のクライアントと同期するネットワーク実装です。
        /// </summary>
        private IBoardNetwork _network;

        /// <summary>
        /// 現在使用しているボードデータです。
        /// </summary>
        private BoardData _boardData;

        /// <summary>
        /// ボードの中央に最も近いヘックスセルの座標です。
        /// </summary>
        private Vector2Int _centerCoordinate;

        /// <summary>
        /// 各座標に設定されている床の高さを保持する二次元マップです。
        /// </summary>
        private List<List<int>> _hexagonMap = new();

        /// <summary>
        /// マクロ・フェイズ用ヘックスセルを座標ごとに保持します。
        /// </summary>
        private readonly Dictionary<Vector2Int, HexCell>
            _macroPhaseHexCells = new();

        private Bounds _macroCameraBounds;

        private bool _hasMacroCameraBounds;

        /// <summary>
        /// ミクロ・フェイズ用ヘックスセルを座標ごとに保持します。
        /// </summary>
        private readonly Dictionary<Vector2Int, HexCell>
            _microPhaseHexCells = new();

        /// <summary>
        /// マップ用ヘックスセルを座標ごとに保持します。
        /// </summary>
        private readonly Dictionary<Vector2Int, HexCell>
            _mapHexCells = new();

        public static BoardManager Instance
        {
            get; private set;
        }

        public event Action BoardCreated;

        public BoardTrapManager Traps { get; private set; }

        /// <summary>
        /// BoardManagerのシングルトンインスタンスを設定します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;
            Traps = GetComponent<BoardTrapManager>();
            if (Traps == null)
            {
                Traps = gameObject.AddComponent<BoardTrapManager>();
            }
            if (GetComponent<MovementPathView>() == null)
            {
                gameObject.AddComponent<MovementPathView>();
            }
        }

        /// <summary>
        /// このインスタンスが破棄されたとき、
        /// シングルトンに保持されている参照を解除します。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// ボード生成に使用するネットワーク実装とボードデータを初期化します。
        /// </summary>
        /// <param name="network">
        /// ボード生成処理を同期するネットワーク実装です。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="network"/>が<c>null</c>の場合に発生します。
        /// </exception>
        public void Initialize(IBoardNetwork network)
        {
            if (network == null)
            {
                throw new ArgumentNullException(nameof(network));
            }

            _network = network;

            GameDataRepository.Instance.SetBoardData(_boardDataAsset);

            _boardData = GameDataRepository.Instance.GetBoardData();
            _hexagonMap = GameDataRepository.Instance.GetHexagonMap();
        }

        /// <summary>
        /// ボード中央に最も近いヘックスセルの座標を取得します。
        /// </summary>
        /// <returns>
        /// ボード中央に最も近いヘックスセルの座標です。
        /// </returns>
        public Vector2Int GetCenterCoordinate()
        {
            return _centerCoordinate;
        }

        /// <summary>
        /// ネットワークを通してボード生成を要求します。
        /// </summary>
        public void RequestBoardCreation()
        {
            if (_network == null)
            {
                Debug.LogWarning(
                    $"{nameof(BoardManager)}が初期化されていません。",
                    this);

                return;
            }

            _network.CreateBoard();
        }

        /// <summary>
        /// 現在生成されているすべてのヘックスセルを削除します。
        /// </summary>
        private void ClearBoard()
        {
            Traps?.ClearTraps();
            _hasMacroCameraBounds = false;
            DestroyChildren(_macroPhaseHexagonRoot);
            DestroyChildren(_microPhaseHexagonRoot);
            DestroyChildren(_mapHexagonRoot);

            _macroPhaseHexCells.Clear();
            _microPhaseHexCells.Clear();
            _mapHexCells.Clear();
        }

        /// <summary>
        /// 指定された親オブジェクトの子オブジェクトをすべて削除します。
        /// </summary>
        /// <param name="parentObject">
        /// 子オブジェクトを削除する親オブジェクトです。
        /// </param>
        private void DestroyChildren(GameObject parentObject)
        {
            if (parentObject == null)
            {
                return;
            }

            foreach (Transform child in parentObject.transform)
            {
                Destroy(child.gameObject);
            }
        }

        /// <summary>
        /// 読み込まれているボードデータに従って、
        /// マクロ・フェイズ、ミクロ・フェイズおよびミニマップ用のボードを生成します。
        /// </summary>
        public void CreateBoard()
        {
            if (!IsBoardConfigurationValid())
            {
                return;
            }

            ClearBoard();

            int height = _hexagonMap.Count;
            int width = _hexagonMap[0].Count;

            float horizontalDistance =
                _boardData.Scale *
                k_HorizontalSpacingMultiplier;

            float verticalDistance =
                _boardData.Scale *
                Mathf.Sin(k_HexagonAngleDegrees) *
                k_VerticalSpacingMultiplier;

            for (int yIndex = 0; yIndex < height; yIndex++)
            {
                for (int xIndex = 0; xIndex < width; xIndex++)
                {
                    int floor = _hexagonMap[yIndex][xIndex];
                    GameObject boardPrefab = GetBoardPrefab(floor);

                    if (boardPrefab == null)
                    {
                        continue;
                    }

                    Vector2Int coordinate =
                        new Vector2Int(
                            xIndex + 1,
                            yIndex + 1);

                    float positionX =
                        horizontalDistance * xIndex;

                    float positionZ =
                        -verticalDistance * yIndex;

                    CreateMacroPhaseHexCell(
                        boardPrefab,
                        coordinate,
                        floor,
                        positionX,
                        positionZ);

                    CreateMicroPhaseHexCell(
                        boardPrefab,
                        coordinate,
                        floor,
                        positionX,
                        positionZ);

                    CreateMapHexCell(
                        coordinate,
                        floor,
                        positionX,
                        positionZ);
                }
            }

            _centerCoordinate = CalculateCenterCoordinate();

            SetMinimapCamera();

            BoardCreated?.Invoke();
        }

        /// <summary>
        /// ボード生成に必要なデータと参照が設定されているか確認します。
        /// </summary>
        /// <returns>
        /// ボードを生成できる場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        private bool IsBoardConfigurationValid()
        {
            if (_boardData == null)
            {
                Debug.LogWarning(
                    "BoardDataが読み込まれていません。",
                    this);

                return false;
            }

            if (_hexagonMap == null ||
                _hexagonMap.Count == 0 ||
                _hexagonMap[0] == null ||
                _hexagonMap[0].Count == 0)
            {
                Debug.LogWarning(
                    "ヘックスマップのデータが設定されていません。",
                    this);

                return false;
            }

            int width = _hexagonMap[0].Count;

            foreach (List<int> row in _hexagonMap)
            {
                if (row == null || row.Count != width)
                {
                    Debug.LogWarning(
                        "ヘックスマップの各行の要素数が一致していません。",
                        this);

                    return false;
                }
            }

            if (_macroPhaseHexagonRoot == null ||
                _microPhaseHexagonRoot == null ||
                _mapHexagonRoot == null)
            {
                Debug.LogWarning(
                    "ヘックスセルの親オブジェクトが設定されていません。",
                    this);

                return false;
            }

            if (_mapHexagonPrefab == null)
            {
                Debug.LogWarning(
                    "ミニマップ用ヘックスセルのPrefabが設定されていません。",
                    this);

                return false;
            }

            return true;
        }

        /// <summary>
        /// マクロ・フェイズ用のヘックスセルを生成します。
        /// </summary>
        /// <param name="prefab">
        /// 生成するヘックスセルのPrefabです。
        /// </param>
        /// <param name="coordinate">
        /// ヘックスマップ上の座標です。
        /// </param>
        /// <param name="floor">
        /// セルに設定する床の高さです。
        /// </param>
        /// <param name="positionX">
        /// 基準となるX座標です。
        /// </param>
        /// <param name="positionZ">
        /// セルを配置するZ座標です。
        /// </param>
        private void CreateMacroPhaseHexCell(
            GameObject prefab,
            Vector2Int coordinate,
            int floor,
            float positionX,
            float positionZ)
        {
            Vector3 position =
                new Vector3(
                    k_MacroPhaseBoardXOffset - positionX,
                    0.0f,
                    positionZ);

            Vector3 scale =
                new Vector3(
                    _boardData.Scale,
                    k_MacroPhaseCellHeightScale,
                    _boardData.Scale);

            HexCell cell = CreateHexCell(
                prefab,
                _macroPhaseHexagonRoot.transform,
                coordinate,
                floor,
                position,
                scale,
                k_PhaseCellNameSeparator);

            if (cell != null)
            {
                _macroPhaseHexCells.Add(coordinate, cell);
            }
        }

        /// <summary>
        /// ミクロ・フェイズ用のヘックスセルを生成します。
        /// </summary>
        /// <param name="prefab">
        /// 生成するヘックスセルのPrefabです。
        /// </param>
        /// <param name="coordinate">
        /// ヘックスマップ上の座標です。
        /// </param>
        /// <param name="floor">
        /// セルに設定する床の高さです。
        /// </param>
        /// <param name="positionX">
        /// 基準となるX座標です。
        /// </param>
        /// <param name="positionZ">
        /// セルを配置するZ座標です。
        /// </param>
        private void CreateMicroPhaseHexCell(
            GameObject prefab,
            Vector2Int coordinate,
            int floor,
            float positionX,
            float positionZ)
        {
            Vector3 position =
                new Vector3(
                    -positionX,
                    0.0f,
                    positionZ);

            Vector3 scale =
                new Vector3(
                    _boardData.Scale,
                    prefab.transform.localScale.y,
                    _boardData.Scale);

            HexCell cell = CreateHexCell(
                prefab,
                _microPhaseHexagonRoot.transform,
                coordinate,
                floor,
                position,
                scale,
                k_PhaseCellNameSeparator);

            if (cell != null)
            {
                _microPhaseHexCells.Add(coordinate, cell);
            }
        }

        /// <summary>
        /// ミニマップ用のヘックスセルを生成します。
        /// </summary>
        /// <param name="coordinate">
        /// ヘックスマップ上の座標です。
        /// </param>
        /// <param name="floor">
        /// セルに設定する床の高さです。
        /// </param>
        /// <param name="positionX">
        /// 基準となるX座標です。
        /// </param>
        /// <param name="positionZ">
        /// セルを配置するZ座標です。
        /// </param>
        private void CreateMapHexCell(
            Vector2Int coordinate,
            int floor,
            float positionX,
            float positionZ)
        {
            Vector3 position =
                new Vector3(
                    k_MapBoardXOffset - positionX,
                    0.0f,
                    positionZ);

            Vector3 scale =
                new Vector3(
                    _boardData.Scale,
                    k_MapCellHeightScale,
                    _boardData.Scale);

            HexCell cell = CreateHexCell(
                _mapHexagonPrefab,
                _mapHexagonRoot.transform,
                coordinate,
                floor,
                position,
                scale,
                k_MapCellNameSeparator);

            if (cell != null)
            {
                _mapHexCells.Add(coordinate, cell);
            }
        }

        /// <summary>
        /// 指定された情報に従ってヘックスセルを生成し、初期化します。
        /// </summary>
        /// <param name="prefab">
        /// 生成するヘックスセルのPrefabです。
        /// </param>
        /// <param name="parent">
        /// 生成したヘックスセルを配置する親Transformです。
        /// </param>
        /// <param name="coordinate">
        /// ヘックスマップ上の座標です。
        /// </param>
        /// <param name="floor">
        /// セルに設定する床の高さです。
        /// </param>
        /// <param name="position">
        /// セルを配置するワールド座標です。
        /// </param>
        /// <param name="scale">
        /// セルに設定するローカルスケールです。
        /// </param>
        /// <param name="nameSeparator">
        /// セル名の座標間に使用する区切り文字です。
        /// </param>
        /// <returns>
        /// 生成されたHexCellです。
        /// HexCellがPrefabに存在しない場合は<c>null</c>です。
        /// </returns>
        private HexCell CreateHexCell(
            GameObject prefab,
            Transform parent,
            Vector2Int coordinate,
            int floor,
            Vector3 position,
            Vector3 scale,
            string nameSeparator)
        {
            GameObject cellObject = Instantiate(
                prefab,
                position,
                Quaternion.Euler(
                    0.0f,
                    k_HexagonRotationY,
                    0.0f),
                parent);

            cellObject.transform.localScale = scale;
            cellObject.name =
                $"{coordinate.x}{nameSeparator}{coordinate.y}";

            if (!cellObject.TryGetComponent(
                    out HexCell cell))
            {
                Debug.LogWarning(
                    $"{prefab.name}に{nameof(HexCell)}が設定されていません。",
                    prefab);

                Destroy(cellObject);

                return null;
            }

            cell.SetFloor(floor);
            cell.SetCoordinate(
                coordinate.x,
                coordinate.y);

            return cell;
        }

        /// <summary>
        /// マクロ・フェイズ用ボードの幾何学的中心に最も近いセル座標を計算します。
        /// </summary>
        /// <returns>
        /// ボードの中心に最も近いセル座標です。
        /// セルが存在しない場合は<see cref="Vector2Int.zero"/>です。
        /// </returns>
        private Vector2Int CalculateCenterCoordinate()
        {
            if (_macroPhaseHexCells.Count == 0)
            {
                return Vector2Int.zero;
            }

            Vector3 centerPosition = Vector3.zero;

            foreach (
                KeyValuePair<Vector2Int, HexCell> pair
                in _macroPhaseHexCells)
            {
                centerPosition += pair.Value.transform.position;
            }

            centerPosition /= _macroPhaseHexCells.Count;

            float minimumDistance = float.MaxValue;
            Vector2Int closestCoordinate = Vector2Int.zero;

            foreach (
                KeyValuePair<Vector2Int, HexCell> pair
                in _macroPhaseHexCells)
            {
                float distance =
                    Vector3.SqrMagnitude(
                        pair.Value.transform.position -
                        centerPosition);

                if (distance >= minimumDistance)
                {
                    continue;
                }

                minimumDistance = distance;
                closestCoordinate = pair.Key;
            }

            return closestCoordinate;
        }

        /// <summary>
        /// Macro盤面のセル端を含む描画範囲を取得し、カメラ用にキャッシュします。
        /// </summary>
        public bool TryGetMacroCameraBounds(out Bounds bounds)
        {
            if (!_hasMacroCameraBounds)
            {
                foreach (HexCell cell in _macroPhaseHexCells.Values)
                {
                    if (cell == null)
                    {
                        continue;
                    }

                    foreach (Renderer renderer in cell.GetComponentsInChildren<Renderer>())
                    {
                        if (!_hasMacroCameraBounds)
                        {
                            _macroCameraBounds = renderer.bounds;
                            _hasMacroCameraBounds = true;
                        }
                        else
                        {
                            _macroCameraBounds.Encapsulate(renderer.bounds);
                        }
                    }
                }
            }

            bounds = _macroCameraBounds;

            return _hasMacroCameraBounds;
        }

        /// <summary>
        /// ミニマップ全体が画面内に収まるようにカメラの位置と向きを設定します。
        /// </summary>
        private void SetMinimapCamera()
        {
            if (_mapHexCells.Count == 0)
            {
                Debug.LogWarning(
                    "ミニマップ用ヘックスセルが生成されていません。",
                    this);

                return;
            }

            if (_minimapCameraObject == null)
            {
                Debug.LogWarning(
                    "ミニマップ用カメラが設定されていません。",
                    this);

                return;
            }

            if (!_minimapCameraObject.TryGetComponent(
                    out Camera mapCamera))
            {
                Debug.LogWarning(
                    "ミニマップ用オブジェクトにCameraが設定されていません。",
                    _minimapCameraObject);

                return;
            }

            Vector3 centerPosition = Vector3.zero;

            float minimumX = float.MaxValue;
            float maximumX = float.MinValue;
            float minimumZ = float.MaxValue;
            float maximumZ = float.MinValue;

            foreach (HexCell cell in _mapHexCells.Values)
            {
                Vector3 cellPosition = cell.transform.position;

                centerPosition += cellPosition;

                minimumX = Mathf.Min(
                    minimumX,
                    cellPosition.x);

                maximumX = Mathf.Max(
                    maximumX,
                    cellPosition.x);

                minimumZ = Mathf.Min(
                    minimumZ,
                    cellPosition.z);

                maximumZ = Mathf.Max(
                    maximumZ,
                    cellPosition.z);
            }

            centerPosition /= _mapHexCells.Count;

            float boardWidth = maximumX - minimumX;
            float boardHeight = maximumZ - minimumZ;

            float verticalFieldOfViewRadians =
                mapCamera.fieldOfView *
                Mathf.Deg2Rad;

            float verticalDistance =
                boardHeight /
                (2.0f *
                Mathf.Tan(verticalFieldOfViewRadians / 2.0f));

            float horizontalDistance =
                boardWidth /
                (2.0f *
                Mathf.Tan(verticalFieldOfViewRadians / 2.0f) *
                mapCamera.aspect);

            float cameraDistance =
                Mathf.Max(
                    verticalDistance,
                    horizontalDistance) *
                k_MinimapCameraMarginMultiplier;

            _minimapCameraObject.transform.position =
                centerPosition +
                Vector3.up * cameraDistance;

            _minimapCameraObject.transform.rotation =
                Quaternion.Euler(
                    k_MinimapCameraRotationX,
                    0.0f,
                    0.0f);
        }

        /// <summary>
        /// 指定された床の高さに対応するヘックスセルのPrefabを取得します。
        /// </summary>
        /// <param name="floor">
        /// 取得するヘックスセルの床の高さです。
        /// </param>
        /// <returns>
        /// 床の高さに対応するPrefabです。
        /// 対応するPrefabがない場合は<c>null</c>です。
        /// </returns>
        public GameObject GetBoardPrefab(int floor)
        {
            return _boardData.GetSpacePrefab(floor);
        }

        /// <summary>
        /// 指定された座標にヘックスセルが存在するか確認します。
        /// </summary>
        /// <param name="x">
        /// 確認するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 確認するセルのY座標です。
        /// </param>
        /// <returns>
        /// セルが存在する場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        public bool ContainsHexCell(int x, int y)
        {
            return _macroPhaseHexCells.ContainsKey(
                new Vector2Int(x, y));
        }

        /// <summary>
        /// ボードの横方向のセル数を取得します。
        /// </summary>
        /// <returns>
        /// ボードの横方向のセル数です。
        /// マップが存在しない場合は0です。
        /// </returns>
        public int GetBoardWidth()
        {
            if (_hexagonMap == null ||
                _hexagonMap.Count == 0 ||
                _hexagonMap[0] == null)
            {
                return 0;
            }

            return _hexagonMap[0].Count;
        }

        /// <summary>
        /// ボードの縦方向のセル数を取得します。
        /// </summary>
        /// <returns>
        /// ボードの縦方向のセル数です。
        /// </returns>
        public int GetBoardHeight()
        {
            return _hexagonMap?.Count ?? 0;
        }

        /// <summary>
        /// 指定された座標のマクロ・フェイズ用ヘックスセルを取得します。
        /// </summary>
        /// <param name="x">
        /// 取得するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 取得するセルのY座標です。
        /// </param>
        /// <returns>
        /// 指定された座標のHexCellです。
        /// セルが存在しない場合は<c>null</c>です。
        /// </returns>
        public HexCell GetMacroPhaseHexCell(int x, int y)
        {
            _macroPhaseHexCells.TryGetValue(
                new Vector2Int(x, y),
                out HexCell cell);

            return cell;
        }

        /// <summary>
        /// 指定された座標のミクロ・フェイズ用ヘックスセルを取得します。
        /// </summary>
        /// <param name="x">
        /// 取得するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 取得するセルのY座標です。
        /// </param>
        /// <returns>
        /// 指定された座標のHexCellです。
        /// セルが存在しない場合は<c>null</c>です。
        /// </returns>
        public HexCell GetMicroPhaseHexCell(int x, int y)
        {
            _microPhaseHexCells.TryGetValue(
                new Vector2Int(x, y),
                out HexCell cell);

            return cell;
        }

        /// <summary>
        /// 指定された座標のミニマップ用ヘックスセルを取得します。
        /// </summary>
        /// <param name="x">
        /// 取得するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 取得するセルのY座標です。
        /// </param>
        /// <returns>
        /// 指定された座標のHexCellです。
        /// セルが存在しない場合は<c>null</c>です。
        /// </returns>
        public HexCell GetMapHexCell(int x, int y)
        {
            _mapHexCells.TryGetValue(
                new Vector2Int(x, y),
                out HexCell cell);

            return cell;
        }

        /// <summary>
        /// ミニマップの描画に使用するカメラオブジェクトを取得します。
        /// </summary>
        /// <returns>
        /// ミニマップ用カメラオブジェクトです。
        /// </returns>
        public GameObject GetMinimapCameraObject()
        {
            return _minimapCameraObject;
        }

        /// <summary>
        /// 指定された座標のマクロ・フェイズ用セルの位置を取得します。
        /// </summary>
        /// <param name="x">
        /// 取得するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 取得するセルのY座標です。
        /// </param>
        /// <returns>
        /// セルのワールド座標です。
        /// セルが存在しない場合は<see cref="Vector3.zero"/>です。
        /// </returns>
        public Vector3 GetMacroPhaseHexCellPosition(int x, int y)
        {
            return GetHexCellPosition(
                _macroPhaseHexCells,
                new Vector2Int(x, y));
        }

        /// <summary>
        /// 指定された座標のミクロ・フェイズ用セルの位置を取得します。
        /// </summary>
        /// <param name="x">
        /// 取得するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 取得するセルのY座標です。
        /// </param>
        /// <returns>
        /// セルのワールド座標です。
        /// セルが存在しない場合は<see cref="Vector3.zero"/>です。
        /// </returns>
        public Vector3 GetMicroPhaseHexCellPosition(int x, int y)
        {
            return GetHexCellPosition(
                _microPhaseHexCells,
                new Vector2Int(x, y));
        }

        /// <summary>
        /// 指定された座標のミニマップ用セルの位置を取得します。
        /// </summary>
        /// <param name="x">
        /// 取得するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 取得するセルのY座標です。
        /// </param>
        /// <returns>
        /// セルのワールド座標です。
        /// セルが存在しない場合は<see cref="Vector3.zero"/>です。
        /// </returns>
        public Vector3 GetMapHexCellPosition(int x, int y)
        {
            return GetHexCellPosition(
                _mapHexCells,
                new Vector2Int(x, y));
        }

        /// <summary>
        /// 指定された辞書からヘックスセルのワールド座標を取得します。
        /// </summary>
        /// <param name="cells">
        /// ヘックスセルを座標ごとに保持する辞書です。
        /// </param>
        /// <param name="coordinate">
        /// 取得するヘックスセルの座標です。
        /// </param>
        /// <returns>
        /// ヘックスセルのワールド座標です。
        /// セルが存在しない場合は<see cref="Vector3.zero"/>です。
        /// </returns>
        private Vector3 GetHexCellPosition(
            IReadOnlyDictionary<Vector2Int, HexCell> cells,
            Vector2Int coordinate)
        {
            if (!cells.TryGetValue(
                    coordinate,
                    out HexCell cell))
            {
                return Vector3.zero;
            }

            return cell.transform.position;
        }

        /// <summary>
        /// 指定された座標のマクロ・フェイズ用セルのスケールを取得します。
        /// </summary>
        /// <param name="x">
        /// 取得するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 取得するセルのY座標です。
        /// </param>
        /// <returns>
        /// セルのワールドスケールです。
        /// セルが存在しない場合は<see cref="Vector3.zero"/>です。
        /// </returns>
        public Vector3 GetMacroPhaseHexCellScale(int x, int y)
        {
            return GetHexCellScale(
                _macroPhaseHexCells,
                new Vector2Int(x, y));
        }

        /// <summary>
        /// 指定された座標のミクロ・フェイズ用セルのスケールを取得します。
        /// </summary>
        /// <param name="x">
        /// 取得するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 取得するセルのY座標です。
        /// </param>
        /// <returns>
        /// セルのワールドスケールです。
        /// セルが存在しない場合は<see cref="Vector3.zero"/>です。
        /// </returns>
        public Vector3 GetMicroPhaseHexCellScale(int x, int y)
        {
            return GetHexCellScale(
                _microPhaseHexCells,
                new Vector2Int(x, y));
        }

        /// <summary>
        /// 指定された座標のミニマップ用セルのスケールを取得します。
        /// </summary>
        /// <param name="x">
        /// 取得するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 取得するセルのY座標です。
        /// </param>
        /// <returns>
        /// セルのワールドスケールです。
        /// セルが存在しない場合は<see cref="Vector3.zero"/>です。
        /// </returns>
        public Vector3 GetMapHexCellScale(int x, int y)
        {
            return GetHexCellScale(
                _mapHexCells,
                new Vector2Int(x, y));
        }

        /// <summary>
        /// 指定された辞書からヘックスセルのワールドスケールを取得します。
        /// </summary>
        /// <param name="cells">
        /// ヘックスセルを座標ごとに保持する辞書です。
        /// </param>
        /// <param name="coordinate">
        /// 取得するヘックスセルの座標です。
        /// </param>
        /// <returns>
        /// ヘックスセルのワールドスケールです。
        /// セルが存在しない場合は<see cref="Vector3.zero"/>です。
        /// </returns>
        private Vector3 GetHexCellScale(
            IReadOnlyDictionary<Vector2Int, HexCell> cells,
            Vector2Int coordinate)
        {
            if (!cells.TryGetValue(
                    coordinate,
                    out HexCell cell))
            {
                return Vector3.zero;
            }

            return cell.transform.lossyScale;
        }

        /// <summary>
        /// 指定されたセルが移動可能な状態か確認します。
        /// </summary>
        /// <param name="x">
        /// 確認するセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 確認するセルのY座標です。
        /// </param>
        /// <returns>
        /// セルのアウトラインが有効な場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        public bool IsMovable(int x, int y)
        {
            HexCell cell = GetMacroPhaseHexCell(x, y);

            return cell != null &&
                cell.IsOutlineEnabled();
        }

        /// <summary>
        /// 指定された座標に隣接する6方向の座標を取得します。
        /// </summary>
        /// <param name="x">
        /// 基準となるセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 基準となるセルのY座標です。
        /// </param>
        /// <returns>
        /// 基準座標に隣接する6つの座標です。
        /// ボード外の座標が含まれる場合があります。
        /// </returns>
        public List<Vector2Int> GetAdjacentHexCellCoordinates(
            int x,
            int y)
        {
            Vector2Int origin = new Vector2Int(x, y);

            List<Vector2Int> adjacentCoordinates =
                new List<Vector2Int>(
                    s_AdjacentCoordinateOffsets.Length);

            foreach (
                Vector2Int offset
                in s_AdjacentCoordinateOffsets)
            {
                adjacentCoordinates.Add(origin + offset);
            }

            return adjacentCoordinates;
        }

        /// <summary>
        /// 指定されたマクロ・フェイズ用ヘックスセルのアウトラインを有効にします。
        /// </summary>
        /// <param name="x">
        /// 対象となるセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 対象となるセルのY座標です。
        /// </param>
        public void EnableHexCellOutline(int x, int y)
        {
            HexCell cell = GetMacroPhaseHexCell(x, y);

            if (cell == null)
            {
                return;
            }

            cell.SetOutlineEnabled(true);
        }

        /// <summary>
        /// 指定されたマクロ・フェイズ用ヘックスセルのアウトラインを無効にします。
        /// </summary>
        /// <param name="x">
        /// 対象となるセルのX座標です。
        /// </param>
        /// <param name="y">
        /// 対象となるセルのY座標です。
        /// </param>
        public void DisableHexCellOutline(int x, int y)
        {
            HexCell cell = GetMacroPhaseHexCell(x, y);

            if (cell == null)
            {
                return;
            }

            cell.SetOutlineEnabled(false);
        }
    }
}
