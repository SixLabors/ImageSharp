// <copyright file="IImageFrame.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public interface IImageFrame<T, TP> : IImageBase<T, TP>
        where T : IPackedVector<TP>, new()
        where TP : struct
    {
    }
}
