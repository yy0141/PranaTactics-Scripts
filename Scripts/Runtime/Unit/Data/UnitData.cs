using System;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Unit.Presentation;

namespace PranaTactics.Unit
{
    /// <summary>
    /// ユニット全体で共通して使用するPrefab、マテリアル、
    /// 移動設定および撃破演出設定を保持します。
    /// </summary>
    /// <remarks>
    /// キャラクター固有の能力値は<see cref="UnitParams"/>で管理し、
    /// 全ユニットで共通する設定をこのクラスで管理します。
    /// </remarks>
    [CreateAssetMenu(
    fileName = "UnitData",
    menuName = "Prana Tactics/Data/Unit Data",
    order = 1)]
    public sealed class UnitData : ScriptableObject
    {
        /// <summary>
        /// Macro Phaseで使用するユニットPrefabです。
        /// </summary>
        [Header("Unit Prefabs")]
        [FormerlySerializedAs("MOVE_UNIT")]
        [SerializeField]
        private GameObject _macroUnitPrefab;

        /// <summary>
        /// Micro Phaseで使用するユニットPrefabです。
        /// </summary>
        [FormerlySerializedAs("ACTION_UNIT")]
        [SerializeField]
        private GameObject _microUnitPrefab;

        /// <summary>
        /// 戦術マップ上で使用するユニットPrefabです。
        /// </summary>
        [FormerlySerializedAs("MAP_UNIT")]
        [SerializeField]
        private GameObject _tacticalMapUnitPrefab;

        /// <summary>
        /// ローカルプレイヤーと同じチームのユニットへ
        /// 適用するマテリアルです。
        /// </summary>
        [Header("Team Materials")]
        [FormerlySerializedAs("AllyMaterial")]
        [SerializeField]
        private Material _allyMaterial;

        /// <summary>
        /// ローカルプレイヤーと異なるチームのユニットへ
        /// 適用するマテリアルです。
        /// </summary>
        [FormerlySerializedAs("EnemyMaterial")]
        [SerializeField]
        private Material _enemyMaterial;

        /// <summary>
        /// Main Pranamの射程表示に使用するPrefabです。
        /// </summary>
        [Header("Pranam Range Prefabs")]
        [FormerlySerializedAs("MAIN_TRIGGER_RANGE_PRE")]
        [SerializeField]
        private GameObject _mainPranamRangePrefab;

        /// <summary>
        /// Sub Pranamの射程表示に使用するPrefabです。
        /// </summary>
        [FormerlySerializedAs("SUB_TRIGGER_RANGE_PRE")]
        [SerializeField]
        private GameObject _subPranamRangePrefab;

        /// <summary>
        /// 全Unitで共有する撃破演出設定です。
        /// </summary>
        [Header("Elimination")]
        [SerializeField]
        private UnitEliminationProfile _eliminationProfile;

        /// <summary>
        /// マクロ・フェイズ時の移動速度です。
        /// </summary>
        [Header("Movement")]
        [Min(0.0f)]
        [SerializeField]
        private float _macroUnitSpeed = 1.0f;

        /// <summary>
        /// マクロフェイズでAIが操作するユニットの移動速度です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _macroAiUnitSpeed = 1.0f;

        [FormerlySerializedAs("DashSpeedMultiplier")]
        [Min(0.0f)]
        [SerializeField]
        private float _dashSpeedMultiplier = 1.0f;

        /// <summary>
        /// 基本移動速度に対する歩行速度の倍率です。
        /// </summary>
        [FormerlySerializedAs("WalkSpeedMultiplier")]
        [Min(0.0f)]
        [SerializeField]
        private float _walkSpeedMultiplier = 1.0f;

        /// <summary>
        /// 1回の移動で上昇できる最大段差です。
        /// </summary>
        /// <remarks>
        /// 下降時の高さ制限には使用しません。
        /// </remarks>
        [FormerlySerializedAs("MaxClimbStep")]
        [Min(0)]
        [SerializeField]
        private int _maxClimbStep;

        /// <summary>
        /// ユニットカメラの基準高さです。
        /// </summary>
        [Header("Camera")]
        [FormerlySerializedAs("CAMERA_HEIGHT")]
        [Min(0.0f)]
        [SerializeField]
        private float _cameraHeight;

        /// <summary>
        /// 1マス移動するときに消費する積極性です。
        /// </summary>
        [Header("Action Cost")]
        [FormerlySerializedAs("MoveProactivenessCostCost")]
        [Min(0)]
        [SerializeField]
        private int _moveProactivenessCost = 1;

        public GameObject MacroUnitPrefab
        {
            get
            {
                return _macroUnitPrefab;
            }
        }

        public GameObject MicroUnitPrefab
        {
            get
            {
                return _microUnitPrefab;
            }
        }

        public GameObject TacticalMapUnitPrefab
        {
            get
            {
                return _tacticalMapUnitPrefab;
            }
        }

        public Material AllyMaterial
        {
            get
            {
                return _allyMaterial;
            }
        }

        public Material EnemyMaterial
        {
            get
            {
                return _enemyMaterial;
            }
        }

        public GameObject MainPranamRangePrefab
        {
            get
            {
                return _mainPranamRangePrefab;
            }
        }

        public GameObject SubPranamRangePrefab
        {
            get
            {
                return _subPranamRangePrefab;
            }
        }

        public UnitEliminationProfile EliminationProfile => _eliminationProfile;

        public float MacroUnitSpeed
        {
            get
            {
                return _macroUnitSpeed;
            }
        }

        public float MacroAiUnitSpeed
        {
            get
            {
                return _macroAiUnitSpeed;
            }
        }

        public float DashSpeedMultiplier
        {
            get
            {
                return _dashSpeedMultiplier;
            }
        }

        public float WalkSpeedMultiplier
        {
            get
            {
                return _walkSpeedMultiplier;
            }
        }

        public int MaxClimbStep
        {
            get
            {
                return _maxClimbStep;
            }
        }

        public float CameraHeight
        {
            get
            {
                return _cameraHeight;
            }
        }

        public int MoveProactivenessCostCost
        {
            get
            {
                return _moveProactivenessCost;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の数値設定を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _dashSpeedMultiplier =
                Mathf.Max(
                    0.0f,
                    _dashSpeedMultiplier);

            _walkSpeedMultiplier =
                Mathf.Max(
                    0.0f,
                    _walkSpeedMultiplier);

            _maxClimbStep =
                Mathf.Max(
                    0,
                    _maxClimbStep);

            _cameraHeight =
                Mathf.Max(
                    0.0f,
                    _cameraHeight);

            _moveProactivenessCost =
                Mathf.Max(
                    0,
                    _moveProactivenessCost);
        }
#endif
    }
}
