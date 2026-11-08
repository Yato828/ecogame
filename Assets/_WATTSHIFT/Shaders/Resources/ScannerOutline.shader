Shader "WATTSHIFT/ScannerOutline"
{
    Properties
    {
        _OutlineColor("Outline", Color) = (1,0.5,0.1,1)
        _OutlineWidth("World width", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+20" }
        Pass
        {
            Name "SourceHull"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                world += TransformObjectToWorldNormal(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(world);
                return output;
            }
            half4 Frag(Varyings input):SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
