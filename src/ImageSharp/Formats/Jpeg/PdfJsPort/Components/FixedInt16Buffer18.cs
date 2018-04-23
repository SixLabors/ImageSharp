// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct FixedInt16Buffer18
    {
        public fixed short Data[18];

        public short this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref short self = ref Unsafe.As<FixedInt16Buffer18, short>(ref this);
                return Unsafe.Add(ref self, idx);
            }
        }
    }
}