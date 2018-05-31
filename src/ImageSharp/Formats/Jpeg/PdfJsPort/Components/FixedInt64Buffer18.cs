// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct FixedInt64Buffer18
    {
        public fixed uint Data[18];

        public uint this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref uint self = ref Unsafe.As<FixedInt64Buffer18, uint>(ref this);
                return Unsafe.Add(ref self, idx);
            }
        }
    }
}