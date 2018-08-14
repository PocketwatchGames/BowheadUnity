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

struct Fragment_t {
	fixed4 albedo;
	fixed4 ao_roughness;
	half3 normal;
};

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

	for (int i = 0; i < 4; ++i) {
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

	for (int i = 0; i < 4; ++i) {
		normal += triplanarSampleWorldNormal(i, UNITY_PASS_TEX2DARRAY(texArray), arrayIndices, uvs, triblend, signNormal, bumpNormals) * texBlend[i];
	}

	return (half3)normalize(normal);
}

fixed4 sampleTerrainAlbedo(float2 uvs[3], half3 triblend, half3 signNormal, half4 texBlend) {
	return triplanarColor(UNITY_PASS_TEX2DARRAY(_AlbedoTextureArray), _AlbedoTextureArrayIndices, uvs, triblend, signNormal, texBlend);
}

half3 sampleTerrainWorldNormal(float2 uvs[3], half3 triblend, half3 signNormal, half3 bumpNormals[3], half4 texBlend) {
	return triplanarWorldNormal(UNITY_PASS_TEX2DARRAY(_NormalsTextureArray), _NormalsTextureArrayIndices, uvs, triblend, signNormal, bumpNormals, texBlend);
}

fixed sampleTerrainAO(float2 uvs[3], half3 triblend, half3 signNormal, half4 texBlend) {
	return triplanarColor(UNITY_PASS_TEX2DARRAY(_AOTextureArray), _AOTextureArrayIndices, uvs, triblend, signNormal, texBlend).r;
}

fixed sampleTerrainRoughness(float2 uvs[3], half3 triblend, half3 signNormal, half4 texBlend) {
	return triplanarColor(UNITY_PASS_TEX2DARRAY(_RoughnessTextureArray), _RoughnessTextureArrayIndices, uvs, triblend, signNormal, texBlend).r;
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

	half3 triblend = saturate(pow(IN.worldNormal, 4));
	triblend /= dot(triblend, half3(1, 1, 1));

	float3 uvbase = IN.worldPos / _TextureScale;

	// triplanar uvs
	float2 uvs[3];
	uvs[0] = uvbase.zy;
	uvs[1] = uvbase.xz;
	uvs[2] = uvbase.xy;

	uvs[1] += 0.33f;
	uvs[2] += 0.67f;

	half3 worldNormal = normalize(IN.worldNormal);
	half3 signNormal = worldNormal < 0 ? -1 : 1;
	half3 absNormal = worldNormal * signNormal;

	uvs[0].x *= signNormal.x;
	uvs[1].x *= signNormal.y;
	uvs[2].x *= -signNormal.z;

	half3 bumpNormals[3];
	bumpNormals[0] = half3(worldNormal.zy, absNormal.x);
	bumpNormals[1] = half3(worldNormal.xz, absNormal.y);
	bumpNormals[2] = half3(worldNormal.xy, absNormal.z);
			
	fixed4 albedo = sampleTerrainAlbedo(uvs, triblend, signNormal, IN.texBlend);
	half3 normal = sampleTerrainWorldNormal(uvs, triblend, signNormal, bumpNormals, IN.texBlend);
	fixed ao = sampleTerrainAO(uvs, triblend, signNormal, IN.texBlend);
	fixed roughness = sampleTerrainRoughness(uvs, triblend, signNormal, IN.texBlend);

	// Albedo comes from a texture tinted by color
	o.Albedo = albedo * _Color;
	o.Normal = WorldToTangentNormalVector(IN, normal);
	o.Occlusion = ao;
	// Metallic and smoothness come from slider variables
	o.Metallic = 0;// _Metallic;
	o.Smoothness = roughness;// _Glossiness;
	o.Alpha = albedo.a * _Color.a;
}
