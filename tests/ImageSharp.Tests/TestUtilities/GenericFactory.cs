// <copyright file="GenericFactory.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    /// <summary>
    /// Utility class to create specialized subclasses of generic classes (eg. <see cref="Image"/>)
    /// Used as parameter for <see cref="WithMemberFactoryAttribute"/> -based factory methods
    /// </summary>
    public class GenericFactory<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        public virtual Image<TColor> CreateImage(int width, int height)
        {
            return new Image<TColor>(width, height);
        }

        public virtual Image<TColor> CreateImage(byte[] bytes)
        {
            return new Image<TColor>(bytes);
        }
    }

    public class DefaultImageClassSpecificFactory : GenericFactory<Color>
    {
        public override Image<Color> CreateImage(byte[] bytes) => new Image(bytes);

        public override Image<Color> CreateImage(int width, int height) => new Image(width, height);
    }
}