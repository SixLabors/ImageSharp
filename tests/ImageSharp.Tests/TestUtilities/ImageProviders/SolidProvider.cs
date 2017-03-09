// <copyright file="SolidProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    /// <summary>
    /// Provides <see cref="Image{TColor}" /> instances for parametric unit tests.
    /// </summary>
    /// <typeparam name="TColor">The pixel format of the image</typeparam>
    public abstract partial class TestImageProvider<TColor>
        where TColor : struct, IPixel<TColor>
    {
        private class SolidProvider : BlankProvider
        {
            private readonly byte a;

            private readonly byte b;

            private readonly byte g;

            private readonly byte r;

            public SolidProvider(int width, int height, byte r, byte g, byte b, byte a)
                : base(width, height)
            {
                this.r = r;
                this.g = g;
                this.b = b;
                this.a = a;
            }

            public override string SourceFileOrDescription
                => $"Solid{this.Width}x{this.Height}_({this.r},{this.g},{this.b},{this.a})";

            public override Image<TColor> GetImage()
            {
                var image = base.GetImage();
                TColor color = default(TColor);
                color.PackFromBytes(this.r, this.g, this.b, this.a);

                return image.Fill(color);
            }
        }
    }
}