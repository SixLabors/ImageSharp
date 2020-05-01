// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Defines the contract for objects that can provide access to configuration.
    /// </summary>
    internal interface IConfigurationProvider
    {
        /// <summary>
        /// Gets the configuration which allows altering default behaviour or extending the library.
        /// </summary>
        Configuration Configuration { get; }
    }
}
