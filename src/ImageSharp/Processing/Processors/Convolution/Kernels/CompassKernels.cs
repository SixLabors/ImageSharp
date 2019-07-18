// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    internal abstract class CompassKernels
    {
        /// <summary>
        /// Gets the North gradient operator.
        /// </summary>
        public abstract DenseMatrix<float> North { get; }

        /// <summary>
        /// Gets the NorthWest gradient operator.
        /// </summary>
        public abstract DenseMatrix<float> NorthWest { get; }

        /// <summary>
        /// Gets the West gradient operator.
        /// </summary>
        public abstract DenseMatrix<float> West { get; }

        /// <summary>
        /// Gets the SouthWest gradient operator.
        /// </summary>
        public abstract DenseMatrix<float> SouthWest { get; }

        /// <summary>
        /// Gets the South gradient operator.
        /// </summary>
        public abstract DenseMatrix<float> South { get; }

        /// <summary>
        /// Gets the SouthEast gradient operator.
        /// </summary>
        public abstract DenseMatrix<float> SouthEast { get; }

        /// <summary>
        /// Gets the East gradient operator.
        /// </summary>
        public abstract DenseMatrix<float> East { get; }

        /// <summary>
        /// Gets the NorthEast gradient operator.
        /// </summary>
        public abstract DenseMatrix<float> NorthEast { get; }

        public DenseMatrix<float>[] Flatten() =>
            new[]
                {
                    this.North, this.NorthWest, this.West, this.SouthWest,
                    this.South, this.SouthEast, this.East, this.NorthEast
                };
    }
}