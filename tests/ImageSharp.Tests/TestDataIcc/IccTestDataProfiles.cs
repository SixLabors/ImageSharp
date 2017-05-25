// <copyright file="IccTestDataProfiles.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System;
using System.Numerics;

namespace ImageSharp.Tests
{
    internal static class IccTestDataProfiles
    {
        public static readonly IccProfileHeader Header_Random_Write = new IccProfileHeader
        {
            Class = IccProfileClass.DisplayDevice,
            CmmType = "abcd",
            CreationDate = new DateTime(1990, 11, 26, 7, 21, 42),
            CreatorSignature = "dcba",
            DataColorSpace = IccColorSpaceType.Rgb,
            DeviceAttributes = IccDeviceAttribute.ChromaBlackWhite | IccDeviceAttribute.OpacityTransparent,
            DeviceManufacturer = 123456789u,
            DeviceModel = 987654321u,
            FileSignature = "ijkl",  // should be overwritten to "acsp"
            Flags = IccProfileFlag.Embedded | IccProfileFlag.Independent,
            Id = new IccProfileId(1, 2, 3, 4),   // should be overwritten
            PcsIlluminant = new Vector3(4, 5, 6),
            PrimaryPlatformSignature = IccPrimaryPlatformType.MicrosoftCorporation,
            ProfileConnectionSpace = IccColorSpaceType.CieXyz,
            RenderingIntent = IccRenderingIntent.AbsoluteColorimetric,
            Size = 562,  // should be overwritten
            Version = new Version(4, 3, 0),
        };

        public static readonly IccProfileHeader Header_Random_Read = new IccProfileHeader
        {
            Class = IccProfileClass.DisplayDevice,
            CmmType = "abcd",
            CreationDate = new DateTime(1990, 11, 26, 7, 21, 42),
            CreatorSignature = "dcba",
            DataColorSpace = IccColorSpaceType.Rgb,
            DeviceAttributes = IccDeviceAttribute.ChromaBlackWhite | IccDeviceAttribute.OpacityTransparent,
            DeviceManufacturer = 123456789u,
            DeviceModel = 987654321u,
            FileSignature = "acsp",
            Flags = IccProfileFlag.Embedded | IccProfileFlag.Independent,
#if !NETSTANDARD1_1
            Id = new IccProfileId(2931428592, 418415738, 3086756963, 2237536530),
#else
            Id = IccProfileId.Zero,
#endif
            PcsIlluminant = new Vector3(4, 5, 6),
            PrimaryPlatformSignature = IccPrimaryPlatformType.MicrosoftCorporation,
            ProfileConnectionSpace = IccColorSpaceType.CieXyz,
            RenderingIntent = IccRenderingIntent.AbsoluteColorimetric,
            Size = 132,
            Version = new Version(4, 3, 0),
        };

        public static readonly byte[] Header_Random_Array =
        {
            0x00, 0x00, 0x00, 0x84,     // Size (132)
            0x61, 0x62, 0x63, 0x64,     // CmmType
            0x04, 0x30, 0x00, 0x00,     // Version
            0x6D, 0x6E, 0x74, 0x72,     // Class
            0x52, 0x47, 0x42, 0x20,     // DataColorSpace
            0x58, 0x59, 0x5A, 0x20,     // ProfileConnectionSpace
            0x07, 0xC6, 0x00, 0x0B, 0x00, 0x1A, 0x00, 0x07, 0x00, 0x15, 0x00, 0x2A,       // CreationDate
            0x61, 0x63, 0x73, 0x70,     // FileSignature
            0x4D, 0x53, 0x46, 0x54,     // PrimaryPlatformSignature
            0x00, 0x00, 0x00, 0x01,     // Flags
            0x07, 0x5B, 0xCD, 0x15,     // DeviceManufacturer
            0x3A, 0xDE, 0x68, 0xB1,     // DeviceModel
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09, // DeviceAttributes
            0x00, 0x00, 0x00, 0x03,     // RenderingIntent
            0x00, 0x04, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00,  // PcsIlluminant
            0x64, 0x63, 0x62, 0x61,     // CreatorSignature

#if !NETSTANDARD1_1
            0xAE, 0xBA, 0x0C, 0xF0, 0x18, 0xF0, 0x84, 0x7A, 0xB7, 0xFC, 0x2C, 0x63, 0x85, 0x5E, 0x19, 0x12, // Id
#else
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Id
#endif
            // Padding
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            // Nr of tag table entries (0)
            0x00, 0x00, 0x00, 0x00,
        };
    }
}
