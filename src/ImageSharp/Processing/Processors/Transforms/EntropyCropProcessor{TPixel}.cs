// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Provides methods to allow the cropping of an image to preserve areas of highest entropy.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class EntropyCropProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly EntropyCropProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntropyCropProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="EntropyCropProcessor"/>.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public EntropyCropProcessor(Configuration configuration, EntropyCropProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.definition = definition;
        }

        /// <inheritdoc/>
        protected override void BeforeImageApply()
        {
            Rectangle rectangle;

            // TODO: This is clunky. We should add behavior enum to ExtractFrame.
            // All frames have be the same size so we only need to calculate the correct dimensions for the first frame
            using (var temp = new Image<TPixel>(this.Configuration, this.Source.Metadata.DeepClone(), new[] { this.Source.Frames.RootFrame.Clone() }))
            {
                Configuration configuration = this.Source.GetConfiguration();

                // Detect the edges.
                new EdgeDetector2DProcessor(KnownEdgeDetectorKernels.Sobel, false).Execute(this.Configuration, temp, this.SourceRectangle);

                // Apply threshold binarization filter.
                new BinaryThresholdProcessor(this.definition.Threshold).Execute(this.Configuration, temp, this.SourceRectangle);

                // Search for the first white pixels
                rectangle = GetFilteredBoundingRectangle(temp.Frames.RootFrame, 0);
            }

            new CropProcessor(rectangle, this.Source.Size()).Execute(this.Configuration, this.Source, this.SourceRectangle);

            base.BeforeImageApply();
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            // All processing happens at the image level within BeforeImageApply();
        }

        /// <summary>
        /// Gets the bounding <see cref="Rectangle"/> from the given points.
        /// </summary>
        /// <param name="topLeft">
        /// The <see cref="Point"/> designating the top left position.
        /// </param>
        /// <param name="bottomRight">
        /// The <see cref="Point"/> designating the bottom right position.
        /// </param>
        /// <returns>
        /// The bounding <see cref="Rectangle"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Rectangle GetBoundingRectangle(Point topLeft, Point bottomRight)
            => new Rectangle(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y);

        /// <summary>
        /// Finds the bounding rectangle based on the first instance of any color component other
        /// than the given one.
        /// </summary>
        /// <param name="bitmap">The <see cref="Image{TPixel}"/> to search within.</param>
        /// <param name="componentValue">The color component value to remove.</param>
        /// <param name="channel">The <see cref="RgbaComponent"/> channel to test against.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        private static Rectangle GetFilteredBoundingRectangle(ImageFrame<TPixel> bitmap, float componentValue, RgbaComponent channel = RgbaComponent.B)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            Point topLeft = default;
            Point bottomRight = default;

            Func<ImageFrame<TPixel>, int, int, float, bool> delegateFunc;

            // Determine which channel to check against
            switch (channel)
            {
                case RgbaComponent.R:
                    delegateFunc = (pixels, x, y, b) => MathF.Abs(pixels[x, y].ToVector4().X - b) > Constants.Epsilon;
                    break;

                case RgbaComponent.G:
                    delegateFunc = (pixels, x, y, b) => MathF.Abs(pixels[x, y].ToVector4().Y - b) > Constants.Epsilon;
                    break;

                case RgbaComponent.B:
                    delegateFunc = (pixels, x, y, b) => MathF.Abs(pixels[x, y].ToVector4().Z - b) > Constants.Epsilon;
                    break;

                default:
                    delegateFunc = (pixels, x, y, b) => MathF.Abs(pixels[x, y].ToVector4().W - b) > Constants.Epsilon;
                    break;
            }

            int GetMinY(ImageFrame<TPixel> pixels)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return y;
                        }
                    }
                }

                return 0;
            }

            int GetMaxY(ImageFrame<TPixel> pixels)
            {
                for (int y = height - 1; y > -1; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return y;
                        }
                    }
                }

                return height;
            }

            int GetMinX(ImageFrame<TPixel> pixels)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return x;
                        }
                    }
                }

                return 0;
            }

            int GetMaxX(ImageFrame<TPixel> pixels)
            {
                for (int x = width - 1; x > -1; x--)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return x;
                        }
                    }
                }

                return width;
            }

            topLeft.Y = GetMinY(bitmap);
            topLeft.X = GetMinX(bitmap);
            bottomRight.Y = Numerics.Clamp(GetMaxY(bitmap) + 1, 0, height);
            bottomRight.X = Numerics.Clamp(GetMaxX(bitmap) + 1, 0, width);

            return GetBoundingRectangle(topLeft, bottomRight);
        }
    }
}
