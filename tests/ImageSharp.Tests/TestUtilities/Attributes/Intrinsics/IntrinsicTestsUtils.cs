// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace SixLabors.ImageSharp.Tests
{
    public static class IntrinsicTestsUtils
    {
        public static bool IntrinsicsSupported
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return true;
#else
                return false;
#endif
            }
        }

        public static _HwIntrinsics GetNotSupportedIntrinsics(this _HwIntrinsics flags)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            _HwIntrinsics notSupported = flags;

            // SSE
            UncheckIfSupported(ref notSupported, _HwIntrinsics.SSE, Sse.IsSupported);
            UncheckIfSupported(ref notSupported, _HwIntrinsics.SSE2, Sse2.IsSupported);
            UncheckIfSupported(ref notSupported, _HwIntrinsics.SSE3, Sse3.IsSupported);
            UncheckIfSupported(ref notSupported, _HwIntrinsics.SSSE3, Ssse3.IsSupported);
            UncheckIfSupported(ref notSupported, _HwIntrinsics.SSE41, Sse41.IsSupported);
            UncheckIfSupported(ref notSupported, _HwIntrinsics.SSE42, Sse42.IsSupported);

            // AVX
            UncheckIfSupported(ref notSupported, _HwIntrinsics.AVX, Avx.IsSupported);
            UncheckIfSupported(ref notSupported, _HwIntrinsics.AVX2, Avx2.IsSupported);

            // BMI
            UncheckIfSupported(ref notSupported, _HwIntrinsics.BMI1, Bmi1.IsSupported);
            UncheckIfSupported(ref notSupported, _HwIntrinsics.BMI2, Bmi2.IsSupported);

            // ABM
            UncheckIfSupported(ref notSupported, _HwIntrinsics.POPCNT, Popcnt.IsSupported);
            UncheckIfSupported(ref notSupported, _HwIntrinsics.LZCNT, Lzcnt.IsSupported);

            // FMA
            UncheckIfSupported(ref notSupported, _HwIntrinsics.FMA, Fma.IsSupported);

            // AES
            UncheckIfSupported(ref notSupported, _HwIntrinsics.AES, Aes.IsSupported);

            // PCLMULQDQ
            UncheckIfSupported(ref notSupported, _HwIntrinsics.PCLMULQDQ, Pclmulqdq.IsSupported);

            return notSupported;
#endif
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        private static void UncheckIfSupported(ref _HwIntrinsics set, _HwIntrinsics value, bool isSupported)
        {
            if (set.IsSet(value) && isSupported)
            {
                set &= ~value;
            }
        }

        private static bool IsSet(this _HwIntrinsics set, _HwIntrinsics value) => (set & value) == value;
#endif
    }

    [Flags]
    public enum _HwIntrinsics
    {
        // Used internally, using this in actual fact/theory would do nothing
        [EditorBrowsable(EditorBrowsableState.Never)]
        None = 0,

        SSE = 1 << 0,
        SSE2 = 1 << 1,
        SSE3 = 1 << 2,
        SSSE3 = 1 << 3,
        SSE41 = 1 << 4,
        SSE42 = 1 << 5,

        AVX = 1 << 6,
        AVX2 = 1 << 7,

        BMI1 = 1 << 8,
        BMI2 = 1 << 9,

        POPCNT = 1 << 10,
        LZCNT = 1 << 11,

        FMA = 1 << 12,

        AES = 1 << 13,

        PCLMULQDQ = 1 << 14
    }
}
