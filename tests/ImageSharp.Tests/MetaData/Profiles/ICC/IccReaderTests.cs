// <copyright file="IccReaderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Icc
{
    using Xunit;

    public class IccReaderTests
    {
        [Fact]
        public void ReadProfile()
        {
            IccReader reader = CreateReader();
            
            IccProfile output = reader.Read(IccTestDataProfiles.Header_Random_Array);

            Assert.Equal(0, output.Entries.Count);
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

        private IccReader CreateReader()
        {
            return new IccReader();
        }
    }
}
