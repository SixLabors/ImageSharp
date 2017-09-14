// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// The Roberts Cross operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Roberts_cross"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    internal class RobertsCrossProcessor<TPixel> : EdgeDetector2DProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The horizontal gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> RobertsCrossX =
            new float[,]
            {
                { 1, 0 },
                { 0, -1 }
            };

        /// <summary>
        /// The vertical gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> RobertsCrossY =
            new float[,]
            {
                { 0, 1 },
                { -1, 0 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="RobertsCrossProcessor{TPixel}"/> class.
        /// </summary>
        public RobertsCrossProcessor()
            : base(RobertsCrossX, RobertsCrossY)
        {
        }
    }
}
