// <copyright file="KayyaliProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// The Kayyali operator filter.
    /// <see href="http://edgedetection.webs.com/"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class KayyaliProcessor<TColor, TPacked> : EdgeDetector2DFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KayyaliProcessor{TColor,TPacked}"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection..</param>
        public KayyaliProcessor(bool grayscale)
            : base(KernelA, KernelB, grayscale)
        {
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public static float[,] KernelA => new float[,]
        {
            { 6, 0, -6 },
            { 0, 0, 0 },
            { -6, 0, 6 }
        };

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public static float[,] KernelB => new float[,]
        {
            { -6, 0, 6 },
            { 0, 0, 0 },
            { 6, 0, -6 }
        };
    }
}
