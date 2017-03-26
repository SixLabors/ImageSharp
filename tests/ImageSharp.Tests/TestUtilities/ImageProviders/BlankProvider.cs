// <copyright file="BlankProvider.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using Xunit.Abstractions;

    public abstract partial class TestImageProvider<TColor>
        where TColor : struct, IPixel<TColor>
    {
        private class BlankProvider : TestImageProvider<TColor>
        {
            public BlankProvider(int width, int height)
            {
                this.Width = width;
                this.Height = height;
            }
            public BlankProvider()
            {
                this.Width = 100;
                this.Height = 100;
            }

            public override string SourceFileOrDescription => $"Blank{this.Width}x{this.Height}";

            protected int Height { get; private set; }

            protected int Width { get; private set; }

            public override Image<TColor> GetImage() => this.Factory.CreateImage(this.Width, this.Height);


            public override void Deserialize(IXunitSerializationInfo info)
            {
                this.Width = info.GetValue<int>("width");
                this.Height = info.GetValue<int>("height");
                base.Deserialize(info);
            }

            public override void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue("width", this.Width);
                info.AddValue("height", this.Height);
                base.Serialize(info);
            }
        }
    }
}