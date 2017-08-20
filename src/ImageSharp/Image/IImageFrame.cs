// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.MetaData;

namespace SixLabors.ImageSharp
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