using UnityEngine;
using UnityEngine.UI;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// スナイパーのADS表示、
    /// スコープ表示およびズーム用カメラを管理します。
    /// </summary>
    public sealed class SniperScopeController : MonoBehaviour
    {
        /// <summary>
        /// Shaderへぼかし強度を渡す
        /// プロパティIDです。
        /// </summary>
        private static readonly int k_BlurStrengthProperty =
            Shader.PropertyToID(
                "_BlurStrength");

        /// <summary>
        /// 通常のゲーム画面を描画するカメラです。
        /// </summary>
        [Header("Camera")]
        [SerializeField]
        private Camera _mainCamera;

        /// <summary>
        /// スコープ内部の映像を描画するカメラです。
        /// </summary>
        [SerializeField]
        private Camera _scopeCamera;

        /// <summary>
        /// スコープカメラの描画先です。
        /// </summary>
        [SerializeField]
        private RenderTexture _scopeRenderTexture;

        /// <summary>
        /// スコープ外側のぼかし用映像を
        /// 描画するカメラです。
        /// </summary>
        [Header("Outside Blur")]
        [SerializeField]
        private Camera _outsideBlurCamera;

        /// <summary>
        /// スコープ外側のぼかし用カメラの
        /// 描画先です。
        /// </summary>
        [SerializeField]
        private RenderTexture _outsideBlurRenderTexture;

        /// <summary>
        /// スコープ外側へ表示する
        /// ぼかし対象の映像です。
        /// </summary>
        [SerializeField]
        private RawImage _outsideBlurView;

        /// <summary>
        /// スコープ外側の映像を
        /// ぼかすためのMaterialです。
        /// </summary>
        [SerializeField]
        private Material _outsideBlurMaterial;

#if UNITY_EDITOR || PV_CAPTURE
        public GameObject CaptureAdsRoot => _adsRoot;

        public GameObject CaptureScopeRoot => _scopeRoot;

#endif

        /// <summary>
        /// ADS表示全体です。
        /// </summary>
        [Header("ADS")]
        [SerializeField]
        private GameObject _adsRoot;

        /// <summary>
        /// ADS用レティクルです。
        /// </summary>
        [SerializeField]
        private Image _adsReticle;

        /// <summary>
        /// スコープ表示全体です。
        /// </summary>
        [Header("Scope")]
        [SerializeField]
        private GameObject _scopeRoot;

        /// <summary>
        /// スコープ映像を切り抜くマスクです。
        /// </summary>
        [SerializeField]
        private Image _scopeMask;

        /// <summary>
        /// スコープ映像です。
        /// </summary>
        [SerializeField]
        private RawImage _scopeView;

        /// <summary>
        /// スコープ外周画像です。
        /// </summary>
        [SerializeField]
        private Image _scopeOverlay;

        /// <summary>
        /// スコープ用レティクルです。
        /// </summary>
        [SerializeField]
        private Image _scopeReticle;

        /// <summary>
        /// 現在使用しているスコープ設定です。
        /// </summary>
        private SniperScopeProfile _currentProfile;

        /// <summary>
        /// 通常時の視野角です。
        /// </summary>
        private float _baseFieldOfView;

        /// <summary>
        /// 実行時に使用する
        /// スコープ外側ぼかし用Materialです。
        /// </summary>
        private Material _runtimeOutsideBlurMaterial;

        public bool IsAiming
        {
            get
            {
                return _currentProfile != null;
            }
        }

        /// <summary>
        /// 初期状態を設定します。
        /// </summary>
        private void Awake()
        {
            InitializeScopeCamera();

            InitializeOutsideBlurCamera();

            InitializeOutsideBlurMaterial();

            HideAll();
        }

        /// <summary>
        /// 実行時に生成したMaterialを解放します。
        /// </summary>
        private void OnDestroy()
        {
            if (_runtimeOutsideBlurMaterial == null)
            {
                return;
            }

            Destroy(
                _runtimeOutsideBlurMaterial);

            _runtimeOutsideBlurMaterial = null;
        }

        /// <summary>
        /// Main Cameraを外部から設定します。
        /// </summary>
        /// <param name="mainCamera">
        /// 使用するMain Cameraです。
        /// </param>
        public void Initialize(
            Camera mainCamera)
        {
            if (mainCamera != null)
            {
                _mainCamera =
                    mainCamera;
            }

            if (_mainCamera == null)
            {
                Debug.LogError(
                    $"{nameof(SniperScopeController)}: " +
                    "Main Cameraが設定されていません。",
                    this);
            }

            if (_scopeCamera == null)
            {
                Debug.LogWarning(
                    $"{nameof(SniperScopeController)}: " +
                    "Scope Cameraが設定されていません。" +
                    " Scope形式の照準は使用できません。",
                    this);
            }

            if (_scopeRenderTexture == null)
            {
                Debug.LogWarning(
                    $"{nameof(SniperScopeController)}: " +
                    "Scope Render Textureが設定されていません。",
                    this);
            }

            if (_outsideBlurCamera == null)
            {
                Debug.LogWarning(
                    $"{nameof(SniperScopeController)}: " +
                    "Outside Blur Cameraが設定されていません。",
                    this);
            }

            if (_outsideBlurRenderTexture == null)
            {
                Debug.LogWarning(
                    $"{nameof(SniperScopeController)}: " +
                    "Outside Blur Render Textureが設定されていません。",
                    this);
            }

            if (_outsideBlurMaterial == null)
            {
                Debug.LogWarning(
                    $"{nameof(SniperScopeController)}: " +
                    "Outside Blur Materialが設定されていません。",
                    this);
            }
        }

        /// <summary>
        /// 照準表示を開始します。
        /// </summary>
        /// <param name="profile">
        /// 使用するスコープ設定です。
        /// </param>
        /// <param name="baseFieldOfView">
        /// 通常時のカメラ視野角です。
        /// </param>
        /// <returns>
        /// 照準表示を開始できた場合は<c>true</c>です。
        /// </returns>
        public bool BeginAim(
            SniperScopeProfile profile,
            float baseFieldOfView)
        {
            if (profile == null)
            {
                Debug.LogWarning(
                    $"{nameof(SniperScopeController)}: " +
                    $"{nameof(SniperScopeProfile)}が設定されていません。",
                    this);

                return false;
            }

            _currentProfile =
                profile;

            _baseFieldOfView =
                baseFieldOfView;

            HideAll();

            switch (profile.ScopeType)
            {
                case SniperScopeType.Ads:
                    return BeginAds();

                case SniperScopeType.Scope:
                    return BeginScope();

                default:
                    _currentProfile = null;

                    return false;
            }
        }

        /// <summary>
        /// 現在の照準視野角を更新します。
        /// </summary>
        /// <param name="fieldOfView">
        /// 設定する垂直視野角です。
        /// </param>
        public void SetFieldOfView(
            float fieldOfView)
        {
            if (_currentProfile == null)
            {
                return;
            }

            switch (_currentProfile.ScopeType)
            {
                case SniperScopeType.Ads:
                    SetAdsFieldOfView(
                        fieldOfView);

                    break;

                case SniperScopeType.Scope:
                    SetScopeFieldOfView(
                        fieldOfView);

                    break;
            }
        }

        /// <summary>
        /// 照準表示を終了します。
        /// </summary>
        public void EndAim()
        {
            if (_mainCamera != null)
            {
                _mainCamera.fieldOfView =
                    _baseFieldOfView;
            }

            if (_scopeCamera != null)
            {
                _scopeCamera.fieldOfView =
                    _baseFieldOfView;

                _scopeCamera.enabled =
                    false;
            }

            if (_outsideBlurCamera != null)
            {
                _outsideBlurCamera.fieldOfView =
                    _baseFieldOfView;

                _outsideBlurCamera.enabled =
                    false;
            }

            if (_outsideBlurView != null)
            {
                _outsideBlurView.enabled =
                    false;
            }

            _currentProfile = null;

            HideAll();
        }

        /// <summary>
        /// スコープ用カメラを初期化します。
        /// </summary>
        private void InitializeScopeCamera()
        {
            if (_scopeCamera != null)
            {
                _scopeCamera.enabled =
                    false;

                if (_scopeRenderTexture != null)
                {
                    _scopeCamera.targetTexture =
                        _scopeRenderTexture;
                }
            }

            if (_scopeView != null &&
                _scopeRenderTexture != null)
            {
                _scopeView.texture =
                    _scopeRenderTexture;
            }
        }

        /// <summary>
        /// スコープ外側ぼかし用カメラを初期化します。
        /// </summary>
        private void InitializeOutsideBlurCamera()
        {
            if (_outsideBlurCamera != null)
            {
                _outsideBlurCamera.enabled =
                    false;

                if (_outsideBlurRenderTexture != null)
                {
                    _outsideBlurCamera.targetTexture =
                        _outsideBlurRenderTexture;
                }
            }

            if (_outsideBlurView != null &&
                _outsideBlurRenderTexture != null)
            {
                _outsideBlurView.texture =
                    _outsideBlurRenderTexture;

                _outsideBlurView.enabled =
                    false;
            }
        }

        /// <summary>
        /// スコープ外側ぼかし用Materialを初期化します。
        /// </summary>
        private void InitializeOutsideBlurMaterial()
        {
            if (_outsideBlurMaterial == null)
            {
                return;
            }

            _runtimeOutsideBlurMaterial =
                Instantiate(
                    _outsideBlurMaterial);

            if (_outsideBlurView != null)
            {
                _outsideBlurView.material =
                    _runtimeOutsideBlurMaterial;
            }
        }

        /// <summary>
        /// ADS表示を開始します。
        /// </summary>
        /// <returns>
        /// ADS表示を開始できた場合は<c>true</c>です。
        /// </returns>
        private bool BeginAds()
        {
            if (_mainCamera == null)
            {
                Debug.LogError(
                    $"{nameof(SniperScopeController)}: " +
                    "ADSに使用するMain Cameraがありません。",
                    this);

                _currentProfile = null;

                return false;
            }

            if (_adsRoot == null)
            {
                Debug.LogError(
                    $"{nameof(SniperScopeController)}: " +
                    "AdsRootが設定されていません。",
                    this);

                _currentProfile = null;

                return false;
            }

            _adsRoot.SetActive(
                true);

            if (_scopeRoot != null)
            {
                _scopeRoot.SetActive(
                    false);
            }

            if (_scopeCamera != null)
            {
                _scopeCamera.enabled =
                    false;
            }

            if (_outsideBlurCamera != null)
            {
                _outsideBlurCamera.enabled =
                    false;
            }

            if (_outsideBlurView != null)
            {
                _outsideBlurView.enabled =
                    false;
            }

            ApplyAdsReticle();

            return true;
        }

        /// <summary>
        /// スコープ表示を開始します。
        /// </summary>
        /// <returns>
        /// スコープ表示を開始できた場合は<c>true</c>です。
        /// </returns>
        private bool BeginScope()
        {
            if (_scopeCamera == null)
            {
                Debug.LogError(
                    $"{nameof(SniperScopeController)}: " +
                    "Scope Cameraが設定されていません。",
                    this);

                _currentProfile = null;

                return false;
            }

            if (_scopeRoot == null)
            {
                Debug.LogError(
                    $"{nameof(SniperScopeController)}: " +
                    "ScopeRootが設定されていません。",
                    this);

                _currentProfile = null;

                return false;
            }

            if (_scopeRenderTexture == null)
            {
                Debug.LogError(
                    $"{nameof(SniperScopeController)}: " +
                    "Scope Render Textureが設定されていません。",
                    this);

                _currentProfile = null;

                return false;
            }

            if (_adsRoot != null)
            {
                _adsRoot.SetActive(
                    false);
            }

            _scopeRoot.SetActive(
                true);

            _scopeCamera.targetTexture =
                _scopeRenderTexture;

            _scopeCamera.fieldOfView =
                _baseFieldOfView;

            _scopeCamera.enabled =
                true;

            ApplyOutsideBlur();

            ApplyScopeView();

            ApplyScopeMask();

            ApplyScopeOverlay();

            ApplyScopeReticle();

            return true;
        }

        /// <summary>
        /// ADS用レティクルを設定します。
        /// </summary>
        private void ApplyAdsReticle()
        {
            if (_adsReticle == null ||
                _currentProfile == null)
            {
                return;
            }

            Sprite reticleSprite =
                _currentProfile.ReticleSprite;

            _adsReticle.sprite =
                reticleSprite;

            _adsReticle.enabled =
                reticleSprite != null;

            _adsReticle.rectTransform.sizeDelta =
                _currentProfile.ReticleSize;
        }

        /// <summary>
        /// スコープ外側のぼかし表示を設定します。
        /// </summary>
        private void ApplyOutsideBlur()
        {
            if (_currentProfile == null)
            {
                return;
            }

            float blurStrength =
                Mathf.Clamp01(
                    _currentProfile.OutsideBlurStrength);

            if (blurStrength <= 0.0f)
            {
                if (_outsideBlurCamera != null)
                {
                    _outsideBlurCamera.enabled =
                        false;
                }

                if (_outsideBlurView != null)
                {
                    _outsideBlurView.enabled =
                        false;
                }

                return;
            }

            if (_outsideBlurCamera == null ||
                _outsideBlurRenderTexture == null ||
                _outsideBlurView == null ||
                _runtimeOutsideBlurMaterial == null)
            {
                Debug.LogWarning(
                    $"{nameof(SniperScopeController)}: " +
                    "スコープ外側のぼかしに必要な設定が不足しています。",
                    this);

                return;
            }

            _outsideBlurCamera.targetTexture =
                _outsideBlurRenderTexture;

            _outsideBlurCamera.fieldOfView =
                _baseFieldOfView;

            _outsideBlurCamera.enabled =
                true;

            _outsideBlurView.texture =
                _outsideBlurRenderTexture;

            _outsideBlurView.material =
                _runtimeOutsideBlurMaterial;

            _outsideBlurView.enabled =
                true;

            _runtimeOutsideBlurMaterial.SetFloat(
                k_BlurStrengthProperty,
                blurStrength);
        }

        /// <summary>
        /// スコープ内部の映像を設定します。
        /// </summary>
        private void ApplyScopeView()
        {
            if (_scopeView == null ||
                _currentProfile == null)
            {
                return;
            }

            _scopeView.texture =
                _scopeRenderTexture;
        }

        /// <summary>
        /// スコープ用マスクを設定します。
        /// </summary>
        private void ApplyScopeMask()
        {
            if (_scopeMask == null ||
                _currentProfile == null)
            {
                return;
            }

            _scopeMask.sprite =
                _currentProfile.ScopeMaskSprite;

            _scopeMask.rectTransform.sizeDelta =
                _currentProfile.ScopeSize;
        }

        /// <summary>
        /// スコープ外周画像を設定します。
        /// </summary>
        private void ApplyScopeOverlay()
        {
            if (_scopeOverlay == null ||
                _currentProfile == null)
            {
                return;
            }

            Sprite overlaySprite =
                _currentProfile.ScopeOverlaySprite;

            _scopeOverlay.sprite =
                overlaySprite;

            _scopeOverlay.enabled =
                overlaySprite != null;

            _scopeOverlay.color =
                _currentProfile.ScopeOverlayColor;
        }

        /// <summary>
        /// スコープ用レティクルを設定します。
        /// </summary>
        private void ApplyScopeReticle()
        {
            if (_scopeReticle == null ||
                _currentProfile == null)
            {
                return;
            }

            Sprite reticleSprite =
                _currentProfile.ReticleSprite;

            _scopeReticle.sprite =
                reticleSprite;

            _scopeReticle.enabled =
                reticleSprite != null;

            _scopeReticle.rectTransform.sizeDelta =
                _currentProfile.ReticleSize;
        }

        /// <summary>
        /// ADS時の視野角を設定します。
        /// </summary>
        private void SetAdsFieldOfView(
            float fieldOfView)
        {
            if (_mainCamera == null)
            {
                return;
            }

            _mainCamera.fieldOfView =
                fieldOfView;
        }

        /// <summary>
        /// スコープ内部の視野角を設定します。
        /// </summary>
        private void SetScopeFieldOfView(
            float fieldOfView)
        {
            if (_scopeCamera == null)
            {
                return;
            }

            _scopeCamera.fieldOfView =
                fieldOfView;
        }

        /// <summary>
        /// すべての照準UIを非表示にします。
        /// </summary>
        private void HideAll()
        {
            if (_adsRoot != null)
            {
                _adsRoot.SetActive(
                    false);
            }

            if (_scopeRoot != null)
            {
                _scopeRoot.SetActive(
                    false);
            }

            if (_outsideBlurView != null)
            {
                _outsideBlurView.enabled =
                    false;
            }
        }
    }
}
