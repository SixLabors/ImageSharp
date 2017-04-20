// <copyright file="ImageFactory.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    public class ImageFactory : GenericFactory<Rgba32>
    {
        public override Image<Rgba32> CreateImage(byte[] bytes) => Image.Load(bytes);

        public override Image<Rgba32> CreateImage(int width, int height) => new Image(width, height);

        public override Image<Rgba32> CreateImage(Image<Rgba32> other)
        {
            Image img = (Image)other;
            return new Image(img);
        }
    }
}
