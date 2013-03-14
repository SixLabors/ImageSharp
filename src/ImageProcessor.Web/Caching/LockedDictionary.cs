// -----------------------------------------------------------------------
// <copyright file="LockedDictionary.cs" company="James South">
//     Copyright (c) James South.
//     Dual licensed under the MIT or GPL Version 2 licenses.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Web.Caching
{
    #region Using
    using System.Collections.Generic;
    using System.Linq;
    #endregion

    /// <summary>
    /// Represents a collection of keys and values that are thread safe.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of the keys in the dictionary.
    /// </typeparam>
    /// <typeparam name="TVal">
    /// The type of the values in the dictionary.
    /// </typeparam>
    internal class LockedDictionary<TKey, TVal> : IDictionary<TKey, TVal>
    {
        /// <summary>
        /// The _inner.
        /// </summary>
        private readonly Dictionary<TKey, TVal> innerDictionary = new Dictionary<TKey, TVal>();

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="LockedDictionary{TKey,TVal}"/> class. 
        /// </summary>
        /// <param name="val">
        /// The value to initialize the LockedDictionary with.
        /// </param>
        public LockedDictionary(IEnumerable<KeyValuePair<TKey, TVal>> val = null)
        {
            if (val != null)
            {
                this.innerDictionary = val.ToDictionary(x => x.Key, x => x.Value);
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets a collection containing the keys in the <see cref="LockedDictionary{TKey,TVal}"/>.
        /// </summary>
        public ICollection<TKey> Keys
        {
            get
            {
                lock (this.innerDictionary)
                {
                    return this.innerDictionary.Keys.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets a collection containing the values in the <see cref="LockedDictionary{TKey,TVal}"/>.
        /// </summary>
        public ICollection<TVal> Values
        {
            get
            {
                lock (this.innerDictionary)
                {
                    return this.innerDictionary.Values.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets the number of key/value pairs contained in the <see cref="LockedDictionary{TKey,TVal}"/>.
        /// </summary>
        public int Count
        {
            get
            {
                lock (this.innerDictionary)
                {
                    return this.innerDictionary.Count;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="LockedDictionary{TKey,TVal}"/> is read only.
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">
        /// The key of the value to get or set.
        /// </param>
        /// <returns>
        /// TThe value associated with the specified key. If the specified key is not found, 
        /// a get operation throws a <see cref="T:System.Collections.Generic.KeyNotFoundException"/> , 
        /// and a set operation creates a new element with the specified key.
        /// </returns>
        public TVal this[TKey key]
        {
            get
            {
                lock (this.innerDictionary)
                {
                    return this.innerDictionary[key];
                }
            }

            set
            {
                lock (this.innerDictionary)
                {
                    this.innerDictionary[key] = value;
                }
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// </summary>
        /// <param name="key">
        /// The key of the element to add.
        /// </param>
        /// <param name="value">
        /// The value of the element to add. The value can be null for reference types.
        /// </param>
        public void Add(TKey key, TVal value)
        {
            lock (this.innerDictionary)
            {
                this.innerDictionary.Add(key, value);
            }
        }

        /// <summary>
        /// Determines whether the LockedDictionary contains the specified key.
        /// </summary>
        /// <param name="key">
        /// The key to locate in the LockedDictionary.
        /// </param>
        /// <returns>
        /// true if the LockedDictionary contains the key; otherwise, false.
        /// </returns>
        public bool ContainsKey(TKey key)
        {
            lock (this.innerDictionary)
            {
                return this.innerDictionary.ContainsKey(key);
            }
        }

        /// <summary>
        /// Removes the value with the specified key from the <see cref="LockedDictionary{TKey,TVal}"/>.
        /// </summary>
        /// <param name="key">
        /// The key of the element to remove.
        /// </param>
        /// <returns>
        /// true if the element is successfully found and removed; otherwise, false. 
        /// This method returns false if key is not found in the <see cref="LockedDictionary{TKey,TVal}"/>.
        /// </returns>
        public bool Remove(TKey key)
        {
            lock (this.innerDictionary)
            {
                return this.innerDictionary.Remove(key);
            }
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">
        /// The key of the value to get.
        /// </param>
        /// <param name="value">
        /// When this method returns, contains the value associated with the specified key, if the key is found; 
        /// otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// true if the <see cref="LockedDictionary{TKey,TVal}"/> contains an element with 
        /// the specified key; otherwise, false.
        /// </returns>
        public bool TryGetValue(TKey key, out TVal value)
        {
            lock (this.innerDictionary)
            {
                return this.innerDictionary.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// </summary>
        /// <param name="item">
        /// The <see cref="System.Collections.Generic.KeyValuePair{TKey, TVal}"/> representing
        /// the item to add.
        /// </param>
        public void Add(KeyValuePair<TKey, TVal> item)
        {
            lock (this.innerDictionary)
            {
                this.innerDictionary.Add(item.Key, item.Value);
            }
        }

        /// <summary>
        /// Removes all keys and values from the <see cref="LockedDictionary{TKey,TVal}"/>.
        /// </summary>
        public void Clear()
        {
            lock (this.innerDictionary)
            {
                this.innerDictionary.Clear();
            }
        }

        /// <summary>
        /// Determines whether the <see cref="LockedDictionary{TKey,TVal}"/> contains the specified key.
        /// </summary>
        /// <param name="item">
        /// The <see cref="System.Collections.Generic.KeyValuePair{TKey, TVal}"/> representing
        /// the item to locate.
        /// </param>
        /// <returns>
        /// true if the <see cref="LockedDictionary{TKey,TVal}"/> contains an element 
        /// with the specified key; otherwise, false.
        /// </returns>
        public bool Contains(KeyValuePair<TKey, TVal> item)
        {
            lock (this.innerDictionary)
            {
                var inner = this.innerDictionary as IDictionary<TKey, TVal>;
                return inner.Contains(item);
            }
        }

        /// <summary>
        /// Copies the elements of an <see cref="LockedDictionary{TKey,TVal}"/> to a one-dimensional
        /// <see cref="T:System.Array"/> starting at a particular <see cref="T:System.Array"/> index.
        /// </summary>
        /// <param name="array">
        /// The one-dimensional <see cref="T:System.Array"/> that is the destination of the elements copied 
        /// from <see cref="LockedDictionary{TKey,TVal}"/>.KeyCollection. 
        /// The <see cref="T:System.Array"/> must have zero-based indexing.
        /// </param>
        /// <param name="arrayIndex">
        /// The zero-based index in array at which copying begins.
        /// </param>
        public void CopyTo(KeyValuePair<TKey, TVal>[] array, int arrayIndex)
        {
            lock (this.innerDictionary)
            {
                var inner = this.innerDictionary as IDictionary<TKey, TVal>;
                inner.CopyTo(array, arrayIndex);
            }
        }

        /// <summary>
        /// Removes the item with the specified <see cref="System.Collections.Generic.KeyValuePair{TKey, TVal}"/> 
        /// from the <see cref="LockedDictionary{TKey,TVal}"/>
        /// </summary>
        /// <param name="item">
        /// The <see cref="System.Collections.Generic.KeyValuePair{TKey, TVal}"/>representing the item to remove.
        /// </param>
        /// <returns>
        /// This method returns false if item is not found in the <see cref="LockedDictionary{TKey,TVal}"/>.
        /// </returns>
        public bool Remove(KeyValuePair<TKey, TVal> item)
        {
            lock (this.innerDictionary)
            {
                var inner = this.innerDictionary as IDictionary<TKey, TVal>;
                return inner.Remove(item);
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="LockedDictionary{TKey,TVal}"/>.KeyCollection.
        /// </summary>
        /// <returns>
        /// A <see cref="System.Collections.Generic.Dictionary{TKey,TValue}.KeyCollection.Enumerator"/> 
        /// for the <see cref="System.Collections.Generic.Dictionary{TKey,TValue}.KeyCollection"/>.
        /// </returns>
        public IEnumerator<KeyValuePair<TKey, TVal>> GetEnumerator()
        {
            lock (this.innerDictionary)
            {
                return this.innerDictionary.ToList().GetEnumerator();
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="LockedDictionary{TKey,TVal}"/>.KeyCollection.
        /// </summary>
        /// <returns>
        /// A <see cref="System.Collections.Generic.Dictionary{TKey,TValue}.Enumerator"/> 
        /// for the <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.
        /// </returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            lock (this.innerDictionary)
            {
                return this.innerDictionary.ToArray().GetEnumerator();
            }
        }
        #endregion
    }
}