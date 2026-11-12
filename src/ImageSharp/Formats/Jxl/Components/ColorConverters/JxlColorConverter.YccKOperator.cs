// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Restores YCbCr-encoded chromatic components before CMYK conversion.
    /// </summary>
    internal readonly struct YccKOperator : IJxlColorConverterOperator
    {
        private readonly YCbCrOperator yCbCr;

        /// <summary>
        /// Initializes a new instance of the <see cref="YccKOperator"/> struct.
        /// </summary>
        public YccKOperator() => this.yCbCr = new YCbCrOperator();

        /// <inheritdoc/>
        public static int ComponentCount => 4;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3)
        {
            // Restore CMY first. K remains separate until all four components become ICC ink amounts.
            this.yCbCr.Invoke(ref c0, ref c1, ref c2, ref c3);
            default(CmykOperator).Invoke(ref c0, ref c1, ref c2, ref c3);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Restore CMY first. K remains separate until all four components become ICC ink amounts.
            this.yCbCr.Invoke(ref c0, ref c1, ref c2, ref c3);
            default(CmykOperator).Invoke(ref c0, ref c1, ref c2, ref c3);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // Restore CMY first. K remains separate until all four components become ICC ink amounts.
            this.yCbCr.Invoke(ref c0, ref c1, ref c2, ref c3);
            default(CmykOperator).Invoke(ref c0, ref c1, ref c2, ref c3);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // Restore CMY first. K remains separate until all four components become ICC ink amounts.
            this.yCbCr.Invoke(ref c0, ref c1, ref c2, ref c3);
            default(CmykOperator).Invoke(ref c0, ref c1, ref c2, ref c3);
        }
    }
}
