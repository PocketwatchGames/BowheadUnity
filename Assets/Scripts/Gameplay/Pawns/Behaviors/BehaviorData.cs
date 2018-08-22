using UnityEngine;

namespace Bowhead.Actors {

	public abstract class BehaviorData : EntityData {

		public Critter.CritterBehavior Create(Critter c, float score, int weaponIndex, int attackIndex) {
			return _Create(c, score, weaponIndex, attackIndex);
		}

		protected abstract Critter.CritterBehavior _Create(Critter c, float score, int weaponIndex, int attackIndex);

		new public static BehaviorData Get(string name) {
			return DataManager.GetData<BehaviorData>(name);
		}
	}

	public abstract class BehaviorData<T> : BehaviorData where T : Critter.CritterBehavior, new() {
		[ClassDropdown(typeof(Critter.CritterBehavior)), SerializeField]
		string _behaviorClass;
				
		public System.Type behaviorClass { get; private set; }

		protected override Critter.CritterBehavior _Create(Critter c, float score, int weaponIndex, int attackIndex) {
			var t = typeof(T);
			return Create(c, score, weaponIndex, attackIndex);
		}

		new public T Create(Critter c, float score, int weaponIndex, int attackIndex) {
			T behavior = (T)System.Activator.CreateInstance(behaviorClass);
			behavior.Init(c, this, score, weaponIndex, attackIndex);
			return behavior;
		}

		new public static BehaviorData<T> Get(string name) {
			return DataManager.GetData<BehaviorData<T>>(name);
		}

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (string.IsNullOrEmpty(_behaviorClass)) {
				behaviorClass = null;
			} else {
				behaviorClass = Utils.GetType(_behaviorClass);
				if (behaviorClass == null) {
					throw new System.Exception("Unable to find type for " + _behaviorClass);
				}
			}
		}
	}

	public partial class Critter : Pawn<Critter, CritterData> {


		abstract public class CritterBehavior {

			protected static EvaluationScore fail = new EvaluationScore() { score = 0 };
			public struct EvaluationScore {
				public EvaluationScore(CritterBehavior b, float score = 0) { this.behavior = b; this.score = score; }

				public float score;
				public CritterBehavior behavior;
			}

			protected Critter _critter;

			public BehaviorData data {
				get;
				private set;
			}
			public float scoreMultiplier;
			public int weaponIndex;
			public int attackIndex;

			virtual public void Init(Critter c, BehaviorData d, float score, int weaponIndex, int attackIndex) {
				data = d;
				_critter = c;
				scoreMultiplier = score;
				this.weaponIndex = weaponIndex;
				this.attackIndex = attackIndex;
			}
			abstract public System.Type GetBehaviorDataType();
			abstract public void Tick(float dt, ref Pawn.Input_t input);
			abstract public EvaluationScore Evaluate();
			virtual public void Start() { }
			virtual public bool IsValid() { return true; }

		}
		abstract public class CritterBehavior<D> : CritterBehavior where D : BehaviorData {

			new public D data {
				get;
				private set;
			}

			override public System.Type GetBehaviorDataType() {
				return typeof(D);
			}


			override public void Init(Critter c, BehaviorData d, float score, int weaponIndex, int attackIndex) {
				base.Init(c, d, score, weaponIndex, attackIndex);
				data = (D)d;
			}
		}

	}
}
