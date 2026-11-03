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
    /// Restores native JPEG XL YCbCr to RGB with the shared BT.601 coefficients.
    /// </summary>
    internal readonly struct YCbCrOperator : IJxlColorConverterOperator
    {
        /// <summary>
        /// The normalized luma offset removed by JPEG XL's reversible YCbCr transform.
        /// </summary>
        private const float LumaOffset = 128F / 255F;

        // Only four BT.601 coefficients are nontrivial. Retain them instead of a general 4x4 matrix.
        private readonly float redCr;
        private readonly float greenCb;
        private readonly float greenCr;
        private readonly float blueCb;

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCrOperator"/> struct.
        /// </summary>
        public YCbCrOperator()
        {
            Matrix4x4 inverse = KnownYCbCrMatrices.BT601.Inverse;
            this.redCr = inverse.M13;
            this.greenCb = inverse.M22;
            this.greenCr = inverse.M23;
            this.blueCb = inverse.M32;
        }

        /// <inheritdoc/>
        public static int ComponentCount => 3;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref float c0, ref float c1, ref float c2, ref float c3)
        {
            // Native planes contain [Cb,Y,Cr]. Restore the normalized luma offset; chroma is already centered.
            // Registers contain independent pixels. Only green depends on both chroma components.
            float cb = c0;
            float y = c1 + LumaOffset;
            float cr = c2;

            c0 = float.MultiplyAddEstimate(cr, this.redCr, y);
            c1 = float.MultiplyAddEstimate(cr, this.greenCr, float.MultiplyAddEstimate(cb, this.greenCb, y));
            c2 = float.MultiplyAddEstimate(cb, this.blueCb, y);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector128<float> c0, ref Vector128<float> c1, ref Vector128<float> c2, ref Vector128<float> c3)
        {
            // Native planes contain [Cb,Y,Cr]. Restore the normalized luma offset; chroma is already centered.
            // Registers contain independent pixels. Only green depends on both chroma components.
            Vector128<float> cb = c0;
            Vector128<float> y = c1 + Vector128.Create(LumaOffset);
            Vector128<float> cr = c2;

            c0 = Vector128.MultiplyAddEstimate(cr, Vector128.Create(this.redCr), y);
            c1 = Vector128.MultiplyAddEstimate(cr, Vector128.Create(this.greenCr), Vector128.MultiplyAddEstimate(cb, Vector128.Create(this.greenCb), y));
            c2 = Vector128.MultiplyAddEstimate(cb, Vector128.Create(this.blueCb), y);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector256<float> c0, ref Vector256<float> c1, ref Vector256<float> c2, ref Vector256<float> c3)
        {
            // Native planes contain [Cb,Y,Cr]. Restore the normalized luma offset; chroma is already centered.
            // Registers contain independent pixels. Only green depends on both chroma components.
            Vector256<float> cb = c0;
            Vector256<float> y = c1 + Vector256.Create(LumaOffset);
            Vector256<float> cr = c2;

            c0 = Vector256.MultiplyAddEstimate(cr, Vector256.Create(this.redCr), y);
            c1 = Vector256.MultiplyAddEstimate(cr, Vector256.Create(this.greenCr), Vector256.MultiplyAddEstimate(cb, Vector256.Create(this.greenCb), y));
            c2 = Vector256.MultiplyAddEstimate(cb, Vector256.Create(this.blueCb), y);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(ref Vector512<float> c0, ref Vector512<float> c1, ref Vector512<float> c2, ref Vector512<float> c3)
        {
            // Native planes contain [Cb,Y,Cr]. Restore the normalized luma offset; chroma is already centered.
            // Registers contain independent pixels. Only green depends on both chroma components.
            Vector512<float> cb = c0;
            Vector512<float> y = c1 + Vector512.Create(LumaOffset);
            Vector512<float> cr = c2;

            c0 = Vector512.MultiplyAddEstimate(cr, Vector512.Create(this.redCr), y);
            c1 = Vector512.MultiplyAddEstimate(cr, Vector512.Create(this.greenCr), Vector512.MultiplyAddEstimate(cb, Vector512.Create(this.greenCb), y));
            c2 = Vector512.MultiplyAddEstimate(cb, Vector512.Create(this.blueCb), y);
        }
    }
}
