using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

public class ReadOnlyCollectionEx<T> : System.Collections.ObjectModel.ReadOnlyCollection<T> {

	public ReadOnlyCollectionEx(IList<T> t) : base(t) { }

	public bool Wraps(IList<T> t) {
		return ReferenceEquals(Items, t);
	}
}
