// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Copyright (c) 2018 Pocketwatch Games LLC.

// Common terrain shading shit.
UNITY_DECLARE_TEX2DARRAY(_AlbedoTextureArray);
UNITY_DECLARE_TEX2DARRAY(_NormalsTextureArray);
UNITY_DECLARE_TEX2DARRAY(_RoughnessTextureArray);
UNITY_DECLARE_TEX2DARRAY(_AOTextureArray);
UNITY_DECLARE_TEX2DARRAY(_HeightTextureArray);

float _AlbedoTextureArrayIndices[12];
float _NormalsTextureArrayIndices[12];
float _RoughnessTextureArrayIndices[12];
float _AOTextureArrayIndices[12];
float _HeightTextureArrayIndices[12];

struct Input {
	//float2 uv_MainTex;
	float4 texBlend;
	float4 clipPos;
	float3 worldPos;
	float3 wNormal;
};

half _Glossiness;
half _Metallic;
float _TextureScale;
float4 _ClipPlane0;
float4 _ClipPlane1;
float4 _ClipPlane2;
float4 _ClipOrigin;
float4 _WorldOrigin;
float4 _ClipRegion;
fixed4 _Color;

// We use the reoriented normal blending technique from here:
// http://blog.selfshadow.com/publications/blending-in-detail/

// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
// #pragma instancing_options assumeuniformscaling
UNITY_INSTANCING_BUFFER_START(Props)
// put more per-instance properties here
UNITY_INSTANCING_BUFFER_END(Props)

fixed4 triplanarColor(UNITY_ARGS_TEX2DARRAY(texArray), float arrayIndices[12], float3 tc, float3 triblend, float3 signNormal, float4 texBlend) {
	fixed4 color = fixed4(0, 0, 0, 0);

	for (int i = 0; i < 4; ++i) {
		fixed4 t;
		fixed4 x = UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.z, tc.y, arrayIndices[i * 3 + 1])) * triblend.x;
		fixed4 y = UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.x, tc.z, arrayIndices[i * 3 + (int)(1 + signNormal.y)])) * triblend.y;
		fixed4 z = UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.x, tc.y, arrayIndices[i * 3 + 1])) * triblend.z;
		t = x + y + z;
		color += t * texBlend[i];
	}

	return color;
}

// Unpack normal as DXT5nm (1, y, 1, x) or BC5 (x, y, 0, 1)
// Note neutral texture like "bump" is (0, 0, 1, 1) to work with both plain RGB normal and DXT5nm/BC5
fixed2 MyUnpackNormalmapRGorAG(fixed4 packednormal) {
	// This do the trick
	packednormal.x *= packednormal.w;

	fixed2 normal;
	normal.xy = packednormal.xy * 2 - 1;
	return normal;
}

inline fixed2 MyUnpackNormal(fixed4 packednormal) {
#if defined(UNITY_NO_DXT5nm)
	return packednormal.xy * 2 - 1;
#else
	return MyUnpackNormalmapRGorAG(packednormal);
#endif
}

float3 blendNormal(float3 src, float3 dst, float blend) {
	float3 n = lerp(src, dst, blend);
	return normalize(n);
}

float3 triplanarNormal(UNITY_ARGS_TEX2DARRAY(texArray), float arrayIndices[12], float3 tc, float3 triblend, float3 signNormal, float4 texBlend) {
	float3 normal = float3(0, 0, 0);

	for (int i = 0; i < 4; ++i) {
		
		float3 x = UnpackNormal(UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.z, tc.y, arrayIndices[i * 3 + 1])));
		float3 y = UnpackNormal(UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.x, tc.z, arrayIndices[i * 3 + (int)(1 + signNormal.y)])));
		float3 z = UnpackNormal(UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.x, tc.y, arrayIndices[i * 3 + 1])));
		
		float3 n = x * triblend.x * texBlend[i];
		n += y * triblend.y * texBlend[i];
		n += z * triblend.z * texBlend[i];

		/*normal = blendNormal(normal, x, absNormal.x * texBlend[i]);
		normal = blendNormal(normal, y, absNormal.y * texBlend[i]);
		normal = blendNormal(normal, z, absNormal.z * texBlend[i]);*/

		normal += n;
	}

	return normalize(normal);
}

fixed4 sampleTerrainAlbedo(float3 tc, float3 triblend, float3 signNormal, float4 texBlend) {
	return triplanarColor(UNITY_PASS_TEX2DARRAY(_AlbedoTextureArray), _AlbedoTextureArrayIndices, tc, triblend, signNormal, texBlend);
}

fixed3 sampleTerrainNormal(float3 tc, float3 triblend, float3 signNormal, float4 texBlend) {
	return triplanarNormal(UNITY_PASS_TEX2DARRAY(_NormalsTextureArray), _NormalsTextureArrayIndices, tc, triblend, signNormal, texBlend);
}

fixed3 sampleTerrainAO(float3 tc, float3 triblend, float3 signNormal, float4 texBlend) {
	return triplanarColor(UNITY_PASS_TEX2DARRAY(_AOTextureArray), _AOTextureArrayIndices, tc, triblend, signNormal, texBlend).r;
}

void clip(Input IN) {
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

			float r = (dxy.x*dxy.x) + (dxy.y*dxy.y); w
				if (r < _ClipRegion.x) {
					clip(-1);
				}
		}

	}
#endif
}

void terrainVert(inout appdata_full v, out Input o) {
	UNITY_INITIALIZE_OUTPUT(Input, o);
	o.clipPos = UnityObjectToClipPos(v.vertex);
	o.wNormal = mul((float3x3)unity_ObjectToWorld, v.normal);
	o.texBlend = v.texcoord;
}

void terrainSurf(Input IN, inout SurfaceOutputStandard o) {
	clip(IN);

	float3 triblend = saturate(pow(IN.wNormal.xyz, 4));
	triblend /= dot(triblend, float3(1, 1, 1));

	float3 worldNormal = normalize(IN.wNormal);
	float3 tc = IN.worldPos / _TextureScale;
	float3 absNormal = abs(worldNormal);
	float3 signNormal = sign(worldNormal);

	fixed4 albedo = sampleTerrainAlbedo(tc, triblend, signNormal, IN.texBlend);
	fixed3 normal = sampleTerrainNormal(tc, triblend, signNormal, IN.texBlend);
	fixed ao = sampleTerrainAO(tc, triblend, signNormal, IN.texBlend);

	// Albedo comes from a texture tinted by color
	o.Albedo = albedo * _Color;
	o.Normal = normal;
	o.Occlusion = ao;
	// Metallic and smoothness come from slider variables
	o.Metallic = 0;// _Metallic;
	o.Smoothness = 0;// _Glossiness;
	o.Alpha = 1;// albedo.a * _Color.a;
}
