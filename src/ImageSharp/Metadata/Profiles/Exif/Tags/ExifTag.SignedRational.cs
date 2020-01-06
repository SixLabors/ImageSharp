// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <content/>
    public abstract partial class ExifTag
    {
        /// <summary>
        /// Gets the ClipPath exif tag.
        /// </summary>
        public static ExifTag<SignedRational> ShutterSpeedValue { get; } = new ExifTag<SignedRational>(ExifTagValue.ShutterSpeedValue);

        /// <summary>
        /// Gets the ClipPath exif tag.
        /// </summary>
        public static ExifTag<SignedRational> BrightnessValue { get; } = new ExifTag<SignedRational>(ExifTagValue.BrightnessValue);

        /// <summary>
        /// Gets the ClipPath exif tag.
        /// </summary>
        public static ExifTag<SignedRational> ExposureBiasValue { get; } = new ExifTag<SignedRational>(ExifTagValue.ExposureBiasValue);

        /// <summary>
        /// Gets the ClipPath exif tag.
        /// </summary>
        public static ExifTag<SignedRational> AmbientTemperature { get; } = new ExifTag<SignedRational>(ExifTagValue.AmbientTemperature);

        /// <summary>
        /// Gets the ClipPath exif tag.
        /// </summary>
        public static ExifTag<SignedRational> WaterDepth { get; } = new ExifTag<SignedRational>(ExifTagValue.WaterDepth);

        /// <summary>
        /// Gets the ClipPath exif tag.
        /// </summary>
        public static ExifTag<SignedRational> CameraElevationAngle { get; } = new ExifTag<SignedRational>(ExifTagValue.CameraElevationAngle);
    }
}
