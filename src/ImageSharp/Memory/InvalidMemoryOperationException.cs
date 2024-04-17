// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Exception thrown when the library detects an invalid memory allocation request,
/// or an attempt has been made to use an invalidated <see cref="IMemoryGroup{T}"/>.
/// </summary>
public class InvalidMemoryOperationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidMemoryOperationException"/> class.
    /// </summary>
    /// <param name="message">The exception message text.</param>
    public InvalidMemoryOperationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidMemoryOperationException"/> class.
    /// </summary>
    public InvalidMemoryOperationException()
    {
    }

    [DoesNotReturn]
    internal static void ThrowNegativeAllocationException(long length) =>
        throw new InvalidMemoryOperationException($"Attempted to allocate a buffer of negative length={length}.");

    [DoesNotReturn]
    internal static void ThrowInvalidAlignmentException(long alignment) =>
        throw new InvalidMemoryOperationException(
                $"The buffer capacity of the provided MemoryAllocator is insufficient for the requested buffer alignment: {alignment}.");

    [DoesNotReturn]
    internal static void ThrowAllocationOverLimitException(ulong length, long limit) =>
            throw new InvalidMemoryOperationException($"Attempted to allocate a buffer of length={length} that exceeded the limit {limit}.");
}
