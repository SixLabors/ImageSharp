// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Extension methods over <see cref="IConfigurable"/>
    /// </summary>
    internal static partial class IConfigurableExtensions
    {
        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <param name="self">The configurable</param>
        /// <returns>Returns the configuration.</returns>
        public static Configuration GetConfiguration(this IConfigurable self)
            => self?.Configuration ?? Configuration.Default;
    }
}