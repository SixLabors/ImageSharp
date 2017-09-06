// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Encapsulates the properties for configuration
    /// </summary>
    internal interface IConfigurable
    {
        /// <summary>
        /// Gets the pixel buffer.
        /// </summary>
        Configuration Configuration { get; }
    }
}