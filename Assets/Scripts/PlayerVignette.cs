using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class PlayerVignette : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("The player transform to center the vignette around. Auto-found by tag 'Player' if empty.")]
    public Transform target;
    [Tooltip("World offset from the target position (e.g. up to chest/torso).")]
    public Vector3 worldOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Vignette Sliders (Adjust Live in Inspector)")]
    [Tooltip("Clear visibility radius around the player. Increase this to make more of the screen visible!")]
    [Range(0.05f, 1.2f)]
    public float innerRadius = 0.28f;

    [Tooltip("Distance where the shadow reaches maximum darkness. Increase this for a wider, gentler fade!")]
    [Range(0.2f, 2.0f)]
    public float outerRadius = 0.85f;

    [Tooltip("Maximum darkness of the shadow (0 = completely invisible, 0.7 = atmospheric shadow, 1.0 = pitch black).")]
    [Range(0f, 1f)]
    public float maxDarkness = 0.75f;

    [Tooltip("Curvature of the fade (1 = linear, 1.5 - 2 = soft cinematic gradient).")]
    [Range(0.5f, 3.5f)]
    public float fadeCurve = 1.4f;

    [Tooltip("The tint of the darkness (default black).")]
    public Color vignetteColor = Color.black;

    [Header("Atmospheric Pulse")]
    [Tooltip("Subtle breathing / flicker of the light radius for extra horror immersion.")]
    public bool enablePulse = true;
    [Tooltip("Amplitude of the pulse effect.")]
    public float pulseAmplitude = 0.015f;
    [Tooltip("Speed of the pulse cycle.")]
    public float pulseSpeed = 1.5f;

    [Header("Shader Reference")]
    public Shader vignetteShader;

    private Material _vignetteMaterial;
    private Image _uiImage;
    private Camera _mainCamera;

    static readonly int s_PlayerViewportPos = Shader.PropertyToID("_PlayerViewportPos");
    static readonly int s_InnerRadius       = Shader.PropertyToID("_InnerRadius");
    static readonly int s_OuterRadius       = Shader.PropertyToID("_OuterRadius");
    static readonly int s_MaxDarkness       = Shader.PropertyToID("_MaxDarkness");
    static readonly int s_FadeExponent      = Shader.PropertyToID("_FadeExponent");
    static readonly int s_AspectRatio       = Shader.PropertyToID("_AspectRatio");
    static readonly int s_VignetteColor     = Shader.PropertyToID("_VignetteColor");

    void OnEnable()
    {
        Initialize();
    }

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        if (target == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) target = go.transform;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (vignetteShader == null)
        {
            vignetteShader = Shader.Find("Custom/PlayerVignette");
        }

        _uiImage = GetComponent<Image>();
        if (_uiImage != null)
        {
            if (_uiImage.material == null || _uiImage.material.shader != vignetteShader)
            {
                if (_vignetteMaterial == null && vignetteShader != null)
                {
                    _vignetteMaterial = new Material(vignetteShader);
                    _vignetteMaterial.hideFlags = HideFlags.DontSave;
                }
                _uiImage.material = _vignetteMaterial;
            }
            else
            {
                _vignetteMaterial = _uiImage.material;
            }

            _uiImage.color = Color.white;
            _uiImage.raycastTarget = false; // Never block touch / clicks!
        }

        UpdateShaderProperties();
    }

    void OnDestroy()
    {
        if (_vignetteMaterial != null && _vignetteMaterial.hideFlags == HideFlags.DontSave)
        {
            if (Application.isPlaying)
                Destroy(_vignetteMaterial);
            else
                DestroyImmediate(_vignetteMaterial);
        }
    }

    void OnValidate()
    {
        // Updates live in the editor whenever you move a slider in the Inspector!
        if (outerRadius <= innerRadius)
            outerRadius = innerRadius + 0.05f;

        UpdateShaderProperties();
    }

    void LateUpdate()
    {
        UpdateShaderProperties();
    }

    void UpdateShaderProperties()
    {
        if (_vignetteMaterial == null)
        {
            Initialize();
            if (_vignetteMaterial == null) return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        Vector3 viewportPos = new Vector3(0.5f, 0.5f, 0f);
        if (_mainCamera != null && target != null)
        {
            Vector3 playerWorldPos = target.position + worldOffset;
            viewportPos = _mainCamera.WorldToViewportPoint(playerWorldPos);
        }

        float aspect = 1.777777f;
        if (Screen.width > 0 && Screen.height > 0)
        {
            aspect = (float)Screen.width / Screen.height;
        }

        float dynamicInner = innerRadius;
        float dynamicOuter = outerRadius;

        if (enablePulse && Application.isPlaying)
        {
            float pulse = Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * pulseAmplitude;
            dynamicInner += pulse;
            dynamicOuter += pulse * 1.2f;
        }

        _vignetteMaterial.SetVector(s_PlayerViewportPos, new Vector4(viewportPos.x, viewportPos.y, 0f, 0f));
        _vignetteMaterial.SetFloat(s_AspectRatio, aspect);
        _vignetteMaterial.SetFloat(s_InnerRadius, Mathf.Max(0.01f, dynamicInner));
        _vignetteMaterial.SetFloat(s_OuterRadius, Mathf.Max(dynamicInner + 0.05f, dynamicOuter));
        _vignetteMaterial.SetFloat(s_MaxDarkness, maxDarkness);
        _vignetteMaterial.SetFloat(s_FadeExponent, Mathf.Max(0.1f, fadeCurve));
        _vignetteMaterial.SetColor(s_VignetteColor, vignetteColor);
    }
}
