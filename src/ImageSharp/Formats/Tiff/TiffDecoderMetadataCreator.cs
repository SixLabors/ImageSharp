// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The decoder metadata creator.
    /// </summary>
    internal static class TiffDecoderMetadataCreator
    {
        public static ImageMetadata Create<TPixel>(List<ImageFrame<TPixel>> frames, bool ignoreMetadata, ByteOrder byteOrder, bool isBigTiff)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (frames.Count < 1)
            {
                TiffThrowHelper.ThrowImageFormatException("Expected at least one frame.");
            }

            ImageMetadata imageMetaData = Create(byteOrder, isBigTiff, frames[0].Metadata.ExifProfile);

            if (!ignoreMetadata)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    ImageFrame<TPixel> frame = frames[i];
                    ImageFrameMetadata frameMetaData = frame.Metadata;
                    if (TryGetIptc(frameMetaData.ExifProfile.Values, out byte[] iptcBytes))
                    {
                        frameMetaData.IptcProfile = new IptcProfile(iptcBytes);
                    }

                    IExifValue<byte[]> xmpProfileBytes = frameMetaData.ExifProfile.GetValue(ExifTag.XMP);
                    if (xmpProfileBytes != null)
                    {
                        frameMetaData.XmpProfile = new XmpProfile(xmpProfileBytes.Value);
                    }

                    IExifValue<byte[]> iccProfileBytes = frameMetaData.ExifProfile.GetValue(ExifTag.IccProfile);
                    if (iccProfileBytes != null)
                    {
                        frameMetaData.IccProfile = new IccProfile(iccProfileBytes.Value);
                    }
                }
            }

            return imageMetaData;
        }

        public static ImageMetadata Create(ByteOrder byteOrder, bool isBigTiff, ExifProfile exifProfile)
        {
            var imageMetaData = new ImageMetadata();
            SetResolution(imageMetaData, exifProfile);

            TiffMetadata tiffMetadata = imageMetaData.GetTiffMetadata();
            tiffMetadata.ByteOrder = byteOrder;
            tiffMetadata.FormatType = isBigTiff ? TiffFormatType.BigTIFF : TiffFormatType.Default;

            return imageMetaData;
        }

        private static void SetResolution(ImageMetadata imageMetaData, ExifProfile exifProfile)
        {
            imageMetaData.ResolutionUnits = exifProfile != null ? UnitConverter.ExifProfileToResolutionUnit(exifProfile) : PixelResolutionUnit.PixelsPerInch;
            double? horizontalResolution = exifProfile?.GetValue(ExifTag.XResolution)?.Value.ToDouble();
            if (horizontalResolution != null)
            {
                imageMetaData.HorizontalResolution = horizontalResolution.Value;
            }

            double? verticalResolution = exifProfile?.GetValue(ExifTag.YResolution)?.Value.ToDouble();
            if (verticalResolution != null)
            {
                imageMetaData.VerticalResolution = verticalResolution.Value;
            }
        }

        private static bool TryGetIptc(IReadOnlyList<IExifValue> exifValues, out byte[] iptcBytes)
        {
            iptcBytes = null;
            IExifValue iptc = exifValues.FirstOrDefault(f => f.Tag == ExifTag.IPTC);

            if (iptc != null)
            {
                if (iptc.DataType == ExifDataType.Byte || iptc.DataType == ExifDataType.Undefined)
                {
                    iptcBytes = (byte[])iptc.GetValue();
                    return true;
                }

                // Some Encoders write the data type of IPTC as long.
                if (iptc.DataType == ExifDataType.Long)
                {
                    uint[] iptcValues = (uint[])iptc.GetValue();
                    iptcBytes = new byte[iptcValues.Length * 4];
                    Buffer.BlockCopy(iptcValues, 0, iptcBytes, 0, iptcValues.Length * 4);
                    if (iptcBytes[0] == 0x1c)
                    {
                        return true;
                    }
                    else if (iptcBytes[3] != 0x1c)
                    {
                        return false;
                    }

                    // Probably wrong endianess, swap byte order.
                    Span<byte> iptcBytesSpan = iptcBytes.AsSpan();
                    Span<byte> buffer = stackalloc byte[4];
                    for (int i = 0; i < iptcBytes.Length; i += 4)
                    {
                        iptcBytesSpan.Slice(i, 4).CopyTo(buffer);
                        iptcBytes[i] = buffer[3];
                        iptcBytes[i + 1] = buffer[2];
                        iptcBytes[i + 2] = buffer[1];
                        iptcBytes[i + 3] = buffer[0];
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
