// <copyright file="DictionaryExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Web.Processors
{
    using System.Collections.Generic;

    /// <summary>
    /// Extension methods for <see cref="IDictionary{TKey, TValue}"/>
    /// </summary>
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Gets the value associated with the specified key or the default value.
        /// </summary>
        /// <param name="dictionary">The dictionary instance.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <returns>The value associated with the specified key or the default value.</returns>
        public static TValue GetValueOrDefault<TValue, TKey>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            return dictionary.ContainsKey(key) ? dictionary[key] : default(TValue);
        }
    }
}