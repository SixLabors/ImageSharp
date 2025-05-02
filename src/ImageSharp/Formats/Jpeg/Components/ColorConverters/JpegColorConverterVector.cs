// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// <see cref="JpegColorConverterBase"/> abstract base for implementations
    /// based on <see cref="Vector"/> API.
    /// </summary>
    /// <remarks>
    /// Converters of this family can work with data of any size.
    /// Even though real life data is guaranteed to be of size
    /// divisible by 8 newer SIMD instructions like AVX512 won't work with
    /// such data out of the box. These converters have fallback code
    /// for 'remainder' data.
    /// </remarks>
    internal abstract class JpegColorConverterVector : JpegColorConverterBase
    {
        protected JpegColorConverterVector(JpegColorSpace colorSpace, int precision)
            : base(colorSpace, precision)
        {
        }

        /// <summary>
        /// Gets a value indicating whether this converter is supported on current hardware.
        /// </summary>
        public static bool IsSupported => Vector.IsHardwareAccelerated && Vector<float>.Count % 4 == 0;

        /// <inheritdoc/>
        public sealed override bool IsAvailable => IsSupported;

        public override int ElementsPerBatch => Vector<float>.Count;

        /// <inheritdoc/>
        public sealed override void ConvertToRgbInPlace(in ComponentValues values)
        {
            DebugGuard.IsTrue(this.IsAvailable, $"{this.GetType().Name} converter is not supported on current hardware.");

            int length = values.Component0.Length;
            int remainder = (int)((uint)length % (uint)Vector<float>.Count);

            int simdCount = length - remainder;
            if (simdCount > 0)
            {
                this.ConvertToRgbInplaceVectorized(values.Slice(0, simdCount));
            }

            // Jpeg images width is always divisible by 8 without a remainder
            // so it's safe to say SSE/AVX1/AVX2 implementations would never have
            // 'remainder' pixels
            // But some exotic simd implementations e.g. AVX-512 can have
            // remainder pixels
            if (remainder > 0)
            {
                this.ConvertToRgbInplaceScalarRemainder(values.Slice(simdCount, remainder));
            }
        }

        /// <inheritdoc/>
        public sealed override void ConvertFromRgb(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
        {
            DebugGuard.IsTrue(this.IsAvailable, $"{this.GetType().Name} converter is not supported on current hardware.");

            int length = values.Component0.Length;
            int remainder = (int)((uint)length % (uint)Vector<float>.Count);

            int simdCount = length - remainder;
            if (simdCount > 0)
            {
                this.ConvertFromRgbVectorized(
                    values.Slice(0, simdCount),
                    r.Slice(0, simdCount),
                    g.Slice(0, simdCount),
                    b.Slice(0, simdCount));
            }

            // Jpeg images width is always divisible by 8 without a remainder
            // so it's safe to say SSE/AVX1/AVX2 implementations would never have
            // 'remainder' pixels
            // But some exotic simd implementations e.g. AVX-512 can have
            // remainder pixels
            if (remainder > 0)
            {
                this.ConvertFromRgbScalarRemainder(
                    values.Slice(simdCount, remainder),
                    r.Slice(simdCount, remainder),
                    g.Slice(simdCount, remainder),
                    b.Slice(simdCount, remainder));
            }
        }

        /// <summary>
        /// Converts planar jpeg component values in <paramref name="values"/>
        /// to RGB color space inplace using <see cref="Vector"/> API.
        /// </summary>
        /// <param name="values">The input/ouptut as a stack-only <see cref="ComponentValues"/> struct</param>
        protected abstract void ConvertToRgbInplaceVectorized(in ComponentValues values);

        /// <summary>
        /// Converts remainder of the planar jpeg component values after
        /// conversion in <see cref="ConvertToRgbInplaceVectorized(in ComponentValues)"/>.
        /// </summary>
        /// <param name="values">The input/ouptut as a stack-only <see cref="ComponentValues"/> struct</param>
        protected abstract void ConvertToRgbInplaceScalarRemainder(in ComponentValues values);

        /// <summary>
        /// Converts RGB lanes to jpeg component values using <see cref="Vector"/> API.
        /// </summary>
        /// <param name="values">Jpeg component values.</param>
        /// <param name="rLane">Red colors lane.</param>
        /// <param name="gLane">Green colors lane.</param>
        /// <param name="bLane">Blue colors lane.</param>
        protected abstract void ConvertFromRgbVectorized(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane);

        /// <summary>
        /// Converts remainder of RGB lanes to jpeg component values after
        ///  conversion in <see cref="ConvertFromRgbVectorized(in ComponentValues, Span{float}, Span{float}, Span{float})"/>.
        /// </summary>
        /// <param name="values">Jpeg component values.</param>
        /// <param name="rLane">Red colors lane.</param>
        /// <param name="gLane">Green colors lane.</param>
        /// <param name="bLane">Blue colors lane.</param>
        protected abstract void ConvertFromRgbScalarRemainder(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane);
    }
}
