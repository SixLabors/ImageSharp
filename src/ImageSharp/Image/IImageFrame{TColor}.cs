// <copyright file="IImageFrame{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public interface IImageFrame<TColor> : IImageBase<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
    }
}
