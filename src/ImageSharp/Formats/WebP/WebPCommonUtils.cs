// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Experimental.WebP
{
    /// <summary>
    /// Utility methods for lossy and lossless webp format.
    /// </summary>
    internal static class WebPCommonUtils
    {
        /// <summary>
        /// Returns 31 ^ clz(n) = log2(n).Returns 31 ^ clz(n) = log2(n).
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int BitsLog2Floor(uint n)
        {
            int logValue = 0;
            while (n >= 256)
            {
                logValue += 8;
                n >>= 8;
            }

            return logValue + Unsafe.Add(ref MemoryMarshal.GetReference(WebPLookupTables.LogTable8Bit), (int)n);
        }
    }
}
