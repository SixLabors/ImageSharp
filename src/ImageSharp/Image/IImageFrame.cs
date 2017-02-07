// <copyright file="IImageFrame.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    /// <summary>
    /// Encapsulates the basic properties and methods required to manipulate images.
    /// </summary>
    internal interface IImageFrame : IImageBase
    {
        /// <summary>
        /// Gets the meta data of the image.
        /// </summary>
        ImageFrameMetaData MetaData { get; }
    }
}