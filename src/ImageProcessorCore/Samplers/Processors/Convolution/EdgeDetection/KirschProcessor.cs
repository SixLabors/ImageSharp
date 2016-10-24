// <copyright file="KirschProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageProcessorCore.Processors
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The Kirsch operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    public class KirschProcessor<TColor, TPacked> : EdgeDetectorCompassFilter<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The North gradient operator
        /// </summary>
        private static readonly float[][] KirschNorth =
        {
           new float[] { 5,  5,  5 },
           new float[] { -3,  0, -3 },
           new float[] { -3, -3, -3 }
        };

        /// <summary>
        /// The NorthWest gradient operator
        /// </summary>
        private static readonly float[][] KirschNorthWest =
        {
           new float[] { 5,  5, -3 },
           new float[] { 5,  0, -3 },
           new float[] { -3, -3, -3 }
        };

        /// <summary>
        /// The West gradient operator
        /// </summary>
        private static readonly float[][] KirschWest =
        {
           new float[] { 5, -3, -3 },
           new float[] { 5,  0, -3 },
           new float[] { 5, -3, -3 }
        };

        /// <summary>
        /// The SouthWest gradient operator
        /// </summary>
        private static readonly float[][] KirschSouthWest =
        {
           new float[] { -3, -3, -3 },
           new float[] { 5, 0, -3 },
           new float[] { 5,  5, -3 }
        };

        /// <summary>
        /// The South gradient operator
        /// </summary>
        private static readonly float[][] KirschSouth =
        {
           new float[] { -3, -3, -3 },
           new float[] { -3,  0, -3 },
           new float[] { 5,  5,  5 }
        };

        /// <summary>
        /// The SouthEast gradient operator
        /// </summary>
        private static readonly float[][] KirschSouthEast =
        {
           new float[] { -3, -3, -3 },
           new float[] { -3,  0,  5 },
           new float[] { -3,  5,  5 }
        };

        /// <summary>
        /// The East gradient operator
        /// </summary>
        private static readonly float[][] KirschEast =
        {
           new float[] { -3, -3, 5 },
           new float[] { -3,  0, 5 },
           new float[] { -3, -3, 5 }
        };

        /// <summary>
        /// The NorthEast gradient operator
        /// </summary>
        private static readonly float[][] KirschNorthEast =
        {
           new float[] { -3,  5,  5 },
           new float[] { -3,  0,  5 },
           new float[] { -3, -3, -3 }
        };

        /// <inheritdoc/>
        public override float[][] North => KirschNorth;

        /// <inheritdoc/>
        public override float[][] NorthWest => KirschNorthWest;

        /// <inheritdoc/>
        public override float[][] West => KirschWest;

        /// <inheritdoc/>
        public override float[][] SouthWest => KirschSouthWest;

        /// <inheritdoc/>
        public override float[][] South => KirschSouth;

        /// <inheritdoc/>
        public override float[][] SouthEast => KirschSouthEast;

        /// <inheritdoc/>
        public override float[][] East => KirschEast;

        /// <inheritdoc/>
        public override float[][] NorthEast => KirschNorthEast;
    }
}