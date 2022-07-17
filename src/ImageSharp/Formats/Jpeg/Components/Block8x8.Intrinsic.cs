// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal unsafe partial struct Block8x8
    {
        [FieldOffset(0)]
        public Vector128<short> V0;
        [FieldOffset(16)]
        public Vector128<short> V1;
        [FieldOffset(32)]
        public Vector128<short> V2;
        [FieldOffset(48)]
        public Vector128<short> V3;
        [FieldOffset(64)]
        public Vector128<short> V4;
        [FieldOffset(80)]
        public Vector128<short> V5;
        [FieldOffset(96)]
        public Vector128<short> V6;
        [FieldOffset(112)]
        public Vector128<short> V7;

        [FieldOffset(0)]
        public Vector256<short> V01;
        [FieldOffset(32)]
        public Vector256<short> V23;
        [FieldOffset(64)]
        public Vector256<short> V45;
        [FieldOffset(96)]
        public Vector256<short> V67;
    }
}
#endif
