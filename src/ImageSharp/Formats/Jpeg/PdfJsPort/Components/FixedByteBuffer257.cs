// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct FixedByteBuffer257
    {
        public fixed byte Data[257];

        public byte this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref byte self = ref Unsafe.As<FixedByteBuffer257, byte>(ref this);
                return Unsafe.Add(ref self, idx);
            }
        }
    }
}