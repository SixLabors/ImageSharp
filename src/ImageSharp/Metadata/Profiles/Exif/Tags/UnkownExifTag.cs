// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal sealed class UnkownExifTag : ExifTag
    {
        internal UnkownExifTag(ExifTagValue value)
            : base((ushort)value)
        {
        }
    }
}
