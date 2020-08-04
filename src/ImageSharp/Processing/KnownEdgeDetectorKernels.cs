// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Contains reusable static instances of known edge detection kernels.
    /// </summary>
    public static class KnownEdgeDetectorKernels
    {
        /// <summary>
        /// Gets the Kayyali edge detector kernel.
        /// </summary>
        public static EdgeDetector2DKernel Kayyali { get; } = EdgeDetector2DKernel.KayyaliKernel;

        /// <summary>
        /// Gets the Kirsch edge detector kernel.
        /// </summary>
        public static EdgeDetectorCompassKernel Kirsch { get; } = EdgeDetectorCompassKernel.Kirsch;

        /// <summary>
        /// Gets the Laplacian 3x3 edge detector kernel.
        /// </summary>
        public static EdgeDetectorKernel Laplacian3x3 { get; } = EdgeDetectorKernel.Laplacian3x3;

        /// <summary>
        /// Gets the Laplacian 5x5 edge detector kernel.
        /// </summary>
        public static EdgeDetectorKernel Laplacian5x5 { get; } = EdgeDetectorKernel.Laplacian5x5;

        /// <summary>
        /// Gets the Laplacian of Gaussian edge detector kernel.
        /// </summary>
        public static EdgeDetectorKernel LaplacianOfGaussian { get; } = EdgeDetectorKernel.LaplacianOfGaussian;

        /// <summary>
        /// Gets the Prewitt edge detector kernel.
        /// </summary>
        public static EdgeDetector2DKernel Prewitt { get; } = EdgeDetector2DKernel.PrewittKernel;

        /// <summary>
        /// Gets the Roberts-Cross edge detector kernel.
        /// </summary>
        public static EdgeDetector2DKernel RobertsCross { get; } = EdgeDetector2DKernel.RobertsCrossKernel;

        /// <summary>
        /// Gets the Robinson edge detector kernel.
        /// </summary>
        public static EdgeDetectorCompassKernel Robinson { get; } = EdgeDetectorCompassKernel.Robinson;

        /// <summary>
        /// Gets the Scharr edge detector kernel.
        /// </summary>
        public static EdgeDetector2DKernel Scharr { get; } = EdgeDetector2DKernel.ScharrKernel;

        /// <summary>
        /// Gets the Sobel edge detector kernel.
        /// </summary>
        public static EdgeDetector2DKernel Sobel { get; } = EdgeDetector2DKernel.SobelKernel;
    }
}
