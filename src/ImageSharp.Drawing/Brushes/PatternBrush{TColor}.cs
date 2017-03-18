// <copyright file="PatternBrush{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;
    using System.Numerics;

    using Processors;

    /// <summary>
    /// Provides an implementation of a pattern brush for painting patterns.
    /// </summary>
    /// <remarks>
    /// The patterns that are used to create a custom pattern brush are made up of a repeating matrix of flags,
    /// where each flag denotes whether to draw the foreground color or the background color.
    /// so to create a new bool[,] with your flags
    /// <para>
    /// For example if you wanted to create a diagonal line that repeat every 4 pixels you would use a pattern like so
    /// 1000
    /// 0100
    /// 0010
    /// 0001
    /// </para>
    /// <para>
    /// or you want a horizontal stripe which is 3 pixels apart you would use a pattern like
    ///  1
    ///  0
    ///  0
    /// </para>
    /// </remarks>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class PatternBrush<TColor> : IBrush<TColor>
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// The pattern.
        /// </summary>
        private readonly Fast2DArray<TColor> pattern;
        private readonly Fast2DArray<Vector4> patternVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TColor}"/> class.
        /// </summary>
        /// <param name="foreColor">Color of the fore.</param>
        /// <param name="backColor">Color of the back.</param>
        /// <param name="pattern">The pattern.</param>
        public PatternBrush(TColor foreColor, TColor backColor, bool[,] pattern)
            : this(foreColor, backColor, new Fast2DArray<bool>(pattern))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TColor}"/> class.
        /// </summary>
        /// <param name="foreColor">Color of the fore.</param>
        /// <param name="backColor">Color of the back.</param>
        /// <param name="pattern">The pattern.</param>
        internal PatternBrush(TColor foreColor, TColor backColor, Fast2DArray<bool> pattern)
        {
            Vector4 foreColorVector = foreColor.ToVector4();
            Vector4 backColorVector = backColor.ToVector4();
            this.pattern = new Fast2DArray<TColor>(pattern.Width, pattern.Height);
            this.patternVector = new Fast2DArray<Vector4>(pattern.Width, pattern.Height);
            for (int i = 0; i < pattern.Data.Length; i++)
            {
                if (pattern.Data[i])
                {
                    this.pattern.Data[i] = foreColor;
                    this.patternVector.Data[i] = foreColorVector;
                }
                else
                {
                    this.pattern.Data[i] = backColor;
                    this.patternVector.Data[i] = backColorVector;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TColor}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        internal PatternBrush(PatternBrush<TColor> brush)
        {
            this.pattern = brush.pattern;
            this.patternVector = brush.patternVector;
        }

        /// <inheritdoc />
        public BrushApplicator<TColor> CreateApplicator(PixelAccessor<TColor> sourcePixels, RectangleF region)
        {
            return new PatternBrushApplicator(sourcePixels, this.pattern, this.patternVector);
        }

        /// <summary>
        /// The pattern brush applicator.
        /// </summary>
        private class PatternBrushApplicator : BrushApplicator<TColor>
        {
            /// <summary>
            /// The pattern.
            /// </summary>
            private readonly Fast2DArray<TColor> pattern;
            private readonly Fast2DArray<Vector4> patternVector;

            /// <summary>
            /// Initializes a new instance of the <see cref="PatternBrushApplicator" /> class.
            /// </summary>
            /// <param name="sourcePixels">The sourcePixels.</param>
            /// <param name="pattern">The pattern.</param>
            /// <param name="patternVector">The patternVector.</param>
            public PatternBrushApplicator(PixelAccessor<TColor> sourcePixels, Fast2DArray<TColor> pattern, Fast2DArray<Vector4> patternVector)
                : base(sourcePixels)
            {
                this.pattern = pattern;
                this.patternVector = patternVector;
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>#
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            /// <returns>
            /// The Color.
            /// </returns>
            internal override TColor this[int x, int y]
            {
                get
                {
                    x = x % this.pattern.Width;
                    y = y % this.pattern.Height;

                    // 2d array index at row/column
                    return this.pattern[y, x];
                }
            }

            /// <inheritdoc />
            public override void Dispose()
            {
                // noop
            }

            /// <inheritdoc />
            internal override void Apply(float[] scanlineBuffer, int scanlineWidth, int offset, int x, int y)
            {
                Guard.MustBeGreaterThanOrEqualTo(scanlineBuffer.Length, offset + scanlineWidth, nameof(scanlineWidth));

                using (PinnedBuffer<float> buffer = new PinnedBuffer<float>(scanlineBuffer))
                {
                    BufferPointer<float> slice = buffer.Slice(offset);

                    for (int xPos = 0; xPos < scanlineWidth; xPos++)
                    {
                        int targetX = xPos + x;
                        int targetY = y;

                        float opacity = slice[xPos];
                        if (opacity > Constants.Epsilon)
                        {
                            Vector4 backgroundVector = this.Target[targetX, targetY].ToVector4();

                            // 2d array index at row/column
                            Vector4 sourceVector = this.patternVector[targetY % this.patternVector.Height, targetX % this.patternVector.Width];

                            Vector4 finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, opacity);

                            TColor packed = default(TColor);
                            packed.PackFromVector4(finalColor);
                            this.Target[targetX, targetY] = packed;
                        }
                    }
                }
            }
        }
    }
}