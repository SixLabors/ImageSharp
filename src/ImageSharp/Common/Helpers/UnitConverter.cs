// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Common.Helpers
{
    /// <summary>
    /// Contains methods for converting values between unit scales.
    /// </summary>
    internal static class UnitConverter
    {
        /// <summary>
        /// The number of centimeters in a meter.
        /// 1 cm is equal to exactly 0.01 meters.
        /// </summary>
        private const double CmsInMeter = 1 / 0.01D;

        /// <summary>
        /// The number of centimeters in an inch.
        /// 1 inch is equal to exactly 2.54 centimeters.
        /// </summary>
        private const double CmsInInch = 2.54D;

        /// <summary>
        /// The number of inches in a meter.
        /// 1 inch is equal to exactly 0.0254 meters.
        /// </summary>
        private const double InchesInMeter = 1 / 0.0254D;

        /// <summary>
        /// The default resolution unit value.
        /// </summary>
        private const PixelResolutionUnit DefaultResolutionUnit = PixelResolutionUnit.PixelsPerInch;

        /// <summary>
        /// Scales the value from centimeters to meters.
        /// </summary>
        /// <param name="x">The value to scale.</param>
        /// <returns>The <see cref="double"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static double CmToMeter(double x) => x * CmsInMeter;

        /// <summary>
        /// Scales the value from meters to centimeters.
        /// </summary>
        /// <param name="x">The value to scale.</param>
        /// <returns>The <see cref="double"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static double MeterToCm(double x) => x / CmsInMeter;

        /// <summary>
        /// Scales the value from meters to inches.
        /// </summary>
        /// <param name="x">The value to scale.</param>
        /// <returns>The <see cref="double"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static double MeterToInch(double x) => x / InchesInMeter;

        /// <summary>
        /// Scales the value from inches to meters.
        /// </summary>
        /// <param name="x">The value to scale.</param>
        /// <returns>The <see cref="double"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static double InchToMeter(double x) => x * InchesInMeter;

        /// <summary>
        /// Scales the value from centimeters to inches.
        /// </summary>
        /// <param name="x">The value to scale.</param>
        /// <returns>The <see cref="double"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static double CmToInch(double x) => x / CmsInInch;

        /// <summary>
        /// Scales the value from inches to centimeters.
        /// </summary>
        /// <param name="x">The value to scale.</param>
        /// <returns>The <see cref="double"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static double InchToCm(double x) => x * CmsInInch;

        /// <summary>
        /// Converts an <see cref="ExifTag.ResolutionUnit"/> to a <see cref="PixelResolutionUnit"/>.
        /// </summary>
        /// <param name="profile">The EXIF profile containing the value.</param>
        /// <returns>The <see cref="PixelResolutionUnit"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static PixelResolutionUnit ExifProfileToResolutionUnit(ExifProfile profile)
        {
            IExifValue<ushort> resolution = profile.GetValue(ExifTag.ResolutionUnit);

            // EXIF is 1, 2, 3 so we minus "1" off the result.
            return resolution is null ? DefaultResolutionUnit : (PixelResolutionUnit)(byte)(resolution.Value - 1);
        }

        /// <summary>
        /// Sets the exif profile resolution values.
        /// </summary>
        /// <param name="exifProfile">The exif profile.</param>
        /// <param name="unit">The resolution unit.</param>
        /// <param name="horizontal">The horizontal resolution value.</param>
        /// <param name="vertical">The vertical resolution value.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void SetResolutionValues(ExifProfile exifProfile, PixelResolutionUnit unit, double horizontal, double vertical)
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

            exifProfile.SetValue(ExifTag.ResolutionUnit, (ushort)(unit + 1));

            if (unit == PixelResolutionUnit.AspectRatio)
            {
                exifProfile.RemoveValue(ExifTag.XResolution);
                exifProfile.RemoveValue(ExifTag.YResolution);
            }
            else
            {
                exifProfile.SetValue(ExifTag.XResolution, new Rational(horizontal));
                exifProfile.SetValue(ExifTag.YResolution, new Rational(vertical));
            }
        }
    }
}
