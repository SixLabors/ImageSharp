// <copyright file="ImageFrame.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class ImageFrame<TColor, TPacked> : ImageBase<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TColor, TPacked}"/> class. 
        /// </summary>
        public ImageFrame()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame{TColor, TPacked}"/> class. 
        /// </summary>
        /// <param name="frame">
        /// The frame to create the frame from.
        /// </param>
        public ImageFrame(ImageFrame<TColor, TPacked> frame)
            : base(frame)
        {
        }
    }
}
