// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <content>
    /// Provides optimized overrides for bulk operations.
    /// </content>
    public partial struct Rg32
    {
        /// <summary>
        /// Provides optimized overrides for bulk operations.
        /// </summary>
        internal class PixelOperations : PixelOperations<Rg32>
        {
            private static readonly Lazy<PixelTypeInfo> LazyInfo =
                new Lazy<PixelTypeInfo>(() => PixelTypeInfo.Create<Rg32>(PixelAlphaRepresentation.None), true);

            /// <inheritdoc />
            public override PixelTypeInfo GetPixelTypeInfo() => LazyInfo.Value;
        }
    }
}
