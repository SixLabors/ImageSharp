// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Selects the shared storage-family implementation.
/// </content>
public partial struct NormalizedShort2
{
    /// <summary>
    /// Inherits the shared bulk conversions for this pixel's storage.
    /// </summary>
    internal partial class PixelOperations : ShortPixelOperations<NormalizedShort2>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelOperations"/> class.
        /// </summary>
        public PixelOperations()
            : base(true)
        {
        }
    }
}
