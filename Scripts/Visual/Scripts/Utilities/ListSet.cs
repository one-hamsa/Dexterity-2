using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual
{
	public class ListSet<T> : List<T> {


		public ListSet() : base() { }

		public ListSet(IEnumerable<T> collection) : base(collection) { }

		public ListSet(int capacity) : base(capacity) { }

		public new bool Add(T item)
        {
			if (Contains(item))
				return false;

			base.Add(item);
			return true;
        }

		public new bool Remove(T item)
        {
			for (var i = 0; i < Count; ++i)
				if (this[i].Equals(item))
                {
					RemoveAt(i);
					return true;
                }

			return false;
        }

		public new void AddRange(IEnumerable<T> collection)
        {
			throw new NotImplementedException();
        }
	}
}