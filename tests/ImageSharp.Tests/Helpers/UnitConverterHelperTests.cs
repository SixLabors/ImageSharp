// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Common.Helpers;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class UnitConverterHelperTests
    {
        [Fact]
        public void InchToFromMeter()
        {
            const double expected = 96D;
            double actual = UnitConverter.InchToMeter(expected);
            actual = UnitConverter.MeterToInch(actual);

            Assert.Equal(expected, actual, 15);
        }

        [Fact]
        public void InchToFromCm()
        {
            const double expected = 96D;
            double actual = UnitConverter.InchToCm(expected);
            actual = UnitConverter.CmToInch(actual);

            Assert.Equal(expected, actual, 15);
        }

        [Fact]
        public void CmToFromMeter()
        {
            const double expected = 96D;
            double actual = UnitConverter.CmToMeter(expected);
            actual = UnitConverter.MeterToCm(actual);

            Assert.Equal(expected, actual, 15);
        }
    }
}
