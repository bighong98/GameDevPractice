Shader "UI/TouchPointPlus"
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
                
                // === PLUS SHAPE GEOMETRY CALCULATION ===
                // Plus shape uses area checks for horizontal and vertical bars
                float2 offset = abs(i.uv - 0.5);
                float sizeScale = 1.0 - age;
                if(_Scaling == 0)
                {
                    sizeScale = 1;
                }                
                
                // Core plus shape parameters
                float coreThickness = _CoreSize * 0.12;
                float coreLength = _CoreSize * 0.6;
                
                // Optimized plus shape detection using step functions
                float coreHorizontalBar = step(offset.y, coreThickness * sizeScale) * step(offset.x, coreLength * sizeScale);
                float coreVerticalBar = step(offset.x, coreThickness * sizeScale) * step(offset.y, coreLength * sizeScale);
                float core = max(coreHorizontalBar, coreVerticalBar);
                
                // Glow plus shape with expanded boundaries
                float glowExpansion = 0.1 * _GlowBlur;
                float glowHorizontalBar = step(offset.y, (coreThickness + glowExpansion) * sizeScale) * 
                                        step(offset.x, (coreLength + glowExpansion) * sizeScale);
                float glowVerticalBar = step(offset.x, (coreThickness + glowExpansion) * sizeScale) * 
                                      step(offset.y, (coreLength + glowExpansion) * sizeScale);
                float glow = max(glowHorizontalBar, glowVerticalBar);
                
                // Ring plus shape using outer and inner boundaries
                float ringOuterThickness = coreThickness + 0.15 * sizeScale;
                float ringOuterLength = coreLength + 0.15 * sizeScale;
                float ringInnerThickness = coreThickness + 0.15 * sizeScale - _RingThickness;
                float ringInnerLength = coreLength + 0.15 * sizeScale - _RingThickness;
                
                float ringOuterHorizontalBar = step(offset.y, ringOuterThickness * sizeScale) * 
                                             step(offset.x, ringOuterLength * sizeScale);
                float ringOuterVerticalBar = step(offset.x, ringOuterThickness * sizeScale) * 
                                            step(offset.y, ringOuterLength * sizeScale);
                float ringOuter = max(ringOuterHorizontalBar, ringOuterVerticalBar);
                
                float ringInnerHorizontalBar = step(offset.y, ringInnerThickness * sizeScale) * 
                                             step(offset.x, ringInnerLength * sizeScale);
                float ringInnerVerticalBar = step(offset.x, ringInnerThickness * sizeScale) * 
                                            step(offset.y, ringInnerLength * sizeScale);
                float ringInner = max(ringInnerHorizontalBar, ringInnerVerticalBar);
                
                float ring = ringOuter * (1.0 - ringInner);
                
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