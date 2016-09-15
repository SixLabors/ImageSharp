// <copyright file="KirschProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Kirsch operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class KirschProcessor<TColor, TPacked> : EdgeDetectorCompassFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public override float[,] North => new float[,]
        {
            { 5,  5,  5 },
            { -3,  0, -3 },
            { -3, -3, -3 }
        };

        /// <inheritdoc/>
        public override float[,] NorthWest => new float[,]
        {
            { 5,  5, -3 },
            { 5,  0, -3 },
            { -3, -3, -3 }
        };

        /// <inheritdoc/>
        public override float[,] West => new float[,]
        {
            { 5, -3, -3 },
            { 5,  0, -3 },
            { 5, -3, -3 }
        };

        /// <inheritdoc/>
        public override float[,] SouthWest => new float[,]
        {
            { -3, -3, -3 },
            { 5, 0, -3 },
            { 5,  5, -3 }
        };

        /// <inheritdoc/>
        public override float[,] South => new float[,]
        {
            { -3, -3, -3 },
            { -3,  0, -3 },
            { 5,  5,  5 }
        };

        /// <inheritdoc/>
        public override float[,] SouthEast => new float[,]
        {
            { -3, -3, -3 },
            { -3,  0,  5 },
            { -3,  5,  5 }
        };

        /// <inheritdoc/>
        public override float[,] East => new float[,]
        {
            { -3, -3, 5 },
            { -3,  0, 5 },
            { -3, -3, 5 }
        };

        /// <inheritdoc/>
        public override float[,] NorthEast => new float[,]
        {
            { -3,  5,  5 },
            { -3,  0,  5 },
            { -3, -3, -3 }
        };
    }
}