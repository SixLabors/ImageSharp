// <copyright file="RobinsonProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// The Kirsch operator filter.
    /// <see href="http://www.tutorialspoint.com/dip/Robinson_Compass_Mask.htm"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    internal class RobinsonProcessor<TPixel> : EdgeDetectorCompassProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The North gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> RobinsonNorth =
            new float[,]
            {
               { 1, 2, 1 },
               { 0,  0, 0 },
               { -1, -2, -1 }
            };

        /// <summary>
        /// The NorthWest gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> RobinsonNorthWest =
            new float[,]
            {
               { 2,  1, 0 },
               { 1,  0, -1 },
               { 0, -1, -2 }
            };

        /// <summary>
        /// The West gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> RobinsonWest =
            new float[,]
            {
               { 1, 0, -1 },
               { 2, 0, -2 },
               { 1, 0, -1 }
            };

        /// <summary>
        /// The SouthWest gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> RobinsonSouthWest =
            new float[,]
            {
               { 0, -1, -2 },
               { 1, 0, -1 },
               { 2, 1,  0 }
            };

        /// <summary>
        /// The South gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> RobinsonSouth =
            new float[,]
            {
               { -1, -2, -1 },
               { 0,  0, 0 },
               { 1,  2,  1 }
            };

        /// <summary>
        /// The SouthEast gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> RobinsonSouthEast =
            new float[,]
            {
               { -2, -1, 0 },
               { -1,  0, 1 },
               { 0,  1,  2 }
            };

        /// <summary>
        /// The East gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> RobinsonEast =
            new float[,]
            {
               { -1, 0, 1 },
               { -2, 0, 2 },
               { -1, 0, 1 }
            };

        /// <summary>
        /// The NorthEast gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> RobinsonNorthEast =
            new float[,]
            {
               { 0,  1,  2 },
               { -1,  0, 1 },
               { -2, -1, 0 }
            };

        /// <inheritdoc/>
        public override Fast2DArray<float> North => RobinsonNorth;

        /// <inheritdoc/>
        public override Fast2DArray<float> NorthWest => RobinsonNorthWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> West => RobinsonWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> SouthWest => RobinsonSouthWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> South => RobinsonSouth;

        /// <inheritdoc/>
        public override Fast2DArray<float> SouthEast => RobinsonSouthEast;

        /// <inheritdoc/>
        public override Fast2DArray<float> East => RobinsonEast;

        /// <inheritdoc/>
        public override Fast2DArray<float> NorthEast => RobinsonNorthEast;
    }
}