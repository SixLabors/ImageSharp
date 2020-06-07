// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    /// <summary>
    /// Cache 8 pixel rows on the stack, which may originate from different buffers of a <see cref="MemoryGroup{T}"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly ref struct RowOctet<T>
        where T : struct
    {
        private readonly Span<T> row0;
        private readonly Span<T> row1;
        private readonly Span<T> row2;
        private readonly Span<T> row3;
        private readonly Span<T> row4;
        private readonly Span<T> row5;
        private readonly Span<T> row6;
        private readonly Span<T> row7;

        public RowOctet(Buffer2D<T> buffer, int startY)
        {
            int y = startY;
            int height = buffer.Height;
            this.row0 = y < height ? buffer.GetRowSpan(y++) : default;
            this.row1 = y < height ? buffer.GetRowSpan(y++) : default;
            this.row2 = y < height ? buffer.GetRowSpan(y++) : default;
            this.row3 = y < height ? buffer.GetRowSpan(y++) : default;
            this.row4 = y < height ? buffer.GetRowSpan(y++) : default;
            this.row5 = y < height ? buffer.GetRowSpan(y++) : default;
            this.row6 = y < height ? buffer.GetRowSpan(y++) : default;
            this.row7 = y < height ? buffer.GetRowSpan(y) : default;
        }

        public Span<T> this[int y]
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get
            {
                // No unsafe tricks, since Span<T> can't be used as a generic argument
                return y switch
                {
                    0 => this.row0,
                    1 => this.row1,
                    2 => this.row2,
                    3 => this.row3,
                    4 => this.row4,
                    5 => this.row5,
                    6 => this.row6,
                    7 => this.row7,
                    _ => ThrowIndexOutOfRangeException()
                };
            }
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private static Span<T> ThrowIndexOutOfRangeException()
        {
            throw new IndexOutOfRangeException();
        }
    }
}
