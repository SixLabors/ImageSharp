// <copyright file="RobertsCrossProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Roberts Cross operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Roberts_cross"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class RobertsCrossProcessor<TColor, TPacked> : EdgeDetector2DFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RobertsCrossProcessor{TColor,TPacked}"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection..</param>
        public RobertsCrossProcessor(bool grayscale)
            : base(KernelA, KernelB, grayscale)
        {
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public static float[,] KernelA => new float[,]
        {
            { 1, 0 },
            { 0, -1 }
        };

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public static float[,] KernelB => new float[,]
        {
            { 0, 1 },
            { -1, 0 }
        };
    }
}
