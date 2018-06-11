using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public enum EDecalRenderMode {
	Unlit,
	Lit
}

public class DeferredDecalRenderer {
	
	public interface Decal {

		Vector3 position {
			get; set;
		}

		Vector3 scale {
			get; set;
		}

		Quaternion rotation {
			get; set;
		}

		Material material {
			get; set;
		}

		bool visible {
			get; set;
		}
	}

	CommandBuffer _cmdBuffer;
	Mesh _unitCube;
	EDecalRenderMode _renderMode;
	List<Decal> _decals = new List<Decal>();
	List<Camera> _cameras = new List<Camera>();
	int _maxDecalsToDraw;
	int _lastAddedIndex;
	int _numDrawn;

	public DeferredDecalRenderer(EDecalRenderMode renderMode, Mesh unitCube, string name, int maxDecalsToDraw) {
		_renderMode = renderMode;
		_cmdBuffer = new CommandBuffer();
		_cmdBuffer.name = name;
		_unitCube = unitCube;
		_maxDecalsToDraw = maxDecalsToDraw;
	}

	public void AddCamera(Camera c) {
		if (!_cameras.Contains(c)) {
			c.AddCommandBuffer(cameraEvent, _cmdBuffer);
			_cameras.Add(c);
		}
	}

	public void RemoveCamera(Camera c) {
		if (_cameras.Contains(c)) {
			c.RemoveCommandBuffer(cameraEvent, _cmdBuffer);
			_cameras.Remove(c);
		}
	}

	public Decal NewDecal(Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool visible) {
		var d = new InternalDecal(position, scale, rotation, material, visible);
		_decals.Add(d);
		return d;
	}

	public void RemoveDecal(Decal d) {
		_decals.Remove(d);
		_lastAddedIndex = 0;
		_numDrawn = 0;
	}

	public void RemoveDecalByIndex(int idx) {
		_decals.RemoveAt(idx);
		_lastAddedIndex = 0;
		_numDrawn = 0;
	}

	public void Rebuild(bool added, bool removed) {
		if (removed || (_lastAddedIndex == 0)) {
			RebuildCommandBuffer();
		} else if (added) {
			AddNewDecalsToCommandBuffer();
		}
	}

	public void RemoveAllCameras() {
		foreach (Camera c in _cameras) {
			if (c != null) {
				c.RemoveCommandBuffer(cameraEvent, _cmdBuffer);
			}
		}
		_cameras.Clear();
	}

	void RebuildCommandBuffer() {
		_cmdBuffer.Clear();
		
		if (_renderMode == EDecalRenderMode.Unlit) {
			_cmdBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);
		} else {
			_cmdBuffer.SetRenderTarget(BuiltinRenderTextureType.GBuffer0, BuiltinRenderTextureType.CameraTarget);
		}
		
		_lastAddedIndex = 0;
		_numDrawn = 0;

		for (int i = 0; i < _decals.Count; ++i) {
			_lastAddedIndex = i + 1;

			var d = _decals[i];
			if (d.visible) {
				++_numDrawn;
				AddDecalDrawCommand(_decals[i]);
				if ((_maxDecalsToDraw > 0) && (_numDrawn >= _maxDecalsToDraw)) {
					break;
				}
			}
		}
	}

	void AddNewDecalsToCommandBuffer() {
		for (int i = _lastAddedIndex; i < _decals.Count; ++i) {
			_lastAddedIndex = i + 1;

			var d = _decals[i];
			if (d.visible) {
				++_numDrawn;
				AddDecalDrawCommand(_decals[i]);
				if ((_maxDecalsToDraw > 0) && (_numDrawn >= _maxDecalsToDraw)) {
					break;
				}
			}
		}
	}

	void AddDecalDrawCommand(Decal decal) {
		_cmdBuffer.DrawMesh(_unitCube, Matrix4x4.TRS(decal.position, decal.rotation, decal.scale), decal.material);
	}

	CameraEvent cameraEvent {
		get {
			return _renderMode == EDecalRenderMode.Unlit ? CameraEvent.AfterFinalPass : CameraEvent.BeforeLighting;
		}
	}

	class InternalDecal : Decal {
		Vector3 _pos;
		Vector3 _scale;
		Quaternion _rotation;
		Material _material;
		bool _visible;

		public InternalDecal(Vector3 pos, Vector3 scale, Quaternion rotation, Material material, bool visible) {
			_pos = pos;
			_scale = scale;
			_rotation = rotation;
			_material = material;
			_visible = visible;
		}

		public Vector3 position {
			get {
				return _pos;
			}
			set {
				_pos = value;
			}
		}

		public Vector3 scale {
			get {
				return _scale;
			}
			set {
				_scale = value;
			}
		}

		public Quaternion rotation {
			get {
				return _rotation;
			}
			set {
				_rotation = value;
			}
		}

		public Material material {
			get {
				return _material;
			}
			set {
				_material = value;
			}
		}

		public bool visible {
			get {
				return _visible;
			}
			set {
				_visible = value;
			}
		}
	}
}
