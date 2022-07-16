// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    internal ref struct RowOctet<T>
        where T : struct
    {
        private Span<T> row0;
        private Span<T> row1;
        private Span<T> row2;
        private Span<T> row3;
        private Span<T> row4;
        private Span<T> row5;
        private Span<T> row6;
        private Span<T> row7;

        // No unsafe tricks, since Span<T> can't be used as a generic argument
        public Span<T> this[int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get =>
                y switch
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set
            {
                switch (y)
                {
                    case 0:
                        this.row0 = value;
                        break;
                    case 1:
                        this.row1 = value;
                        break;
                    case 2:
                        this.row2 = value;
                        break;
                    case 3:
                        this.row3 = value;
                        break;
                    case 4:
                        this.row4 = value;
                        break;
                    case 5:
                        this.row5 = value;
                        break;
                    case 6:
                        this.row6 = value;
                        break;
                    default:
                        this.row7 = value;
                        break;
                }
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Update(Buffer2D<T> buffer, int startY)
        {
            // We don't actually have to assign values outside of the
            // frame pixel buffer since they are never requested.
            int y = startY;
            int yEnd = Math.Min(y + 8, buffer.Height);

            int i = 0;
            while (y < yEnd)
            {
                this[i++] = buffer.DangerousGetRowSpan(y++);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Span<T> ThrowIndexOutOfRangeException()
        => throw new IndexOutOfRangeException();
    }
}
