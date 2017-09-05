// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed partial class Image<TPixel> : IImageFrame<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
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
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        public Image(int width, int height)
            : this(null, width, height)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}"/> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The images metadata.</param>
        internal Image(Configuration configuration, int width, int height, ImageMetaData metadata)
            : this(configuration, width, height, metadata, null)
        {
        }

        /// <summary>
        /// Switches the buffers used by the image and the pixelSource meaning that the Image will "own" the buffer from the pixelSource and the pixelSource will now own the Images buffer.
        /// </summary>
        /// <param name="pixelSource">The pixel source.</param>
        internal void SwapPixelsBuffers(Image<TPixel> pixelSource)
        {
            Guard.NotNull(pixelSource, nameof(pixelSource));

            int newHeight = pixelSource.Height;

            for (int i = 0; i < this.Frames.Count; i++)
            {
                this.Frames[i].SwapPixelsBuffers(pixelSource.Frames[i]);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Image{TPixel}" /> class
        /// with the height and the width of the image.
        /// </summary>
        /// <param name="configuration">The configuration providing initialization code which allows extending the library.</param>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="metadata">The images metadata.</param>
        /// <param name="frames">The frames that will be owned by this image instance.</param>
        internal Image(Configuration configuration, int width, int height, ImageMetaData metadata, IEnumerable<ImageFrame<TPixel>> frames)
        {
            this.ImageConfiguration = configuration ?? Configuration.Default;
            this.MetaData = metadata ?? new ImageMetaData();

            this.Frames = new ImageFrameCollection<TPixel>(this);

            if (frames != null)
            {
                foreach (ImageFrame<TPixel> f in frames)
                {
                    this.Frames.Add(f);
                }
            }

            if (this.Frames.Count == 0)
            {
                this.Frames.Add(new ImageFrame<TPixel>(width, height));
            }
        }

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        internal Configuration ImageConfiguration { get; }

        /// <summary>
        /// Gets the width.
        /// </summary>
        public int Width => this.RootFrame?.Width ?? 0;

        /// <summary>
        /// Gets the height.
        /// </summary>
        public int Height => this.RootFrame?.Height ?? 0;

        /// <summary>
        /// Gets the meta data of the image.
        /// </summary>
        public ImageMetaData MetaData { get; private set; } = new ImageMetaData();

        /// <summary>
        /// Gets the frames.
        /// </summary>
        public ImageFrameCollection<TPixel> Frames { get; private set; }

        /// <summary>
        /// Gets the root frame.
        /// </summary>
        private IImageFrame<TPixel> RootFrame => this.Frames.RootFrame;

        /// <inheritdoc/>
        Buffer2D<TPixel> IImageFrame<TPixel>.PixelBuffer => this.RootFrame.PixelBuffer;

        /// <inheritdoc/>
        ImageFrameMetaData IImageFrame.MetaData => this.RootFrame.MetaData;

        /// <inheritdoc/>
        Image<TPixel> IImageFrame<TPixel>.Parent => this.RootFrame.Parent;

        /// <summary>
        /// Gets or sets the pixel at the specified position.
        /// </summary>
        /// <param name="x">The x-coordinate of the pixel. Must be greater than or equal to zero and less than the width of the image.</param>
        /// <param name="y">The y-coordinate of the pixel. Must be greater than or equal to zero and less than the height of the image.</param>
        /// <returns>The <see typeparam="TPixel"/> at the specified position.</returns>
        public TPixel this[int x, int y]
        {
            get
            {
                return this.RootFrame.PixelBuffer[x, y];
            }

            set
            {
                this.RootFrame.PixelBuffer[x, y] = value;
            }
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
        public Image<TPixel> Clone()
        {
            IEnumerable<ImageFrame<TPixel>> frames = this.Frames.Select(x => x.Clone()).ToArray();

            return new Image<TPixel>(this.ImageConfiguration, this.Width, this.Height, this.MetaData.Clone(), frames);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Image<{typeof(TPixel).Name}>: {this.Width}x{this.Height}";
        }

        /// <summary>
        /// Returns a copy of the image in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel2">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel2}"/></returns>
        public Image<TPixel2> CloneAs<TPixel2>()
            where TPixel2 : struct, IPixel<TPixel2>
        {
            IEnumerable<ImageFrame<TPixel2>> frames = this.Frames.Select(x => x.CloneAs<TPixel2>()).ToArray();
            var target = new Image<TPixel2>(this.ImageConfiguration, this.Width, this.Height, this.MetaData, frames);

            return target;
        }

        /// <summary>
        /// Releases managed resources.
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < this.Frames.Count; i++)
            {
                this.Frames[i].Dispose();
            }
        }
    }
}