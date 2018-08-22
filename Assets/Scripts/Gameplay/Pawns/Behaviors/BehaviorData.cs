using UnityEngine;

namespace Bowhead.Actors {

	public abstract class BehaviorData : EntityData {

		public Critter.CritterBehavior Create(Critter c) {
			return _Create(c);
		}

		protected abstract Critter.CritterBehavior _Create(Critter c);

		new public static BehaviorData Get(string name) {
			return DataManager.GetData<BehaviorData>(name);
		}
	}

	public abstract class BehaviorData<T> : BehaviorData where T : Critter.CritterBehavior, new() {
		protected override Critter.CritterBehavior _Create(Critter c) {
			return Create(c);
		}
		new public T Create(Critter c) {
			T behavior = new T();
			behavior.Init(c, this);
			return behavior;
		}

		new public static BehaviorData<T> Get(string name) {
			return DataManager.GetData<BehaviorData<T>>(name);
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

			virtual public void Init(Critter c, BehaviorData d) {
				data = d;
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


			override public void Init(Critter c, BehaviorData d) {
				base.Init(c, d);
				_critter = c;
				data = (D)d;
			}
		}

	}
}
