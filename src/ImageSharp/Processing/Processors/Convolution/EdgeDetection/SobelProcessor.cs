// <copyright file="SobelProcessor.cs" company="James Jackson-South">
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
    /// The Sobel operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Sobel_operator"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    internal class SobelProcessor<TPixel> : EdgeDetector2DProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The horizontal gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> SobelX =
            new float[,]
            {
                { -1, 0, 1 },
                { -2, 0, 2 },
                { -1, 0, 1 }
            };

        /// <summary>
        /// The vertical gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> SobelY =
            new float[,]
            {
                { -1, -2, -1 },
                { 0, 0, 0 },
                { 1, 2, 1 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="SobelProcessor{TPixel}"/> class.
        /// </summary>
        public SobelProcessor()
            : base(SobelX, SobelY)
        {
        }
    }
}
