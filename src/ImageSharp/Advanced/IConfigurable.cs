// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Encapsulates the properties for configuration.
    /// </summary>
    internal interface IConfigurable
    {
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        Configuration Configuration { get; }
    }
}