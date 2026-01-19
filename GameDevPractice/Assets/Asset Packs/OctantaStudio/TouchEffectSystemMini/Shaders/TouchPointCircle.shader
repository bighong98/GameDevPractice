Shader "UI/TouchPointCircle"
{
    Properties
    {
        // Core system properties - managed by TouchGlowUI script
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _TimeNow ("Time Now", Float) = 0
        [HideInInspector] _StartTime ("Start Time", Float) = 0
        [HideInInspector] _Lifetime ("Lifetime", Float) = 2.0
        
        // Core animation properties
        [Toggle] _Scaling ("Disappearing Scaling", Float) = 0
        [Toggle] _Fading ("Disappearing Fading", Float) = 1

        // Core layer properties - solid center of the particle
        _CoreColor ("Core Color", Color) = (1, 1, 1, 1)
        _CoreSize ("Core Size", Range(0.1, 0.4)) = 0.25
        _CoreOpacity ("Core Opacity", Range(0.0, 1.0)) = 1.0
        
        // Glow layer properties - soft outer illumination
        _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowBlur ("Glow Blur", Range(0.0, 1.0)) = 0.3
        _GlowOpacity ("Glow Opacity", Range(0.0, 1.0)) = 0.3
        
        // Ring layer properties - optional outer ring structure
        _RingColor ("Ring Color", Color) = (1, 1, 1, 1)
        _RingThickness ("Ring Thickness", Range(0.01, 0.1)) = 0
        _RingOpacity ("Ring Opacity", Range(0.0, 1.0)) = 0
    }
    
    SubShader
    {
        // UI transparency rendering configuration
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

            // Shader property declarations
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
                // === PARTICLE LIFETIME MANAGEMENT ===
                float age = (_TimeNow - _StartTime) / _Lifetime;
                if (age < 0.0 || age >= 1.0) return float4(0, 0, 0, 0);
                
                // === COORDINATE SYSTEM AND DISTANCE CALCULATION ===
                // Circle uses standard Euclidean distance from center
                float distance = length(i.uv - 0.5);
                
                // === SIZE SCALING OVER LIFETIME ===
                float sizeScale = 1.0 - age;
                if(_Scaling == 0)
                {
                    sizeScale = 1;
                }                
                
                // === LAYER GENERATION SYSTEM ===
                // Core layer - solid center with sharp edges
                float core = step(distance, _CoreSize * sizeScale);
                
                // Glow layer - soft outer illumination with blur control
                float glowRadius = 0.35 * sizeScale;
                float glowSoftness = 0.1 * _GlowBlur;
                float glow = 1.0 - smoothstep(glowRadius - glowSoftness, glowRadius + glowSoftness, distance);
                
                // Ring layer - optional outer ring structure
                float ringRadius = min(0.42 * sizeScale, 0.42);
                float ringInner = ringRadius - _RingThickness;
                float ringOuter = ringRadius;
                float ring = smoothstep(ringInner - 0.02, ringInner, distance) * 
                           (1.0 - smoothstep(ringOuter, ringOuter + 0.02, distance));
                
                // === FADE EFFECTS ===
                // Edge fade prevents artifacts at texture boundaries
                float2 edgeDistance = min(i.uv, 1.0 - i.uv);
                float edgeFade = smoothstep(0.0, 0.05, min(edgeDistance.x, edgeDistance.y));
                
                // Time fade creates natural particle death animation
                float timeFade = 1.0 - age;
                 if(_Fading == 0)
                {
                    timeFade = 1;
                }       

                // === LAYER COMPOSITION ===
                // Calculate alpha values for each layer
                float coreAlpha = core * _CoreOpacity * timeFade * edgeFade;
                float glowAlpha = glow * _GlowOpacity * timeFade * edgeFade;
                float ringAlpha = ring * _RingOpacity * timeFade * edgeFade;
                
                // === OPTIMIZED COLOR BLENDING ===
                // Determine layer dominance without branching using step functions
                float finalAlpha = max(max(coreAlpha, glowAlpha), ringAlpha);
                float coreWeight = step(max(glowAlpha, ringAlpha), coreAlpha);
                float ringWeight = step(glowAlpha, ringAlpha) * (1.0 - coreWeight);
                float glowWeight = (1.0 - coreWeight) * (1.0 - ringWeight);
                
                // Blend colors based on layer weights
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
