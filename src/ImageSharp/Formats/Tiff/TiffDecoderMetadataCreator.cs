// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The decoder metadata creator.
    /// </summary>
    internal static class TiffDecoderMetadataCreator
    {
        public static ImageMetadata Create(List<TiffFrameMetadata> frames, bool ignoreMetadata, ByteOrder byteOrder)
        {
            if (frames.Count < 1)
            {
                TiffThrowHelper.ThrowImageFormatException("Expected at least one frame.");
            }

            var coreMetadata = new ImageMetadata();
            TiffFrameMetadata rootFrameMetadata = frames[0];

            coreMetadata.ResolutionUnits = rootFrameMetadata.ResolutionUnit;
            if (rootFrameMetadata.HorizontalResolution != null)
            {
                coreMetadata.HorizontalResolution = rootFrameMetadata.HorizontalResolution.Value;
            }

            if (rootFrameMetadata.VerticalResolution != null)
            {
                coreMetadata.VerticalResolution = rootFrameMetadata.VerticalResolution.Value;
            }

            TiffMetadata tiffMetadata = coreMetadata.GetTiffMetadata();
            tiffMetadata.ByteOrder = byteOrder;
            tiffMetadata.BitsPerPixel = GetBitsPerPixel(rootFrameMetadata);
            tiffMetadata.Compression = rootFrameMetadata.Compression;
            tiffMetadata.PhotometricInterpretation = rootFrameMetadata.PhotometricInterpretation;

            if (!ignoreMetadata)
            {
                foreach (TiffFrameMetadata frame in frames)
                {
                    if (tiffMetadata.XmpProfile == null)
                    {
                        IExifValue<byte[]> val = frame.ExifProfile.GetValue(ExifTag.XMP);
                        if (val != null)
                        {
                            tiffMetadata.XmpProfile = val.Value;
                        }
                    }

                    if (coreMetadata.ExifProfile == null)
                    {
                        coreMetadata.ExifProfile = frame?.ExifProfile.DeepClone();
                    }

                    if (coreMetadata.IptcProfile == null)
                    {
                        if (TryGetIptc(frame.ExifProfile.Values, out byte[] iptcBytes))
                        {
                            coreMetadata.IptcProfile = new IptcProfile(iptcBytes);
                        }
                    }

                    if (coreMetadata.IccProfile == null)
                    {
                        IExifValue<byte[]> val = frame.ExifProfile.GetValue(ExifTag.IccProfile);
                        if (val != null)
                        {
                            coreMetadata.IccProfile = new IccProfile(val.Value);
                        }
                    }
                }
            }

            return coreMetadata;
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
            => (TiffBitsPerPixel)firstFrameMetaData.BitsPerSample.BitsPerPixel();
    }
}
