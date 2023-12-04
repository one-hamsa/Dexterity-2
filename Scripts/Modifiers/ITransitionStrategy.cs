using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [Serializable]
    public class InsertSortList<T>
    {
        [SerializeField]
        private readonly List<(int Key, T Value)> _data;

        public int Count => _data.Count;

        public List<(int Key, T Value)> keyValuePairs => _data;

        public InsertSortList()
        {
            _data = new(16);
        }

        public InsertSortList(int capacity)
        {
            _data = new(capacity);
        }

        public void Clear() => _data.Clear();

        public T this[int id]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                for (int i = 0; i < _data.Count; i++)
                {
                    if (_data[i].Key == id)
                        return _data[i].Value;
                }
                
                throw new IndexOutOfRangeException($"Collection does not contain ID = {id}");    
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => AddOrUpdate(id, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOrUpdate(int id, T value)
        {
            for (int i = 0; i < _data.Count; i++)
            {
                if (_data[i].Key == id)
                {
                    // Replace existing value
                    _data[i] = (id, value);
                    return;
                }
                if (_data[i].Key > id)
                {
                    // insert new value
                    _data.Insert(i, (id, value));
                    return;
                }
            }
            
            // add new value
            _data.Add((id, value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(int id, out T value)
        {
            for (int i = 0; i < _data.Count; i++)
            {
                var pair = _data[i];
                if (pair.Key == id)
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = default(T);
            return false;
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int id)
        {
            for (int i = 0; i < _data.Count; i++)
            {
                if (_data[i].Key == id)
                {
                    _data.RemoveAt(i);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int id)
        {
            for (int i = 0; i < _data.Count; i++)
            {
                if (_data[i].Key == id)
                    return true;
            }

            return false;
        }
    }
    
    public interface ITransitionStrategy
    {
        InsertSortList<float> Initialize(int[] states, int currentState);

        // should be called each frame
        InsertSortList<float> GetTransition(InsertSortList<float> prevState, 
            int currentState, double timeSinceStateChange, double deltaTime, out bool changed);

        public class TransitionInitializationException : Exception { 
            public TransitionInitializationException(string message) : base(message) { }
        }
    }
}
