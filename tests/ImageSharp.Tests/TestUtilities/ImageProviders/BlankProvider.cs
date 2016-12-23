// <copyright file="BlankProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    public abstract partial class TestImageProvider<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        private class BlankProvider : TestImageProvider<TColor>
        {
            public BlankProvider(int width, int height)
            {
                this.Width = width;
                this.Height = height;
            }

            public override string SourceFileOrDescription => $"Blank{this.Width}x{this.Height}";

            protected int Height { get; }

            protected int Width { get; }

            public override Image<TColor> GetImage() => this.Factory.CreateImage(this.Width, this.Height);
        }
    }
}