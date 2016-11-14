// <copyright file="Image.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Diagnostics;

    /// <summary>
    /// An optimized frame for the <see cref="Image"/> class.
    /// </summary>
    [DebuggerDisplay("ImageFrame: {Width}x{Height}")]
    public class ImageFrame : ImageFrame<Color, uint>
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
        /// <param name="image">
        /// The image to create the frame from.
        /// </param>
        public ImageFrame(ImageBase<Color, uint> image)
            : base(image)
        {
        }

        /// <inheritdoc />
        public override PixelAccessor<Color, uint> Lock()
        {
            return new PixelAccessor(this);
        }

        /// <inheritdoc />
        internal override ImageFrame<Color, uint> Clone()
        {
            return new ImageFrame(this);
        }
    }
}
