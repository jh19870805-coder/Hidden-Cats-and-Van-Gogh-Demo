Shader "HiddenCats/WindowTransitionDissolve"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _DissolveProgress ("Dissolve Progress", Range(0, 1)) = 0
        _DotSize ("Dot Size", Range(0.01, 0.5)) = 0.1
        _Softness ("Softness", Range(0, 0.5)) = 0.05
        _NoiseScale ("Noise Scale", Range(1, 20)) = 5
        _EdgeColor ("Edge Color", Color) = (1, 1, 1, 1)
        _EdgeWidth ("Edge Width", Range(0, 0.2)) = 0.05
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
            float _Softness;
            float _NoiseScale;
            float4 _EdgeColor;
            float _EdgeWidth;

            // 简单的 Hash 函数
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 基础纹理颜色 - 全白用于显示遮罩
                fixed4 col = tex2D(_MainTex, i.uv);

                // 计算基于距离的圆形溶解阈值
                // 从中心到边缘的圆形溶解
                float centerDist = length(i.uv - 0.5) * 2.0; // 0 = 中心, 1 = 边缘
                float dissolveThreshold = _DissolveProgress;

                // 计算波点图案
                float2 gridUV = i.uv * _NoiseScale;
                float2 gridID = floor(gridUV);
                float2 gridLocalUV = frac(gridUV);

                // 获取每个格子的随机阈值（0.1 ~ 0.9）
                float cellThreshold = hash(gridID) * 0.8 + 0.1;

                // 圆形波点形状判断
                float dist = length(gridLocalUV - 0.5);
                float dotRadius = _DotSize * 0.5;
                float circleDot = smoothstep(dotRadius + _Softness, dotRadius - _Softness, dist);

                // 圆形溶解：基于距离判断是否显示
                // centerDist < dissolveThreshold 时显示（从中心向外）
                float showDot = step(centerDist, dissolveThreshold);

                // 计算圆形边缘（溶解边缘的发光效果）
                float edgeFactor = 0;
                if (_EdgeWidth > 0)
                {
                    float edgeStart = dissolveThreshold - _EdgeWidth;
                    float edgeEnd = dissolveThreshold;
                    // 边缘宽度按距离计算，确保圆形边缘效果
                    float distFromEdge = centerDist - edgeStart;
                    edgeFactor = smoothstep(0.0, _EdgeWidth, distFromEdge) * (1 - smoothstep(_EdgeWidth, _EdgeWidth * 2.0, distFromEdge));
                }

                // 最终颜色：边缘发光
                col.rgb = lerp(col.rgb, _EdgeColor.rgb, edgeFactor);

                // 最终 alpha：圆形溶解 + 波点图案
                col.a = showDot * (circleDot * edgeFactor + showDot * (1 - edgeFactor));

                return col;
            }
            ENDCG
        }
    }

    FallBack "Transparent/VertexLit"
}