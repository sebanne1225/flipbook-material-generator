Shader "Sebanne/FlipbookSequenceShader"
{
    Properties
    {
        _MainTex ("Sprite Sheet", 2D) = "white" {}
        _Columns ("Columns", Int) = 1
        _Rows    ("Rows",    Int) = 1
        _TotalFrames ("Total Frames", Int) = 1
        _CurrentFrame ("Current Frame", Float) = 0
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
            Name "FLIPBOOK_SEQ"
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            int       _Columns;
            int       _Rows;
            int       _TotalFrames;
            float     _CurrentFrame;
            float     _Cutoff;

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
                // Frame index driven by Animator (no _Time.y)
                float frameIndex = clamp(floor(_CurrentFrame), 0, (float)(_TotalFrames - 1));

                // Grid position (left-to-right, top-to-bottom)
                float col = fmod(frameIndex, (float)_Columns);
                float row = floor(frameIndex / (float)_Columns);

                // UV mapping — top-left origin to match SheetBuilder layout
                float2 uv;
                uv.x = (col + i.uv.x) / (float)_Columns;
                uv.y = 1.0 - (row + 1.0 - i.uv.y) / (float)_Rows;

                fixed4 c = tex2D(_MainTex, uv);

                clip(c.a - _Cutoff);

                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Texture"
}
