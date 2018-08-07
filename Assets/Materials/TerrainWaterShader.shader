Shader "Custom/TerrainWaterShader" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		//_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_ClipPlane0("ClipPlane0", Vector) = (1,0,0,0)
		_Up("Up", Vector) = (1,0,0,0)
		_Left("Left", Vector) = (1,0,0,0)
		_CylinderSizeSq("CylinderSize", Float) = 1
	}
	SubShader {
		Tags { 
			"Queue"="Transparent" 
			"RenderType"="Transparent" 
		}
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert alpha:fade

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
			float4 vertColor;
			float3 worldPos;
		};

		half _Glossiness;
		half _Metallic;
		float4 _ClipPlane0;
		float4 _Up;
		float4 _Left;
		float _CylinderSizeSq;
		fixed4 _Color;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void vert(inout appdata_full v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			o.worldPos = mul(unity_ObjectToWorld, v.vertex);
			o.vertColor = v.color;
		}

		void surf (Input IN, inout SurfaceOutputStandard o) {
#if 0
			float d = dot(IN.worldPos, _ClipPlane0.xyz) - _ClipPlane0.w;
			if (d < 0) {
				float u = dot(IN.worldPos, _Up.xyz) - _Up.w;
				float l = dot(IN.worldPos, _Left.xyz) - _Left.w;
				float z = u * u + l * l;
				clip((z > _CylinderSizeSq) ? -1 : 1);
			}
#endif

			// Albedo comes from a texture tinted by color
			fixed4 c = _Color * IN.vertColor;
			o.Albedo = c.rgb;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = _Color.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
