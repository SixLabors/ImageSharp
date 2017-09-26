// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Common.Extensions
{
    /// <summary>
    /// Encapsulates a series of time saving extension methods to the <see cref="T:System.Collections.Generic.List"/> class.
    /// </summary>
    internal static class ListExtensions
    {
        /// <summary>
        /// Inserts an item at the given index automatically expanding the capacity if required.
        /// </summary>
        /// <typeparam name="T">The type of object within the list</typeparam>
        /// <param name="list">The list</param>
        /// <param name="index">The index</param>
        /// <param name="item">The item to insert</param>
        public static void SafeInsert<T>(this List<T> list, int index, T item)
        {
            if (index >= list.Count)
            {
                list.Add(item);
            }
            else
            {
                list[index] = item;
            }
        }

        /// <summary>
        /// Removes the last element from a list and returns that element. This method changes the length of the list.
        /// </summary>
        /// <typeparam name="T">The type of object within the list</typeparam>
        /// <param name="list">The list</param>
        /// <returns>The last element in the specified sequence.</returns>
        public static T Pop<T>(this List<T> list)
        {
            int last = list.Count - 1;
            T item = list[last];
            list.RemoveAt(last);
            return item;
        }
    }
}