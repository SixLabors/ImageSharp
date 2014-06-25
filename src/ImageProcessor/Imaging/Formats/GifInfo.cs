// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GifInfo.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides information about an image.
//   <see href="http://madskristensen.net/post/examine-animated-gife28099s-in-c" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System.Collections.Generic;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Provides information about an image.
    /// <see href="http://madskristensen.net/post/examine-animated-gife28099s-in-c"/>
    /// </summary>
    public class GifInfo
    {
        /// <summary>
        /// Gets or sets the image width.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the image height.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the image is animated.
        /// </summary>
        public bool IsAnimated { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the image is looped.
        /// </summary>
        public bool IsLooped { get; set; }

        /// <summary>
        /// Gets or sets the loop count.
        /// </summary>
        public int LoopCount { get; set; }

        /// <summary>
        /// Gets or sets the gif frames.
        /// </summary>
        public ICollection<GifFrame> GifFrames { get; set; }

        /// <summary>
        /// Gets or sets the animation length in milliseconds.
        /// </summary>
        public int AnimationLength { get; set; }
    }
}