// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal static class ExifDataTypes
    {
        /// <summary>
        /// Gets the size in bytes of the given data type.
        /// </summary>
        /// <param name="dataType">The data type.</param>
        /// <returns>
        /// The <see cref="uint"/>.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Thrown if the type is unsupported.
        /// </exception>
        public static uint GetSize(ExifDataType dataType)
        {
            switch (dataType)
            {
                case ExifDataType.Ascii:
                case ExifDataType.Byte:
                case ExifDataType.SignedByte:
                case ExifDataType.Undefined:
                    return 1;
                case ExifDataType.Short:
                case ExifDataType.SignedShort:
                    return 2;
                case ExifDataType.Long:
                case ExifDataType.SignedLong:
                case ExifDataType.SingleFloat:
                case ExifDataType.Ifd:
                    return 4;
                case ExifDataType.DoubleFloat:
                case ExifDataType.Rational:
                case ExifDataType.SignedRational:
                case ExifDataType.Long8:
                case ExifDataType.SignedLong8:
                case ExifDataType.Ifd8:
                    return 8;
                default:
                    throw new NotSupportedException(dataType.ToString());
            }
        }
    }
}
