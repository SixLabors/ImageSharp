// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

/// <summary>
/// Utilities for working with arrays.
/// </summary>
internal static class ArrayHelpers
{
    /// <summary>
    /// Equivalent to a normal <c>new T[length]</c> but each item is
    /// initialized with a lambda expression instead of being null.
    /// </summary>
    /// <typeparam name="T">The array type.</typeparam>
    /// <param name="length">The array length.</param>
    /// <param name="initializer">Each array item is computed with this function.</param>
    /// <returns>An array with items initialized using the <paramref name="initializer"/> function.</returns>
    public static T[] CreateAndInitialize<T>(int length, Func<T> initializer)
    {
        T[] array = new T[length];

        for (int i = 0; i < length; i++)
        {
            array[i] = initializer();
        }

        return array;
    }
}
