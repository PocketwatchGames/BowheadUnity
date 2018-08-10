// Copyright (c) 2018 Pocketwatch Games LLC.

// Common terrain shading shit.
UNITY_DECLARE_TEX2DARRAY(_AlbedoTextureArray);

struct Input {
	//float2 uv_MainTex;
	float4 vertColor : COLOR;
	float4 texBlend;
	float4 clipPos;
	float3 worldPos;
	float3 worldNormal;
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
float _AlbedoTextureArrayIndex[12];
fixed4 _Color;

// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
// #pragma instancing_options assumeuniformscaling
UNITY_INSTANCING_BUFFER_START(Props)
// put more per-instance properties here
UNITY_INSTANCING_BUFFER_END(Props)

fixed4 sampleTerrainAlbedo(float3 tc, float3 absNormal, float3 signNormal, float4 texBlend) {
	fixed4 color = fixed4(0, 0, 0, 0);

	for (int i = 0; i < 4; ++i) {
		fixed4 t;
		fixed4 x = UNITY_SAMPLE_TEX2DARRAY(_AlbedoTextureArray, float3(tc.z, tc.y, _AlbedoTextureArrayIndex[i*3+1])) * absNormal.x;
		fixed4 y = UNITY_SAMPLE_TEX2DARRAY(_AlbedoTextureArray, float3(tc.x, tc.z, _AlbedoTextureArrayIndex[i*3+0])) * absNormal.y;
		fixed4 z = UNITY_SAMPLE_TEX2DARRAY(_AlbedoTextureArray, float3(tc.x, tc.y, _AlbedoTextureArrayIndex[i*3+1])) * absNormal.z;
		t = x + y + z;
		color += t * texBlend[i];
	}
	
	return color;
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
	o.vertColor = v.color;
	o.texBlend = v.texcoord;
}

void terrainSurf(Input IN, inout SurfaceOutputStandard o) {
	clip(IN);

	float3 tc = IN.worldPos / _TextureScale;
	float3 absNormal = normalize(abs(IN.worldNormal));
	float3 signNormal = sign(IN.worldNormal);

	fixed4 albedo = sampleTerrainAlbedo(tc, absNormal, signNormal, IN.texBlend);

	// Albedo comes from a texture tinted by color
	o.Albedo = albedo * _Color;
	// Metallic and smoothness come from slider variables
	o.Metallic = _Metallic;
	o.Smoothness = _Glossiness;
	o.Alpha = albedo.a;
}
