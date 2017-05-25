// <copyright file="IccWriterTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Icc
{
    using Xunit;

    public class IccWriterTests
    {
        [Fact]
        public void WriteProfile()
        {
            IccWriter writer = CreateWriter();

            IccProfile profile = new IccProfile()
            {
                Header = IccTestDataProfiles.Header_Random_Write
            };
            byte[] output = writer.Write(profile);
            
            Assert.Equal(IccTestDataProfiles.Header_Random_Array, output);
        }

        private IccWriter CreateWriter()
        {
            return new IccWriter();
        }
    }
}
