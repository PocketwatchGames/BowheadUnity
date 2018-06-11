using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Bowhead.Client {

	public class Decal : System.IDisposable {
		public delegate void UpdateDecalDelegate(Decal decal, float dt);

		internal DeferredDecalRenderer.Decal drd;

		DecalGroup _group;
		float _time;
		float _lifetime;
		UpdateDecalDelegate _update;
		bool _disposed;

		public virtual void Init(DeferredDecalRenderer.Decal drd, DecalGroup group, float lifetime, UpdateDecalDelegate update) {
			this.drd = drd;
			_group = group;
			_lifetime = lifetime;
			_update = update;
			autoDispose = true;
		}

		public void Dispose() {
			if (!_disposed) {
				_disposed = true;
				OnDisposed(true);
			}
		}

		public virtual void OnDisposed(bool dispose) {
			_group.RemoveDecal(this);
		}

		public virtual bool Tick(float dt) {
			if (_update != null) {
				_update(this, dt);
			}
			_time += dt;

			if (_lifetime > 0f) {
				return _time >= _lifetime;
			}
			return false;
		}

		public float timeFraction {
			get {
				return (_lifetime > 0f) ? (_time / _lifetime) : 1f;
			}
		}

		public bool autoDispose {
			get;
			set;
		}

		public Vector3 position {
			get {
				return drd.position;
			}
			set {
				if (drd.position != value) {
					drd.position = value;
					_group.DecalMovedOrRemoved();
				}
			}
		}

		public Vector3 scale {
			get {
				return drd.scale;
			}
			set {
				if (drd.scale != value) {
					drd.scale = value;
					_group.DecalMovedOrRemoved();
				}
			}
		}

		public Quaternion rotation {
			get {
				return drd.rotation;
			}
			set {
				if (drd.rotation != value) {
					drd.rotation = value;
					_group.DecalMovedOrRemoved();
				}
			}
		}

		public Material material {
			get {
				return drd.material;
			}
			set {
				if (drd.material != value) {
					drd.material = value;
					_group.DecalMovedOrRemoved();
				}
			}
		}

		public bool visible {
			get {
				return drd.visible;
			}
			set {
				if (drd.visible != value) {
					drd.visible = value;
					_group.DecalMovedOrRemoved();
				}
			}
		}
	}

	public class FadeDecal : Decal {

		Material _m;
		Material _shared;

		public override bool Tick(float dt) {

			if (_shared != material) {
				if (_m != null) {
					GameObject.Destroy(_m);
					_m = null;
				}
				if (material != null) {
					_m = GameObject.Instantiate<Material>(material);
					material = _m;
				}
				_shared = material;
			}

			if (_m != null) {
				var c = _m.color;
				c.a = 1f-timeFraction;
				_m.color = c;
			}

			return base.Tick(dt);
		}

		public override void OnDisposed(bool dispose) {
			base.OnDisposed(dispose);
			if (_m != null) {
				GameObject.Destroy(_m);
			}
		}
	}

	public class DecalGroup : System.IDisposable {
				
		bool _decalsAdded;
		bool _decalsRemoved;
		bool _disposed;
		int _maxDecals;
		DeferredDecalRenderer _decalRenderer;
		List<Decal> _decals = new List<Decal>();
		ReadOnlyCollection<Decal> _roDecals;

		public DecalGroup(EDecalRenderMode renderMode, Mesh decalUnitCube, string name, int maxDecals) {
			_decalRenderer = new DeferredDecalRenderer(renderMode, decalUnitCube, name, 0);
			_roDecals = new ReadOnlyCollection<Decal>(_decals);
			_maxDecals = maxDecals;
		}

		public Decal NewDecal(float lifetime, Decal.UpdateDecalDelegate update, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool visible) {
			return NewDecal<Decal>(lifetime, update, position, scale, rotation, material, visible);
		}

		public Decal NewDecal<T>(float lifetime, Decal.UpdateDecalDelegate update, Vector3 position, Vector3 scale, Quaternion rotation, Material material, bool visible) where T : Decal, new() {
			if ((_maxDecals > 0) && (_decals.Count >= _maxDecals)) {
				RemoveDecalByIndex(0);
			}

			var d = new T();
			d.Init(_decalRenderer.NewDecal(Vector3.zero, Vector3.one, Quaternion.identity, material, visible), this, lifetime, update);
			d.drd.position = position;
			d.drd.scale = scale;
			d.drd.rotation = rotation;
			_decals.Add(d);
			if (visible) {
				_decalsAdded = true;
			}
			return d;
		}

		public void RemoveDecal(Decal d) {
			if (_decals != null) {
				_decals.Remove(d);
			}
			if (_decalRenderer != null) {
				_decalRenderer.RemoveDecal(d.drd);
				if (d.visible) {
					_decalsRemoved = true;
				}
			}
		}

		void TickDecals(float dt) {
			for (int i = 0; i < _decals.Count;) {
				var d = _decals[i];
				if (d.visible && d.Tick(dt)) {
					if (d.autoDispose) {
						d.Dispose();
					} else {
						_decals.RemoveAt(i);
						_decalRenderer.RemoveDecalByIndex(i);
					}
					_decalsRemoved = true;
				} else {
					++i;
				}
			}
		}

		public void Update(float dt) {
			TickDecals(dt);
			if (_decalsAdded || _decalsRemoved) {
				_decalRenderer.Rebuild(_decalsAdded, _decalsRemoved);
				_decalsAdded = false;
				_decalsRemoved = false;
			}
		}

		public void Dispose() {
			if (!_disposed) {
				_disposed = true;
				if (_decalRenderer != null) {
					_decalRenderer.RemoveAllCameras();
					_decalRenderer = null;
				}
				if (_decals != null) {
					while (_decals.Count > 0) {
						_decals[0].Dispose();
					}
					_decals.Clear();
					_decals = null;
				}
			}
		}

		internal void DecalMovedOrRemoved() {
			_decalsRemoved = true;
		}

		public void AddDecalRendererToCamera(Camera c) {
			_decalRenderer.AddCamera(c);
		}

		public void RemoveDecalRendererFromCamera(Camera c) {
			_decalRenderer.RemoveCamera(c);
		}

		public void OnLevelStart() {
			_decalRenderer.Rebuild(true, true);
		}

		public void RemoveDecalByIndex(int index) {
			var d = _decals[index];
			_decals.RemoveAt(index);
			_decalRenderer.RemoveDecalByIndex(index);
			if (d.visible) {
				_decalsRemoved = true;
			}
		}

		public ReadOnlyCollection<Decal> decals {
			get {
				return _roDecals;
			}
		}

		public int maxDecals {
			get {
				return _maxDecals;
			}
			set {
				_maxDecals = value;
				if (_maxDecals > 0) {
					while (_decals.Count > _maxDecals) {
						RemoveDecalByIndex(0);
					}
				}
			}
		}
	}
}