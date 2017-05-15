// <copyright file="KayyaliProcessor.cs" company="James Jackson-South">
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
    /// The Kayyali operator filter.
    /// <see href="http://edgedetection.webs.com/"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    internal class KayyaliProcessor<TPixel> : EdgeDetector2DProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The horizontal gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> KayyaliX =
            new float[,]
            {
                { 6, 0, -6 },
                { 0, 0, 0 },
                { -6, 0, 6 }
            };

        /// <summary>
        /// The vertical gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> KayyaliY =
            new float[,]
            {
                { -6, 0, 6 },
                { 0, 0, 0 },
                { 6, 0, -6 }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="KayyaliProcessor{TPixel}"/> class.
        /// </summary>
        public KayyaliProcessor()
            : base(KayyaliX, KayyaliY)
        {
        }
    }
}
