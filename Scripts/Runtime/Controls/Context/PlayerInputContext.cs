using System;
using UnityEngine;

namespace PranaTactics.Controls
{
    /// <summary>
    /// プレイヤーまたはAIから取得したユニット入力を保持し、
    /// ゲーム内の操作判定へ提供します。
    /// </summary>
    public sealed class PlayerInputContext
    {
        /// <summary>
        /// ボード上のオブジェクトを選択するマウスボタン番号です。
        /// </summary>
        private const int k_BoardSelectionMouseButton = 0;

        /// <summary>
        /// 現在の入力状態を提供する入力ソースです。
        /// </summary>
        private IUnitInput _currentInput;

        /// <summary>
        /// 最後に更新されたユニット入力状態です。
        /// </summary>
        private UnitInputState _currentState;

        public PlayerInputContext(IUnitInput input)
        {
            _currentInput =
                input ?? throw new ArgumentNullException(nameof(input));
        }

        public UnitInputState CurrentState
        {
            get
            {
                return _currentState;
            }
        }

        public bool IsScrollingUp
        {
            get
            {
                return _currentState.MouseScrollDelta > 0.0f;
            }
        }

        public bool IsScrollingDown
        {
            get
            {
                return _currentState.MouseScrollDelta < 0.0f;
            }
        }

        public int SelectedSlotNumber
        {
            get
            {
                return _currentState.SelectedSlotNumber;
            }
        }

        /// <summary>
        /// 入力状態の取得に使用する入力ソースを変更します。
        /// </summary>
        /// <param name="input">
        /// 新しく使用する入力ソースです。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="input"/>が<c>null</c>の場合に発生します。
        /// </exception>
        public void SetInputSource(IUnitInput input)
        {
            _currentInput =
                input ?? throw new ArgumentNullException(nameof(input));
        }

        /// <summary>
        /// 現在の入力ソースから最新の入力状態を取得して保持します。
        /// </summary>
        public void UpdateState()
        {
            _currentState = _currentInput.CurrentState;
        }

        /// <summary>
        /// マウスの左ボタンが押されたとき、
        /// 指定されたレイヤー上に存在するボードオブジェクトを取得します。
        /// </summary>
        /// <param name="camera">
        /// マウス位置からレイを生成するカメラです。
        /// </param>
        /// <param name="layerMask">
        /// レイキャストの対象とするレイヤーです。
        /// </param>
        /// <param name="hit">
        /// オブジェクトを取得できた場合のレイキャスト結果です。
        /// </param>
        /// <returns>
        /// 対象のボードオブジェクトを取得できた場合は<c>true</c>、
        /// それ以外の場合は<c>false</c>です。
        /// </returns>
        public bool TryGetClickedBoardObject(
            Camera camera,
            LayerMask layerMask,
            out RaycastHit hit)
        {
            hit = default;

            if (PranaTactics.Tutorial.TutorialSession.BlocksInput)
            {
                return false;
            }

            if (camera == null)
            {
                Debug.LogWarning(
                    $"{nameof(PlayerInputContext)}: " +
                    "レイキャストに使用するCameraが指定されていません。");

                return false;
            }

            if (!Input.GetMouseButtonDown(
                    k_BoardSelectionMouseButton))
            {
                return false;
            }

            return InputRaycastUtility.TryGetWorldObject(
                camera,
                layerMask,
                out hit);
        }
    }
}
