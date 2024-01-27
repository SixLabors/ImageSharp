// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#pragma warning disable

#if !NET6_0_OR_GREATER && !USE_SIMD_INTRINSICS
namespace System.Runtime.Intrinsics
{
    namespace X86
    {
        internal static class Avx
        {
            public const bool IsSupported = false;
        }

        internal static class Avx2
        {
            public const bool IsSupported = false;
        }

        internal static class Sse
        {
            public const bool IsSupported = false;
        }

        internal static class Sse41
        {
            public const bool IsSupported = false;
        }

        internal static class Fma
        {
            public const bool IsSupported = false;
        }
    }

    namespace Arm
    {
        internal static class ArmBase
        {
            public const bool IsSupported = false;
        }

        internal static class AdvSimd
        {
            public const bool IsSupported = false;
        }
    }
}
#endif
