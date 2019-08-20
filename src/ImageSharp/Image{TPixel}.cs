// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// For generic <see cref="Image{TPixel}"/>-s the pixel type is known at compile time.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class Image<TPixel> : Image
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public Image(Configuration configuration, int width, int height)
            : this(configuration, width, height, new ImageMetadata())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="backgroundColor">The color to initialize the pixels with.</param>
        public Image(Configuration configuration, int width, int height, TPixel backgroundColor)
            : this(configuration, width, height, backgroundColor, new ImageMetadata())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public Image(int width, int height)
            : this(Configuration.Default, width, height)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The images metadata.</param>
        internal Image(Configuration configuration, int width, int height, ImageMetadata metadata)
            : base(configuration, PixelTypeInfo.Create<TPixel>(), metadata, width, height)
        {
            this.Frames = new ImageFrameCollection<TPixel>(this, width, height, default(TPixel));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// wrapping an external <see cref="MemorySource{T}"/>.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="memorySource">The memory source.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The images metadata.</param>
        internal Image(
            Configuration configuration,
            MemorySource<TPixel> memorySource,
            int width,
            int height,
            ImageMetadata metadata)
            : base(configuration, PixelTypeInfo.Create<TPixel>(), metadata, width, height)
        {
            this.Frames = new ImageFrameCollection<TPixel>(this, width, height, memorySource);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="backgroundColor">The color to initialize the pixels with.</param>
        /// <param name="metadata">The images metadata.</param>
        internal Image(
            Configuration configuration,
            int width,
            int height,
            TPixel backgroundColor,
            ImageMetadata metadata)
            : base(configuration, PixelTypeInfo.Create<TPixel>(), metadata, width, height)
        {
            this.Frames = new ImageFrameCollection<TPixel>(this, width, height, backgroundColor);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}" /> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="metadata">The images metadata.</param>
        /// <param name="frames">The frames that will be owned by this image instance.</param>
        internal Image(Configuration configuration, ImageMetadata metadata, IEnumerable<ImageFrame<TPixel>> frames)
            : base(configuration, PixelTypeInfo.Create<TPixel>(), metadata, ValidateFramesAndGetSize(frames))
        {
            this.Frames = new ImageFrameCollection<TPixel>(this, frames);
        }

        /// <inheritdoc />
        protected override ImageFrameCollection NonGenericFrameCollection => this.Frames;

        /// <summary>
        /// Gets the frames.
        /// </summary>
        public new ImageFrameCollection<TPixel> Frames { get; }

        /// <summary>
        /// Gets the root frame.
        /// </summary>
        private IPixelSource<TPixel> PixelSource => this.Frames?.RootFrame ?? throw new ObjectDisposedException(nameof(Image<TPixel>));

        /// <summary>
        /// Gets or sets the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
        /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
        public TPixel this[int x, int y]
        {
            get => this.PixelSource.PixelBuffer[x, y];
            set => this.PixelSource.PixelBuffer[x, y] = value;
        }

        /// <summary>
        /// Clones the current image
        /// </summary>
        /// <returns>Returns a new image with all the same metadata as the original.</returns>
        public Image<TPixel> Clone() => this.Clone(this.Configuration);

        /// <summary>
        /// Clones the current image with the given configuration.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <returns>Returns a new <see cref="Image{TPixel}"/> with all the same pixel data as the original.</returns>
        public Image<TPixel> Clone(Configuration configuration)
        {
            this.EnsureNotDisposed();

            IEnumerable<ImageFrame<TPixel>> clonedFrames =
                this.Frames.Select<ImageFrame<TPixel>, ImageFrame<TPixel>>(x => x.Clone(configuration));
            return new Image<TPixel>(configuration, this.Metadata.DeepClone(), clonedFrames);
        }

        /// <summary>
        /// Returns a copy of the image in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <returns>The <see cref="Image{TPixel2}"/>.</returns>
        public override Image<TPixel2> CloneAs<TPixel2>(Configuration configuration)
        {
            this.EnsureNotDisposed();

            IEnumerable<ImageFrame<TPixel2>> clonedFrames =
                this.Frames.Select<ImageFrame<TPixel>, ImageFrame<TPixel2>>(x => x.CloneAs<TPixel2>(configuration));
            return new Image<TPixel2>(configuration, this.Metadata.DeepClone(), clonedFrames);
        }

        /// <inheritdoc/>
        protected override void DisposeImpl() => this.Frames.Dispose();

        /// <inheritdoc />
        internal override void AcceptVisitor(IImageVisitor visitor)
        {
            this.EnsureNotDisposed();

            visitor.Visit(this);
        }

        /// <inheritdoc/>
        public override string ToString() => $"Image<{typeof(TPixel).Name}>: {this.Width}x{this.Height}";

        /// <summary>
        /// Switches the buffers used by the image and the pixelSource meaning that the Image will "own" the buffer from the pixelSource and the pixelSource will now own the Images buffer.
        /// </summary>
        /// <param name="pixelSource">The pixel source.</param>
        internal void SwapOrCopyPixelsBuffersFrom(Image<TPixel> pixelSource)
        {
            Guard.NotNull(pixelSource, nameof(pixelSource));

            for (int i = 0; i < this.Frames.Count; i++)
            {
                this.Frames[i].SwapOrCopyPixelsBufferFrom(pixelSource.Frames[i]);
            }

            this.UpdateSize(pixelSource.Size());
        }

        private static Size ValidateFramesAndGetSize(IEnumerable<ImageFrame<TPixel>> frames)
        {
            Guard.NotNull(frames, nameof(frames));

            ImageFrame<TPixel> rootFrame = frames.FirstOrDefault();

            if (rootFrame == null)
            {
                throw new ArgumentException("Must not be empty.", nameof(frames));
            }

            Size rootSize = rootFrame.Size();

            if (frames.Any(f => f.Size() != rootSize))
            {
                throw new ArgumentException("The provided frames must be of the same size.", nameof(frames));
            }

            return rootSize;
        }
    }
}
