using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Board
{
    /// <summary>
    /// ボードの構成データ、ヘックスセルのPrefabおよび
    /// ボード生成時に使用する設定を保持します。
    /// </summary>
    [CreateAssetMenu(
    fileName = "BoardData",
    menuName = "Prana Tactics/Board/Board Data",
    order = 1)]

    public sealed class BoardData : ScriptableObject
    {
        /// <summary>
        /// ボードの配置情報を記述したJSONファイルです。
        /// </summary>
        [SerializeField]
        private TextAsset _boardJson;

        /// <summary>
        /// 高さ0のヘックスセルとして使用するPrefabです。
        /// </summary>
        [SerializeField]
        private GameObject _spaceLevelZeroPrefab;

        /// <summary>
        /// 高さ1のヘックスセルとして使用するPrefabです。
        /// </summary>
        [SerializeField]
        private GameObject _spaceLevelOnePrefab;

        /// <summary>
        /// 高さ2のヘックスセルとして使用するPrefabです。
        /// </summary>
        [SerializeField]
        private GameObject _spaceLevelTwoPrefab;

        /// <summary>
        /// 高さ3のヘックスセルとして使用するPrefabです。
        /// </summary>
        [SerializeField]
        private GameObject _spaceLevelThreePrefab;

        /// <summary>
        /// 高さ4のヘックスセルとして使用するPrefabです。
        /// </summary>
        [SerializeField]
        private GameObject _spaceLevelFourPrefab;

        /// <summary>
        /// 高さ5のヘックスセルとして使用するPrefabです。
        /// </summary>
        [SerializeField]
        private GameObject _spaceLevelFivePrefab;

        /// <summary>
        /// ヘックスセルの生成時に適用する基準スケールです。
        /// </summary>
        [Min(1)]
        [SerializeField]
        private int _scale = 1;

        /// <summary>
        /// ボード上の経路や境界線の表示に使用するPrefabです。
        /// </summary>
        [SerializeField]
        private GameObject _linePrefab;

        public TextAsset BoardJson
        {
            get
            {
                return _boardJson;
            }
        }

        public GameObject SpaceLevelZeroPrefab
        {
            get
            {
                return _spaceLevelZeroPrefab;
            }
        }

        public GameObject SpaceLevelOnePrefab
        {
            get
            {
                return _spaceLevelOnePrefab;
            }
        }

        public GameObject SpaceLevelTwoPrefab
        {
            get
            {
                return _spaceLevelTwoPrefab;
            }
        }

        public GameObject SpaceLevelThreePrefab
        {
            get
            {
                return _spaceLevelThreePrefab;
            }
        }

        public GameObject SpaceLevelFourPrefab
        {
            get
            {
                return _spaceLevelFourPrefab;
            }
        }

        public GameObject SpaceLevelFivePrefab
        {
            get
            {
                return _spaceLevelFivePrefab;
            }
        }

        public int Scale
        {
            get
            {
                return _scale;
            }
        }

        public GameObject LinePrefab
        {
            get
            {
                return _linePrefab;
            }
        }

        /// <summary>
        /// 指定された高さに対応するヘックスセルのPrefabを取得します。
        /// </summary>
        /// <param name="level">
        /// 取得するヘックスセルの高さです。
        /// </param>
        /// <returns>
        /// 指定された高さに対応するPrefabです。
        /// 対応する高さが存在しない場合は<c>null</c>です。
        /// </returns>
        public GameObject GetSpacePrefab(int level)
        {
            switch (level)
            {
                case -1:
                    return null;

                case 0:
                    return _spaceLevelZeroPrefab;

                case 1:
                    return _spaceLevelOnePrefab;

                case 2:
                    return _spaceLevelTwoPrefab;

                case 3:
                    return _spaceLevelThreePrefab;

                case 4:
                    return _spaceLevelFourPrefab;

                case 5:
                    return _spaceLevelFivePrefab;

                default:
                    Debug.LogWarning(
                        $"対応していないヘックスセルの高さが指定されました: {level}",
                        this);

                    return null;
            }
        }
    }
}
