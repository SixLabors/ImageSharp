// <copyright file="ImageFactory.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using ImageSharp.PixelFormats;

    public class ImageFactory : GenericFactory<Rgba32>
    {
        public override Image<Rgba32> CreateImage(byte[] bytes) => Image.Load<Rgba32>(bytes);

        public override Image<Rgba32> CreateImage(int width, int height) => new Image<Rgba32>(width, height);

        public override Image<Rgba32> CreateImage(Image<Rgba32> other)
        {
            return new Image<Rgba32>(other);
        }
    }
}
