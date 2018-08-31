// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Contains reusable static instances of known resampling algorithms
    /// </summary>
    public static class KnownResamplers
    {
        /// <summary>
        /// Gets the Bicubic sampler that implements the bicubic kernel algorithm W(x)
        /// </summary>
        public static IResampler Bicubic { get; } = new BicubicResampler();

        /// <summary>
        /// Gets the Box sampler that implements the box algorithm. Similar to nearest neighbor when upscaling.
        /// When downscaling the pixels will average, merging pixels together.
        /// </summary>
        public static IResampler Box { get; } = new BoxResampler();

        /// <summary>
        /// Gets the Catmull-Rom sampler, a well known standard Cubic Filter often used as a interpolation function
        /// </summary>
        public static IResampler CatmullRom { get; } = new CatmullRomResampler();

        /// <summary>
        /// Gets the Hermite sampler. A type of smoothed triangular interpolation filter that rounds off strong edges while
        /// preserving flat 'color levels' in the original image.
        /// </summary>
        public static IResampler Hermite { get; } = new HermiteResampler();

        /// <summary>
        /// Gets the Lanczos kernel sampler that implements smooth interpolation with a radius of 2 pixels.
        /// This algorithm provides sharpened results when compared to others when downsampling.
        /// </summary>
        public static IResampler Lanczos2 { get; } = new Lanczos2Resampler();

        /// <summary>
        /// Gets the Lanczos kernel sampler that implements smooth interpolation with a radius of 3 pixels
        /// This algorithm provides sharpened results when compared to others when downsampling.
        /// </summary>
        public static IResampler Lanczos3 { get; } = new Lanczos3Resampler();

        /// <summary>
        /// Gets the Lanczos kernel sampler that implements smooth interpolation with a radius of 5 pixels
        /// This algorithm provides sharpened results when compared to others when downsampling.
        /// </summary>
        public static IResampler Lanczos5 { get; } = new Lanczos5Resampler();

        /// <summary>
        /// Gets the Lanczos kernel sampler that implements smooth interpolation with a radius of 8 pixels
        /// This algorithm provides sharpened results when compared to others when downsampling.
        /// </summary>
        public static IResampler Lanczos8 { get; } = new Lanczos8Resampler();

        /// <summary>
        /// Gets the Mitchell-Netravali sampler. This seperable cubic algorithm yields a very good equilibrium between
        /// detail preservation (sharpness) and smoothness.
        /// </summary>
        public static IResampler MitchellNetravali { get; } = new MitchellNetravaliResampler();

        /// <summary>
        /// Gets the Nearest-Neighbour sampler that implements the nearest neighbor algorithm. This uses a very fast, unscaled filter
        /// which will select the closest pixel to the new pixels position.
        /// </summary>
        public static IResampler NearestNeighbor { get; } = new NearestNeighborResampler();

        /// <summary>
        /// Gets the Robidoux sampler. This algorithm developed by Nicolas Robidoux providing a very good equilibrium between
        /// detail preservation (sharpness) and smoothness comprable to <see cref="MitchellNetravali"/>.
        /// </summary>
        public static IResampler Robidoux { get; } = new RobidouxResampler();

        /// <summary>
        /// Gets the Robidoux Sharp sampler. A sharpend form of the <see cref="Robidoux"/> sampler
        /// </summary>
        public static IResampler RobidouxSharp { get; } = new RobidouxSharpResampler();

        /// <summary>
        /// Gets the Spline sampler. A seperable cubic algorithm similar to <see cref="MitchellNetravali"/> but yielding smoother results.
        /// </summary>
        public static IResampler Spline { get; } = new SplineResampler();

        /// <summary>
        /// Gets the Triangle sampler, otherwise known as Bilinear. This interpolation algorithm can be used where perfect image transformation
        /// with pixel matching is impossible, so that one can calculate and assign appropriate intensity values to pixels
        /// </summary>
        public static IResampler Triangle { get; } = new TriangleResampler();

        /// <summary>
        /// Gets the Welch sampler. A high speed algorthm that delivers very sharpened results.
        /// </summary>
        public static IResampler Welch { get; } = new WelchResampler();
    }
}