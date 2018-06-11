// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead.Actors {

	public sealed class ActorProperty : ScriptableObject, ISerializationCallbackReceiver {

		[SerializeField]
		ActorPropertyClass _class;

		[SerializeField]
		ActorPropertyMetaClass[] metaClasses;

		public float initialValue;
		public float minValue;
		public float maxValue;
		public bool replicates;
		public bool hidden;

		HashSet<ActorPropertyMetaClass> _metaClasses = new HashSet<ActorPropertyMetaClass>();

		public bool IsA(ActorPropertyMetaClass metaClass) {
			if (metaClasses != null) {
				for (int i = 0; i < metaClasses.Length; ++i) {
					var c = metaClasses[i];
					if (c.IsA(metaClass)) {
						return true;
					}
				}
			}
			return false;
		}

		public bool IsAny(ActorPropertyMetaClass[] metaClasses) {
			if (this.metaClasses != null) {
				for (int i = 0; i < this.metaClasses.Length; ++i) {
					var c = this.metaClasses[i];
					if (c.IsAny(metaClasses)) {
						return true;
					}
				}
			}
			return false;
		}

		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			_metaClasses.Clear();
			for (int i = 0; i < metaClasses.Length; ++i) {
				_metaClasses.Add(metaClasses[i]);
			}
		}

		public ActorPropertyClass propertyClass {
			get {
				return _class;
			}
		}
	}

	public class ActorPropertyInstance : ImmutableActorPropertyInstance {

		ActorProperty _property;
		float _min;
		float _max;
		float _value;
		float _scale;
		float _bonus;
		int _level;
		
		public ActorPropertyInstance(ActorProperty property, int serverIndex, int clientIndex) : base(serverIndex, clientIndex) {
			base.parent = this;
			_property = property;
			_level = 1;
			_scale = 1;
			_bonus = 0;
			if (property != null) {
				_value = property.initialValue;
				_min = property.minValue;
				_max = property.maxValue;
			}
		}
		
		public void ServerBeginRefresh(float dt) {
			_min = _scale*_property.minValue;
			_max = (_scale*_property.maxValue)+_bonus;
		}

		public void ServerEndRefresh() {
			ServerClampValue();
		}

		public void ServerInitLevel(int level) {
			_level = level;
			_scale = _property.propertyClass.GetLevelScaling(level);
			_bonus = 0;
			_value = _scale * _property.initialValue;
			_min = _scale * _property.minValue;
			_max = _scale * _property.maxValue;
		}

		public void ServerInitGear(MetaGame.PlayerInventorySkills.ItemStats gear) {

			_bonus = 0;

			for (int i = 0; i < gear.stats.Values.Count; ++i) {
				var itemStat = gear.stats.Values[i];
				var scale = _property.propertyClass.GetStatBonusScale(itemStat.itemStatClass.metaClass);
				_bonus += scale*itemStat.total;
			}

			_value = (_scale*_property.initialValue)+_bonus;
			_max = (_scale*_property.maxValue)+_bonus;
		}

		public void ServerSetLevel(int level) {
			_level = level;
			_scale = _property.propertyClass.GetLevelScaling(level);
			_min = _scale*_property.minValue;
			_max = (_scale*_property.maxValue)+_bonus;
		}

		public void ServerSetGear(MetaGame.PlayerInventorySkills.ItemStats gear) {

			_bonus = 0;

			for (int i = 0; i < gear.stats.Values.Count; ++i) {
				var itemStat = gear.stats.Values[i];
				var scale = _property.propertyClass.GetStatBonusScale(itemStat.itemStatClass.metaClass);
				_bonus += scale*itemStat.total;
			}

			_max = (_scale*_property.maxValue)+_bonus;
		}

		public void ServerClampValue() {
			_value = Mathf.Clamp(_value, _min, _max);
		}

		public void ClientUpdate(float value, float min, float max) {
			_value = value;
			_min = min;
			_max = max;
		}

		public new float min {
			get {
				return _min;
			}
			set {
				_min = Mathf.Min(_min, value);
			}
		}

		public new float max {
			get {
				return _max;
			}
			set {
				_max = Mathf.Max(_max, value);
			}
		}

		public new float value {
			get {
				return _value;
			}

			set {
				_value = Mathf.Clamp(value, _min, _max);
			}
		}

		public bool replicates {
			get {
				return _property.replicates;
			}
		}

		new public ActorProperty property {
			get {
				return _property;
			}
		}
	}

	public class ImmutableActorPropertyInstance {

		ActorPropertyInstance _property;

		protected ImmutableActorPropertyInstance(int serverIndex, int clientIndex) {
			this.index = clientIndex;
			this.serverIndex = serverIndex;
		}

		public ImmutableActorPropertyInstance(ActorPropertyInstance property, int index) {
			this.index = index;
			_property = property;
		}

		public float GetPercentBonus(int toLevel) {
			if (property.propertyClass.percentBased) {
				return property.propertyClass.GetPPPLevelScaling(toLevel, value);
			}
			return 0;
		}

		public float GetPercentBonus(int toLevel, float amount) {
			var percent = GetPercentBonus(toLevel);
			amount += (amount/100f)*percent;
			return amount;
		}

		public float GetPercentReduction(int toLevel, float amount) {
			var percent = Mathf.Min(100f, GetPercentBonus(toLevel));
			amount -= (amount/100f)*percent;
			return amount;
		}

		public int index {
			get;
			private set;
		}

		public int serverIndex {
			get;
			private set;
		}

		protected ActorPropertyInstance parent {
			set {
				_property = value;
			}
		}

		public float min {
			get {
				return _property.min;
			}
		}

		public float max {
			get {
				return _property.max;
			}
		}

		public float value {
			get {
				return _property.value;
			}
		}

		public ActorProperty property {
			get {
				return _property.property;
			}
		}
	}

	[ReplicatedUsing(typeof(ReplicatedActorPropertiesSerializer))]
	public sealed class ReplicatedActorProperties : IEquatable<ReplicatedActorProperties> {
		const int NUM_INDEX_BITS = 4;
		const int MAX_PROPERTIES = (1<<NUM_INDEX_BITS)-1;

		class ReplProperty : IEquatable<ReplProperty> {
			ActorPropertyInstance p;
			bool didReplicate;

			public ReplProperty() { }

			public ReplProperty(ActorPropertyInstance p) {
				this.p = p;
				value = p.value;
				min = p.min;
				max = p.max;
				index = p.serverIndex;
			}

			public ReplProperty(ReplProperty p) {
				value = p.p.value;
				min = p.p.min;
				max = p.p.max;
				didReplicate = true;
			}

			public bool Equals(ReplProperty other) {
				return (value == other.p.value) &&
					(min == other.p.min) &&
					(max == other.p.max);
			}

			public bool WillSerialize(int index, ReplicatedActorProperties lastRepState) {
				if (!p.replicates) {
					return false;
				}

				if (lastRepState == null) {
					return true;
				}

				var repl = lastRepState._replProperties[index];
				return !repl.didReplicate || (repl.value != p.value) || (repl.min != p.min) || (repl.max != p.max);
			}

			public bool DeltaSerialize(Archive archive, int index, ReplicatedActorProperties lastRepState) {
				if (archive.isLoading) {
					uint mask = archive.ReadUnsignedBits(3);
					if (mask > 0) {
						this.index = (int)archive.ReadUnsignedBits(NUM_INDEX_BITS);
						if ((mask&1) != 0) {
							min = archive.ReadFloat();
						}
						if ((mask&2) != 0) {
							max = archive.ReadFloat();
						}
						if ((mask&4) != 0) {
							value = archive.ReadFloat();
						}

						return true;
					}
				} else if (p.replicates) {

					var repl = (lastRepState != null) ? lastRepState._replProperties[index] : null;
					var didReplicate = repl != null;

					uint mask = 0;
					if (!didReplicate || (repl.min != p.min)) {
						mask |= 1;
					}
					if (!didReplicate || (repl.max != p.max)) {
						mask |= 2;
					}
					if (!didReplicate || (repl.value != p.value)) {
						mask |= 4;
					}
					archive.WriteUnsignedBits(mask, 3);
					if (mask > 0) {
						archive.WriteUnsignedBits(index, NUM_INDEX_BITS);
						if ((mask&1) != 0) {
							min = p.min;
							archive.Write(min);
						}
						if ((mask&2) != 0) {
							max = p.max;
							archive.Write(max);
						}
						if ((mask&4) != 0) {
							value = p.value;
							archive.Write(value);
						}
						return true;
					}
				}
				return false;
			}

			public float value {
				get;
				private set;
			}

			public float min {
				get;
				private set;
			}

			public float max {
				get;
				private set;
			}
			
			public int index {
				get;
				private set;
			}
		}

		ReplProperty[] _replProperties;
		bool[] _replFlags;

		public ReplicatedActorProperties(ActorPropertyInstance[] properties) {
			if (properties.Length > MAX_PROPERTIES) {
				throw new System.Exception("Too many properties on actor to replicate! MAX = " + MAX_PROPERTIES);
			}
			_replFlags = new bool[properties.Length];
			_replProperties = new ReplProperty[properties.Length];

			for (int i = 0; i < properties.Length; ++i) {
				_replProperties[i] = new ReplProperty(properties[i]);
			}
		}

		public ReplicatedActorProperties(ReplicatedActorProperties other) {
			_replProperties = new ReplProperty[other._replProperties.Length];
			for (int i = 0; i < _replProperties.Length; ++i) {
				_replProperties[i] = new ReplProperty(other._replProperties[i]);
			}
		}

		public ReplicatedActorProperties() {}

		public bool Equals(ReplicatedActorProperties other) {
			if (_replProperties.Length != other._replProperties.Length) {
				return false;
			}
			for (int i = 0; i < _replProperties.Length; ++i) {
				if (!_replProperties[i].Equals(other._replProperties[i])) {
					return false;
				}
			}
			return true;
		}

		public bool Serialize(Archive archive, ReplicatedActorProperties lastRepState) {
			if (archive.isLoading) {
				uint count = archive.ReadUnsignedBits(NUM_INDEX_BITS);

				if (_replProperties == null) {
					// first time being serialized on a client...
					_replProperties = new ReplProperty[count];
					for (int i = 0; i < _replProperties.Length; ++i) {
						_replProperties[i] = new ReplProperty();
					}

					_replFlags = new bool[count];
				}

				for (int i = 0; (count > 0) && (i < _replProperties.Length); ++i) {
					var p = _replProperties[i];
					if ((count > 0) && p.DeltaSerialize(archive, i, null)) {
						_replFlags[i] = true;
						--count;
					} else {
						_replFlags[i] = false;
					}
				}
				
			} else {
				int count = 0;
				for (int i = 0; i < _replProperties.Length; ++i) {
					var p = _replProperties[i];
					if (p.WillSerialize(i, lastRepState)) {
						++count;
					}
				}
				archive.WriteUnsignedBits(count, NUM_INDEX_BITS);

				for (int i = 0; (count > 0) && (i < _replProperties.Length); ++i) {
					var p = _replProperties[i];
					if (p.DeltaSerialize(archive, i, lastRepState)) {
						--count;
					}
				}
			}
			return archive.isLoading;
		}

		public ActorPropertyInstance ClientNewPropertyInstance(IList<ActorProperty> properties, int index) {
			var r = _replProperties[index];
			return new ActorPropertyInstance(properties[r.index], r.index, index);
		}

		public bool ClientOnRepProperty(ActorPropertyInstance p, int index) {
			if (_replFlags[index]) {
				_replFlags[index] = false;
				var r = _replProperties[index];
				p.ClientUpdate(r.value, r.min, r.max);
				return true;
			}
			return false;
		}

		public int count {
			get {
				return _replProperties.Length;
			}
		}
	}

	public sealed class ReplicatedActorPropertiesSerializer : SerializableObjectNonReferenceFieldSerializer<ReplicatedActorPropertiesSerializer> {
		
		public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
			ReplicatedActorProperties props = (ReplicatedActorProperties)field;
			if (props == null) {
				props = new ReplicatedActorProperties();
				field = props;
			}
			return props.Serialize(archive, (ReplicatedActorProperties)lastFieldState);
		}

		public override bool FieldsAreEqual(object a, object b) {
			return (a != null) && (b != null) && ((ReplicatedActorProperties)a).Equals((ReplicatedActorProperties)b);
		}

		public override object Copy(object toCopy) {
			return new ReplicatedActorProperties((ReplicatedActorProperties)toCopy);
		}
	}
}
