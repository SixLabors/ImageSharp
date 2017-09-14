// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests
{
    internal static class IccTestDataProfiles
    {
        public static readonly IccProfileHeader Header_Random_Write = CreateHeaderRandomValue(
            562,        // should be overwritten
            new IccProfileId(1, 2, 3, 4),   // should be overwritten
            "ijkl");    // should be overwritten to "acsp"

        public static readonly IccProfileHeader Header_Random_Read = CreateHeaderRandomValue(132,
#if !NETSTANDARD1_1
            new IccProfileId(2931428592, 418415738, 3086756963, 2237536530),
#else
            IccProfileId.Zero,
#endif
            "acsp");

        public static readonly byte[] Header_Random_Array = CreateHeaderRandomArray(132, 0, new byte[]
        {
#if !NETSTANDARD1_1
                0xAE, 0xBA, 0x0C, 0xF0, 0x18, 0xF0, 0x84, 0x7A, 0xB7, 0xFC, 0x2C, 0x63, 0x85, 0x5E, 0x19, 0x12,
#else
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
#endif
        });

        public static IccProfileHeader CreateHeaderRandomValue(uint size, IccProfileId id, string fileSignature)
        {
            return new IccProfileHeader
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
                Size = size,
                Version = new Version(4, 3, 0),
            };
        }

        public static byte[] CreateHeaderRandomArray(uint size, uint nrOfEntries, byte[] profileId)
        {
            return ArrayHelper.Concat(
                 new byte[]
                 {
                    (byte)(size >> 24), (byte)(size >> 16), (byte)(size >> 8), (byte)size,     // Size
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
                 },
                 profileId,
                 new byte[]
                 { 
                    // Padding
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00,
                    // Nr of tag table entries (0)
                    (byte)(nrOfEntries >> 24), (byte)(nrOfEntries >> 16), (byte)(nrOfEntries >> 8), (byte)nrOfEntries
                 });
        }

        public static byte[] Profile_Random_Array = ArrayHelper.Concat(CreateHeaderRandomArray(168, 2, new byte[] 
            {
#if !NETSTANDARD1_1
                0xA9, 0x71, 0x8F, 0xC1, 0x1E, 0x2D, 0x64, 0x1B, 0x10, 0xF4, 0x7D, 0x6A, 0x5B, 0xF6, 0xAC, 0xB9
#else
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
#endif
            }),
            new byte[]
            {
                0x00, 0x00, 0x00, 0x00,     // tag signature (Unknown)
                0x00, 0x00, 0x00, 0x9C,     // tag offset (156)
                0x00, 0x00, 0x00, 0x0C,     // tag size (12)
                
                0x00, 0x00, 0x00, 0x00,     // tag signature (Unknown)
                0x00, 0x00, 0x00, 0x9C,     // tag offset (156)
                0x00, 0x00, 0x00, 0x0C,     // tag size (12)
            },
            IccTestDataTagDataEntry.TagDataEntryHeader_UnknownArr,
            IccTestDataTagDataEntry.Unknown_Arr
        );

        public static IccProfile Profile_Random_Val = new IccProfile(CreateHeaderRandomValue(168,
#if !NETSTANDARD1_1
            new IccProfileId(0xA9718FC1, 0x1E2D641B, 0x10F47D6A, 0x5BF6ACB9),
#else
            IccProfileId.Zero,
#endif
            "acsp"),
            new IccTagDataEntry[]
            {
                IccTestDataTagDataEntry.Unknown_Val,
                IccTestDataTagDataEntry.Unknown_Val
            });
    }
}
