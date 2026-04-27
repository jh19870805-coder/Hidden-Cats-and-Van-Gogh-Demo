Shader "HiddenCats/CircularDissolveTransition"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _DissolveProgress ("Dissolve Progress", Range(0, 1)) = 0
        _DotSize ("Dot Size", Range(0.01, 2)) = 0.5
        _DotCount ("Dot Count", Range(5, 80)) = 24
        _DotSpacing ("Dot Spacing", Range(-0.5, 1)) = 0
        _Softness ("Softness", Range(0, 0.3)) = 0.02
        _EdgeColor ("Edge Color", Color) = (1, 1, 1, 1)
        _EdgeWidth ("Edge Width", Range(0, 0.15)) = 0.06
        _SolidColor ("Solid Color", Color) = (0.3, 0.3, 0.3, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "IgnoreProjector"="True" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _DissolveProgress;
            float _DotSize;
            float _DotCount;
            float _DotSpacing;
            float _Softness;
            float4 _EdgeColor;
            float _EdgeWidth;
            float4 _SolidColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float phase1End = 0.35;
                float phase2End = 0.65;

                float screenRatio = _ScreenParams.x / _ScreenParams.y;
                float targetRatio = 16.0 / 9.0;
                float2 uv169;

                if (screenRatio > targetRatio)
                {
                    float scale = targetRatio / screenRatio;
                    uv169.x = (i.uv.x - 0.5) / scale + 0.5;
                    uv169.y = i.uv.y;
                }
                else
                {
                    float scale = screenRatio / targetRatio;
                    uv169.x = i.uv.x;
                    uv169.y = (i.uv.y - 0.5) / scale + 0.5;
                }

                bool in169 = (uv169.x >= 0.0 && uv169.x <= 1.0 && uv169.y >= 0.0 && uv169.y <= 1.0);

                float2 centeredUV = uv169 - 0.5;
                float diagonal169 = length(float2(0.5, 0.5));
                centeredUV.x *= targetRatio;
                float pixelDist = length(centeredUV) * 2.0;
                float dissolveRadius = diagonal169 * 2.0;

                // Pillarbox/letterbox area: always solid black.
                // This ensures the black border is consistent whether during transition or gameplay.
                if (!in169)
                {
                    return fixed4(0.0, 0.0, 0.0, 1.0);
                }

                if (_DissolveProgress < phase1End)
                {
                    float t = _DissolveProgress / phase1End;
                    float circleRadius = t * dissolveRadius;
                    float currentDotSize = lerp(0.02, _DotSize, t);
                    float showCircle = step(pixelDist, circleRadius);

                    float2 gridUV = uv169;
                    gridUV.x *= targetRatio; // Scale x to make cells square
                    gridUV *= _DotCount;
                    float2 gridLocalUV = frac(gridUV);
                    float dist = length(gridLocalUV - 0.5);

                    float dotRadius = currentDotSize * 0.5 * (1.0 - _DotSpacing);
                    dotRadius = max(0.001, dotRadius);
                    float dot = smoothstep(dotRadius + _Softness, dotRadius - _Softness, dist);

                    float alpha = showCircle * dot;

                    float edgeDist = circleRadius - pixelDist;
                    float edgeFactor = smoothstep(0.0, _EdgeWidth, edgeDist) * smoothstep(_EdgeWidth * 2.0, _EdgeWidth, edgeDist);

                    return fixed4(_EdgeColor.rgb, max(alpha, edgeFactor * 0.5));
                }
                else if (_DissolveProgress < phase2End)
                {
                    return _SolidColor;
                }
                else
                {
                    float t = (_DissolveProgress - phase2End) / (1.0 - phase2End);
                    float circleRadius = lerp(dissolveRadius, 0.0, t);
                    float currentDotSize = lerp(_DotSize, 0.02, t);
                    float showCircle = step(pixelDist, circleRadius);

                    float2 gridUV = uv169;
                    gridUV.x *= targetRatio; // Scale x to make cells square
                    gridUV *= _DotCount;
                    float2 gridLocalUV = frac(gridUV);
                    float dist = length(gridLocalUV - 0.5);

                    float dotRadius = currentDotSize * 0.5 * (1.0 - _DotSpacing);
                    dotRadius = max(0.001, dotRadius);
                    float dot = smoothstep(dotRadius + _Softness, dotRadius - _Softness, dist);

                    float alpha = showCircle * dot;

                    float edgeDist = circleRadius - pixelDist;
                    float edgeFactor = smoothstep(0.0, _EdgeWidth, edgeDist) * smoothstep(_EdgeWidth * 2.0, _EdgeWidth, edgeDist);

                    return fixed4(_EdgeColor.rgb, max(alpha, edgeFactor * 0.5));
                }
            }
            ENDCG
        }
    }

    FallBack "Transparent/VertexLit"
}
