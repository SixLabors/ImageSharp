// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp.Tests
{
    public static class IntrinsicTestsUtils
    {
        public static bool HasVector4
        {
            get
            {
                return SimdUtils.HasVector4;
            }
        }

        public static bool HasSse
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Sse.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasSse2
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Sse2.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasSse3
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Sse3.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasSsse3
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Ssse3.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasSse41
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Sse41.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasSse42
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Sse42.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasVector8
        {
            get
            {
                return SimdUtils.HasVector8;
            }
        }

        public static bool HasAvx
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Avx.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasAvx2
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Avx2.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasBmi1
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Bmi1.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasBmi2
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Bmi2.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasPOPCNT
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Popcnt.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasLZCNT
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Lzcnt.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasFma
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Fma.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasAes
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Aes.IsSupported;
#else
                return false;
#endif
            }
        }

        public static bool HasPCLMULQDQ
        {
            get
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                return Pclmulqdq.IsSupported;
#else
                return false;
#endif
            }
        }

        public static RuntimeFeature GetNotSupportedIntrinsics(this RuntimeFeature flags)
        {
            RuntimeFeature notSupported = flags;

            // SSE
            UncheckIfSupported(ref notSupported, RuntimeFeature.Vector4, HasVector4);
            UncheckIfSupported(ref notSupported, RuntimeFeature.SSE, HasSse);
            UncheckIfSupported(ref notSupported, RuntimeFeature.SSE2, HasSse2);
            UncheckIfSupported(ref notSupported, RuntimeFeature.SSE3, HasSse3);
            UncheckIfSupported(ref notSupported, RuntimeFeature.SSSE3, HasSsse3);
            UncheckIfSupported(ref notSupported, RuntimeFeature.SSE41, HasSse41);
            UncheckIfSupported(ref notSupported, RuntimeFeature.SSE42, HasSse42);

            // AVX
            UncheckIfSupported(ref notSupported, RuntimeFeature.Vector8, HasVector8);
            UncheckIfSupported(ref notSupported, RuntimeFeature.AVX, HasAvx);
            UncheckIfSupported(ref notSupported, RuntimeFeature.AVX2, HasAvx2);

            // BMI
            UncheckIfSupported(ref notSupported, RuntimeFeature.BMI1, HasBmi1);
            UncheckIfSupported(ref notSupported, RuntimeFeature.BMI2, HasBmi2);

            // ABM
            UncheckIfSupported(ref notSupported, RuntimeFeature.POPCNT, HasPOPCNT);
            UncheckIfSupported(ref notSupported, RuntimeFeature.LZCNT, HasLZCNT);

            // FMA
            UncheckIfSupported(ref notSupported, RuntimeFeature.FMA, HasFma);

            // AES
            UncheckIfSupported(ref notSupported, RuntimeFeature.AES, HasAes);

            // PCLMULQDQ
            UncheckIfSupported(ref notSupported, RuntimeFeature.PCLMULQDQ, HasPCLMULQDQ);

            return notSupported;
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        private static void UncheckIfSupported(ref RuntimeFeature features, RuntimeFeature value, bool isSupported)
        {
            if (isSupported)
            {
                features &= ~value;
            }
        }
#endif
    }

    [Flags]
    public enum RuntimeFeature
    {
        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Used internally, general use case should not use this option.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        None = 0,

        Vector4 = 1 << 0,
        SSE = 1 << 1,
        SSE2 = 1 << 2,
        SSE3 = 1 << 3,
        SSSE3 = 1 << 4,
        SSE41 = 1 << 5,
        SSE42 = 1 << 6,

        Vector8 = 1 << 7,
        AVX = 1 << 8,
        AVX2 = 1 << 9,

        BMI1 = 1 << 10,
        BMI2 = 1 << 11,

        POPCNT = 1 << 12,
        LZCNT = 1 << 13,

        FMA = 1 << 14,

        AES = 1 << 15,

        PCLMULQDQ = 1 << 16
    }
}
