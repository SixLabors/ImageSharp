// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct FixedInt16Buffer18
    {
        public fixed int Data[18];

        public int this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref int self = ref Unsafe.As<FixedInt16Buffer18, int>(ref this);
                return Unsafe.Add(ref self, idx);
            }
        }
    }
}