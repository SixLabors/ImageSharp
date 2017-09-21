// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
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
