Shader "Custom/TerrainShader" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		//_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_ClipPlane0("ClipPlane0", Vector) = (1,0,0,0)
		_ClipPlane1("ClipPlane1", Vector) = (1,0,0,0)
		_ClipPlane2("ClipPlane2", Vector) = (1,0,0,0)
		_ClipOrigin("ClipOrigin", Vector) = (1,0,0,0)
		_ClipRegion("ClipRegion", Vector) = (1,0,0,0)
		_WorldOrigin("WorldOrigin", Vector) = (1,0,0,0)
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			//float2 uv_MainTex;
			float4 vertColor;
			float4 clipPos;
			float3 worldPos;
		};

		half _Glossiness;
		half _Metallic;
		float4 _ClipPlane0;
		float4 _ClipPlane1;
		float4 _ClipPlane2;
		float4 _ClipOrigin;
		float4 _WorldOrigin;
		float4 _ClipRegion;
		fixed4 _Color;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void vert(inout appdata_full v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			o.clipPos = UnityObjectToClipPos(v.vertex);
			o.vertColor = v.color;
		}

		void surf (Input IN, inout SurfaceOutputStandard o) {
#if 0
			float3 geoNormal = -normalize(cross(ddx(IN.worldPos.xyz), ddy(IN.worldPos.xyz)));

			float3 worldOrigin = _WorldOrigin + geoNormal * 1;

			float3 worldDir = worldOrigin - IN.worldPos;
			float3 worldNml = normalize(worldDir);
			
			float dd = dot(worldNml, geoNormal);

			if (dd < 0) {
				float worldDist = length(IN.worldPos - _WorldOrigin);
				if (worldDist > _ClipRegion.y) {
					IN.clipPos = IN.clipPos / IN.clipPos.w;

					float2 dxy = IN.clipPos.xy - _ClipOrigin.xy;
					dxy.y /= _ClipRegion.z;

					float r = (dxy.x*dxy.x) + (dxy.y*dxy.y);
					if (r < _ClipRegion.x) {
						clip(-1);
					}
				}

			}
#endif

			// Albedo comes from a texture tinted by color
			fixed4 c = _Color * IN.vertColor;
			o.Albedo = c;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
