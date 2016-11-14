// <copyright file="ImageFrame.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class ImageFrame<TColor, TPacked> : ImageBase<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
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
        /// <param name="image">
        /// The image to create the frame from.
        /// </param>
        public ImageFrame(ImageBase<TColor, TPacked> image)
            : base(image)
        {
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"ImageFrame: {this.Width}x{this.Height}";
        }

        internal virtual ImageFrame<TColor, TPacked> Clone()
        {
            return new ImageFrame<TColor, TPacked>(this);
        }
    }
}
