// <copyright file="IPackedPixel.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// An interface that represents a packed pixel type.
    /// </summary>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public interface IPackedPixel<TPacked> : IPackedVector<TPacked>, IPackedBytes
        where TPacked : struct
    {
    }
}
