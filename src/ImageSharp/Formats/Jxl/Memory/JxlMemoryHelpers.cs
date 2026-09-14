// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

/// <summary>
/// Memory management utilities.
/// </summary>
internal static class JxlMemoryHelpers
{
    public static Memory<TOutputType> CastMemory<TSourceType, TOutputType>(Memory<TSourceType> memory)
        where TSourceType : unmanaged
        where TOutputType : unmanaged
    {
        CastingMemoryManager<TSourceType, TOutputType> memoryManager = new(memory);
        return memoryManager.Memory;
    }

    private sealed class CastingMemoryManager<TSourceType, TOutputType>(Memory<TSourceType> memory)
        : MemoryManager<TOutputType>
        where TSourceType : unmanaged
        where TOutputType : unmanaged
    {
        public override Span<TOutputType> GetSpan() => MemoryMarshal.Cast<TSourceType, TOutputType>(memory.Span);

        // We don't use these.
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotImplementedException();

        public override void Unpin() => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            // blank
        }
    }
}
