// <copyright file="IJpegEncoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Encapsulates the options for the <see cref="JpegEncoder"/>.
    /// </summary>
    public interface IJpegEncoderOptions : IEncoderOptions
    {
        /// <summary>
        /// Gets the quality, that will be used to encode the image. Quality
        /// index must be between 0 and 100 (compression from max to min).
        /// </summary>
        /// <value>The quality of the jpg image from 0 to 100.</value>
        int Quality { get; }

        /// <summary>
        /// Gets the subsample ration, that will be used to encode the image.
        /// </summary>
        /// <value>The subsample ratio of the jpg image.</value>
        JpegSubsample? Subsample { get; }
    }
}
