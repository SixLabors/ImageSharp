// <copyright file="ImageFactory.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    public class ImageFactory : GenericFactory<Color>
    {
        public override Image<Color> CreateImage(byte[] bytes) => new Image(bytes);

        public override Image<Color> CreateImage(int width, int height) => new Image(width, height);
    }
}
