// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// This interface exists for ensuring signature compatibility to MonoGame and XNA packed color types.
    /// <see href="https://msdn.microsoft.com/en-us/library/bb197661.aspx" />
    /// </summary>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public interface IPackedVector<TPacked> : IPixel
        where TPacked : struct, IEquatable<TPacked>
    {
        /// <summary>
        /// Gets or sets the packed representation of the value.
        /// </summary>
        TPacked PackedValue { get; set; }
    }
}
