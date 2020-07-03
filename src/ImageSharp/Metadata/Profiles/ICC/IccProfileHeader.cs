// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Contains all values of an ICC profile header.
    /// </summary>
    public sealed class IccProfileHeader
    {
        /// <summary>
        /// Gets or sets the profile size in bytes (will be ignored when writing a profile).
        /// </summary>
        public uint Size { get; set; }

        /// <summary>
        /// Gets or sets the preferred CMM (Color Management Module) type.
        /// </summary>
        public string CmmType { get; set; }

        /// <summary>
        /// Gets or sets the profiles version number.
        /// </summary>
        public IccVersion Version { get; set; }

        /// <summary>
        /// Gets or sets the type of the profile.
        /// </summary>
        public IccProfileClass Class { get; set; }

        /// <summary>
        /// Gets or sets the data colorspace.
        /// </summary>
        public IccColorSpaceType DataColorSpace { get; set; }

        /// <summary>
        /// Gets or sets the profile connection space.
        /// </summary>
        public IccColorSpaceType ProfileConnectionSpace { get; set; }

        /// <summary>
        /// Gets or sets the date and time this profile was created.
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// Gets or sets the file signature. Should always be "acsp".
        /// Value will be ignored when writing a profile.
        /// </summary>
        public string FileSignature { get; set; }

        /// <summary>
        /// Gets or sets the primary platform this profile as created for
        /// </summary>
        public IccPrimaryPlatformType PrimaryPlatformSignature { get; set; }

        /// <summary>
        /// Gets or sets the profile flags to indicate various options for the CMM
        /// such as distributed processing and caching options.
        /// </summary>
        public IccProfileFlag Flags { get; set; }

        /// <summary>
        /// Gets or sets the device manufacturer of the device for which this profile is created.
        /// </summary>
        public uint DeviceManufacturer { get; set; }

        /// <summary>
        /// Gets or sets the model of the device for which this profile is created.
        /// </summary>
        public uint DeviceModel { get; set; }

        /// <summary>
        /// Gets or sets the device attributes unique to the particular device setup such as media type.
        /// </summary>
        public IccDeviceAttribute DeviceAttributes { get; set; }

        /// <summary>
        /// Gets or sets the rendering Intent.
        /// </summary>
        public IccRenderingIntent RenderingIntent { get; set; }

        /// <summary>
        /// Gets or sets The normalized XYZ values of the illuminant of the PCS.
        /// </summary>
        public Vector3 PcsIlluminant { get; set; }

        /// <summary>
        /// Gets or sets profile creator signature.
        /// </summary>
        public string CreatorSignature { get; set; }

        /// <summary>
        /// Gets or sets the profile ID (hash).
        /// </summary>
        public IccProfileId Id { get; set; }
    }
}
