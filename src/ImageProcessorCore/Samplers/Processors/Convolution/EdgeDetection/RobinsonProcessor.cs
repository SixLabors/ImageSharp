// <copyright file="RobinsonProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Kirsch operator filter.
    /// <see href="http://www.tutorialspoint.com/dip/Robinson_Compass_Mask.htm"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class RobinsonProcessor<TColor, TPacked> : EdgeDetectorCompassFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public override float[,] North => new float[,]
        {
            { 1, 2, 1 },
            { 0,  0, 0 },
            { -1, -2, -1 }
        };

        /// <inheritdoc/>
        public override float[,] NorthWest => new float[,]
        {
            { 2,  1, 0 },
            { 1,  0, -1 },
            { 0, -1, -2 }
        };

        /// <inheritdoc/>
        public override float[,] West => new float[,]
        {
            { 1, 0, -1 },
            { 2, 0, -2 },
            { 1, 0, -1 }
        };

        /// <inheritdoc/>
        public override float[,] SouthWest => new float[,]
        {
            { 0, -1, -2 },
            { 1, 0, -1 },
            { 2, 1,  0 }
        };

        /// <inheritdoc/>
        public override float[,] South => new float[,]
        {
            { -1, -2, -1 },
            { 0,  0, 0 },
            { 1,  2,  1 }
        };

        /// <inheritdoc/>
        public override float[,] SouthEast => new float[,]
        {
            { -2, -1, 0 },
            { -1,  0, 1 },
            { 0,  1,  2 }
        };

        /// <inheritdoc/>
        public override float[,] East => new float[,]
        {
            { -1, 0, 1 },
            { -2, 0, 2 },
            { -1, 0, 1 }
        };

        /// <inheritdoc/>
        public override float[,] NorthEast => new float[,]
        {
            { 0,  1,  2 },
            { -1,  0, 1 },
            { -2, -1, 0 }
        };
    }
}