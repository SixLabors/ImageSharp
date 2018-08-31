// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccProfileTests
    {

#if !NETSTANDARD1_1

        [Theory]
        [MemberData(nameof(IccTestDataProfiles.ProfileIdTestData), MemberType = typeof(IccTestDataProfiles))]
        public void CalculateHash_WithByteArray_CalculatesProfileHash(byte[] data, IccProfileId expected)
        {
            IccProfileId result = IccProfile.CalculateHash(data);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void CalculateHash_WithByteArray_DoesNotModifyData()
        {
            byte[] data = IccTestDataProfiles.Profile_Random_Array;
            byte[] copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);

            IccProfileId result = IccProfile.CalculateHash(data);

            Assert.Equal(data, copy);
        }

#endif

        [Theory]
        [MemberData(nameof(IccTestDataProfiles.ProfileValidityTestData), MemberType = typeof(IccTestDataProfiles))]
        public void CheckIsValid_WithProfiles_ReturnsValidity(byte[] data, bool expected)
        {
            var profile = new IccProfile(data);

            bool result = profile.CheckIsValid();

            Assert.Equal(expected, result);
        }
    }
}
