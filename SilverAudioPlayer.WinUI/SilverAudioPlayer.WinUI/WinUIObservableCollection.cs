﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Interop;

namespace SilverAudioPlayer.WinUI
{
    
    public class WinUIObservableCollection<T> : Collection<T>,  INotifyPropertyChanged, global::Windows.UI.Xaml.Interop.INotifyCollectionChanged
    {
        private ReentrancyGuard reentrancyGuard = null;

        private class ReentrancyGuard : IDisposable
        {
            private WinUIObservableCollection<T> owningCollection;

            public ReentrancyGuard(WinUIObservableCollection<T> owningCollection)
            {
                owningCollection.CheckReentrancy();
                owningCollection.reentrancyGuard = this;
                this.owningCollection = owningCollection;
            }

            public void Dispose()
            {
                owningCollection.reentrancyGuard = null;
            }
        }

        public WinUIObservableCollection() : base() { }
        public WinUIObservableCollection(IList<T> list) : base(list.ToList()) { }
        public WinUIObservableCollection(IEnumerable<T> collection) : base(collection.ToList()) { }

       

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public void Move(int oldIndex, int newIndex)
        {
            MoveItem(oldIndex, newIndex);
        }

        protected IDisposable BlockReentrancy()
        {
            return new ReentrancyGuard(this);
        }

        protected void CheckReentrancy()
        {
            if (reentrancyGuard != null)
            {
                throw new InvalidOperationException("Collection cannot be modified in a collection changed handler.");
            }
        }

        protected override void ClearItems()
        {
            CheckReentrancy();

            TestBindableVector<T> oldItems = new TestBindableVector<T>(this);

            base.ClearItems();
            OnCollectionChanged(
                System.Collections.Specialized.NotifyCollectionChangedAction.Reset,
                null, oldItems, 0, 0);
        }

        protected override void InsertItem(int index, T item)
        {
            CheckReentrancy();

            TestBindableVector<T> newItem = new TestBindableVector<T>();
            newItem.Add(item);

            base.InsertItem(index, item);
            OnCollectionChanged(
                System.Collections.Specialized.NotifyCollectionChangedAction.Add,
                newItem, null, index, 0);
        }

        protected virtual void MoveItem(int oldIndex, int newIndex)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[oldIndex]);
            TestBindableVector<T> newItem = new TestBindableVector<T>(oldItem);

            T item = this[oldIndex];
            base.RemoveAt(oldIndex);
            base.InsertItem(newIndex, item);
            OnCollectionChanged(
                System.Collections.Specialized.NotifyCollectionChangedAction.Move,
                newItem, oldItem, newIndex, oldIndex);
        }

        protected override void RemoveItem(int index)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[index]);

            base.RemoveItem(index);
            OnCollectionChanged(
                System.Collections.Specialized.NotifyCollectionChangedAction.Remove,
                null, oldItem, 0, index);
        }

        protected override void SetItem(int index, T item)
        {
            CheckReentrancy();

            TestBindableVector<T> oldItem = new TestBindableVector<T>();
            oldItem.Add(this[index]);
            TestBindableVector<T> newItem = new TestBindableVector<T>();
            newItem.Add(item);

            base.SetItem(index, item);
            OnCollectionChanged(
                System.Collections.Specialized.NotifyCollectionChangedAction.Replace,
                newItem, oldItem, index, index);
        }

        protected virtual void OnCollectionChanged(
            System.Collections.Specialized.NotifyCollectionChangedAction action,
            IBindableVector newItems,
            IBindableVector oldItems,
            int newIndex,
            int oldIndex)
        {
            var a = (action) switch
            {
                System.Collections.Specialized.NotifyCollectionChangedAction.Add => global::Windows.UI.Xaml.Interop.NotifyCollectionChangedAction.Add,
                System.Collections.Specialized.NotifyCollectionChangedAction.Remove => global::Windows.UI.Xaml.Interop.NotifyCollectionChangedAction.Remove,
                System.Collections.Specialized.NotifyCollectionChangedAction.Replace => global::Windows.UI.Xaml.Interop.NotifyCollectionChangedAction.Replace,
                System.Collections.Specialized.NotifyCollectionChangedAction.Move => global::Windows.UI.Xaml.Interop.NotifyCollectionChangedAction.Move,
                System.Collections.Specialized.NotifyCollectionChangedAction.Reset => global::Windows.UI.Xaml.Interop.NotifyCollectionChangedAction.Reset,
            };
            OnCollectionChanged(new NotifyCollectionChangedEventArgs( a, newItems, oldItems, newIndex, oldIndex));
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            using (BlockReentrancy())
            {
                CollectionChanged?.Invoke(this, e);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class TestBindableVector<T> : IList<T>, IBindableVector
    {
        IList<T> implementation;

        public TestBindableVector() { implementation = new List<T>(); }
        public TestBindableVector(IList<T> list) { implementation = new List<T>(list); }

        public T this[int index] { get => implementation[index]; set => implementation[index] = value; }

        public int Count => implementation.Count;

        public virtual bool IsReadOnly => implementation.IsReadOnly;

        public void Add(T item)
        {
            implementation.Add(item);
        }

        public void Clear()
        {
            implementation.Clear();
        }

        public bool Contains(T item)
        {
            return implementation.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            implementation.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return implementation.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return implementation.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            implementation.Insert(index, item);
        }

        public bool Remove(T item)
        {
            return implementation.Remove(item);
        }

        public void RemoveAt(int index)
        {
            implementation.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return implementation.GetEnumerator();
        }

        public object GetAt(uint index)
        {
            return implementation[(int)index];
        }

        public IBindableVectorView GetView()
        {
            return new TestBindableVectorView<T>(implementation);
        }

        public bool IndexOf(object value, out uint index)
        {
            int indexOf = implementation.IndexOf((T)value);

            if (indexOf >= 0)
            {
                index = (uint)indexOf;
                return true;
            }
            else
            {
                index = 0;
                return false;
            }
        }

        public void SetAt(uint index, object value)
        {
            implementation[(int)index] = (T)value;
        }

        public void InsertAt(uint index, object value)
        {
            implementation.Insert((int)index, (T)value);
        }

        public void RemoveAt(uint index)
        {
            implementation.RemoveAt((int)index);
        }

        public void Append(object value)
        {
            implementation.Add((T)value);
        }

        public void RemoveAtEnd()
        {
            implementation.RemoveAt(implementation.Count - 1);
        }

        public uint Size => (uint)implementation.Count;

        public IBindableIterator First()
        {
            return new TestBindableIterator<T>(implementation);
        }
    }

    public class TestBindableVectorView<T> : TestBindableVector<T>, IBindableVectorView
    {
        public TestBindableVectorView(IList<T> list) : base(list) { }

        public override bool IsReadOnly => true;
    }

    public class TestBindableIterator<T> : IBindableIterator
    {
        private readonly IEnumerator<T> enumerator;

        public TestBindableIterator(IEnumerable<T> enumerable) { enumerator = enumerable.GetEnumerator(); }

        public bool MoveNext()
        {
            return enumerator.MoveNext();
        }

        public object Current => enumerator.Current;

        public bool HasCurrent => enumerator.Current != null;
    }
}
