// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccReaderTests
    {
        [Fact]
        public void ReadProfile_NoEntries()
        {
            IccReader reader = this.CreateReader();

            IccProfile output = reader.Read(IccTestDataProfiles.Header_Random_Array);

            Assert.Equal(0, output.Entries.Length);
            Assert.NotNull(output.Header);

            IccProfileHeader header = output.Header;
            IccProfileHeader expected = IccTestDataProfiles.Header_Random_Read;
            Assert.Equal(header.Class, expected.Class);
            Assert.Equal(header.CmmType, expected.CmmType);
            Assert.Equal(header.CreationDate, expected.CreationDate);
            Assert.Equal(header.CreatorSignature, expected.CreatorSignature);
            Assert.Equal(header.DataColorSpace, expected.DataColorSpace);
            Assert.Equal(header.DeviceAttributes, expected.DeviceAttributes);
            Assert.Equal(header.DeviceManufacturer, expected.DeviceManufacturer);
            Assert.Equal(header.DeviceModel, expected.DeviceModel);
            Assert.Equal(header.FileSignature, expected.FileSignature);
            Assert.Equal(header.Flags, expected.Flags);
            Assert.Equal(header.Id, expected.Id);
            Assert.Equal(header.PcsIlluminant, expected.PcsIlluminant);
            Assert.Equal(header.PrimaryPlatformSignature, expected.PrimaryPlatformSignature);
            Assert.Equal(header.ProfileConnectionSpace, expected.ProfileConnectionSpace);
            Assert.Equal(header.RenderingIntent, expected.RenderingIntent);
            Assert.Equal(header.Size, expected.Size);
            Assert.Equal(header.Version, expected.Version);
        }

        [Fact]
        public void ReadProfile_DuplicateEntry()
        {
            IccReader reader = this.CreateReader();

            IccProfile output = reader.Read(IccTestDataProfiles.Profile_Random_Array);

            Assert.Equal(2, output.Entries.Length);
            Assert.True(ReferenceEquals(output.Entries[0], output.Entries[1]));
        }

        private IccReader CreateReader()
        {
            return new IccReader();
        }
    }
}
