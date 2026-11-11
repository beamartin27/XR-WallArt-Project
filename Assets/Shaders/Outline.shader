Shader "Custom/URPOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)  
        _OutlineWidth ("Outline Width", Range(0.0, 0.05)) = 0.01  
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue" = "Overlay" }  
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front  
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            float _OutlineWidth;  
            float4 _OutlineColor;  

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz).xyz;

                
                positionWS += normalWS * _OutlineWidth;

                
                OUT.positionHCS = TransformWorldToHClip(positionWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;  
            }
            ENDHLSL
        }
    }

    FallBack Off
}
