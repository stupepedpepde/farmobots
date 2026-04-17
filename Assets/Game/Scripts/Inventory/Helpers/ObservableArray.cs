using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Scripts.Inventory.Helpers {
    public class ObservableArray<T> : IEnumerable<T> where T : class {
        private T[] array;

        public event Action<T[]> AnyValueChanged;
        public event Action<T, int> ValueChanged;

        public int Length => array.Length;

        public ObservableArray(int capacity) => array = new T[capacity];

        public T this[int index] {
            get => index >= 0 && index < array.Length ? array[index] : null;
            set {
                if (index >= 0 && index < array.Length) {
                    array[index] = value;
                    ValueChanged?.Invoke(value, index);
                    AnyValueChanged?.Invoke(array);
                }
            }
        }

        public void Clear() {
            for (int i = 0; i < array.Length; i++)
                array[i] = null;
            AnyValueChanged?.Invoke(array);
        }

        public bool TryAdd(T item) {
            for (int i = 0; i < array.Length; i++)
                if (array[i] == null) {
                    array[i] = item;
                    ValueChanged?.Invoke(item, i);
                    return true;
                }
            return false;
        }

        public bool TryRemove(T item) {
            for (int i = 0; i < array.Length; i++)
                if (array[i] == item) {
                    array[i] = null;
                    ValueChanged?.Invoke(null, i);
                    return true;
                }
            return false;
        }

        public void Swap(int index1, int index2) {
            if (index1 >= 0 && index1 < array.Length && index2 >= 0 && index2 < array.Length) {
                (array[index1], array[index2]) = (array[index2], array[index1]);

                ValueChanged?.Invoke(array[index1], index1);
                ValueChanged?.Invoke(array[index2], index2);
            }
        }

        public IEnumerator<T> GetEnumerator() {
            foreach (T t in array)
                yield return t;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}