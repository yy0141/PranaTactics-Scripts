using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Controls
{
    [Serializable]
    public struct UnitInputState
    {
        public bool IsMapHeld
        {
            get; set;
        }

        /// <summary>
        /// 兵装スロットが選択されていないことを表す値です。
        /// </summary>
        public const int NoSlotSelected = -1;

        /// <summary>
        /// メイン兵装の照準操作に使用する入力軸です。
        /// </summary>
        [SerializeField]
        private Vector2 _primaryAxis;

        /// <summary>
        /// サブ兵装の照準操作に使用する入力軸です。
        /// </summary>
        [SerializeField]
        private Vector2 _secondaryAxis;

        /// <summary>
        /// このフレームでメイン兵装の使用ボタンが
        /// 押されたかどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _isPrimaryPressed;

        /// <summary>
        /// メイン兵装の使用ボタンが
        /// 押され続けているかどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _isPrimaryHeld;

        /// <summary>
        /// このフレームでメイン兵装の使用ボタンが
        /// 離されたかどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _isPrimaryReleased;

        /// <summary>
        /// このフレームでサブ兵装の使用ボタンが
        /// 押されたかどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _isSecondaryPressed;

        /// <summary>
        /// サブ兵装の使用ボタンが
        /// 押され続けているかどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _isSecondaryHeld;

        /// <summary>
        /// このフレームでサブ兵装の使用ボタンが
        /// 離されたかどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _isSecondaryReleased;

        /// <summary>
        /// 歩行操作が有効かどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _isWalking;

        /// <summary>
        /// 停止操作が有効かどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _isStopping;

        /// <summary>
        /// このフレームで視点切り替え操作が
        /// 行われたかどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _shouldSwitchPerspective;

        /// <summary>
        /// このフレームでマップカメラの切り替え操作が
        /// 行われたかどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _shouldToggleMapCamera;

        /// <summary>
        /// マウスホイールのスクロール量です。
        /// </summary>
        [SerializeField]
        private float _mouseScrollDelta;

        /// <summary>
        /// このフレームで選択された兵装スロット番号です。
        /// </summary>
        [SerializeField]
        private int _selectedSlotNumber;

        /// <summary>
        /// このフレームで設定画面の表示切り替え操作が
        /// 行われたかどうかを示します。
        /// </summary>
        [SerializeField]
        private bool _shouldToggleSettingsMenu;

        public Vector2 PrimaryAxis
        {
            readonly get
            {
                return _primaryAxis;
            }
            set
            {
                _primaryAxis = value;
            }
        }

        public Vector2 SecondaryAxis
        {
            readonly get
            {
                return _secondaryAxis;
            }
            set
            {
                _secondaryAxis = value;
            }
        }

        public bool IsPrimaryPressed
        {
            readonly get
            {
                return _isPrimaryPressed;
            }
            set
            {
                _isPrimaryPressed = value;
            }
        }

        public bool IsPrimaryHeld
        {
            readonly get
            {
                return _isPrimaryHeld;
            }
            set
            {
                _isPrimaryHeld = value;
            }
        }

        public bool IsPrimaryReleased
        {
            readonly get
            {
                return _isPrimaryReleased;
            }
            set
            {
                _isPrimaryReleased = value;
            }
        }

        public bool IsSecondaryPressed
        {
            readonly get
            {
                return _isSecondaryPressed;
            }
            set
            {
                _isSecondaryPressed = value;
            }
        }

        public bool IsSecondaryHeld
        {
            readonly get
            {
                return _isSecondaryHeld;
            }
            set
            {
                _isSecondaryHeld = value;
            }
        }

        public bool IsSecondaryReleased
        {
            readonly get
            {
                return _isSecondaryReleased;
            }
            set
            {
                _isSecondaryReleased = value;
            }
        }

        public bool IsWalking
        {
            readonly get
            {
                return _isWalking;
            }
            set
            {
                _isWalking = value;
            }
        }

        public bool IsStopping
        {
            readonly get
            {
                return _isStopping;
            }
            set
            {
                _isStopping = value;
            }
        }

        public bool ShouldSwitchPerspective
        {
            readonly get
            {
                return _shouldSwitchPerspective;
            }
            set
            {
                _shouldSwitchPerspective = value;
            }
        }

        public bool ShouldToggleMapCamera
        {
            readonly get
            {
                return _shouldToggleMapCamera;
            }
            set
            {
                _shouldToggleMapCamera = value;
            }
        }

        public float MouseScrollDelta
        {
            readonly get
            {
                return _mouseScrollDelta;
            }
            set
            {
                _mouseScrollDelta = value;
            }
        }

        public int SelectedSlotNumber
        {
            readonly get
            {
                return _selectedSlotNumber;
            }
            set
            {
                _selectedSlotNumber = value;
            }
        }

        public bool ShouldToggleSettingsMenu
        {
            readonly get
            {
                return _shouldToggleSettingsMenu;
            }
            set
            {
                _shouldToggleSettingsMenu = value;
            }
        }

        public readonly bool HasSelectedSlot
        {
            get
            {
                return _selectedSlotNumber !=
                    NoSlotSelected;
            }
        }

        /// <summary>
        /// 入力が設定されていない初期状態を生成します。
        /// </summary>
        /// <returns>
        /// 兵装スロットが未選択に設定された入力状態です。
        /// </returns>
        public static UnitInputState CreateDefault()
        {
            return new UnitInputState
            {
                SelectedSlotNumber =
                    NoSlotSelected
            };
        }
    }
}
