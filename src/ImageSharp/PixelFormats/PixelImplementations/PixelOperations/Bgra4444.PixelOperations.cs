// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct Bgra4444
    {
        /// <summary>
        /// Provides optimized overrides for bulk operations.
        /// </summary>
        internal class PixelOperations : PixelOperations<Bgra4444>
        {
            /// <inheritdoc />
            public override PixelTypeInfo GetPixelTypeInfo() => PixelTypeInfo.Create<Bgra4444>(PixelAlphaRepresentation.Unassociated);
        }
    }
}
