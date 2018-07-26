Shader "Unlit/SilhouetteStencil"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
		_Ref("Ref", Int) = 1
		_ReadMask("ReadMask", Int) = 255
		_WriteMask("WriteMask", Int) = 255
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			ColorMask 0
			ZWrite Off
			ZTest Equal
			Stencil
			{
				Ref [_Ref]
				Comp Always
				Pass Replace
				ZFail Keep
				ReadMask[_ReadMask]
				WriteMask[_WriteMask]
			}
		}
	}
}
