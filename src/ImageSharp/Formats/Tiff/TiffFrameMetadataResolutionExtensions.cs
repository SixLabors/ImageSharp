// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Experimental.Tiff
{
    internal static class TiffFrameMetadataResolutionExtensions
    {
        public static void SetResolutions(this TiffFrameMetadata meta, PixelResolutionUnit unit, double horizontal, double vertical)
        {
            switch (unit)
            {
                case PixelResolutionUnit.AspectRatio:
                case PixelResolutionUnit.PixelsPerInch:
                case PixelResolutionUnit.PixelsPerCentimeter:
                    break;
                case PixelResolutionUnit.PixelsPerMeter:
                    {
                        unit = PixelResolutionUnit.PixelsPerCentimeter;
                        horizontal = UnitConverter.MeterToCm(horizontal);
                        vertical = UnitConverter.MeterToCm(vertical);
                    }

                    break;
                default:
                    unit = PixelResolutionUnit.PixelsPerInch;
                    break;
            }

            meta.SetSingle(ExifTag.ResolutionUnit, (ushort)unit + 1);
            meta.SetResolution(ExifTag.XResolution, horizontal);
            meta.SetResolution(ExifTag.YResolution, vertical);
        }

        public static PixelResolutionUnit GetResolutionUnit(this TiffFrameMetadata meta)
        {
            if (!meta.TryGetSingle(ExifTag.ResolutionUnit, out ushort res))
            {
                res = TiffFrameMetadata.DefaultResolutionUnit;
            }

            return (PixelResolutionUnit)(res - 1);
        }

        public static double? GetResolution(this TiffFrameMetadata meta, ExifTag tag)
        {
            if (!meta.TryGetSingle(tag, out Rational resolution))
            {
                return null;
            }

            double res = resolution.ToDouble();
            switch (meta.ResolutionUnit)
            {
                case PixelResolutionUnit.AspectRatio:
                    return 0;
                case PixelResolutionUnit.PixelsPerCentimeter:
                    return UnitConverter.CmToInch(res);
                case PixelResolutionUnit.PixelsPerMeter:
                    return UnitConverter.MeterToInch(res);
                case PixelResolutionUnit.PixelsPerInch:
                default:
                    // DefaultResolutionUnit is Inch
                    return res;
            }
        }

        private static void SetResolution(this TiffFrameMetadata meta, ExifTag tag, double? value)
        {
            if (value == null)
            {
                meta.Remove(tag);
                return;
            }
            else
            {
                double res = value.Value;
                switch (meta.ResolutionUnit)
                {
                    case PixelResolutionUnit.AspectRatio:
                        res = 0;
                        break;
                    case PixelResolutionUnit.PixelsPerCentimeter:
                        res = UnitConverter.InchToCm(res);
                        break;
                    case PixelResolutionUnit.PixelsPerMeter:
                        res = UnitConverter.InchToMeter(res);
                        break;
                    case PixelResolutionUnit.PixelsPerInch:
                    default:
                        break;
                }

                meta.SetSingle(tag, new Rational(res));
            }
        }
    }
}
