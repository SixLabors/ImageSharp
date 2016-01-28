// <copyright file="ImageFrame.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;

    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    public class ImageFrame : ImageBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame"/> class.
        /// </summary>
        public ImageFrame()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame"/> class.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageBase"/> to create this instance from.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the given <see cref="ImageFrame"/> is null.
        /// </exception>
        public ImageFrame(ImageFrame other)
            : base(other)
        {
        }
    }
}
