Shader "UI/TouchPointTriangle"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _TimeNow ("Time Now", Float) = 0
        [HideInInspector] _StartTime ("Start Time", Float) = 0
        [HideInInspector] _Lifetime ("Lifetime", Float) = 2.0
        
        // Core animation properties
        [Toggle] _Scaling ("Disappearing Scaling", Float) = 0
        [Toggle] _Fading ("Disappearing Fading", Float) = 1

        _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        _CoreSize ("Core Size", Range(0.1, 0.4)) = 0.25
        _CoreOpacity ("Core Opacity", Range(0.0, 1.0)) = 1.0
        _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowBlur ("Glow Blur", Range(0.0, 1.0)) = 0.3
        _GlowOpacity ("Glow Opacity", Range(0.0, 1.0)) = 0.3
        _RingColor ("Ring Color", Color) = (1, 1, 1, 1)
        _RingThickness ("Ring Thickness", Range(0.01, 0.1)) = 0
        _RingOpacity ("Ring Opacity", Range(0.0, 1.0)) = 0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex; float4 _MainTex_ST;
            float _TimeNow, _StartTime, _Lifetime;
            fixed4 _CoreColor, _GlowColor, _RingColor;
            float _CoreSize, _CoreOpacity, _GlowBlur, _GlowOpacity, _RingThickness, _RingOpacity;
            float _Scaling, _Fading;
            float _UseMobileOptimization;

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float age = (_TimeNow - _StartTime) / _Lifetime;
                if (age < 0.0 || age >= 1.0) return float4(0, 0, 0, 0);
                
                // === TRIANGLE GEOMETRY DISTANCE CALCULATION ===
                // Triangle uses specialized distance function for equilateral triangle
                float2 center = float2(0.5, 0.5);
                float2 p = i.uv - center;
                float distance = max(abs(p.x) + p.y * 0.577, -p.y * 1.155) * 2.0;
                
                float sizeScale = 1.0 - age;
                if(_Scaling == 0)
                {
                    sizeScale = 1;
                }                
                
                float core = step(distance, _CoreSize * sizeScale);
                float glowRadius = 0.35 * sizeScale;
                float glowSoftness = 0.1 * _GlowBlur;
                float glow = 1.0 - smoothstep(glowRadius - glowSoftness, glowRadius + glowSoftness, distance);
                
                float ringRadius = min(0.42 * sizeScale, 0.42);
                float ringInner = ringRadius - _RingThickness;
                float ringOuter = ringRadius;
                float ring = smoothstep(ringInner - 0.02, ringInner, distance) * 
                           (1.0 - smoothstep(ringOuter, ringOuter + 0.02, distance));
                
                float2 edgeDistance = min(i.uv, 1.0 - i.uv);
                float edgeFade = smoothstep(0.0, 0.05, min(edgeDistance.x, edgeDistance.y));
                float timeFade = 1.0 - age;
                 if(_Fading == 0)
                {
                    timeFade = 1;
                }    
                
                float coreAlpha = core * _CoreOpacity * timeFade * edgeFade;
                float glowAlpha = glow * _GlowOpacity * timeFade * edgeFade;
                float ringAlpha = ring * _RingOpacity * timeFade * edgeFade;
                
                float finalAlpha = max(max(coreAlpha, glowAlpha), ringAlpha);
                float coreWeight = step(max(glowAlpha, ringAlpha), coreAlpha);
                float ringWeight = step(glowAlpha, ringAlpha) * (1.0 - coreWeight);
                float glowWeight = (1.0 - coreWeight) * (1.0 - ringWeight);
                
                float3 finalColor = _CoreColor.rgb * coreWeight + 
                                  _RingColor.rgb * ringWeight + 
                                  _GlowColor.rgb * glowWeight;
                
                return float4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}