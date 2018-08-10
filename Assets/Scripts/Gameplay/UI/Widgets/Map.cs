// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bowhead.Client.UI {
	using IChunk = World.Streaming.IChunk;

	[RequireComponent(typeof(RawImage))]
	public sealed class Map : MonoBehaviourEx {

		struct Reveal_t {
			public Vector2 pos;
			public int radius;
		};

		const uint HASH_SIZE_XZ = World.VOXEL_CHUNK_SIZE_XZ;
		const uint HASH_SIZE = HASH_SIZE_XZ * HASH_SIZE_XZ;

		List<Reveal_t> _reveals = new List<Reveal_t>();

		[SerializeField]
		int _worldSize;
		[SerializeField]
		int _size;
		[SerializeField]
		int _revealSize;
		[SerializeField]
		float _iconScale;
		[SerializeField]
		Material _maskBlitMaterial;
		[SerializeField]
		Texture2D _revealTexture;
		[SerializeField]
		RawImage _maskImage;
		[SerializeField]
		Transform _markers;
		[SerializeField]
		Transform _alwaysVisibleMarkers;

		int _originX;
		int _originZ;

		WorldStreaming.IWorldStreaming _streaming;
		Texture2D _mainTexture;
		RenderTexture _maskTexture;
		Texture2D _blitTexture;
		Texture2D _blackTexture;
		RawImage _image;
		Color32[] _pixels;
		Vector3 _markersOrigin;
		bool _dirty;

		void Awake() {

			_mainTexture = AddGC(new Texture2D(_size, _size, TextureFormat.ARGB32, false));
			_maskTexture = AddGC(new RenderTexture(_size, _size, 1, RenderTextureFormat.R8, RenderTextureReadWrite.Linear));
			_blitTexture = AddGC(new Texture2D(_revealSize, _revealSize, TextureFormat.ARGB32, false));
			_blackTexture = AddGC(new Texture2D(_revealSize, _revealSize, TextureFormat.ARGB32, false));

			_pixels = _blitTexture.GetPixels32();

			_blackTexture.Clear(Color.black);
			_blackTexture.Apply(true, true);

			_mainTexture.Clear(Color.black);
			_mainTexture.Apply(true, true);

			_maskTexture.useMipMap = false;
			_maskTexture.Create();
			Graphics.Blit(_blackTexture, _maskTexture);

			_image = GetComponent<RawImage>();
			_image.texture = _mainTexture;

			_maskImage.texture = _maskTexture;
			
			_originX = int.MaxValue;
			_originZ = int.MaxValue;


			var scale = _image.rectTransform.sizeDelta / _worldSize;

			_markersOrigin = _markers.localPosition;
			_markers.localScale = new Vector3(scale.x, scale.y, 1);
			_alwaysVisibleMarkers.localScale = _markers.localScale;
		}

		void Update() {
			if (_dirty) {
				DirtyUpdate();
			}
		}


		public void SetStreaming(WorldStreaming.EGenerator genType) {
			_streaming = Bowhead.WorldStreaming.NewProceduralWorldStreaming(0, genType);
		}

		public void SetOrigin(int chunkX, int chunkZ) {
			if ((_originX == chunkX) && (_originZ == chunkZ)) {
				return;
			}

			_originX = chunkX;
			_originZ = chunkZ;

			FullUpdate();

			//_markers.localPosition = _markersOrigin - Vector3.Scale(_markers.localScale, new Vector3(_originX * World.VOXEL_CHUNK_SIZE_XZ, _originZ * World.VOXEL_CHUNK_SIZE_XZ, 0));
			_alwaysVisibleMarkers.localPosition = _markers.localPosition;
		}

		public void RevealArea(Vector2 pos, int radius) {
			_reveals.Add(new Reveal_t() {
				pos = pos,
				radius = radius
			});
			Reveal(pos, radius);
		}

		void Reveal(Vector2 pos, int radius) {

			float pixelsPerVoxel = (float)_size / _worldSize;
			var mapBottomLeft = new WorldVoxelPos_t((int)((_originX-_worldSize/2) * pixelsPerVoxel), 0,(int)((_originZ-_worldSize/2) * pixelsPerVoxel));

			var blockColors = World.Streaming.blockColors;

			int minX = (int)(pos.x * pixelsPerVoxel) - _revealSize / 2;
			int minZ = (int)(pos.y * pixelsPerVoxel) - _revealSize / 2;

			for (int z = 0; z < _revealSize; ++z) {
				var zofs = z * _revealSize;
				int vz = (int)pos.y + (int)((z - _revealSize / 2) / pixelsPerVoxel);
				for (int x = 0; x < _revealSize; ++x) {
					var ofs = zofs + x;
					int vx = (int)pos.x + (int)((x-_revealSize/2) / pixelsPerVoxel);

					EVoxelBlockType voxel;
					int elevation;
					_streaming.GetElevationAndTopBlock(vx, vz, out elevation, out voxel);
					if (voxel == EVoxelBlockType.Air) {
						continue;
					}
					var color = blockColors[(int)(voxel)-1];
					const float minElevation = -10;
					const float maxElevation = 54;
					float elevationT = (float)(elevation - minElevation) / (maxElevation - minElevation);
					Color32 elevationColor = Color32.Lerp(new Color(elevationT, elevationT, elevationT, 1f), color, 0.35f);
					_pixels[ofs] = elevationColor;
				}
			}

			_blitTexture.SetPixels32(_pixels);
			_blitTexture.Apply();
			BlitToMainTexture(_blitTexture, minX- mapBottomLeft.vx, minZ- mapBottomLeft.vz);



			GL.sRGBWrite = false;
			Graphics.SetRenderTarget(_maskTexture);
			GL.PushMatrix();
			GL.LoadPixelMatrix(0, _size, 0, _size);
			Graphics.DrawTexture(new Rect(minX- mapBottomLeft.vx, minZ- mapBottomLeft.vz, _revealSize, _revealSize), _revealTexture, _maskBlitMaterial);
			GL.PopMatrix();
			Graphics.SetRenderTarget(null);
		}

		public T CreateMarker<T>(T prefab, EMapMarkerStyle style) where T: UnityEngine.Object {
			var marker = Instantiate(prefab, (style == EMapMarkerStyle.Normal) ? _markers.transform : _alwaysVisibleMarkers.transform, false);
			marker.GetGameObject().transform.localScale = new Vector3(_iconScale, _iconScale, 1);
			return marker;
		}

		void FullUpdate() {
			_dirty = false;

			Graphics.Blit(_blackTexture, _maskTexture);

			foreach (var reveal in _reveals) {
				Reveal(reveal.pos, reveal.radius);
			}
		}

		void DirtyUpdate() {
			_dirty = false;
		}


		void BlitToMainTexture(Texture2D tex, int x, int y) {
			int right = Math.Min(x + tex.width, _mainTexture.width);
			int bottom = Math.Min(y + tex.height, _mainTexture.height);
			x = Math.Max(x, 0);
			y = Math.Max(y, 0);
			if (right > x && bottom > y) {
				Graphics.CopyTexture(tex, 0, 0, 0, 0, right - x, bottom - y, _mainTexture, 0, 0, x, y);
			}
		}


	}
}