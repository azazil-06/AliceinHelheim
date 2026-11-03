Shader "Custom/PlayerVignette"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}
        _VignetteColor ("Vignette Color", Color) = (0, 0, 0, 1)
        _PlayerViewportPos ("Player Viewport Pos", Vector) = (0.5, 0.5, 0, 0)
        _InnerRadius ("Inner Radius", Range(0, 1.5)) = 0.28
        _OuterRadius ("Outer Radius", Range(0.1, 2.0)) = 0.85
        _MaxDarkness ("Max Darkness", Range(0, 1)) = 0.75
        _AspectRatio ("Aspect Ratio", Float) = 1.777777
        _FadeExponent ("Fade Exponent", Range(0.5, 4.0)) = 1.4
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent+500" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _VignetteColor;
            float4 _PlayerViewportPos;
            float _InnerRadius;
            float _OuterRadius;
            float _MaxDarkness;
            float _AspectRatio;
            float _FadeExponent;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uvDiff = IN.texcoord - _PlayerViewportPos.xy;
                uvDiff.x *= _AspectRatio;

                float dist = length(uvDiff);

                // Smooth normalized falloff between inner and outer radius
                float range = max(_OuterRadius - _InnerRadius, 0.001);
                float normalizedDist = saturate((dist - _InnerRadius) / range);

                // Curved falloff for cinematic softness
                float fade = pow(normalizedDist, _FadeExponent);

                float finalAlpha = _VignetteColor.a * _MaxDarkness * fade;

                return fixed4(_VignetteColor.rgb, finalAlpha);
            }
            ENDCG
        }
    }
}
