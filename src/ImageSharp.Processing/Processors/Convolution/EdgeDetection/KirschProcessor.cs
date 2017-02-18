// <copyright file="KirschProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The Kirsch operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    public class KirschProcessor<TColor> : EdgeDetectorCompassProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The North gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> KirschNorth =
            new Fast2DArray<float>(new float[,]
            {
               { 5,  5,  5 },
               { -3,  0, -3 },
               { -3, -3, -3 }
            });

        /// <summary>
        /// The NorthWest gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> KirschNorthWest =
            new Fast2DArray<float>(new float[,]
            {
               { 5,  5, -3 },
               { 5,  0, -3 },
               { -3, -3, -3 }
            });

        /// <summary>
        /// The West gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> KirschWest =
            new Fast2DArray<float>(new float[,]
            {
               { 5, -3, -3 },
               { 5,  0, -3 },
               { 5, -3, -3 }
            });

        /// <summary>
        /// The SouthWest gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> KirschSouthWest =
            new Fast2DArray<float>(new float[,]
            {
               { -3, -3, -3 },
               { 5, 0, -3 },
               { 5,  5, -3 }
            });

        /// <summary>
        /// The South gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> KirschSouth =
            new Fast2DArray<float>(new float[,]
            {
               { -3, -3, -3 },
               { -3,  0, -3 },
               { 5,  5,  5 }
            });

        /// <summary>
        /// The SouthEast gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> KirschSouthEast =
            new Fast2DArray<float>(new float[,]
            {
               { -3, -3, -3 },
               { -3,  0,  5 },
               { -3,  5,  5 }
            });

        /// <summary>
        /// The East gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> KirschEast =
            new Fast2DArray<float>(new float[,]
            {
               { -3, -3, 5 },
               { -3,  0, 5 },
               { -3, -3, 5 }
            });

        /// <summary>
        /// The NorthEast gradient operator
        /// </summary>
        private static readonly Fast2DArray<float> KirschNorthEast =
            new Fast2DArray<float>(new float[,]
            {
               { -3,  5,  5 },
               { -3,  0,  5 },
               { -3, -3, -3 }
            });

        /// <inheritdoc/>
        public override Fast2DArray<float> North => KirschNorth;

        /// <inheritdoc/>
        public override Fast2DArray<float> NorthWest => KirschNorthWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> West => KirschWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> SouthWest => KirschSouthWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> South => KirschSouth;

        /// <inheritdoc/>
        public override Fast2DArray<float> SouthEast => KirschSouthEast;

        /// <inheritdoc/>
        public override Fast2DArray<float> East => KirschEast;

        /// <inheritdoc/>
        public override Fast2DArray<float> NorthEast => KirschNorthEast;
    }
}