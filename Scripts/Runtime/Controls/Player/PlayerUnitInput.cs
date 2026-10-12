using UnityEngine;

using PranaTactics.Match.Phases;
using PranaTactics.UI.Screens;
using PranaTactics.Settings;

namespace PranaTactics.Controls
{
    /// <summary>
    /// Unityの入力システムからプレイヤーの操作を取得し、
    /// ユニットの入力状態として提供します。
    /// </summary>
    public sealed class PlayerUnitInput : IUnitInput
    {
        /// <summary>
        /// メイン照準の水平方向に使用する入力軸名です。
        /// </summary>
        private const string k_MouseXAxisName = "Mouse X";

        /// <summary>
        /// メイン照準の垂直方向に使用する入力軸名です。
        /// </summary>
        private const string k_MouseYAxisName = "Mouse Y";

        /// <summary>
        /// サブ照準の水平方向に使用する入力軸名です。
        /// </summary>
        private const string k_HorizontalAxisName = "Horizontal";

        /// <summary>
        /// サブ照準の垂直方向に使用する入力軸名です。
        /// </summary>
        private const string k_VerticalAxisName = "Vertical";

        /// <summary>
        /// マウスホイールの入力に使用する入力軸名です。
        /// </summary>
        private const string k_MouseScrollDeltaAxisName = "Mouse ScrollWheel";

        /// <summary>
        /// メイン兵装の操作に使用するマウスボタン番号です。
        /// </summary>
        private const int k_PrimaryMouseButton = 0;

        /// <summary>
        /// 視点切り替えに使用するマウスボタン番号です。
        /// </summary>
        private const int k_PerspectiveMouseButton = 1;

        /// <summary>
        /// 兵装スロットが選択されていないことを示す値です。
        /// </summary>
        private const int k_UnselectedSlotNumber = -1;

        private readonly MovementSpeedInputState _movement = new MovementSpeedInputState();

        private int _lastMovementInputFrame = -1;

        private bool _wasHoldWalk;

        private bool _wasHoldStop;

        private string _previousWalkKey;

        private string _previousStopKey;

        public UnitInputState CurrentState
        {
            get
            {
                return CreateCurrentState();
            }
        }

        /// <summary>
        /// 現在のプレイヤー入力からユニット入力状態を生成します。
        /// </summary>
        /// <returns>
        /// 現在のキー、マウスおよび入力軸の状態を格納した
        /// <see cref="UnitInputState"/>です。
        /// </returns>
        private UnitInputState CreateCurrentState()
        {
            if (PranaTactics.Tutorial.TutorialSession.BlocksInput ||
                PranaTactics.UI.Screens.Training.TrainingCharacterSelectionView.IsOpen)
            {
                return new UnitInputState { SelectedSlotNumber = k_UnselectedSlotNumber };
            }

            UpdateMovementMode();

            UnitInputState state = new UnitInputState
            {
                PrimaryAxis = GetPrimaryAxis(),
                SecondaryAxis = GetSecondaryAxis(),
                IsPrimaryPressed =
                    Input.GetMouseButtonDown(k_PrimaryMouseButton),
                IsPrimaryHeld =
                    Input.GetMouseButton(k_PrimaryMouseButton),
                IsPrimaryReleased =
                    Input.GetMouseButtonUp(k_PrimaryMouseButton),
                IsSecondaryPressed =
                    Input.GetKeyDown(KeyCode.Space),
                IsSecondaryHeld =
                    Input.GetKey(KeyCode.Space),
                IsSecondaryReleased =
                    Input.GetKeyUp(KeyCode.Space),
                IsWalking =
                    _movement.IsWalking,
                IsStopping =
                    _movement.IsStopping,
                ShouldSwitchPerspective =
                    Input.GetMouseButtonDown(k_PerspectiveMouseButton),
                ShouldToggleMapCamera =
                    KeyboardBindingInput.IsPressed(SettingsManager.Current.Controls.MapKey),
                IsMapHeld = KeyboardBindingInput.IsHeld(SettingsManager.Current.Controls.MapKey),
                MouseScrollDelta =
                    Input.GetAxis(k_MouseScrollDeltaAxisName),
                SelectedSlotNumber =
                    GetSelectedSlotNumber(),
                ShouldToggleSettingsMenu =
                    Input.GetKeyDown(KeyCode.Escape)
            };

            return state;
        }

        /// <summary>
        /// 保存された操作方式に従って速度を変更します。同じフレームでは再処理しません。
        /// 新しい戦闘フェーズは通常移動で開始し、設定操作中は切り替えません。
        /// 同時押しは停止を優先し、速度モードは排他的に扱います。
        /// </summary>
        private void UpdateMovementMode()
        {
            if (_lastMovementInputFrame == Time.frameCount)
            {
                return;
            }

            _lastMovementInputFrame = Time.frameCount;

            ControlsSettings controls = SettingsManager.Current.Controls;

            if (controls.HoldWalk != _wasHoldWalk || controls.HoldStop != _wasHoldStop ||
                controls.WalkKey != _previousWalkKey || controls.StopKey != _previousStopKey)
            {
                _movement.Reset();
                _wasHoldWalk = controls.HoldWalk;
                _wasHoldStop = controls.HoldStop;
                _previousWalkKey = controls.WalkKey;
                _previousStopKey = controls.StopKey;
            }

            if (MatchPhaseManager.Instance == null ||
                MatchPhaseManager.Instance.CurrentPhase != MatchPhase.Micro)
            {
                _movement.Reset();

                return;
            }

            if (!Application.isFocused ||
                Input.GetKeyDown(KeyCode.Escape) ||
                (SettingsUIManager.Instance != null && SettingsUIManager.Instance.IsOpen))
            {
                _movement.Suspend(controls.HoldWalk, controls.HoldStop);

                return;
            }

            _movement.Update(controls.HoldWalk, controls.HoldStop,
                KeyboardBindingInput.IsHeld(controls.WalkKey), KeyboardBindingInput.IsPressed(
                    controls.WalkKey),
                KeyboardBindingInput.IsHeld(controls.StopKey), KeyboardBindingInput.IsPressed(
                    controls.StopKey));
        }

        /// <summary>
        /// マウス操作によるメイン照準の入力軸を取得します。
        /// </summary>
        /// <returns>
        /// X要素に水平方向、Y要素に垂直方向の入力値を格納した
        /// ベクトルです。
        /// </returns>
        private static Vector2 GetPrimaryAxis()
        {
            return new Vector2(
                Input.GetAxis(k_MouseXAxisName),
                Input.GetAxis(k_MouseYAxisName));
        }

        /// <summary>
        /// キーボード操作によるサブ照準の入力軸を取得します。
        /// </summary>
        /// <returns>
        /// X要素に水平方向、Y要素に垂直方向の入力値を格納した
        /// ベクトルです。
        /// </returns>
        private static Vector2 GetSecondaryAxis()
        {
            return new Vector2(
                Input.GetAxisRaw(k_HorizontalAxisName),
                Input.GetAxisRaw(k_VerticalAxisName));
        }

        /// <summary>
        /// 数字キーの入力から選択された兵装スロット番号を取得します。
        /// </summary>
        /// <returns>
        /// 選択されたスロット番号です。
        /// 数字キーが押されていない場合は
        /// <see cref="k_UnselectedSlotNumber"/>を返します。
        /// </returns>
        private static int GetSelectedSlotNumber()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                return 1;
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                return 2;
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                return 3;
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                return 4;
            }

            return k_UnselectedSlotNumber;
        }
    }
}
