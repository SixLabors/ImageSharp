// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct Rgb24
    {
        /// <summary>
        /// Provides optimized overrides for bulk operations.
        /// </summary>
        internal partial class PixelOperations : PixelOperations<Rgb24>
        {
            /// <inheritdoc />
            public override PixelTypeInfo GetPixelTypeInfo()
                => PixelTypeInfo.Create<Rgb24>(PixelAlphaRepresentation.None);
        }
    }
}
