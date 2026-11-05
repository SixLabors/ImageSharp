// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Reconstructs coding XYB directly to prepared linear RGB coordinates with shared scalar and SIMD arithmetic.
    /// </summary>
    internal readonly struct XybOperator : IJxlColorConverterOperator
    {
        private readonly Xyb.InverseOperator inverse;

        /// <summary>
        /// Initializes a new instance of the <see cref="XybOperator"/> struct.
        /// </summary>
        /// <param name="transform">The header's inverse absorbance matrix, biases and intensity target.</param>
        public XybOperator(XybTransform transform)
        {
            // The shared preparation leaves the default sRGB coordinates unchanged and applies
            // only the header intensity scale after transposing the serialized inverse matrix.
            this.inverse = new Xyb.InverseOperator(transform, KnownRgbWorkingSpaces.SRgb, false);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XybOperator"/> struct for the original RGB or grayscale coordinates.
        /// </summary>
        /// <param name="transform">The header's inverse absorbance matrix, biases and intensity target.</param>
        /// <param name="target">The selected original working space, with its transfer applied after reconstruction.</param>
        /// <param name="grayscale">Whether the three outputs repeat the D65 luminance component.</param>
        public XybOperator(XybTransform transform, RgbWorkingSpace target, bool grayscale)
            => this.inverse = new Xyb.InverseOperator(transform, target, grayscale);

        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3)
        {
            // Restore linear RGB directly. No intermediate XYZ coordinates or packed color row are needed.
            this.inverse.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Restore linear RGB directly. No intermediate XYZ coordinates or packed color row are needed.
            this.inverse.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // Restore linear RGB directly. No intermediate XYZ coordinates or packed color row are needed.
            this.inverse.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // Restore linear RGB directly. No intermediate XYZ coordinates or packed color row are needed.
            this.inverse.Invoke(c0, c1, c2, out c0, out c1, out c2);
        }
    }
}
