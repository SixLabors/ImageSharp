// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// The decoder metadata creator.
/// </summary>
internal static class TiffDecoderMetadataCreator
{
    public static ImageMetadata Create(List<ImageFrameMetadata> frames, bool ignoreMetadata, ByteOrder byteOrder, bool isBigTiff)
    {
        if (frames.Count < 1)
        {
            TiffThrowHelper.ThrowImageFormatException("Expected at least one frame.");
        }

        if (!ignoreMetadata)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                // ICC profile data has already been resolved in the frame metadata,
                // as it is required for color conversion.
                ImageFrameMetadata frameMetaData = frames[i];
                if (TryGetIptc(frameMetaData.ExifProfile.Values, out byte[] iptcBytes))
                {
                    frameMetaData.IptcProfile = new IptcProfile(iptcBytes);
                }

                if (frameMetaData.ExifProfile.TryGetValue(ExifTag.XMP, out IExifValue<byte[]> xmpProfileBytes))
                {
                    frameMetaData.XmpProfile = new XmpProfile(xmpProfileBytes.Value);
                }
            }
        }

        // IPTC and XMP are materialized from IFD tags above, so image-level metadata
        // must be created after all frame profiles are available.
        return Create(byteOrder, isBigTiff, frames[0]);
    }

    private static ImageMetadata Create(ByteOrder byteOrder, bool isBigTiff, ImageFrameMetadata rootFrameMetadata)
    {
        // TIFF stores metadata per IFD, while ImageMetadata is the source used by single-frame encoders.
        // Mirror the root IFD profiles at image scope so cross-format encoding preserves them. Each profile
        // is cloned because image-level and frame-level metadata are independently mutable public APIs.
        ImageMetadata imageMetaData = new()
        {
            ExifProfile = rootFrameMetadata.ExifProfile?.DeepClone(),
            IccProfile = rootFrameMetadata.IccProfile?.DeepClone(),
            IptcProfile = rootFrameMetadata.IptcProfile?.DeepClone(),
            XmpProfile = rootFrameMetadata.XmpProfile?.DeepClone()
        };

        SetResolution(imageMetaData, rootFrameMetadata.ExifProfile);

        TiffMetadata tiffMetadata = imageMetaData.GetTiffMetadata();
        tiffMetadata.ByteOrder = byteOrder;
        tiffMetadata.FormatType = isBigTiff ? TiffFormatType.BigTIFF : TiffFormatType.Default;

        TiffFrameMetadata tiffFrameMetadata = rootFrameMetadata.GetTiffMetadata();
        tiffMetadata.BitsPerPixel = tiffFrameMetadata.BitsPerPixel;
        tiffMetadata.BitsPerSample = tiffFrameMetadata.BitsPerSample;
        tiffMetadata.Compression = tiffFrameMetadata.Compression;
        tiffMetadata.PhotometricInterpretation = tiffFrameMetadata.PhotometricInterpretation;
        tiffMetadata.Predictor = tiffFrameMetadata.Predictor;

        return imageMetaData;
    }

    private static void SetResolution(ImageMetadata imageMetaData, ExifProfile exifProfile)
    {
        imageMetaData.ResolutionUnits = exifProfile != null ? UnitConverter.ExifProfileToResolutionUnit(exifProfile) : PixelResolutionUnit.PixelsPerInch;

        if (exifProfile is null)
        {
            return;
        }

        if (exifProfile.TryGetValue(ExifTag.XResolution, out IExifValue<Rational> horizontalResolution))
        {
            imageMetaData.HorizontalResolution = horizontalResolution.Value.ToDouble();
        }

        if (exifProfile.TryGetValue(ExifTag.YResolution, out IExifValue<Rational> verticalResolution))
        {
            imageMetaData.VerticalResolution = verticalResolution.Value.ToDouble();
        }
    }

    private static bool TryGetIptc(IReadOnlyList<IExifValue> exifValues, out byte[] iptcBytes)
    {
        iptcBytes = null;
        IExifValue iptc = exifValues.FirstOrDefault(f => f.Tag == ExifTag.IPTC);

        if (iptc != null)
        {
            if (iptc.DataType is ExifDataType.Byte or ExifDataType.Undefined)
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

                // Probably wrong endianness, swap byte order.
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
