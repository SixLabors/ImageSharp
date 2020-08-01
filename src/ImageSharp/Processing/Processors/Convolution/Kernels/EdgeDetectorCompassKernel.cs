// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Represents an edge detection convolution kernel consisting of eight gradient operators.
    /// </summary>
    public readonly struct EdgeDetectorCompassKernel : IEquatable<EdgeDetectorCompassKernel>
    {
        /// <summary>
        /// An edge detection kenel comprised of Kirsch gradient operators.
        /// <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>.
        /// </summary>
        public static EdgeDetectorCompassKernel Kirsch =
            new EdgeDetectorCompassKernel(
                KirschKernels.North,
                KirschKernels.NorthWest,
                KirschKernels.West,
                KirschKernels.SouthWest,
                KirschKernels.South,
                KirschKernels.SouthEast,
                KirschKernels.East,
                KirschKernels.NorthEast);

        /// <summary>
        /// An edge detection kenel comprised of Robinson gradient operators.
        /// <see href="http://www.tutorialspoint.com/dip/Robinson_Compass_Mask.htm"/>
        /// </summary>
        public static EdgeDetectorCompassKernel Robinson =
            new EdgeDetectorCompassKernel(
                RobinsonKernels.North,
                RobinsonKernels.NorthWest,
                RobinsonKernels.West,
                RobinsonKernels.SouthWest,
                RobinsonKernels.South,
                RobinsonKernels.SouthEast,
                RobinsonKernels.East,
                RobinsonKernels.NorthEast);

        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorCompassKernel"/> struct.
        /// </summary>
        /// <param name="north">The north gradient operator.</param>
        /// <param name="northWest">The north-west gradient operator.</param>
        /// <param name="west">The west gradient operator.</param>
        /// <param name="southWest">The south-west gradient operator.</param>
        /// <param name="south">The south gradient operator.</param>
        /// <param name="southEast">The south-east gradient operator.</param>
        /// <param name="east">The east gradient operator.</param>
        /// <param name="northEast">The north-east gradient operator.</param>
        public EdgeDetectorCompassKernel(
            DenseMatrix<float> north,
            DenseMatrix<float> northWest,
            DenseMatrix<float> west,
            DenseMatrix<float> southWest,
            DenseMatrix<float> south,
            DenseMatrix<float> southEast,
            DenseMatrix<float> east,
            DenseMatrix<float> northEast)
        {
            this.North = north;
            this.NorthWest = northWest;
            this.West = west;
            this.SouthWest = southWest;
            this.South = south;
            this.SouthEast = southEast;
            this.East = east;
            this.NorthEast = northEast;
        }

        /// <summary>
        /// Gets the North gradient operator.
        /// </summary>
        public DenseMatrix<float> North { get; }

        /// <summary>
        /// Gets the NorthWest gradient operator.
        /// </summary>
        public DenseMatrix<float> NorthWest { get; }

        /// <summary>
        /// Gets the West gradient operator.
        /// </summary>
        public DenseMatrix<float> West { get; }

        /// <summary>
        /// Gets the SouthWest gradient operator.
        /// </summary>
        public DenseMatrix<float> SouthWest { get; }

        /// <summary>
        /// Gets the South gradient operator.
        /// </summary>
        public DenseMatrix<float> South { get; }

        /// <summary>
        /// Gets the SouthEast gradient operator.
        /// </summary>
        public DenseMatrix<float> SouthEast { get; }

        /// <summary>
        /// Gets the East gradient operator.
        /// </summary>
        public DenseMatrix<float> East { get; }

        /// <summary>
        /// Gets the NorthEast gradient operator.
        /// </summary>
        public DenseMatrix<float> NorthEast { get; }

        /// <summary>
        /// Checks whether two <see cref="EdgeDetectorCompassKernel"/> structures are equal.
        /// </summary>
        /// <param name="left">The left hand <see cref="EdgeDetectorCompassKernel"/> operand.</param>
        /// <param name="right">The right hand <see cref="EdgeDetectorCompassKernel"/> operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter;
        /// otherwise, false.
        /// </returns>
        public static bool operator ==(EdgeDetectorCompassKernel left, EdgeDetectorCompassKernel right)
            => left.Equals(right);

        /// <summary>
        /// Checks whether two <see cref="EdgeDetectorCompassKernel"/> structures are equal.
        /// </summary>
        /// <param name="left">The left hand <see cref="EdgeDetectorCompassKernel"/> operand.</param>
        /// <param name="right">The right hand <see cref="EdgeDetectorCompassKernel"/> operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter;
        /// otherwise, false.
        /// </returns>
        public static bool operator !=(EdgeDetectorCompassKernel left, EdgeDetectorCompassKernel right)
            => !(left == right);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is EdgeDetectorCompassKernel kernel && this.Equals(kernel);

        /// <inheritdoc/>
        public bool Equals(EdgeDetectorCompassKernel other) => this.North.Equals(other.North) && this.NorthWest.Equals(other.NorthWest) && this.West.Equals(other.West) && this.SouthWest.Equals(other.SouthWest) && this.South.Equals(other.South) && this.SouthEast.Equals(other.SouthEast) && this.East.Equals(other.East) && this.NorthEast.Equals(other.NorthEast);

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(
                this.North,
                this.NorthWest,
                this.West,
                this.SouthWest,
                this.South,
                this.SouthEast,
                this.East,
                this.NorthEast);

        internal DenseMatrix<float>[] Flatten() =>
             new[]
                 {
                    this.North, this.NorthWest, this.West, this.SouthWest,
                    this.South, this.SouthEast, this.East, this.NorthEast
                 };
    }
}
