// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Provides the base methods to perform affine transforms on an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class AffineProcessor<TPixel> : ResamplingWeightedProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        // TODO: Move to constants somewhere else to prevent generic type duplication.
        private static readonly Rectangle DefaultRectangle = new Rectangle(0, 0, 1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="AffineProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sampler">The sampler to perform the resize operation.</param>
        protected AffineProcessor(IResampler sampler)
            : base(sampler, 1, 1, DefaultRectangle) // Hack to prevent Guard throwing in base, we always set the canvas
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether to expand the canvas to fit the skewed image.
        /// </summary>
        public bool Expand { get; set; } = true;

        /// <summary>
        /// Returns the processing matrix used for transforming the image.
        /// </summary>
        /// <returns>The <see cref="Matrix3x2"/></returns>
        protected abstract Matrix3x2 CreateProcessingMatrix();

        /// <summary>
        /// Creates a new target canvas to contain the results of the matrix transform.
        /// </summary>
        /// <param name="sourceRectangle">The source rectangle.</param>
        protected virtual void CreateNewCanvas(Rectangle sourceRectangle)
        {
            if (this.ResizeRectangle == DefaultRectangle)
            {
                if (this.Expand)
                {
                    this.ResizeRectangle = Matrix3x2.Invert(this.CreateProcessingMatrix(), out Matrix3x2 sizeMatrix)
                        ? ImageMaths.GetBoundingRectangle(sourceRectangle, sizeMatrix)
                        : sourceRectangle;
                }
                else
                {
                    this.ResizeRectangle = sourceRectangle;
                }
            }

            this.Width = this.ResizeRectangle.Width;
            this.Height = this.ResizeRectangle.Height;
        }

        /// <inheritdoc/>
        protected override Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            this.CreateNewCanvas(sourceRectangle);

            // We will always be creating the clone even for mutate because we may need to resize the canvas
            IEnumerable<ImageFrame<TPixel>> frames =
                source.Frames.Select(x => new ImageFrame<TPixel>(this.ResizeRectangle.Width, this.ResizeRectangle.Height, x.MetaData.Clone()));

            // Use the overload to prevent an extra frame being added
            return new Image<TPixel>(source.GetConfiguration(), source.MetaData.Clone(), frames);
        }

        /// <summary>
        /// Gets a transform matrix adjusted to center upon the target image bounds.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="matrix">The transform matrix.</param>
        /// <returns>
        /// The <see cref="Matrix3x2"/>.
        /// </returns>
        protected Matrix3x2 GetCenteredMatrix(ImageFrame<TPixel> source, Matrix3x2 matrix)
        {
            var translationToTargetCenter = Matrix3x2.CreateTranslation(-this.ResizeRectangle.Width * .5F, -this.ResizeRectangle.Height * .5F);
            var translateToSourceCenter = Matrix3x2.CreateTranslation(source.Width * .5F, source.Height * .5F);
            return (translationToTargetCenter * matrix) * translateToSourceCenter;
        }

        /// <summary>
        /// Computes the weighted sum at the given XY position
        /// </summary>
        /// <param name="source">The source image</param>
        /// <param name="maxX">The maximum x value</param>
        /// <param name="maxY">The maximum y value</param>
        /// <param name="windowX">The horizontal weights</param>
        /// <param name="windowY">The vertical weights</param>
        /// <param name="point">The transformed position</param>
        /// <returns>The <see cref="Vector4"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector4 ComputeWeightedSumAtPosition(ImageFrame<TPixel> source, int maxX, int maxY, ref WeightsWindow windowX, ref WeightsWindow windowY, ref Point point)
        {
            ref float horizontalValues = ref windowX.GetStartReference();
            ref float verticalValues = ref windowY.GetStartReference();
            int xLeft = windowX.Left;
            int yLeft = windowY.Left;
            int xLength = windowX.Length;
            int yLength = windowY.Length;
            Vector4 result = Vector4.Zero;

            // TODO: Fix this.
            // The output for skew is shrunken, offset, with right/bottom banding.
            // For rotate values are offset
            for (int y = 0; y < yLength; y++)
            {
                float yweight = Unsafe.Add(ref verticalValues, y);
                int offsetY = yLeft + y + point.Y;
                offsetY = offsetY.Clamp(0, maxY);

                for (int x = 0; x < xLength; x++)
                {
                    float xweight = Unsafe.Add(ref horizontalValues, x);
                    int offsetX = xLeft + x + point.X;
                    offsetX = offsetX.Clamp(0, maxX);
                    float weight = yweight * xweight;

                    result += source[offsetX, offsetY].ToVector4() * weight;
                }
            }

            return result;
        }
    }
}