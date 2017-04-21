// <copyright file="RecolorBrush{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System.Numerics;
    using ImageSharp.PixelFormats;
    using Processors;

    /// <summary>
    /// Provides an implementation of a brush that can recolor an image
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public class RecolorBrush<TPixel> : IBrush<TPixel>
    where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecolorBrush{TPixel}" /> class.
        /// </summary>
        /// <param name="sourceColor">Color of the source.</param>
        /// <param name="targeTPixel">Color of the target.</param>
        /// <param name="threshold">The threshold as a value between 0 and 1.</param>
        public RecolorBrush(TPixel sourceColor, TPixel targeTPixel, float threshold)
        {
            this.SourceColor = sourceColor;
            this.Threshold = threshold;
            this.TargeTPixel = targeTPixel;
        }

        /// <summary>
        /// Gets the threshold.
        /// </summary>
        /// <value>
        /// The threshold.
        /// </value>
        public float Threshold { get; }

        /// <summary>
        /// Gets the source color.
        /// </summary>
        /// <value>
        /// The color of the source.
        /// </value>
        public TPixel SourceColor { get; }

        /// <summary>
        /// Gets the target color.
        /// </summary>
        /// <value>
        /// The color of the target.
        /// </value>
        public TPixel TargeTPixel { get; }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator(PixelAccessor<TPixel> sourcePixels, RectangleF region)
        {
            return new RecolorBrushApplicator(sourcePixels, this.SourceColor, this.TargeTPixel, this.Threshold);
        }

        /// <summary>
        /// The recolor brush applicator.
        /// </summary>
        private class RecolorBrushApplicator : BrushApplicator<TPixel>
        {
            /// <summary>
            /// The source color.
            /// </summary>
            private readonly Vector4 sourceColor;

            /// <summary>
            /// The target color.
            /// </summary>
            private readonly Vector4 targeTPixel;

            /// <summary>
            /// The threshold.
            /// </summary>
            private readonly float threshold;

            /// <summary>
            /// Initializes a new instance of the <see cref="RecolorBrushApplicator" /> class.
            /// </summary>
            /// <param name="sourcePixels">The source pixels.</param>
            /// <param name="sourceColor">Color of the source.</param>
            /// <param name="targeTPixel">Color of the target.</param>
            /// <param name="threshold">The threshold .</param>
            public RecolorBrushApplicator(PixelAccessor<TPixel> sourcePixels, TPixel sourceColor, TPixel targeTPixel, float threshold)
                : base(sourcePixels)
            {
                this.sourceColor = sourceColor.ToVector4();
                this.targeTPixel = targeTPixel.ToVector4();

                // Lets hack a min max extreams for a color space by letteing the IPackedPixel clamp our values to something in the correct spaces :)
                TPixel maxColor = default(TPixel);
                maxColor.PackFromVector4(new Vector4(float.MaxValue));
                TPixel minColor = default(TPixel);
                minColor.PackFromVector4(new Vector4(float.MinValue));
                this.threshold = Vector4.DistanceSquared(maxColor.ToVector4(), minColor.ToVector4()) * threshold;
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            /// <returns>
            /// The color
            /// </returns>
            internal override TPixel this[int x, int y]
            {
                get
                {
                    // Offset the requested pixel by the value in the rectangle (the shapes position)
                    TPixel result = this.Target[x, y];
                    Vector4 background = result.ToVector4();
                    float distance = Vector4.DistanceSquared(background, this.sourceColor);
                    if (distance <= this.threshold)
                    {
                        float lerpAmount = (this.threshold - distance) / this.threshold;
                        Vector4 blended = Vector4BlendTransforms.PremultipliedLerp(
                            background,
                            this.targeTPixel,
                            lerpAmount);
                        result.PackFromVector4(blended);
                    }

                    return result;
                }
            }

            /// <inheritdoc />
            public override void Dispose()
            {
            }

            /// <inheritdoc />
            internal override void Apply(float[] scanlineBuffer, int scanlineWidth, int offset, int x, int y)
            {
                Guard.MustBeGreaterThanOrEqualTo(scanlineBuffer.Length, offset + scanlineWidth, nameof(scanlineWidth));

                using (Buffer<float> buffer = new Buffer<float>(scanlineBuffer))
                {
                    BufferSpan<float> slice = buffer.Slice(offset);

                    for (int xPos = 0; xPos < scanlineWidth; xPos++)
                    {
                        int targetX = xPos + x;
                        int targetY = y;

                        float opacity = slice[xPos];
                        if (opacity > Constants.Epsilon)
                        {
                            Vector4 backgroundVector = this.Target[targetX, targetY].ToVector4();

                            Vector4 sourceVector = backgroundVector;
                            float distance = Vector4.DistanceSquared(sourceVector, this.sourceColor);
                            if (distance <= this.threshold)
                            {
                                float lerpAmount = (this.threshold - distance) / this.threshold;
                                sourceVector = Vector4BlendTransforms.PremultipliedLerp(
                                    sourceVector,
                                    this.targeTPixel,
                                    lerpAmount);

                                Vector4 finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, opacity);

                                TPixel packed = default(TPixel);
                                packed.PackFromVector4(finalColor);
                                this.Target[targetX, targetY] = packed;
                            }
                        }
                    }
                }
            }
        }
    }
}