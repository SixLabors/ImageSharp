// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;

internal readonly struct JxlAcStrategyRow(ReadOnlyMemory<byte> row)
{
    public readonly JxlAcStrategy this[int x]
    {
        get
        {
            ReadOnlySpan<byte> span = row.Span;

            DebugGuard.MustBeLessThan(x * 8, span.Length, "x overflows");

            ref byte first = ref MemoryMarshal.GetReference(span);
            JxlAcStrategyType strategy = (JxlAcStrategyType)(Unsafe.Add(ref Unsafe.As<byte, int>(ref first), x) >> 1);
            bool isFirst = Unsafe.Add(ref first, x) != 0;

            return new JxlAcStrategy(strategy, isFirst);
        }
    }
}
