Shader "Sebanne/FlipbookArrayShader"
{
    Properties
    {
        _MainTex ("Texture Array", 2DArray) = "" {}
        _TotalFrames ("Total Frames", Int) = 1
        _FPS     ("FPS", Float) = 12
        _Cutoff  ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue"      = "AlphaTest"
        }

        Pass
        {
            Name "FLIPBOOK_ARRAY"
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma require 2darray

            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            float4 _MainTex_ST;
            int    _TotalFrames;
            float  _FPS;
            float  _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float frameIndex = floor(_Time.y * _FPS);
                frameIndex = fmod(frameIndex, (float)_TotalFrames);

                fixed4 c = UNITY_SAMPLE_TEX2DARRAY(_MainTex, float3(i.uv, frameIndex));

                clip(c.a - _Cutoff);

                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Texture"
}
