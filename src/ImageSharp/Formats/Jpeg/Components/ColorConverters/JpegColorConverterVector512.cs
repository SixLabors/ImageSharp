// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

internal abstract partial class JpegColorConverterBase
{
    /// <summary>
    /// <see cref="JpegColorConverterBase"/> abstract base for implementations
    /// based on <see cref="Vector512{T}"/> instructions.
    /// </summary>
    internal abstract class JpegColorConverterVector512 : JpegColorConverterBase
    {
        protected JpegColorConverterVector512(JpegColorSpace colorSpace, int precision)
            : base(colorSpace, precision)
        {
        }

        public static bool IsSupported => Vector512.IsHardwareAccelerated && Vector512<float>.IsSupported;

        /// <inheritdoc/>
        public override bool IsAvailable => IsSupported;

        /// <inheritdoc/>
        public override int ElementsPerBatch => Vector512<float>.Count;

        /// <inheritdoc/>
        public sealed override void ConvertFromRgb(in ComponentValues values, Span<float> rLane, Span<float> gLane, Span<float> bLane)
        {
            DebugGuard.IsTrue(this.IsAvailable, $"{this.GetType().Name} converter is not supported on current hardware.");

            int length = values.Component0.Length;
            int remainder = (int)((uint)length % (uint)Vector512<float>.Count);

            int simdCount = length - remainder;
            if (simdCount > 0)
            {
                this.ConvertFromRgbVectorized(
                    values.Slice(0, simdCount),
                    rLane[..simdCount],
                    gLane[..simdCount],
                    bLane[..simdCount]);
            }

            if (remainder > 0)
            {
                this.ConvertFromRgbScalarRemainder(
                    values.Slice(simdCount, remainder),
                    rLane.Slice(simdCount, remainder),
                    gLane.Slice(simdCount, remainder),
                    bLane.Slice(simdCount, remainder));
            }
        }

        /// <inheritdoc/>
        public sealed override void ConvertToRgbInPlace(in ComponentValues values)
        {
            DebugGuard.IsTrue(this.IsAvailable, $"{this.GetType().Name} converter is not supported on current hardware.");

            int length = values.Component0.Length;
            int remainder = (int)((uint)length % (uint)Vector512<float>.Count);

            int simdCount = length - remainder;
            if (simdCount > 0)
            {
                this.ConvertToRgbInPlaceVectorized(values.Slice(0, simdCount));
            }

            if (remainder > 0)
            {
                this.ConvertToRgbInPlaceScalarRemainder(values.Slice(simdCount, remainder));
            }
        }

        /// <summary>
        /// Converts planar jpeg component values in <paramref name="values"/>
        /// to RGB color space in place using <see cref="Vector"/> API.
        /// </summary>
        /// <param name="values">The input/output as a stack-only <see cref="ComponentValues"/> struct</param>
        protected abstract void ConvertToRgbInPlaceVectorized(in ComponentValues values);

        /// <summary>
        /// Converts remainder of the planar jpeg component values after
        /// conversion in <see cref="ConvertToRgbInPlaceVectorized(in ComponentValues)"/>.
        /// </summary>
        /// <param name="values">The input/output as a stack-only <see cref="ComponentValues"/> struct</param>
        protected abstract void ConvertToRgbInPlaceScalarRemainder(in ComponentValues values);

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
