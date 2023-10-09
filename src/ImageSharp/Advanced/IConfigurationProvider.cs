// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Advanced;

/// <summary>
/// Defines the contract for objects that can provide access to configuration.
/// </summary>
public interface IConfigurationProvider
{
    /// <summary>
    /// Gets the configuration which allows altering default behaviour or extending the library.
    /// </summary>
    Configuration Configuration { get; }
}
