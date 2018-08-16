// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Copyright (c) 2018 Pocketwatch Games LLC.
#ifdef FOUR_MATS
#define NUM_MATS 4
#elif defined(THREE_MATS)
#define NUM_MATS 3
#elif defined(TWO_MATS)
#define NUM_MATS 2
#else
#define NUM_MATS 1
#endif
#define TRIP_SAMPLE_COUNT NUM_MATS*3

// Common terrain shading shit.
UNITY_DECLARE_TEX2DARRAY(_AlbedoTextureArray);
UNITY_DECLARE_TEX2DARRAY(_NormalsTextureArray);
//UNITY_DECLARE_TEX2DARRAY(_RoughnessTextureArray);
//UNITY_DECLARE_TEX2DARRAY(_AOTextureArray);
//UNITY_DECLARE_TEX2DARRAY(_HeightTextureArray);
UNITY_DECLARE_TEX2DARRAY(_RHOTextureArray);

float _AlbedoTextureArrayIndices[12];
float _NormalsTextureArrayIndices[12];
//float _RoughnessTextureArrayIndices[12];
//float _AOTextureArrayIndices[12];
//float _HeightTextureArrayIndices[12];
float _RHOTextureArrayIndices[12];

struct Input {
	//float2 uv_MainTex;
	half4 texBlend;
	//float4 clipPos;
	float3 worldPos;
	float3 worldNormal;
	INTERNAL_DATA
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

// hack to work around the way Unity passes the tangent to world matrix to surface shaders to prevent compiler errors
// from: https://github.com/bgolus/Normal-Mapping-for-a-Triplanar-Shader/blob/master/TriplanarSurfaceShader.shader
#if defined(INTERNAL_DATA) && (defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_FORWARDADD) || defined(UNITY_PASS_DEFERRED) || defined(UNITY_PASS_META))
#define WorldToTangentNormalVector(data,normal) mul(normal, half3x3(data.internalSurfaceTtoW0, data.internalSurfaceTtoW1, data.internalSurfaceTtoW2))
#else
#define WorldToTangentNormalVector(data,normal) normal
#endif

fixed4 triplanarSampleColor(int i, UNITY_ARGS_TEX2DARRAY(texArray), float arrayIndices[12], float2 uvs[3], half3 triblend, half3 signNormal) {
	fixed4 t;
	fixed4 x = UNITY_SAMPLE_TEX2DARRAY(texArray, float3(uvs[0], arrayIndices[i * 3 + 1])) * triblend.x;
	fixed4 y = UNITY_SAMPLE_TEX2DARRAY(texArray, float3(uvs[1], arrayIndices[i * 3 + (int)(1 + signNormal.y)])) * triblend.y;
	fixed4 z = UNITY_SAMPLE_TEX2DARRAY(texArray, float3(uvs[2], arrayIndices[i * 3 + 1])) * triblend.z;
	t = x + y + z;
	return t;
}

fixed4 triplanarColor(UNITY_ARGS_TEX2DARRAY(texArray), float arrayIndices[12], float2 uvs[3], half3 triblend, half3 signNormal, half4 texBlend) {
	fixed4 color = fixed4(0, 0, 0, 0);

	[unroll]
	for (int i = 0; i < NUM_MATS; ++i) {
		color += triplanarSampleColor(i, UNITY_PASS_TEX2DARRAY(texArray), arrayIndices, uvs, triblend, signNormal) * texBlend[i];
	}

	return color;
}

// Reoriented Normal Mapping
// http://blog.selfshadow.com/publications/blending-in-detail/
// Altered to take normals (-1 to 1 ranges) rather than unsigned normal maps (0 to 1 ranges)
half3 blend_rnm(half3 n1, half3 n2) {
	n1.z += 1;
	n2.xy = -n2.xy;
	return n1 * dot(n1, n2) / n1.z - n2;
}

half3 triplanarSampleWorldNormal(int i, UNITY_ARGS_TEX2DARRAY(texArray), float arrayIndices[12], float2 uvs[3], half3 triblend, half3 signNormal, half3 bumpNormals[3]) {
	half3 x = UnpackNormal(UNITY_SAMPLE_TEX2DARRAY(texArray, float3(uvs[0], arrayIndices[i * 3 + 1])));
	half3 y = UnpackNormal(UNITY_SAMPLE_TEX2DARRAY(texArray, float3(uvs[1], arrayIndices[i * 3 + (int)(1 + signNormal.y)])));
	half3 z = UnpackNormal(UNITY_SAMPLE_TEX2DARRAY(texArray, float3(uvs[2], arrayIndices[i * 3 + 1])));

	x.x *= signNormal.x;
	y.x *= signNormal.y;
	z.x *= -signNormal.z;

	half3 nx = blend_rnm(bumpNormals[0], x);
	half3 ny = blend_rnm(bumpNormals[1], y);
	half3 nz = blend_rnm(bumpNormals[2], z);

	nx.z *= signNormal.x;
	ny.z *= signNormal.y;
	nz.z *= signNormal.z;

	// world space normal
	half3 wn = normalize((nx.zyx * triblend.x) + (ny.xzy * triblend.y) + (nz.xyz * triblend.z));

	return wn;
}

half3 triplanarWorldNormal(UNITY_ARGS_TEX2DARRAY(texArray), float arrayIndices[12], float2 uvs[3], half3 triblend, half3 signNormal, half3 bumpNormals[3], half4 texBlend) {
	float3 normal = float3(0, 0, 0);

	[unroll]
	for (int i = 0; i < NUM_MATS; ++i) {
		normal += triplanarSampleWorldNormal(i, UNITY_PASS_TEX2DARRAY(texArray), arrayIndices, uvs, triblend, signNormal, bumpNormals) * texBlend[i];
	}

	return (half3)normalize(normal);
}

half4 sampleTerrainHeight(float2 uvs[3], half3 triblend, half3 signNormal, half4 texBlend) {
	half4 height;

	height.r = triplanarSampleColor(0, UNITY_PASS_TEX2DARRAY(_RHOTextureArray), _RHOTextureArrayIndices, uvs, triblend, signNormal).a * texBlend[0];
	height.g = triplanarSampleColor(1, UNITY_PASS_TEX2DARRAY(_RHOTextureArray), _RHOTextureArrayIndices, uvs, triblend, signNormal).a * texBlend[1];
	height.b = triplanarSampleColor(2, UNITY_PASS_TEX2DARRAY(_RHOTextureArray), _RHOTextureArrayIndices, uvs, triblend, signNormal).a * texBlend[2];
	height.a = triplanarSampleColor(3, UNITY_PASS_TEX2DARRAY(_RHOTextureArray), _RHOTextureArrayIndices, uvs, triblend, signNormal).a * texBlend[3];

	return height;
}

fixed4 sampleTerrainAlbedo(float2 uvs[3], half3 triblend, half3 signNormal, half4 texBlend) {
	return triplanarColor(UNITY_PASS_TEX2DARRAY(_AlbedoTextureArray), _AlbedoTextureArrayIndices, uvs, triblend, signNormal, texBlend);
}

half3 sampleTerrainWorldNormal(float2 uvs[3], half3 triblend, half3 signNormal, half3 bumpNormals[3], half4 texBlend) {
	return triplanarWorldNormal(UNITY_PASS_TEX2DARRAY(_NormalsTextureArray), _NormalsTextureArrayIndices, uvs, triblend, signNormal, bumpNormals, texBlend);
}

fixed sampleTerrainAO(float2 uvs[3], half3 triblend, half3 signNormal, half4 texBlend) {
	return triplanarColor(UNITY_PASS_TEX2DARRAY(_RHOTextureArray), _RHOTextureArrayIndices, uvs, triblend, signNormal, texBlend).g;
}

fixed sampleTerrainRoughness(float2 uvs[3], half3 triblend, half3 signNormal, half4 texBlend) {
	return triplanarColor(UNITY_PASS_TEX2DARRAY(_RHOTextureArray), _RHOTextureArrayIndices, uvs, triblend, signNormal, texBlend).r;
}

void sampleRHO(float2 uvs[3], half3 signNormal, out fixed4 samples[TRIP_SAMPLE_COUNT]) {
	[unroll]
	for (int i = 0; i < NUM_MATS; ++i) {
		samples[i*3+0] = UNITY_SAMPLE_TEX2DARRAY(_RHOTextureArray, float3(uvs[0], _RHOTextureArrayIndices[i * 3 + 1]));
		samples[i*3+1] = UNITY_SAMPLE_TEX2DARRAY(_RHOTextureArray, float3(uvs[1], _RHOTextureArrayIndices[i * 3 + (int)(1 + signNormal.y)]));
		samples[i*3+2] = UNITY_SAMPLE_TEX2DARRAY(_RHOTextureArray, float3(uvs[2], _RHOTextureArrayIndices[i * 3 + 1]));
	}
}

half4 rho_Height(half3 triblend, half4 texBlend, fixed4 samples[TRIP_SAMPLE_COUNT]) {
	half4 height = half4(0, 0, 0, 0);
	height.x = (samples[0].w + triblend.x) + (samples[1].w + triblend.y) + (samples[2].w + triblend.z);
#if NUM_MATS > 1
	height.y = (samples[3].w + triblend.x) + (samples[4].w + triblend.y) + (samples[5].w + triblend.z);
#endif
#if NUM_MATS > 2
	height.z = (samples[6].w + triblend.x) + (samples[7].w + triblend.y) + (samples[8].w + triblend.z);
#endif
#if NUM_MATS > 3
	height.w = (samples[9].w + triblend.x) + (samples[10].w + triblend.y) + (samples[11].w + triblend.z);
#endif
	return height * texBlend;
}

fixed4 rho_Blend(half3 triblend, half4 texBlend, fixed4 samples[TRIP_SAMPLE_COUNT]) {
	fixed4 sum;
	fixed4 z;

	z = (samples[0] * triblend.x) + (samples[1] * triblend.y) + (samples[2] * triblend.z);
	sum = z * texBlend.x;
	
#if NUM_MATS > 1
	z = (samples[3] * triblend.x) + (samples[4] * triblend.y) + (samples[5] * triblend.z);
	sum += z * texBlend.y;
#endif
#if NUM_MATS > 2
	z = (samples[6] * triblend.x) + (samples[7] * triblend.y) + (samples[8] * triblend.z);
	sum += z * texBlend.z;
#endif
#if NUM_MATS > 3
	z = (samples[9] * triblend.x) + (samples[10] * triblend.y) + (samples[11] * triblend.z);
	sum += z * texBlend.w;
#endif

	return sum;
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
	//o.clipPos = UnityObjectToClipPos(v.vertex);
	o.texBlend = v.texcoord;
}

void terrainSurf(Input IN, inout SurfaceOutputStandard o) {
	clip(IN);
	IN.worldNormal = WorldNormalVector(IN, float3(0, 0, 1));

	half3 worldNormal = normalize(IN.worldNormal);
	half3 signNormal = worldNormal < 0 ? -1 : 1;
	half3 absNormal = worldNormal * signNormal;

	half3 triblend = saturate(pow(worldNormal, 4));
	triblend /= dot(triblend, half3(1, 1, 1));
	
	float3 uvbase = IN.worldPos / _TextureScale;

	// triplanar uvs
	float2 uvs[3];
	uvs[0] = uvbase.zy;
	uvs[1] = uvbase.xz;
	uvs[2] = uvbase.xy;

	//uvs[1] += 0.33f;
	//uvs[2] += 0.67f;
	
	uvs[0].x *= signNormal.x;
	uvs[1].x *= signNormal.y;
	uvs[2].x *= -signNormal.z;
	
	fixed4 rhoSamples[TRIP_SAMPLE_COUNT];
	sampleRHO(uvs, signNormal, rhoSamples);
	
	half4 height = rho_Height(triblend, IN.texBlend, rhoSamples);//sampleTerrainHeight(uvs, triblend, signNormal, IN.texBlend);// +(IN.texBlend * 0.1);
	//height = pow(height, 4);
	half baseHeight = max(max(max(height.x, height.y), height.z), height.w) * 0.3;// -0.3;
	height = max(height - baseHeight.xxxx, half4(0, 0, 0, 0));
	height /= dot(height, half4(1, 1, 1, 1));

	fixed4 rho = rho_Blend(triblend, height, rhoSamples);
	
	fixed4 albedo = sampleTerrainAlbedo(uvs, triblend, signNormal, height);

	half3 bumpNormals[3];
	bumpNormals[0] = half3(worldNormal.zy, absNormal.x);
	bumpNormals[1] = half3(worldNormal.xz, absNormal.y);
	bumpNormals[2] = half3(worldNormal.xy, absNormal.z);
		
	half3 normal = sampleTerrainWorldNormal(uvs, triblend, signNormal, bumpNormals, height);
	
	// Albedo comes from a texture tinted by color
	o.Albedo = albedo * _Color;

	o.Normal = fixed3(0, 0, 1);// WorldToTangentNormalVector(IN, normal);
	o.Occlusion = rho.y;

	// Metallic and smoothness come from slider variables
	o.Metallic = 0;// _Metallic;
	o.Smoothness = rho.x;// _Glossiness;
	o.Alpha = albedo.a * _Color.a;
}
