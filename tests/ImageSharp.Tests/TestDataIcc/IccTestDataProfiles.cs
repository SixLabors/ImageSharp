// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests
{
    internal static class IccTestDataProfiles
    {
        public static readonly IccProfileId Header_Random_Id_Value = new IccProfileId(0x84A8D460, 0xC716B6F3, 0x9B0E4C3D, 0xAB95F838);
        public static readonly IccProfileId Profile_Random_Id_Value = new IccProfileId(0x917D6DE6, 0x84C958D1, 0x3BB0F5BB, 0xADD1134F);

        public static readonly byte[] Header_Random_Id_Array =
        {
            0x84, 0xA8, 0xD4, 0x60, 0xC7, 0x16, 0xB6, 0xF3, 0x9B, 0x0E, 0x4C, 0x3D, 0xAB, 0x95, 0xF8, 0x38,
        };

        public static readonly byte[] Profile_Random_Id_Array =
        {
            0x91, 0x7D, 0x6D, 0xE6, 0x84, 0xC9, 0x58, 0xD1, 0x3B, 0xB0, 0xF5, 0xBB, 0xAD, 0xD1, 0x13, 0x4F,
        };

        public static readonly IccProfileHeader Header_Random_Write = CreateHeaderRandomValue(
            562,        // should be overwritten
            new IccProfileId(1, 2, 3, 4),   // should be overwritten
            "ijkl");    // should be overwritten to "acsp"

        public static readonly IccProfileHeader Header_Random_Read = CreateHeaderRandomValue(132, Header_Random_Id_Value, "acsp");

        public static readonly byte[] Header_Random_Array = CreateHeaderRandomArray(132, 0, Header_Random_Id_Array);

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
                Id = id,
                PcsIlluminant = new Vector3(4, 5, 6),
                PrimaryPlatformSignature = IccPrimaryPlatformType.MicrosoftCorporation,
                ProfileConnectionSpace = IccColorSpaceType.CieXyz,
                RenderingIntent = IccRenderingIntent.AbsoluteColorimetric,
                Size = size,
                Version = new IccVersion(4, 3, 0),
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
#pragma warning disable SA1118 // Parameter should not span multiple lines
                 new byte[]
                 {
                    // Padding
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00,

                    // Nr of tag table entries
                    (byte)(nrOfEntries >> 24),
                    (byte)(nrOfEntries >> 16),
                    (byte)(nrOfEntries >> 8),
                    (byte)nrOfEntries
                 });
#pragma warning restore SA1118 // Parameter should not span multiple lines
        }

        public static readonly byte[] Profile_Random_Array = ArrayHelper.Concat(
            CreateHeaderRandomArray(168, 2, Profile_Random_Id_Array),
#pragma warning disable SA1118 // Parameter should not span multiple lines
            new byte[]
            {
                0x00, 0x00, 0x00, 0x00,     // tag signature (Unknown)
                0x00, 0x00, 0x00, 0x9C,     // tag offset (156)
                0x00, 0x00, 0x00, 0x0C,     // tag size (12)
                0x00, 0x00, 0x00, 0x00,     // tag signature (Unknown)
                0x00, 0x00, 0x00, 0x9C,     // tag offset (156)
                0x00, 0x00, 0x00, 0x0C,     // tag size (12)
            },
#pragma warning restore SA1118 // Parameter should not span multiple lines
            IccTestDataTagDataEntry.TagDataEntryHeader_UnknownArr,
            IccTestDataTagDataEntry.Unknown_Arr);

        public static readonly IccProfile Profile_Random_Val = new IccProfile(
            CreateHeaderRandomValue(
                168,
                Profile_Random_Id_Value,
                "acsp"),
            new IccTagDataEntry[] { IccTestDataTagDataEntry.Unknown_Val, IccTestDataTagDataEntry.Unknown_Val });

        public static readonly byte[] Header_CorruptDataColorSpace_Array =
        {
            0x00, 0x00, 0x00, 0x80,     // Size
            0x61, 0x62, 0x63, 0x64,     // CmmType
            0x04, 0x30, 0x00, 0x00,     // Version
            0x6D, 0x6E, 0x74, 0x72,     // Class
            0x68, 0x45, 0x8D, 0x6A,     // DataColorSpace
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

            // Profile ID
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,

            // Padding
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };

        public static readonly byte[] Header_CorruptProfileConnectionSpace_Array =
        {
            0x00, 0x00, 0x00, 0x80,     // Size
            0x62, 0x63, 0x64, 0x65,     // CmmType
            0x04, 0x30, 0x00, 0x00,     // Version
            0x6D, 0x6E, 0x74, 0x72,     // Class
            0x52, 0x47, 0x42, 0x20,     // DataColorSpace
            0x68, 0x45, 0x8D, 0x6A,     // ProfileConnectionSpace
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

            // Profile ID
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,

            // Padding
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };

        public static readonly byte[] Header_CorruptRenderingIntent_Array =
        {
            0x00, 0x00, 0x00, 0x80,     // Size
            0x63, 0x64, 0x65, 0x66,     // CmmType
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
            0x33, 0x41, 0x30, 0x6B,     // RenderingIntent
            0x00, 0x04, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00,  // PcsIlluminant
            0x64, 0x63, 0x62, 0x61,     // CreatorSignature

            // Profile ID
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,

            // Padding
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };

        public static readonly byte[] Header_DataTooSmall_Array = new byte[127];

        public static readonly byte[] Header_InvalidSizeSmall_Array = CreateHeaderRandomArray(127, 0, Header_Random_Id_Array);

        public static readonly byte[] Header_InvalidSizeBig_Array = CreateHeaderRandomArray(50_000_000, 0, Header_Random_Id_Array);

        public static readonly byte[] Header_SizeBiggerThanData_Array = CreateHeaderRandomArray(160, 0, Header_Random_Id_Array);

        public static readonly object[][] ProfileIdTestData =
        {
            new object[] { Header_Random_Array, Header_Random_Id_Value },
            new object[] { Profile_Random_Array, Profile_Random_Id_Value },
        };

        public static readonly object[][] ProfileValidityTestData =
        {
            new object[] { Header_CorruptDataColorSpace_Array, false },
            new object[] { Header_CorruptProfileConnectionSpace_Array, false },
            new object[] { Header_CorruptRenderingIntent_Array, false },
            new object[] { Header_DataTooSmall_Array, false },
            new object[] { Header_InvalidSizeSmall_Array, false },
            new object[] { Header_InvalidSizeBig_Array, false },
            new object[] { Header_SizeBiggerThanData_Array, false },
            new object[] { Header_Random_Array, true },
        };
    }
}
