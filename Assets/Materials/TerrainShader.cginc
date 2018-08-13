// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Copyright (c) 2018 Pocketwatch Games LLC.

// Common terrain shading shit.
UNITY_DECLARE_TEX2DARRAY(_AlbedoTextureArray);
UNITY_DECLARE_TEX2DARRAY(_NormalsTextureArray);

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
float _AlbedoTextureArrayIndices[12];
float _NormalsTextureArrayIndices[12];
fixed4 _Color;

// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
// #pragma instancing_options assumeuniformscaling
UNITY_INSTANCING_BUFFER_START(Props)
// put more per-instance properties here
UNITY_INSTANCING_BUFFER_END(Props)

fixed4 triplanarColor(UNITY_ARGS_TEX2DARRAY(texArray), float arrayIndices[12], float3 tc, float3 absNormal, float3 signNormal, float4 texBlend) {
	fixed4 color = fixed4(0, 0, 0, 0);

	for (int i = 0; i < 4; ++i) {
		fixed4 t;
		fixed4 x = UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.z, tc.y, arrayIndices[i * 3 + 1])) * absNormal.x;
		fixed4 y = UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.x, tc.z, arrayIndices[i * 3 + (int)(1 + signNormal.y)])) * absNormal.y;
		fixed4 z = UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.x, tc.y, arrayIndices[i * 3 + 1])) * absNormal.z;
		t = x + y + z;
		color += t * texBlend[i];
	}

	return color;
}

fixed3 triplanarNormal(UNITY_ARGS_TEX2DARRAY(texArray), float arrayIndices[12], float3 tc, float3 absNormal, float3 signNormal, float4 texBlend) {
	fixed3 normal = fixed3(0, 0, 0);

	for (int i = 0; i < 4; ++i) {
		fixed3 t;
		fixed3 x = UnpackNormal(UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.z, tc.y, arrayIndices[i * 3 + 1]))) * absNormal.x;
		fixed3 y = UnpackNormal(UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.x, tc.z, arrayIndices[i * 3 + (int)(1 + signNormal.y)]))) * absNormal.y;
		fixed3 z = UnpackNormal(UNITY_SAMPLE_TEX2DARRAY(texArray, float3(tc.x, tc.y, arrayIndices[i * 3 + 1]))) * absNormal.z;
		t = normalize(x + y + z);
		normal += t * texBlend[i];
	}

	return normalize(normal);
}

fixed4 sampleTerrainAlbedo(float3 tc, float3 absNormal, float3 signNormal, float4 texBlend) {
	return triplanarColor(UNITY_PASS_TEX2DARRAY(_AlbedoTextureArray), _AlbedoTextureArrayIndices, tc, absNormal, signNormal, texBlend);
}

fixed3 sampleTerrainNormal(float3 tc, float3 absNormal, float3 signNormal, float4 texBlend) {
	return triplanarNormal(UNITY_PASS_TEX2DARRAY(_NormalsTextureArray), _NormalsTextureArrayIndices, tc, absNormal, signNormal, texBlend);
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

	float3 worldNormal = normalize(IN.wNormal);
	float3 tc = IN.worldPos / _TextureScale;
	float3 absNormal = abs(worldNormal);
	float3 signNormal = sign(worldNormal);

	fixed4 albedo = sampleTerrainAlbedo(tc, absNormal, signNormal, IN.texBlend);
	fixed3 normal = sampleTerrainNormal(tc, absNormal, signNormal, IN.texBlend);

	// Albedo comes from a texture tinted by color
	o.Albedo = albedo * _Color;
	o.Normal = normal;
	// Metallic and smoothness come from slider variables
	o.Metallic = _Metallic;
	o.Smoothness = _Glossiness;
	o.Alpha = albedo.a * _Color.a;
}
