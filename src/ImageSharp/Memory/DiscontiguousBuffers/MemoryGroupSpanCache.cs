// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Memory;

/// <summary>
/// Cached pointer or array data enabling fast <see cref="Span{T}"/> access from
/// known <see cref="IMemoryOwner{T}"/> implementations.
/// </summary>
internal unsafe struct MemoryGroupSpanCache
{
    public SpanCacheMode Mode;

    // Managed backing
    public byte[]? SingleArray;
    public int SingleArrayOffsetBytes;

    // Unmanaged backing
    public void* SinglePointer;
    public void*[] MultiPointer;

    public static MemoryGroupSpanCache Create<T>(IMemoryOwner<T>[] memoryOwners)
        where T : struct
    {
        IMemoryOwner<T> owner0 = memoryOwners[0];
        MemoryGroupSpanCache memoryGroupSpanCache = default;
        if (memoryOwners.Length == 1)
        {
            if (owner0 is SharedArrayPoolBuffer<T> sharedPoolBuffer)
            {
                memoryGroupSpanCache.Mode = SpanCacheMode.SingleArray;
                memoryGroupSpanCache.SingleArray = sharedPoolBuffer.Array;
                memoryGroupSpanCache.SingleArrayOffsetBytes = sharedPoolBuffer.AlignedOffsetBytes;
            }
            else if (owner0 is UnmanagedBuffer<T> unmanagedBuffer)
            {
                memoryGroupSpanCache.Mode = SpanCacheMode.SinglePointer;
                memoryGroupSpanCache.SinglePointer = unmanagedBuffer.Pointer;
            }
        }
        else if (owner0 is UnmanagedBuffer<T>)
        {
            memoryGroupSpanCache.Mode = SpanCacheMode.MultiPointer;
            memoryGroupSpanCache.MultiPointer = new void*[memoryOwners.Length];
            for (int i = 0; i < memoryOwners.Length; i++)
            {
                memoryGroupSpanCache.MultiPointer[i] = ((UnmanagedBuffer<T>)memoryOwners[i]).Pointer;
            }
        }

        return memoryGroupSpanCache;
    }
}
