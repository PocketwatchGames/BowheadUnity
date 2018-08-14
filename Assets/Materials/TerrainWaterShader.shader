Shader "Custom/TerrainWaterShader" {
	Properties {
		[NoScaleOffset] _AlbedoTextureArray("Albedo (RGB)", 2DArray) = "white" {}
		[NoScaleOffset] _NormalsTextureArray("Normals (RGB)", 2DArray) = "white" {}
		[NoScaleOffset] _RoughnessTextureArray("Roughness (RGB)", 2DArray) = "white" {}
		[NoScaleOffset] _AOTextureArray("AO (RGB)", 2DArray) = "white" {}
		[NoScaleOffset] _HeightTextureArray("Height (RGB)", 2DArray) = "white" {}
		_Color("Color", Color) = (1,1,1,1)
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_ClipPlane0("ClipPlane0", Vector) = (1,0,0,0)
		_ClipPlane1("ClipPlane1", Vector) = (1,0,0,0)
		_ClipPlane2("ClipPlane2", Vector) = (1,0,0,0)
		_ClipOrigin("ClipOrigin", Vector) = (1,0,0,0)
		_ClipRegion("ClipRegion", Vector) = (1,0,0,0)
		_WorldOrigin("WorldOrigin", Vector) = (1,0,0,0)
		_TextureScale("TextureScale", Float) = 1
	}
	SubShader {
		Tags {
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
		}

		LOD 200

		CGPROGRAM
		
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert alpha:fade

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.5

		#include "TerrainShader.cginc"

		void vert(inout appdata_full v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			terrainVert(v, o);
		}

		void surf(Input IN, inout SurfaceOutputStandard o) {
			terrainSurf(IN, o);
		}
											
		ENDCG
	}
	FallBack "Diffuse"
}
