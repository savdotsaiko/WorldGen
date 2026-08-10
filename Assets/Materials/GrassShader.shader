Shader "Custom/GrassInstanced"
{
    Properties
    {
        _BaseColor    ("Base Color",    Color)  = (0.2, 0.6, 0.1, 1)
        _TipColor     ("Tip Color",     Color)  = (0.5, 0.9, 0.2, 1)
        _WindStrength ("Wind Strength", Float)  = 0.3
        _WindSpeed    ("Wind Speed",    Float)  = 1.0
        _WindScale    ("Wind Scale",    Float)  = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // The buffer your CPU fills — one matrix per blade
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            StructuredBuffer<float4x4> _InstanceBuffer;
            #endif

            

            // InvertMatrix helper (Unity doesn't expose this by default in URP)
            float4x4 InvertMatrix(float4x4 m)
            {
                float n11=m[0][0], n12=m[1][0], n13=m[2][0], n14=m[3][0];
                float n21=m[0][1], n22=m[1][1], n23=m[2][1], n24=m[3][1];
                float n31=m[0][2], n32=m[1][2], n33=m[2][2], n34=m[3][2];
                float n41=m[0][3], n42=m[1][3], n43=m[2][3], n44=m[3][3];
                float t11=n23*n34*n42-n24*n33*n42+n24*n32*n43-n22*n34*n43-n23*n32*n44+n22*n33*n44;
                float t12=n14*n33*n42-n13*n34*n42-n14*n32*n43+n12*n34*n43+n13*n32*n44-n12*n33*n44;
                float t13=n13*n24*n42-n14*n23*n42+n14*n22*n43-n12*n24*n43-n13*n22*n44+n12*n23*n44;
                float t14=n14*n23*n32-n13*n24*n32-n14*n22*n33+n12*n24*n33+n13*n22*n34-n12*n23*n34;
                float det=n11*t11+n21*t12+n31*t13+n41*t14;
                float idet=1.0f/det;
                float4x4 ret;
                ret[0][0]=t11*idet; ret[0][1]=(n24*n33*n41-n23*n34*n41-n24*n31*n43+n21*n34*n43+n23*n31*n44-n21*n33*n44)*idet;
                ret[0][2]=(n22*n34*n41-n24*n32*n41+n24*n31*n42-n21*n34*n42-n22*n31*n44+n21*n32*n44)*idet;
                ret[0][3]=(n23*n32*n41-n22*n33*n41-n23*n31*n42+n21*n33*n42+n22*n31*n43-n21*n32*n43)*idet;
                ret[1][0]=t12*idet; ret[1][1]=(n13*n34*n41-n14*n33*n41+n14*n31*n43-n11*n34*n43-n13*n31*n44+n11*n33*n44)*idet;
                ret[1][2]=(n14*n32*n41-n12*n34*n41-n14*n31*n42+n11*n34*n42+n12*n31*n44-n11*n32*n44)*idet;
                ret[1][3]=(n12*n33*n41-n13*n32*n41+n13*n31*n42-n11*n33*n42-n12*n31*n43+n11*n32*n43)*idet;
                ret[2][0]=t13*idet; ret[2][1]=(n14*n23*n41-n13*n24*n41-n14*n21*n43+n11*n24*n43+n13*n21*n44-n11*n23*n44)*idet;
                ret[2][2]=(n12*n24*n41-n14*n22*n41+n14*n21*n42-n11*n24*n42-n12*n21*n44+n11*n22*n44)*idet;
                ret[2][3]=(n13*n22*n41-n12*n23*n41-n13*n21*n42+n11*n23*n42+n12*n21*n43-n11*n22*n43)*idet;
                ret[3][0]=t14*idet; ret[3][1]=(n13*n24*n31-n14*n23*n31+n14*n21*n33-n11*n24*n33-n13*n21*n34+n11*n23*n34)*idet;
                ret[3][2]=(n14*n22*n31-n12*n24*n31-n14*n21*n32+n11*n24*n32+n12*n21*n34-n11*n22*n34)*idet;
                ret[3][3]=(n12*n23*n31-n13*n22*n31+n13*n21*n32-n11*n23*n32-n12*n21*n33+n11*n22*n33)*idet;
                return ret;
            }
            void setup()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                unity_ObjectToWorld = _InstanceBuffer[unity_InstanceID];
                unity_WorldToObject = InvertMatrix(unity_ObjectToWorld);
            #endif
            }
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float  _WindStrength;
                float  _WindSpeed;
                float  _WindScale;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;

                float3 posWS = TransformObjectToWorld(IN.positionOS);

                // Wind — only affects tip (high UV.y)
                float windPhase = dot(posWS.xz, float2(1, 0.5)) * _WindScale
                                  + _Time.y * _WindSpeed;
                float wind = sin(windPhase) * _WindStrength * IN.uv.y * IN.uv.y;
                posWS.x += wind;
                posWS.z += wind * 0.3;

                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
{
    float4 col = lerp(_BaseColor, _TipColor, IN.uv.y);
    
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
    float shadow = mainLight.shadowAttenuation;
    float lighting = saturate(dot(normalize(float3(0,1,0)), mainLight.direction)) * shadow;
    lighting = lerp(0.2, 1.0, lighting);
    
    col.rgb *= lighting;
    return col;
}
            ENDHLSL
        }
    }
}