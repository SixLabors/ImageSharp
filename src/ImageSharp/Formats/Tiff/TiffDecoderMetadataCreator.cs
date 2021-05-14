// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The decoder metadata creator.
    /// </summary>
    internal static class TiffDecoderMetadataCreator
    {
        public static ImageMetadata Create<TPixel>(List<ImageFrame<TPixel>> frames, List<TiffFrameMetadata> framesMetaData, bool ignoreMetadata, ByteOrder byteOrder)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            DebugGuard.IsTrue(frames.Count() == framesMetaData.Count, nameof(frames), "Image frames and frames metdadata should be the same size.");

            if (framesMetaData.Count < 1)
            {
                TiffThrowHelper.ThrowImageFormatException("Expected at least one frame.");
            }

            var imageMetaData = new ImageMetadata();
            TiffFrameMetadata rootFrameMetadata = framesMetaData[0];
            SetResolution(imageMetaData, rootFrameMetadata);

            TiffMetadata tiffMetadata = imageMetaData.GetTiffMetadata();
            tiffMetadata.ByteOrder = byteOrder;
            tiffMetadata.BitsPerPixel = GetBitsPerPixel(rootFrameMetadata);

            if (!ignoreMetadata)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    ImageFrame<TPixel> frame = frames[i];
                    ImageFrameMetadata frameMetaData = frame.Metadata;
                    if (frameMetaData.XmpProfile == null)
                    {
                        IExifValue<byte[]> val = frameMetaData.ExifProfile.GetValue(ExifTag.XMP);
                        if (val != null)
                        {
                            frameMetaData.XmpProfile = val.Value;
                        }
                    }

                    if (imageMetaData.IptcProfile == null)
                    {
                        if (TryGetIptc(frameMetaData.ExifProfile.Values, out byte[] iptcBytes))
                        {
                            imageMetaData.IptcProfile = new IptcProfile(iptcBytes);
                        }
                    }

                    if (imageMetaData.IccProfile == null)
                    {
                        IExifValue<byte[]> val = frameMetaData.ExifProfile.GetValue(ExifTag.IccProfile);
                        if (val != null)
                        {
                            imageMetaData.IccProfile = new IccProfile(val.Value);
                        }
                    }
                }
            }

            return imageMetaData;
        }

        public static ImageMetadata Create(List<TiffFrameMetadata> framesMetaData, ByteOrder byteOrder)
        {
            if (framesMetaData.Count < 1)
            {
                TiffThrowHelper.ThrowImageFormatException("Expected at least one frame.");
            }

            var imageMetaData = new ImageMetadata();
            TiffFrameMetadata rootFrameMetadata = framesMetaData[0];
            SetResolution(imageMetaData, rootFrameMetadata);

            TiffMetadata tiffMetadata = imageMetaData.GetTiffMetadata();
            tiffMetadata.ByteOrder = byteOrder;
            tiffMetadata.BitsPerPixel = GetBitsPerPixel(rootFrameMetadata);

            return imageMetaData;
        }

        private static void SetResolution(ImageMetadata imageMetaData, TiffFrameMetadata rootFrameMetadata)
        {
            imageMetaData.ResolutionUnits = rootFrameMetadata.ResolutionUnit;
            if (rootFrameMetadata.HorizontalResolution != null)
            {
                imageMetaData.HorizontalResolution = rootFrameMetadata.HorizontalResolution.Value;
            }

            if (rootFrameMetadata.VerticalResolution != null)
            {
                imageMetaData.VerticalResolution = rootFrameMetadata.VerticalResolution.Value;
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

                return false;
            }

            return false;
        }

        private static TiffBitsPerPixel GetBitsPerPixel(TiffFrameMetadata firstFrameMetaData)
            => (TiffBitsPerPixel)firstFrameMetaData.BitsPerPixel;
    }
}
