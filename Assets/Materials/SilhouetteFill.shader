Shader "Unlit/SilhouetteFill"
{
	Properties
	{
		_MainTex("", 2D) = "white" {}
		_Color ("Color", Color) = (1,1,1,1)
		_Ref ("Ref", Int) = 1
		_ReadMask ("ReadMask", Int) = 255
		_WriteMask ("WriteMask", Int) = 255
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			ZWrite Off
			ZTest Greater
			ColorMask RGBA
			Stencil
			{
				Ref [_Ref]
				Comp NotEqual
				Pass Replace
				ZFail Keep
				ReadMask [_ReadMask]
				WriteMask [_WriteMask]
			}
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 screenPos : TEXTURE0;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;

			fixed4 _Color;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.screenPos = o.vertex;
				o.screenPos.y *= _ProjectionParams.x;
				o.screenPos.xy = TRANSFORM_TEX(o.screenPos.xy, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				float2 uv = (i.screenPos.xy / i.screenPos.w) * 0.5f + 0.5f;
				return _Color * tex2D(_MainTex, uv);
			}
			ENDCG
		}
	}
}
