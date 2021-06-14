// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Numerics;
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

        private static void UncheckIfSupported(ref RuntimeFeature features, RuntimeFeature value, bool isSupported)
        {
            if (isSupported)
            {
                features &= ~value;
            }
        }
    }

    /// <summary>
    /// Flags for determining if testing environment supports certain runtime features
    /// </summary>
    [Flags]
    public enum RuntimeFeature
    {
        /// <summary>
        /// No flags set
        /// </summary>
        /// <remarks>Used internally, general use case should not use this option.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        None = 0,

        /// <summary>
        /// SSE hardware support
        /// </summary>
        /// <remarks>Should be used only for testing <see cref="Vector"/> based intrinsic code</remarks>
        Vector4 = 1 << 0,

        /// <summary>
        /// Equivalent of Sse.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        SSE = 1 << 1,

        /// <summary>
        /// Equivalent of Sse2.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        SSE2 = 1 << 2,

        /// <summary>
        /// Equivalent of Sse3.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        SSE3 = 1 << 3,

        /// <summary>
        /// Equivalent of Ssse3.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        SSSE3 = 1 << 4,

        /// <summary>
        /// Equivalent of Sse41.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        SSE41 = 1 << 5,

        /// <summary>
        /// Equivalent of Sse42.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        SSE42 = 1 << 6,

        /// <summary>
        /// Avx hardware support
        /// </summary>
        /// <remarks>Should be used only for testing <see cref="Vector"/> based intrinsic code</remarks>
        Vector8 = 1 << 7,

        /// <summary>
        /// Equivalent of Avx.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        AVX = 1 << 8,

        /// <summary>
        /// Equivalent of Avx2.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        AVX2 = 1 << 9,

        /// <summary>
        /// Equivalent of Bmi1.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        BMI1 = 1 << 10,

        /// <summary>
        /// Equivalent of Bmi2.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        BMI2 = 1 << 11,

        /// <summary>
        /// Equivalent of Popcnt.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        POPCNT = 1 << 12,

        /// <summary>
        /// Equivalent of Lzcnt.IsSupported;
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        LZCNT = 1 << 13,

        /// <summary>
        /// Equivalent of Fma.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        FMA = 1 << 14,

        /// <summary>
        /// Equivalent of Aes.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        AES = 1 << 15,

        /// <summary>
        /// Equivalent of Pclmulqdq.IsSupported
        /// </summary>
        /// <remarks>Not supported in certain runtimes even if hardware supports it</remarks>
        PCLMULQDQ = 1 << 16
    }
}
