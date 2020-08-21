// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Cubic filters contain a collection of different filters of varying B-Spline and
    /// Cardinal values. With these two values you can generate any smoothly fitting
    /// (continuious first derivative) piece-wise cubic filter.
    /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
    /// <see href="https://www.cs.utexas.edu/~fussell/courses/cs384g-fall2013/lectures/mitchell/Mitchell.pdf"/>
    /// </summary>
    public readonly struct CubicResampler : IResampler
    {
        private readonly float bspline;
        private readonly float cardinal;

        /// <summary>
        /// The Catmull-Rom filter is a well known standard Cubic Filter often used as a interpolation function.
        /// This filter produces a reasonably sharp edge, but without a the pronounced gradient change on large
        /// scale image enlargements that a 'Lagrange' filter can produce.
        /// </summary>
        public static CubicResampler CatmullRom = new CubicResampler(2, 0, .5F);

        /// <summary>
        /// The Hermite filter is type of smoothed triangular interpolation Filter,
        /// This filter rounds off strong edges while preserving flat 'color levels' in the original image.
        /// </summary>
        public static CubicResampler Hermite = new CubicResampler(2, 0, 0);

        /// <summary>
        /// The function implements the Mitchell-Netravali algorithm as described on
        /// <see href="https://de.wikipedia.org/wiki/Mitchell-Netravali-Filter">Wikipedia</see>
        /// </summary>
        public static CubicResampler MitchellNetravali = new CubicResampler(2, .3333333F, .3333333F);

        /// <summary>
        /// The function implements the Robidoux algorithm.
        /// <see href="http://www.imagemagick.org/Usage/filter/#robidoux"/>
        /// </summary>
        public static CubicResampler Robidoux = new CubicResampler(2, .37821575509399867F, .31089212245300067F);

        /// <summary>
        /// The function implements the Robidoux Sharp algorithm.
        /// <see href="http://www.imagemagick.org/Usage/filter/#robidoux"/>
        /// </summary>
        public static CubicResampler RobidouxSharp = new CubicResampler(2, .2620145123990142F, .3689927438004929F);

        /// <summary>
        /// The function implements the spline algorithm.
        /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
        /// </summary>
        /// <summary>
        /// The function implements the Robidoux Sharp algorithm.
        /// <see href="http://www.imagemagick.org/Usage/filter/#robidoux"/>
        /// </summary>
        public static CubicResampler Spline = new CubicResampler(2, 1, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="CubicResampler"/> struct.
        /// </summary>
        /// <param name="radius">The sampling radius.</param>
        /// <param name="bspline">The B-Spline value.</param>
        /// <param name="cardinal">The Cardinal cubic value.</param>
        public CubicResampler(float radius, float bspline, float cardinal)
        {
            this.Radius = radius;
            this.bspline = bspline;
            this.cardinal = cardinal;
        }

        /// <inheritdoc/>
        public float Radius { get; }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public float GetValue(float x)
        {
            float b = this.bspline;
            float c = this.cardinal;

            if (x < 0F)
            {
                x = -x;
            }

            float temp = x * x;
            if (x < 1F)
            {
                x = ((12 - (9 * b) - (6 * c)) * (x * temp)) + ((-18 + (12 * b) + (6 * c)) * temp) + (6 - (2 * b));
                return x / 6F;
            }

            if (x < 2F)
            {
                x = ((-b - (6 * c)) * (x * temp)) + (((6 * b) + (30 * c)) * temp) + (((-12 * b) - (48 * c)) * x) + ((8 * b) + (24 * c));
                return x / 6F;
            }

            return 0F;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void ApplyTransform<TPixel>(IResamplingTransformImageProcessor<TPixel> processor)
            where TPixel : unmanaged, IPixel<TPixel>
            => processor.ApplyTransform(in this);
    }
}
