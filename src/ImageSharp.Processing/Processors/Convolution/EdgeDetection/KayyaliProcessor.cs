// <copyright file="KayyaliProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The Kayyali operator filter.
    /// <see href="http://edgedetection.webs.com/"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    public class KayyaliProcessor<TColor> : EdgeDetector2DProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The horizontal gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> KayyaliX =
            new Fast2DArray<float>(new float[,]
            {
                { 6, 0, -6 },
                { 0, 0, 0 },
                { -6, 0, 6 }
            });

        /// <summary>
        /// The vertical gradient operator.
        /// </summary>
        private static readonly Fast2DArray<float> KayyaliY =
            new Fast2DArray<float>(new float[,]
            {
                { -6, 0, 6 },
                { 0, 0, 0 },
                { 6, 0, -6 }
            });

        /// <summary>
        /// Initializes a new instance of the <see cref="KayyaliProcessor{TColor}"/> class.
        /// </summary>
        public KayyaliProcessor()
            : base(KayyaliX, KayyaliY)
        {
        }
    }
}
