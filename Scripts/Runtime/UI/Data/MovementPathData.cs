using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.UI.Gameplay
{
    public enum MovementPathDirectionShape
    {
        Triangle,
        Chevron
    }

    public enum MovementPathGoalShape
    {
        Hexagon,
        Circle
    }

    /// <summary>
    /// 経路本体、方向マーカー、目的地とアニメーションの表示設定を保持します。
    /// </summary>
    [CreateAssetMenu(fileName = "MovementPathData", menuName = "Prana Tactics/Data/Movement Path Data")]
    public sealed class MovementPathData : ScriptableObject
    {
        /// <summary>
        /// 操作盤面の線幅倍率です。
        /// </summary>
        [Tooltip("操作盤面の線幅倍率です。")]
        [Header("Route Line")]
        [SerializeField, Range(0.25f, 4.0f)]
        private float _macroLineWidthMultiplier = 1.0f;

        /// <summary>
        /// 共用マップの線幅倍率です。
        /// </summary>
        [Tooltip("共用マップの線幅倍率です。")]
        [SerializeField, Range(0.25f, 4.0f)]
        private float _mapLineWidthMultiplier = 1.0f;

        /// <summary>
        /// 内側ラインの基本ピクセル幅です。
        /// </summary>
        [Tooltip("内側ラインの基本ピクセル幅です。")]
        [SerializeField, Min(1.0f)]
        private float _lineWidthPixels = 4.0f;

        /// <summary>
        /// 外側アウトラインの基本ピクセル幅です。
        /// </summary>
        [Tooltip("外側アウトラインの基本ピクセル幅です。")]
        [SerializeField, Min(1.0f)]
        private float _outlineWidthPixels = 8.0f;

        /// <summary>
        /// ラインとマーカーのアウトライン色です。
        /// </summary>
        [Tooltip("ラインとマーカーのアウトライン色です。")]
        [SerializeField]
        private Color _outlineColor = new Color(0.015f, 0.025f, 0.04f, 1.0f);

        /// <summary>
        /// 設定画面で保存した線と方向マーカーの色を使用するかです。
        /// </summary>
        [Tooltip("設定画面で保存した線と方向マーカーの色を使用するかです。")]
        [SerializeField]
        private bool _useSavedColors = true;

        /// <summary>
        /// 保存色を使わない場合のライン色です。
        /// </summary>
        [Tooltip("保存色を使わない場合のライン色です。")]
        [SerializeField]
        private Color _defaultLineColor = new Color(0.2f, 0.8f, 1.0f, 1.0f);

        /// <summary>
        /// 保存色を使わない場合の方向マーカー色です。
        /// </summary>
        [Tooltip("保存色を使わない場合の方向マーカー色です。")]
        [SerializeField]
        private Color _defaultDirectionColor = Color.white;

        /// <summary>
        /// 開始地点の円の基本半径です。
        /// </summary>
        [Tooltip("開始地点の円の基本半径です。")]
        [FormerlySerializedAs("_markerRadiusPixels")]
        [Header("Start and Goal")]
        [SerializeField, Min(1.0f)]
        private float _startRadiusPixels = 5.0f;

        /// <summary>
        /// 到達地点の基本半径です。
        /// </summary>
        [Tooltip("到達地点の基本半径です。")]
        [SerializeField, Min(1.0f)]
        private float _goalRadiusPixels = 11.0f;

        /// <summary>
        /// 到達地点を表示する形です。
        /// </summary>
        [Tooltip("到達地点を表示する形です。")]
        [SerializeField]
        private MovementPathGoalShape _goalShape = MovementPathGoalShape.Hexagon;

        /// <summary>
        /// 拡大時にユニット番号を表示するかです。
        /// </summary>
        [Tooltip("拡大時にユニット番号を表示するかです。")]
        [SerializeField]
        private bool _showUnitLabels = true;

        /// <summary>
        /// 番号を表示する最小のセル投影サイズです。
        /// </summary>
        [Tooltip("番号を表示する最小のセル投影サイズです。")]
        [SerializeField, Min(1.0f)]
        private float _labelMinimumCellPixels = 100.0f;

        /// <summary>
        /// 方向マーカーの画面上の長さです。
        /// </summary>
        [Tooltip("方向マーカーの画面上の長さです。")]
        [FormerlySerializedAs("_arrowLengthPixels")]
        [Header("Direction Markers")]
        [SerializeField, Min(4.0f)]
        private float _directionMarkerLengthPixels = 20.0f;

        /// <summary>
        /// 方向マーカーの画面上の幅です。
        /// </summary>
        [Tooltip("方向マーカーの画面上の幅です。")]
        [FormerlySerializedAs("_arrowWidthPixels")]
        [SerializeField, Min(4.0f)]
        private float _directionMarkerWidthPixels = 18.0f;

        /// <summary>
        /// 方向マーカーの形です。
        /// </summary>
        [Tooltip("方向マーカーの形です。")]
        [SerializeField]
        private MovementPathDirectionShape _directionShape = MovementPathDirectionShape.Triangle;

        /// <summary>
        /// 進行方向マーカーを表示するかです。
        /// </summary>
        [Tooltip("進行方向マーカーを表示するかです。")]
        [SerializeField]
        private bool _showDirectionMarkers = true;

        /// <summary>
        /// 近景として扱う最小のセル投影サイズです。
        /// </summary>
        [Tooltip("近景として扱う最小のセル投影サイズです。")]
        [Header("Distance and Density")]
        [SerializeField, Min(1.0f)]
        private float _nearCellPixels = 120.0f;

        /// <summary>
        /// 中景として扱う最小のセル投影サイズです。
        /// </summary>
        [Tooltip("中景として扱う最小のセル投影サイズです。")]
        [SerializeField, Min(1.0f)]
        private float _middleCellPixels = 55.0f;

        /// <summary>
        /// 近景での方向マーカー間隔をセル数で指定です。
        /// </summary>
        [Tooltip("近景での方向マーカー間隔をセル数で指定です。")]
        [SerializeField, Min(0.25f)]
        private float _nearMarkerSpacingCells = 1.5f;

        /// <summary>
        /// 中景での方向マーカー間隔をセル数で指定です。
        /// </summary>
        [Tooltip("中景での方向マーカー間隔をセル数で指定です。")]
        [SerializeField, Min(0.25f)]
        private float _middleMarkerSpacingCells = 3.5f;

        /// <summary>
        /// 遠景での方向マーカー間隔をセル数で指定です。
        /// </summary>
        [Tooltip("遠景での方向マーカー間隔をセル数で指定です。")]
        [SerializeField, Min(0.25f)]
        private float _farMarkerSpacingCells = 8.0f;

        /// <summary>
        /// 一体の経路に表示する方向マーカーの上限です。
        /// </summary>
        [Tooltip("一体の経路に表示する方向マーカーの上限です。")]
        [SerializeField, Range(1, 128)]
        private int _maximumDirectionMarkers = 24;

        /// <summary>
        /// 近景と中景の曲がり角に方向マーカーを追加するかです。
        /// </summary>
        [Tooltip("近景と中景の曲がり角に方向マーカーを追加するかです。")]
        [SerializeField]
        private bool _highlightTurns = true;

        /// <summary>
        /// 遠景でも曲がり角マーカーを追加するかです。
        /// </summary>
        [Tooltip("遠景でも曲がり角マーカーを追加するかです。")]
        [SerializeField]
        private bool _highlightTurnsAtFarDistance = false;

        /// <summary>
        /// 曲がり角として扱う最小角度です。
        /// </summary>
        [Tooltip("曲がり角として扱う最小角度です。")]
        [SerializeField, Range(5.0f, 175.0f)]
        private float _turnAngleDegrees = 35.0f;

        /// <summary>
        /// 方向マーカーを経路に沿って動かすかです。
        /// </summary>
        [Tooltip("方向マーカーを経路に沿って動かすかです。")]
        [Header("Animation")]
        [SerializeField]
        private bool _animateDirectionMarkers = true;

        /// <summary>
        /// 方向マーカーの移動速度をセル毎秒で指定です。
        /// </summary>
        [Tooltip("方向マーカーの移動速度をセル毎秒で指定です。")]
        [SerializeField, Min(0.0f)]
        private float _directionSpeedCellsPerSecond = 0.8f;

        /// <summary>
        /// 到達地点を拡大縮小して強調するかです。
        /// </summary>
        [Tooltip("到達地点を拡大縮小して強調するかです。")]
        [SerializeField]
        private bool _pulseGoal = true;

        /// <summary>
        /// 到達地点の拡大率の振幅です。
        /// </summary>
        [Tooltip("到達地点の拡大率の振幅です。")]
        [SerializeField, Range(0.0f, 0.5f)]
        private float _goalPulseAmplitude = 0.15f;

        /// <summary>
        /// 到達地点の一秒あたりのパルス周期です。
        /// </summary>
        [Tooltip("到達地点の一秒あたりのパルス周期です。")]
        [SerializeField, Min(0.0f)]
        private float _goalPulseCyclesPerSecond = 0.8f;

        /// <summary>
        /// マクロフェイズでアニメーションを再生するかです。
        /// </summary>
        [Tooltip("マクロフェイズでアニメーションを再生するかです。")]
        [SerializeField]
        private bool _animateInMacro = true;

        /// <summary>
        /// ミクロフェイズのマップでもアニメーションを再生するかです。
        /// </summary>
        [Tooltip("ミクロフェイズのマップでもアニメーションを再生するかです。")]
        [SerializeField]
        private bool _animateInMicro = true;

        /// <summary>
        /// ゲーム速度に依存しない時刻でアニメーションを再生するかです。
        /// </summary>
        [Tooltip("ゲーム速度に依存しない時刻でアニメーションを再生するかです。")]
        [SerializeField]
        private bool _useUnscaledTime = true;

        /// <summary>
        /// アニメーション用メッシュの最大更新回数です。
        /// </summary>
        [Tooltip("アニメーション用メッシュの最大更新回数です。")]
        [SerializeField, Range(1, 60)]
        private int _animationFramesPerSecond = 30;

        /// <summary>
        /// プレイヤー以外の味方経路を破線で描画するかです。
        /// </summary>
        [Tooltip("プレイヤー以外の味方経路を破線で描画するかです。")]
        [Header("Ally Routes and Opacity")]
        [SerializeField]
        private bool _dashAllyRoutes = true;

        /// <summary>
        /// 破線の線部分の長さをセル数で指定です。
        /// </summary>
        [Tooltip("破線の線部分の長さをセル数で指定です。")]
        [SerializeField, Min(0.05f)]
        private float _dashLengthCells = 0.45f;

        /// <summary>
        /// 破線の空白部分の長さをセル数で指定です。
        /// </summary>
        [Tooltip("破線の空白部分の長さをセル数で指定です。")]
        [SerializeField, Min(0.05f)]
        private float _dashGapCells = 0.2f;

        /// <summary>
        /// 移動済み区間の不透明度です。
        /// </summary>
        [Tooltip("移動済み区間の不透明度です。")]
        [SerializeField, Range(0.0f, 1.0f)]
        private float _completedOpacity = 0.22f;

        /// <summary>
        /// 未移動区間の不透明度です。
        /// </summary>
        [Tooltip("未移動区間の不透明度です。")]
        [SerializeField, Range(0.0f, 1.0f)]
        private float _activeOpacity = 0.95f;

        /// <summary>
        /// 重複レーン全体のセル幅に対する上限です。
        /// </summary>
        [Tooltip("重複レーン全体のセル幅に対する上限です。")]
        [Header("Layout Limits")]
        [SerializeField, Range(0.1f, 1.0f)]
        private float _maximumLaneGroupCells = 0.75f;

        /// <summary>
        /// 極端な遠景での方向マーカーのセル幅に対する上限です。
        /// </summary>
        [Tooltip("極端な遠景での方向マーカーのセル幅に対する上限です。")]
        [SerializeField, Range(0.1f, 1.0f)]
        private float _maximumMarkerCells = 0.6f;

        /// <summary>
        /// 複数カメラが共有するマップの基準セル投影サイズです。
        /// </summary>
        [Tooltip("複数カメラが共有するマップの基準セル投影サイズです。")]
        [SerializeField, Min(1.0f)]
        private float _mapReferenceCellPixels = 80.0f;

        /// <summary>
        /// 経路ライン本体を表示するかどうかです。
        /// </summary>
        [Tooltip("経路ライン本体を表示するかどうかです。")]
        [SerializeField]
        private bool _showRouteLines = true;

        /// <summary>
        /// 開始地点マーカーを表示するかどうかです。
        /// </summary>
        [Tooltip("開始地点マーカーを表示するかどうかです。")]
        [SerializeField]
        private bool _showStartMarker = true;

        /// <summary>
        /// 目的地マーカーを表示するかどうかです。
        /// </summary>
        [Tooltip("目的地マーカーを表示するかどうかです。")]
        [SerializeField]
        private bool _showGoalMarker = true;

        /// <summary>
        /// 曲がり角に置く方向マーカーのサイズ倍率です。
        /// </summary>
        [Tooltip("曲がり角に置く方向マーカーのサイズ倍率です。")]
        [SerializeField, Range(1.0f, 2.0f)]
        private float _turnMarkerScale = 1.15f;

        /// <summary>
        /// 端点で重なるユニット番号をずらす画面ピクセル間隔です。
        /// </summary>
        [Tooltip("端点で重なるユニット番号をずらす画面ピクセル間隔です。")]
        [SerializeField, Min(0.0f)]
        private float _labelSpacingPixels = 14.0f;

        public float LabelSpacingPixels => Sanitize(_labelSpacingPixels, 14.0f, 0.0f, 60.0f);

        public bool ShowRouteLines => _showRouteLines;
        public bool ShowStartMarker => _showStartMarker;
        public bool ShowGoalMarker => _showGoalMarker;
        public float TurnMarkerScale => Sanitize(_turnMarkerScale, 1.15f, 1.0f, 2.0f);

        public float MacroLineWidthMultiplier => Sanitize(_macroLineWidthMultiplier, 1.0f, 0.25f, 4.0f);
        public float MapLineWidthMultiplier => Sanitize(_mapLineWidthMultiplier, 1.0f, 0.25f, 4.0f);
        public float LineWidthPixels => Sanitize(_lineWidthPixels, 4.0f, 1.0f, 32.0f);
        public float OutlineWidthPixels => Sanitize(_outlineWidthPixels, 8.0f, 1.0f, 48.0f);
        public Color OutlineColor => _outlineColor;
        public bool UseSavedColors => _useSavedColors;
        public Color DefaultLineColor => _defaultLineColor;
        public Color DefaultDirectionColor => _defaultDirectionColor;
        public float StartRadiusPixels => Sanitize(_startRadiusPixels, 5.0f, 1.0f, 40.0f);
        public float GoalRadiusPixels => Sanitize(_goalRadiusPixels, 11.0f, 1.0f, 60.0f);
        public MovementPathGoalShape GoalShape => _goalShape;
        public bool ShowUnitLabels => _showUnitLabels;
        public float LabelMinimumCellPixels => Sanitize(_labelMinimumCellPixels, 100.0f, 1.0f, 1000.0f);
        public float DirectionMarkerLengthPixels => Sanitize(_directionMarkerLengthPixels, 20.0f, 4.0f, 64.0f);
        public float DirectionMarkerWidthPixels => Sanitize(_directionMarkerWidthPixels, 18.0f, 4.0f, 64.0f);
        public MovementPathDirectionShape DirectionShape => _directionShape;
        public bool ShowDirectionMarkers => _showDirectionMarkers;
        public float NearCellPixels => Sanitize(_nearCellPixels, 120.0f, 2.0f, 1000.0f);
        public float MiddleCellPixels => Sanitize(_middleCellPixels, 55.0f, 1.0f, 999.0f);
        public float NearMarkerSpacingCells => Sanitize(_nearMarkerSpacingCells, 1.5f, 0.25f, 30.0f);
        public float MiddleMarkerSpacingCells => Sanitize(_middleMarkerSpacingCells, 3.5f, 0.25f, 30.0f);
        public float FarMarkerSpacingCells => Sanitize(_farMarkerSpacingCells, 8.0f, 0.25f, 50.0f);
        public int MaximumDirectionMarkers => Mathf.Clamp(_maximumDirectionMarkers, 1, 128);
        public bool HighlightTurns => _highlightTurns;
        public bool HighlightTurnsAtFarDistance => _highlightTurnsAtFarDistance;
        public float TurnAngleDegrees => Sanitize(_turnAngleDegrees, 35.0f, 5.0f, 175.0f);
        public bool AnimateDirectionMarkers => _animateDirectionMarkers;
        public float DirectionSpeedCellsPerSecond => Sanitize(_directionSpeedCellsPerSecond, 0.8f, 0.0f, 20.0f);
        public bool PulseGoal => _pulseGoal;
        public float GoalPulseAmplitude => Sanitize(_goalPulseAmplitude, 0.15f, 0.0f, 0.5f);
        public float GoalPulseCyclesPerSecond => Sanitize(_goalPulseCyclesPerSecond, 0.8f, 0.0f, 10.0f);
        public bool AnimateInMacro => _animateInMacro;
        public bool AnimateInMicro => _animateInMicro;
        public bool UseUnscaledTime => _useUnscaledTime;
        public int AnimationFramesPerSecond => Mathf.Clamp(_animationFramesPerSecond, 1, 60);
        public bool DashAllyRoutes => _dashAllyRoutes;
        public float DashLengthCells => Sanitize(_dashLengthCells, 0.45f, 0.05f, 4.0f);
        public float DashGapCells => Sanitize(_dashGapCells, 0.2f, 0.05f, 4.0f);
        public float CompletedOpacity => Sanitize(_completedOpacity, 0.22f, 0.0f, 1.0f);
        public float ActiveOpacity => Sanitize(_activeOpacity, 0.95f, 0.0f, 1.0f);
        public float MaximumLaneGroupCells => Sanitize(_maximumLaneGroupCells, 0.75f, 0.1f, 1.0f);
        public float MaximumMarkerCells => Sanitize(_maximumMarkerCells, 0.6f, 0.1f, 1.0f);
        public float MapReferenceCellPixels => Sanitize(_mapReferenceCellPixels, 80.0f, 1.0f, 1000.0f);

        /// <summary>
        /// カメラ距離に応じた方向マーカーの間隔を取得します。
        /// </summary>
        public float GetMarkerSpacing(float pixelsPerCell)
        {
            if (pixelsPerCell >= Mathf.Max(NearCellPixels, MiddleCellPixels + 1.0f))
            {
                return NearMarkerSpacingCells;
            }

            return pixelsPerCell >= MiddleCellPixels ? MiddleMarkerSpacingCells : FarMarkerSpacingCells;
        }

        /// <summary>
        /// 共有データの編集を検出するハッシュを取得します。
        /// </summary>
        public int GetConfigurationHash()
        {
            int hash = 17;
            unchecked
            {
                hash = hash * 397 ^ _macroLineWidthMultiplier.GetHashCode();
                hash = hash * 397 ^ _mapLineWidthMultiplier.GetHashCode();
                hash = hash * 397 ^ _lineWidthPixels.GetHashCode();
                hash = hash * 397 ^ _outlineWidthPixels.GetHashCode();
                hash = hash * 397 ^ _outlineColor.GetHashCode();
                hash = hash * 397 ^ _useSavedColors.GetHashCode();
                hash = hash * 397 ^ _defaultLineColor.GetHashCode();
                hash = hash * 397 ^ _defaultDirectionColor.GetHashCode();
                hash = hash * 397 ^ _startRadiusPixels.GetHashCode();
                hash = hash * 397 ^ _goalRadiusPixels.GetHashCode();
                hash = hash * 397 ^ _goalShape.GetHashCode();
                hash = hash * 397 ^ _showUnitLabels.GetHashCode();
                hash = hash * 397 ^ _labelMinimumCellPixels.GetHashCode();
                hash = hash * 397 ^ _directionMarkerLengthPixels.GetHashCode();
                hash = hash * 397 ^ _directionMarkerWidthPixels.GetHashCode();
                hash = hash * 397 ^ _directionShape.GetHashCode();
                hash = hash * 397 ^ _showDirectionMarkers.GetHashCode();
                hash = hash * 397 ^ _nearCellPixels.GetHashCode();
                hash = hash * 397 ^ _middleCellPixels.GetHashCode();
                hash = hash * 397 ^ _nearMarkerSpacingCells.GetHashCode();
                hash = hash * 397 ^ _middleMarkerSpacingCells.GetHashCode();
                hash = hash * 397 ^ _farMarkerSpacingCells.GetHashCode();
                hash = hash * 397 ^ _maximumDirectionMarkers.GetHashCode();
                hash = hash * 397 ^ _highlightTurns.GetHashCode();
                hash = hash * 397 ^ _highlightTurnsAtFarDistance.GetHashCode();
                hash = hash * 397 ^ _turnAngleDegrees.GetHashCode();
                hash = hash * 397 ^ _animateDirectionMarkers.GetHashCode();
                hash = hash * 397 ^ _directionSpeedCellsPerSecond.GetHashCode();
                hash = hash * 397 ^ _pulseGoal.GetHashCode();
                hash = hash * 397 ^ _goalPulseAmplitude.GetHashCode();
                hash = hash * 397 ^ _goalPulseCyclesPerSecond.GetHashCode();
                hash = hash * 397 ^ _animateInMacro.GetHashCode();
                hash = hash * 397 ^ _animateInMicro.GetHashCode();
                hash = hash * 397 ^ _useUnscaledTime.GetHashCode();
                hash = hash * 397 ^ _animationFramesPerSecond.GetHashCode();
                hash = hash * 397 ^ _dashAllyRoutes.GetHashCode();
                hash = hash * 397 ^ _dashLengthCells.GetHashCode();
                hash = hash * 397 ^ _dashGapCells.GetHashCode();
                hash = hash * 397 ^ _completedOpacity.GetHashCode();
                hash = hash * 397 ^ _activeOpacity.GetHashCode();
                hash = hash * 397 ^ _maximumLaneGroupCells.GetHashCode();
                hash = hash * 397 ^ _maximumMarkerCells.GetHashCode();
                hash = hash * 397 ^ _mapReferenceCellPixels.GetHashCode();
                hash = hash * 397 ^ _showRouteLines.GetHashCode();
                hash = hash * 397 ^ _showStartMarker.GetHashCode();
                hash = hash * 397 ^ _showGoalMarker.GetHashCode();
                hash = hash * 397 ^ _turnMarkerScale.GetHashCode();
                hash = hash * 397 ^ _labelSpacingPixels.GetHashCode();
            }

            return hash;
        }

        /// <summary>
        /// 無効な数値を既定値へ戻し、表示可能な範囲へ収めます。
        /// </summary>
        private static float Sanitize(float value, float fallback, float minimum, float maximum)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback : Mathf.Clamp(value, minimum, maximum);
        }
    }
}
