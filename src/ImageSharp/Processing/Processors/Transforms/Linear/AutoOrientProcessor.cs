// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Adjusts an image so that its orientation is suitable for viewing. Adjustments are based on EXIF metadata embedded in the image.
    /// </summary>
    public sealed class AutoOrientProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AutoOrientProcessor"/> class.
        /// </summary>
        public AutoOrientProcessor()
            : this(AutoOrientMode.ResetExifValue);

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoOrientProcessor"/> class.
        /// </summary>
        /// <param name="autoOrientMode">The <see cref="AutoOrientMode"/> used to perform the auto orientation.</param>
        public AutoOrientProcessor(AutoOrientMode autoOrientMode) => this.AutoOrientMode = autoOrientMode;

        /// <summary>
        /// Gets the <see cref="AutoOrientMode"/> used to perform the auto orientation.
        /// </summary>
        public AutoOrientMode AutoOrientMode { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new AutoOrientProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
