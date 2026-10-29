// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

internal static partial class IccColorConverter
{
    /// <summary>
    /// Applies the sampled channel tables of a legacy ICC LUT.
    /// </summary>
    internal readonly struct LutTrcOperator : IIccColorConverterOperator
    {
        private readonly LutOperator curve0;
        private readonly LutOperator curve1;
        private readonly LutOperator curve2;
        private readonly LutOperator curve3;
        private readonly int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="LutTrcOperator"/> struct.
        /// </summary>
        /// <param name="entries">The sampled tables for one to four device channels.</param>
        public LutTrcOperator(ReadOnlySpan<IccLut> entries)
        {
            if (entries.Length > 4)
            {
                throw new InvalidIccProfileException("ICC conversion supports at most four channel tables.");
            }

            // Validate profile tables before shared interpolation can read a neighboring sample.
            // Curve tags with zero or one entry use identity or gamma conversion instead of this sampled-table path.
            foreach (IccLut entry in entries)
            {
                if (entry.Values.Length < 2)
                {
                    throw new InvalidIccProfileException("Sampled ICC tables require at least two entries.");
                }
            }

            // Legacy tag constructors require at least one table. Each table is a forward lookup;
            // its position in the transform determines whether it maps device codes or PCS codes.
            this.count = entries.Length;
            this.curve0 = new LutOperator(entries[0].Values, false);
            this.curve1 = entries.Length > 1 ? new LutOperator(entries[1].Values, false) : default;
            this.curve2 = entries.Length > 2 ? new LutOperator(entries[2].Values, false) : default;
            this.curve3 = entries.Length > 3 ? new LutOperator(entries[3].Values, false) : default;
        }

        /// <inheritdoc/>
        public bool IsTrc => true;

        /// <inheritdoc/>
        public bool Is16BitLut => false;

        /// <inheritdoc/>
        public Vector4 Convert(Vector4 value)
        {
            value.X = this.curve0.ConvertForward(value.X);
            if (this.count > 1)
            {
                value.Y = this.curve1.ConvertForward(value.Y);
            }

            if (this.count > 2)
            {
                value.Z = this.curve2.ConvertForward(value.Z);
            }

            if (this.count > 3)
            {
                value.W = this.curve3.ConvertForward(value.W);
            }

            return value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w)
        {
            // Each lane is one color. Sampled lookup needs no gamma or parametric-curve dispatch.
            x = this.curve0.ConvertForward(x);
            if (this.count > 1)
            {
                y = this.curve1.ConvertForward(y);
            }

            if (this.count > 2)
            {
                z = this.curve2.ConvertForward(z);
            }

            if (this.count > 3)
            {
                w = this.curve3.ConvertForward(w);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w)
        {
            // Each lane is one color. Sampled lookup needs no gamma or parametric-curve dispatch.
            x = this.curve0.ConvertForward(x);
            if (this.count > 1)
            {
                y = this.curve1.ConvertForward(y);
            }

            if (this.count > 2)
            {
                z = this.curve2.ConvertForward(z);
            }

            if (this.count > 3)
            {
                w = this.curve3.ConvertForward(w);
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w)
        {
            // Each lane is one color. Sampled lookup needs no gamma or parametric-curve dispatch.
            x = this.curve0.ConvertForward(x);
            if (this.count > 1)
            {
                y = this.curve1.ConvertForward(y);
            }

            if (this.count > 2)
            {
                z = this.curve2.ConvertForward(z);
            }

            if (this.count > 3)
            {
                w = this.curve3.ConvertForward(w);
            }
        }
    }
}
