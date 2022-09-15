// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Common.Helpers;

/// <summary>
/// A helper class that with utility methods for dealing with references, and other low-level details.
/// </summary>
internal static class RuntimeUtility
{
    // Tuple swap uses 2 more IL bytes
#pragma warning disable IDE0180 // Use tuple to swap values
    /// <summary>
    /// Swaps the two references.
    /// </summary>
    /// <typeparam name="T">The type to swap.</typeparam>
    /// <param name="a">The first item.</param>
    /// <param name="b">The second item.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Swap<T>(ref T a, ref T b)
    {
        T tmp = a;
        a = b;
        b = tmp;
    }

    /// <summary>
    /// Swaps the two references.
    /// </summary>
    /// <typeparam name="T">The type to swap.</typeparam>
    /// <param name="a">The first item.</param>
    /// <param name="b">The second item.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Swap<T>(ref Span<T> a, ref Span<T> b)
    {
        // Tuple swap uses 2 more IL bytes
        Span<T> tmp = a;
        a = b;
        b = tmp;
    }
#pragma warning restore IDE0180 // Use tuple to swap values
}
