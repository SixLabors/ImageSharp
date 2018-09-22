// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class Image<TPixel> : IImage, IConfigurable
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly Configuration configuration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public Image(Configuration configuration, int width, int height)
            : this(configuration, width, height, new ImageMetaData())
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
            : this(configuration, width, height, backgroundColor, new ImageMetaData())
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
        internal Image(Configuration configuration, int width, int height, ImageMetaData metadata)
        {
            this.configuration = configuration ?? Configuration.Default;
            this.PixelType = new PixelTypeInfo(Unsafe.SizeOf<TPixel>() * 8);
            this.MetaData = metadata ?? new ImageMetaData();
            this.Frames = new ImageFrameCollection<TPixel>(this, width, height, default(TPixel));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// wrapping an external <see cref="MemorySource{T}"/>
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="memorySource">The memory source.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The images metadata.</param>
        internal Image(Configuration configuration, MemorySource<TPixel> memorySource, int width, int height, ImageMetaData metadata)
        {
            this.configuration = configuration;
            this.PixelType = new PixelTypeInfo(Unsafe.SizeOf<TPixel>() * 8);
            this.MetaData = metadata;
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
        internal Image(Configuration configuration, int width, int height, TPixel backgroundColor, ImageMetaData metadata)
        {
            this.configuration = configuration ?? Configuration.Default;
            this.PixelType = new PixelTypeInfo(Unsafe.SizeOf<TPixel>() * 8);
            this.MetaData = metadata ?? new ImageMetaData();
            this.Frames = new ImageFrameCollection<TPixel>(this, width, height, backgroundColor);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}" /> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="metadata">The images metadata.</param>
        /// <param name="frames">The frames that will be owned by this image instance.</param>
        internal Image(Configuration configuration, ImageMetaData metadata, IEnumerable<ImageFrame<TPixel>> frames)
        {
            this.configuration = configuration ?? Configuration.Default;
            this.PixelType = new PixelTypeInfo(Unsafe.SizeOf<TPixel>() * 8);
            this.MetaData = metadata ?? new ImageMetaData();

            this.Frames = new ImageFrameCollection<TPixel>(this, frames);
        }

        /// <summary>
        /// Gets the pixel buffer.
        /// </summary>
        Configuration IConfigurable.Configuration => this.configuration;

        /// <inheritdoc/>
        public PixelTypeInfo PixelType { get; }

        /// <inheritdoc/>
        public int Width => this.Frames.RootFrame.Width;

        /// <inheritdoc/>
        public int Height => this.Frames.RootFrame.Height;

        /// <inheritdoc/>
        public ImageMetaData MetaData { get; }

        /// <summary>
        /// Gets the frames.
        /// </summary>
        public ImageFrameCollection<TPixel> Frames { get; }

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
        /// Saves the image to the given stream using the given image encoder.
        /// </summary>
        /// <param name="stream">The stream to save the image to.</param>
        /// <param name="encoder">The encoder to save the image with.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if the stream or encoder is null.</exception>
        public void Save(Stream stream, IImageEncoder encoder)
        {
            Guard.NotNull(stream, nameof(stream));
            Guard.NotNull(encoder, nameof(encoder));

            encoder.Encode(this, stream);
        }

        /// <summary>
        /// Clones the current image
        /// </summary>
        /// <returns>Returns a new image with all the same metadata as the original.</returns>
        public Image<TPixel> Clone() => this.Clone(this.configuration);

        /// <summary>
        /// Clones the current image with the given configueation.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <returns>Returns a new <see cref="Image{TPixel}"/> with all the same pixel data as the original.</returns>
        public Image<TPixel> Clone(Configuration configuration)
        {
            IEnumerable<ImageFrame<TPixel>> clonedFrames = this.Frames.Select(x => x.Clone(configuration));
            return new Image<TPixel>(configuration, this.MetaData.DeepClone(), clonedFrames);
        }

        /// <summary>
        /// Returns a copy of the image in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel2}"/></returns>
        public Image<TPixel2> CloneAs<TPixel2>()
            where TPixel2 : struct, IPixel<TPixel2> => this.CloneAs<TPixel2>(this.configuration);

        /// <summary>
        /// Returns a copy of the image in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <returns>The <see cref="Image{TPixel2}"/></returns>
        public Image<TPixel2> CloneAs<TPixel2>(Configuration configuration)
            where TPixel2 : struct, IPixel<TPixel2>
        {
            IEnumerable<ImageFrame<TPixel2>> clonedFrames = this.Frames.Select(x => x.CloneAs<TPixel2>(configuration));
            return new Image<TPixel2>(configuration, this.MetaData.DeepClone(), clonedFrames);
        }

        /// <inheritdoc/>
        public void Dispose() => this.Frames.Dispose();

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
        }
    }
}