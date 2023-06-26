using System;
using System.Collections;
using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual.Utilities
{
	public class ListDictionary<TKey, TValue> : IDictionary<TKey, TValue> where TKey : IEquatable<TKey>
	{
		private readonly List<TKey> keys = new();
		private readonly List<TValue> values = new();

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			for (var i = 0; i < keys.Count; ++i)
				yield return new KeyValuePair<TKey, TValue>(keys[i], values[i]);
		}
		
		private int IndexOf(TKey key)
		{
			for (var i = 0; i < keys.Count; ++i)
			{
				if (keys[i].Equals(key))
					return i;
			}
			
			return -1;
		}
		
		private void RemoveAt(int index)
		{
			keys.RemoveAt(index);
			values.RemoveAt(index);
		}

		public void Add(KeyValuePair<TKey, TValue> item)
		{
			Add(item.Key, item.Value);
		}

		public void Clear()
		{
			keys.Clear();
			values.Clear();
		}

		public bool Contains(KeyValuePair<TKey, TValue> item)
		{
			var index = IndexOf(item.Key);
			return index != -1 && values[index].Equals(item.Value);
		}

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public bool Remove(KeyValuePair<TKey, TValue> item)
		{
			var index = IndexOf(item.Key);
			if (index == -1 || !values[index].Equals(item.Value))
				return false;
			
			RemoveAt(index);
			return true;
		}

		public int Count => keys.Count;

		bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;
		public void Add(TKey key, TValue value)
		{
			if (ContainsKey(key))
				throw new ArgumentException("An element with the same key already exists in the dictionary.");
			
			keys.Add(key);
			values.Add(value);
		}

		public bool ContainsKey(TKey key)
		{
			return IndexOf(key) != -1;
		}

		public bool Remove(TKey key)
		{
			var index = IndexOf(key);
			if (index == -1)
				return false;
			
			RemoveAt(index);
			return true;
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			var index = IndexOf(key);
			if (index == -1)
			{
				value = default;
				return false;
			}

			value = values[index];
			return true;
		}

		public TValue this[TKey key]
		{
			get
			{
				var index = IndexOf(key);
				if (index == -1)
					throw new KeyNotFoundException();
				
				return values[index];
			}
			set
			{
				var index = IndexOf(key);
				if (index == -1)
					Add(key, value);
				else
					values[index] = value;
			}
		}

		public ICollection<TKey> Keys => keys;
		public ICollection<TValue> Values => values;
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}