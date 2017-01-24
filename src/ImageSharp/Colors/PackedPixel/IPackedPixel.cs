// <copyright file="IPackedPixel.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// An interface that represents a generic packed pixel type.
    /// </summary>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public interface IPackedPixel<TPacked> : IPackedPixel, IPackedVector<TPacked>
        where TPacked : struct, IEquatable<TPacked>
    {
    }

    /// <summary>
    /// An interface that represents a packed pixel type.
    /// </summary>
    public interface IPackedPixel : IPackedVector, IPackedBytes
    {
    }
}