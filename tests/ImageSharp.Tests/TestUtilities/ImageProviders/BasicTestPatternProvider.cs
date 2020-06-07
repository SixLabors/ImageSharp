// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    public abstract partial class TestImageProvider<TPixel> : IXunitSerializable
    {
        public virtual TPixel GetExpectedBasicTestPatternPixelAt(int x, int y)
        {
            throw new NotSupportedException("GetExpectedBasicTestPatternPixelAt(x,y) only works with BasicTestPattern");
        }

        private class BasicTestPatternProvider : BlankProvider
        {
            private static readonly TPixel TopLeftColor = Color.Red.ToPixel<TPixel>();
            private static readonly TPixel TopRightColor = Color.Green.ToPixel<TPixel>();
            private static readonly TPixel BottomLeftColor = Color.Blue.ToPixel<TPixel>();

            // Transparent purple:
            private static readonly TPixel BottomRightColor = GetBottomRightColor();

            public BasicTestPatternProvider(int width, int height)
                : base(width, height)
            {
            }

            // This parameterless constructor is needed for xUnit deserialization
            public BasicTestPatternProvider()
            {
            }

            public override string SourceFileOrDescription => TestUtils.AsInvariantString($"BasicTestPattern{this.Width}x{this.Height}");

            public override Image<TPixel> GetImage()
            {
                var result = new Image<TPixel>(this.Configuration, this.Width, this.Height);

                int midY = this.Height / 2;
                int midX = this.Width / 2;

                for (int y = 0; y < midY; y++)
                {
                    Span<TPixel> row = result.GetPixelRowSpan(y);

                    row.Slice(0, midX).Fill(TopLeftColor);
                    row.Slice(midX, this.Width - midX).Fill(TopRightColor);
                }

                for (int y = midY; y < this.Height; y++)
                {
                    Span<TPixel> row = result.GetPixelRowSpan(y);

                    row.Slice(0, midX).Fill(BottomLeftColor);
                    row.Slice(midX, this.Width - midX).Fill(BottomRightColor);
                }

                return result;
            }

            public override TPixel GetExpectedBasicTestPatternPixelAt(int x, int y)
            {
                int midY = this.Height / 2;
                int midX = this.Width / 2;

                if (y < midY)
                {
                    return x < midX ? TopLeftColor : TopRightColor;
                }
                else
                {
                    return x < midX ? BottomLeftColor : BottomRightColor;
                }
            }

            private static TPixel GetBottomRightColor()
            {
                TPixel bottomRightColor = default;
                bottomRightColor.FromVector4(new Vector4(1f, 0f, 1f, 0.5f));
                return bottomRightColor;
            }
        }
    }
}
