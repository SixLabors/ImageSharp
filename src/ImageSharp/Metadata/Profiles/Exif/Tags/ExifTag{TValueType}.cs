// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// Class that represents an exif tag from the Exif standard 2.31 with <typeparamref name="TValueType"/> as the data type of the tag.
    /// </summary>
    /// <typeparam name="TValueType">The data type of the tag.</typeparam>
    public sealed class ExifTag<TValueType> : ExifTag
    {
        internal ExifTag(ExifTagValue value)
            : base((ushort)value)
        {
        }
    }
}
