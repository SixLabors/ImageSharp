// <copyright file="RobinsonProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageProcessorCore.Processors
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The Kirsch operator filter.
    /// <see href="http://www.tutorialspoint.com/dip/Robinson_Compass_Mask.htm"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    public class RobinsonProcessor<TColor, TPacked> : EdgeDetectorCompassFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The North gradient operator
        /// </summary>
        private static readonly float[][] RobinsonNorth =
        {
           new float[] { 1, 2, 1 },
           new float[] { 0,  0, 0 },
           new float[] { -1, -2, -1 }
        };

        /// <summary>
        /// The NorthWest gradient operator
        /// </summary>
        private static readonly float[][] RobinsonNorthWest =
        {
           new float[] { 2,  1, 0 },
           new float[] { 1,  0, -1 },
           new float[] { 0, -1, -2 }
        };

        /// <summary>
        /// The West gradient operator
        /// </summary>
        private static readonly float[][] RobinsonWest =
        {
           new float[] { 1, 0, -1 },
           new float[] { 2, 0, -2 },
           new float[] { 1, 0, -1 }
        };

        /// <summary>
        /// The SouthWest gradient operator
        /// </summary>
        private static readonly float[][] RobinsonSouthWest =
        {
           new float[] { 0, -1, -2 },
           new float[] { 1, 0, -1 },
           new float[] { 2, 1,  0 }
        };

        /// <summary>
        /// The South gradient operator
        /// </summary>
        private static readonly float[][] RobinsonSouth =
        {
           new float[] { -1, -2, -1 },
           new float[] { 0,  0, 0 },
           new float[] { 1,  2,  1 }
        };

        /// <summary>
        /// The SouthEast gradient operator
        /// </summary>
        private static readonly float[][] RobinsonSouthEast =
        {
           new float[] { -2, -1, 0 },
           new float[] { -1,  0, 1 },
           new float[] { 0,  1,  2 }
        };

        /// <summary>
        /// The East gradient operator
        /// </summary>
        private static readonly float[][] RobinsonEast =
        {
           new float[] { -1, 0, 1 },
           new float[] { -2, 0, 2 },
           new float[] { -1, 0, 1 }
        };

        /// <summary>
        /// The NorthEast gradient operator
        /// </summary>
        private static readonly float[][] RobinsonNorthEast =
        {
           new float[] { 0,  1,  2 },
           new float[] { -1,  0, 1 },
           new float[] { -2, -1, 0 }
        };

        /// <inheritdoc/>
        public override float[][] North => RobinsonNorth;

        /// <inheritdoc/>
        public override float[][] NorthWest => RobinsonNorthWest;

        /// <inheritdoc/>
        public override float[][] West => RobinsonWest;

        /// <inheritdoc/>
        public override float[][] SouthWest => RobinsonSouthWest;

        /// <inheritdoc/>
        public override float[][] South => RobinsonSouth;

        /// <inheritdoc/>
        public override float[][] SouthEast => RobinsonSouthEast;

        /// <inheritdoc/>
        public override float[][] East => RobinsonEast;

        /// <inheritdoc/>
        public override float[][] NorthEast => RobinsonNorthEast;
    }
}