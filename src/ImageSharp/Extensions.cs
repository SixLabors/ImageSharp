// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp;

internal static class ArgumentOutOfRange
{
    /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is negative or zero.</summary>
    /// <param name="value">The argument to validate as non-zero or non-negative.</param>
    /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
    public static void ThrowIfNegativeOrZero(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
#if !NET8_0_OR_GREATER
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} ('{value}') must be a non-negative and non-zero value.");
        }
#else
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, paramName);
#endif
    }
}

internal static class ObjectDisposed
{
    /// <summary>Throws an <see cref="ObjectDisposedException"/> if the specified <paramref name="condition"/> is <see langword="true"/>.</summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="instance">The object whose type's full name should be included in any resulting <see cref="ObjectDisposedException"/>.</param>
    /// <exception cref="ObjectDisposedException">The <paramref name="condition"/> is <see langword="true"/>.</exception>
    public static void ThrowIf([DoesNotReturnIf(true)] bool condition, object instance)
    {
#if !NET7_0_OR_GREATER
        if (condition)
        {
            throw new ObjectDisposedException(instance?.GetType().FullName);
        }
#else
        ObjectDisposedException.ThrowIf(condition, instance);
#endif
    }
}
