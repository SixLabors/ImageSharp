// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Utility methods for lossy and lossless webp format.
    /// </summary>
    public static class WebPCommonUtils
    {
        // Returns 31 ^ clz(n) = log2(n).Returns 31 ^ clz(n) = log2(n).
        [MethodImpl(InliningOptions.ShortMethod)]
        public static int BitsLog2Floor(uint n)
        {
            int logValue = 0;
            while (n >= 256)
            {
                logValue += 8;
                n >>= 8;
            }

            return logValue + WebPLookupTables.LogTable8bit[n];
        }
    }
}
