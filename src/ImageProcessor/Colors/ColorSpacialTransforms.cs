// <copyright file="ColorSpacialTransforms.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System.Numerics;

    public partial struct Color
    {
        public static Color Multiply(Color source, Color destination)
        {
            if (destination == Color.Black)
            {
                return Color.Black;
            }
            if (destination == Color.White)
            {
                return source;
            }
            return
                new Color(
                    new Vector4(
                        source.backingVector.X * destination.backingVector.X,
                        source.backingVector.Y * destination.backingVector.Y,
                        source.backingVector.Z * destination.backingVector.Z,
                        source.backingVector.W * destination.backingVector.W));
        }
    }
}
