// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccWriterTests
    {
        [Fact]
        public void WriteProfile_NoEntries()
        {
            IccWriter writer = this.CreateWriter();

            var profile = new IccProfile
            {
                Header = IccTestDataProfiles.Header_Random_Write
            };
            byte[] output = writer.Write(profile);

            Assert.Equal(IccTestDataProfiles.Header_Random_Array, output);
        }

        [Fact]
        public void WriteProfile_DuplicateEntry()
        {
            IccWriter writer = this.CreateWriter();

            byte[] output = writer.Write(IccTestDataProfiles.Profile_Random_Val);

            Assert.Equal(IccTestDataProfiles.Profile_Random_Array, output);
        }

        private IccWriter CreateWriter()
        {
            return new IccWriter();
        }
    }
}
