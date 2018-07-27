// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Unlit/XRayClip"
{
	Properties
	{
		_MainTex("", 2D) = "white" {}
		_Color ("Color", Color) = (1,1,1,1)
		_Ref ("Ref", Int) = 1
		_ReadMask ("ReadMask", Int) = 255
		_WriteMask ("WriteMask", Int) = 255
		_ClipPlane0("ClipPlane0", Vector) = (1,0,0,0)
		_ClipPlane1("ClipPlane1", Vector) = (1,0,0,0)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			//ZWrite On
			//ZTest Greater
			//ZTest Less
			//ColorMask RGBA
			Cull Front
			//Stencil
			//{
			//	Ref [_Ref]
			//	//Comp NotEqual
			//	Comp Always
			//	Pass Replace
			//	ZFail Keep
			//	ReadMask [_ReadMask]
			//	WriteMask [_WriteMask]
			//}
			//Blend SrcAlpha OneMinusSrcAlpha

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
				float3 worldPos : TEXTURE1;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _ClipPlane0;
			float4 _ClipPlane1;

			fixed4 _Color;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.screenPos = o.vertex;
				o.screenPos.y *= _ProjectionParams.x;
				o.screenPos.xy = TRANSFORM_TEX(o.screenPos.xy, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				float d = dot(i.worldPos, _ClipPlane0.xyz) - _ClipPlane0.w;
				clip(d);
				d = dot(i.worldPos, _ClipPlane1.xyz) - _ClipPlane1.w;
				clip(d);

				float2 uv = (i.screenPos.xy / i.screenPos.w) * 0.5f + 0.5f;
				return float4(1, 0, 0, 1);//_Color * tex2D(_MainTex, uv);
			}
			ENDCG
		}
	}
}
