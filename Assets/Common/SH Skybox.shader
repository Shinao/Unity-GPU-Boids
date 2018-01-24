// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "ProbePolisher/SH Skybox" {
    Properties {
        _Intensity ("Intensity", Float) = 1.0
        _SHAr ("SHAr", Vector) = (0, 0, 0, 0)
        _SHAg ("SHAg", Vector) = (0, 0, 0, 0)
        _SHAb ("SHAb", Vector) = (0, 0, 0, 0)
        _SHBr ("SHBr", Vector) = (0, 0, 0, 0)
        _SHBg ("SHBg", Vector) = (0, 0, 0, 0)
        _SHBb ("SHBb", Vector) = (0, 0, 0, 0)
        _SHC  ("SHC",  Vector) = (0, 0, 0, 0)
    }
    SubShader {
        Tags { "RenderType"="Background" "Queue"="Background" }
        Pass {
            ZWrite Off
            Cull Off
            Fog { Mode Off }
            
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                half3 uvw : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 pos : POSITION;
                half3 uvw : TEXCOORD0;
            };
            
            half _Intensity;
            half4 _SHAr;
            half4 _SHAg;
            half4 _SHAb;
            half4 _SHBr;
            half4 _SHBg;
            half4 _SHBb;
            half3 _SHC;

            half3 MyShadeSH9 (half4 n)
            {
                half3 x1, x2, x3;
                
                // Linear + constant polynomial terms
                x1.x = dot(_SHAr, n);
                x1.y = dot(_SHAg, n);
                x1.z = dot(_SHAb, n);
                
                // 4 of the quadratic polynomials
                half4 vB = n.yzzx * n.xyzz;
                x2.x = dot(_SHBr, vB);
                x2.y = dot(_SHBg, vB);
                x2.z = dot(_SHBb, vB);
                
                // Final quadratic polynomial
                half vC = n.x * n.x - n.y * n.y;
                x3 = _SHC * vC;
                
                return x1 + x2 + x3;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos (v.vertex);
                o.uvw = v.uvw;
                return o;
            }
            
            half4 frag (v2f i) : COLOR
            {
                half3 c = MyShadeSH9 (half4(normalize(i.uvw), 1.0f));
                return half4(c, 1.0f) * _Intensity;
            }
            
            ENDCG
        }
    }
    CustomEditor "ShSkyboxMaterialEditor"
}
