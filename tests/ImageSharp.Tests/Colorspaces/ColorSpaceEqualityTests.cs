// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Test implementations of IEquatable and IAlmostEquatable in our colorspaces
    /// </summary>
    public class ColorSpaceEqualityTests
    {
        internal static readonly Dictionary<string, IColorVector> EmptyDataLookup =
            new Dictionary<string, IColorVector>
                {
                    {nameof( CieLab), default(CieLab) },
                    {nameof( CieLch), default(CieLch) },
                    {nameof( CieLchuv), default(CieLchuv) },
                    {nameof( CieLuv), default(CieLuv) },
                    {nameof( CieXyz), default(CieXyz) },
                    {nameof( CieXyy), default(CieXyy) },
                    {nameof( Hsl), default(Hsl) },
                    {nameof( HunterLab), default(HunterLab) },
                    {nameof( Lms), default(Lms) },
                    {nameof( LinearRgb), default(LinearRgb) },
                    {nameof( Rgb), default(Rgb) },
                    {nameof( YCbCr), default(YCbCr) }
                };

        public static readonly IEnumerable<object[]> EmptyData = EmptyDataLookup.Select(x => new [] { x.Key });

        public static readonly TheoryData<object, object, Type> EqualityData =
           new TheoryData<object, object, Type>
               {
               { new CieLab(Vector3.One), new CieLab(Vector3.One), typeof(CieLab) },
               { new CieLch(Vector3.One), new CieLch(Vector3.One), typeof(CieLch) },
               { new CieLchuv(Vector3.One), new CieLchuv(Vector3.One), typeof(CieLchuv) },
               { new CieLuv(Vector3.One), new CieLuv(Vector3.One), typeof(CieLuv) },
               { new CieXyz(Vector3.One), new CieXyz(Vector3.One), typeof(CieXyz) },
               { new CieXyy(Vector3.One), new CieXyy(Vector3.One), typeof(CieXyy) },
               { new HunterLab(Vector3.One), new HunterLab(Vector3.One), typeof(HunterLab) },
               { new Lms(Vector3.One), new Lms(Vector3.One), typeof(Lms) },
               { new LinearRgb(Vector3.One), new LinearRgb(Vector3.One), typeof(LinearRgb) },
               { new Rgb(Vector3.One), new Rgb(Vector3.One), typeof(Rgb) },
               { new Hsl(Vector3.One), new Hsl(Vector3.One), typeof(Hsl) },
               { new Hsv(Vector3.One), new Hsv(Vector3.One), typeof(Hsv) },
               { new YCbCr(Vector3.One), new YCbCr(Vector3.One), typeof(YCbCr) },
           };

        public static readonly TheoryData<object, object, Type> NotEqualityDataNulls =
            new TheoryData<object, object, Type>
                {
                // Valid object against null
               { new CieLab(Vector3.One), null, typeof(CieLab) },
               { new CieLch(Vector3.One), null, typeof(CieLch) },
               { new CieLchuv(Vector3.One), null, typeof(CieLchuv) },
               { new CieLuv(Vector3.One), null, typeof(CieLuv) },
               { new CieXyz(Vector3.One), null, typeof(CieXyz) },
               { new CieXyy(Vector3.One), null, typeof(CieXyy) },
               { new HunterLab(Vector3.One), null, typeof(HunterLab) },
               { new Lms(Vector3.One), null, typeof(Lms) },
               { new LinearRgb(Vector3.One), null, typeof(LinearRgb) },
               { new Rgb(Vector3.One), null, typeof(Rgb) },
               { new Hsl(Vector3.One), null, typeof(Hsl) },
               { new Hsv(Vector3.One), null, typeof(Hsv) },
               { new YCbCr(Vector3.One), null, typeof(YCbCr) },
            };

        public static readonly TheoryData<object, object, Type> NotEqualityDataDifferentObjects =
           new TheoryData<object, object, Type>
               {
                // Valid objects of different types but not equal
                { new CieLab(Vector3.One), new CieLch(Vector3.Zero), null },
                { new CieLuv(Vector3.One), new CieLchuv(Vector3.Zero), null },
                { new CieXyz(Vector3.One), new HunterLab(Vector3.Zero), null },
                { new Rgb(Vector3.One), new LinearRgb(Vector3.Zero), null },
                { new Rgb(Vector3.One), new Lms(Vector3.Zero), null },
                { new Cmyk(Vector4.One), new Hsl(Vector3.Zero), null },
                { new YCbCr(Vector3.One), new CieXyy(Vector3.Zero), null },
                { new Hsv(Vector3.One), new Hsl(Vector3.Zero), null },
           };

        public static readonly TheoryData<object, object, Type> NotEqualityData =
           new TheoryData<object, object, Type>
               {
                // Valid objects of the same type but not equal
               { new CieLab(Vector3.One), new CieLab(Vector3.Zero), typeof(CieLab) },
               { new CieLch(Vector3.One), new CieLch(Vector3.Zero), typeof(CieLch) },
               { new CieLchuv(Vector3.One), new CieLchuv(Vector3.Zero), typeof(CieLchuv) },
               { new CieLuv(Vector3.One), new CieLuv(Vector3.Zero), typeof(CieLuv) },
               { new CieXyz(Vector3.One), new CieXyz(Vector3.Zero), typeof(CieXyz) },
               { new CieXyy(Vector3.One), new CieXyy(Vector3.Zero), typeof(CieXyy) },
               { new HunterLab(Vector3.One), new HunterLab(Vector3.Zero), typeof(HunterLab) },
               { new Lms(Vector3.One), new Lms(Vector3.Zero), typeof(Lms) },
               { new LinearRgb(Vector3.One), new LinearRgb(Vector3.Zero), typeof(LinearRgb) },
               { new Rgb(Vector3.One), new Rgb(Vector3.Zero), typeof(Rgb) },
               { new Cmyk(Vector4.One), new Cmyk(Vector4.Zero), typeof(Cmyk) },
               { new Hsl(Vector3.One), new Hsl(Vector3.Zero), typeof(Hsl) },
               { new Hsv(Vector3.One), new Hsv(Vector3.Zero), typeof(Hsv) },
               { new YCbCr(Vector3.One), new YCbCr(Vector3.Zero), typeof(YCbCr) },
           };

        public static readonly TheoryData<object, object, Type, float> AlmostEqualsData =
            new TheoryData<object, object, Type, float>
                {
                { new CieLab(0F, 0F, 0F), new CieLab(0F, 0F, 0F), typeof(CieLab), 0F },
                { new CieLab(0F, 0F, 0F), new CieLab(0F, 0F, 0F), typeof(CieLab), .001F },
                { new CieLab(0F, 0F, 0F), new CieLab(0F, 0F, 0F), typeof(CieLab), .0001F },
                { new CieLab(0F, 0F, 0F), new CieLab(0F, 0F, 0F), typeof(CieLab), .0005F },
                { new CieLab(0F, 0F, 0F), new CieLab(0F, .001F, 0F), typeof(CieLab), .001F },
                { new CieLab(0F, 0F, 0F), new CieLab(0F, 0F, .0001F), typeof(CieLab), .0001F },
                { new CieLab(0F, 0F, 0F), new CieLab(.0005F, 0F, 0F), typeof(CieLab), .0005F },
                { new CieLch(0F, 0F, 0F), new CieLch(0F, .001F, 0F), typeof(CieLch), .001F },
                { new CieLchuv(0F, 0F, 0F), new CieLchuv(0F, .001F, 0F), typeof(CieLchuv), .001F },
                { new CieLuv(0F, 0F, 0F), new CieLuv(0F, .001F, 0F), typeof(CieLuv), .001F },
                { new CieXyz(380F, 380F, 380F), new CieXyz(380F, 380F, 380F), typeof(CieXyz), 0F },
                { new CieXyz(380F, 380F, 380F), new CieXyz(380.001F, 380F, 380F), typeof(CieXyz), .01F },
                { new CieXyz(380F, 380F, 380F), new CieXyz(380F, 380.001F, 380F), typeof(CieXyz), .01F },
                { new CieXyz(380F, 380F, 380F), new CieXyz(380F, 380F, 380.001F), typeof(CieXyz), .01F },
                { new Cmyk(1, 1, 1, 1), new Cmyk(1, 1, 1, .99F), typeof(Cmyk), .01F },
                { new YCbCr(255F, 128F, 128F), new YCbCr(255F, 128F, 128.001F), typeof(YCbCr), .01F },
                { new Hsv(0F, 0F, 0F), new Hsv(0F, 0F, 0F), typeof(Hsv), 0F },
                { new Hsl(0F, 0F, 0F), new Hsl(0F, 0F, 0F), typeof(Hsl), 0F },
            };

        public static readonly TheoryData<object, object, Type, float> AlmostNotEqualsData =
            new TheoryData<object, object, Type, float>
                {
                { new CieLab(0F, 0F, 0F), new CieLab(0.1F, 0F, 0F), typeof(CieLab), .001F },
                { new CieLab(0F, 0F, 0F), new CieLab(0F, 0.1F, 0F), typeof(CieLab), .001F },
                { new CieLab(0F, 0F, 0F), new CieLab(0F, 0F, 0.1F), typeof(CieLab), .001F },
                { new CieXyz(380F, 380F, 380F), new CieXyz(380.1F, 380F, 380F), typeof(CieXyz), .001F },
                { new CieXyz(380F, 380F, 380F), new CieXyz(380F, 380.1F, 380F), typeof(CieXyz), .001F },
                { new CieXyz(380F, 380F, 380F), new CieXyz(380F, 380F, 380.1F), typeof(CieXyz), .001F },
            };

        [Theory]
        [MemberData(nameof(EmptyData))]
        public void Vector_Equals_WhenTrue(string color)
        {
            IColorVector colorVector = EmptyDataLookup[color];
            // Act
            bool equal = colorVector.Vector.Equals(Vector3.Zero);

            // Assert
            Assert.True(equal);
        }

        [Theory]
        [MemberData(nameof(EqualityData))]
        public void Equals_WhenTrue(object first, object second, Type type)
        {
            // Act
            bool equal = first.Equals(second);

            // Assert
            Assert.True(equal);
        }

        [Theory]
        [MemberData(nameof(NotEqualityDataNulls))]
        [MemberData(nameof(NotEqualityDataDifferentObjects))]
        [MemberData(nameof(NotEqualityData))]
        public void Equals_WhenFalse(object first, object second, Type type)
        {
            // Act
            bool equal = first.Equals(second);

            // Assert
            Assert.False(equal);
        }

        [Theory]
        [MemberData(nameof(EqualityData))]
        public void GetHashCode_WhenEqual(object first, object second, Type type)
        {
            // Act
            bool equal = first.GetHashCode() == second.GetHashCode();

            // Assert
            Assert.True(equal);
        }

        [Theory]
        [MemberData(nameof(NotEqualityDataDifferentObjects))]
        public void GetHashCode_WhenNotEqual(object first, object second, Type type)
        {
            // Act
            bool equal = first.GetHashCode() == second.GetHashCode();

            // Assert
            Assert.False(equal);
        }

        [Theory]
        [MemberData(nameof(EqualityData))]
        public void GenericEquals_WhenTrue(object first, object second, Type type)
        {
            // Arrange
            // Cast to the known object types, this is so that we can hit the
            // equality operator on the concrete type, otherwise it goes to the
            // default "object" one :)
            dynamic firstObject = Convert.ChangeType(first, type);
            dynamic secondObject = Convert.ChangeType(second, type);

            // Act
            dynamic equal = firstObject.Equals(secondObject);

            // Assert
            Assert.True(equal);
        }

        [Theory]
        [MemberData(nameof(NotEqualityData))]
        public void GenericEquals_WhenFalse(object first, object second, Type type)
        {
            // Arrange
            // Cast to the known object types, this is so that we can hit the
            // equality operator on the concrete type, otherwise it goes to the
            // default "object" one :)
            dynamic firstObject = Convert.ChangeType(first, type);
            dynamic secondObject = Convert.ChangeType(second, type);

            // Act
            dynamic equal = firstObject.Equals(secondObject);

            // Assert
            Assert.False(equal);
        }

        // TODO:Disabled due to RuntypeBinder errors while structs are internal
        //[Theory]
        //[MemberData(nameof(EqualityData))]
        //public void EqualityOperator(object first, object second, Type type)
        //{
        //    // Arrange
        //    // Cast to the known object types, this is so that we can hit the
        //    // equality operator on the concrete type, otherwise it goes to the
        //    // default "object" one :)
        //    dynamic firstObject = Convert.ChangeType(first, type);
        //    dynamic secondObject = Convert.ChangeType(second, type);

        //    // Act
        //    dynamic equal = firstObject == secondObject;

        //    // Assert
        //    Assert.True(equal);
        //}

        [Theory]
        [MemberData(nameof(NotEqualityData))]
        public void Operator_WhenTrue(object first, object second, Type type)
        {
            // Arrange
            // Cast to the known object types, this is so that we can hit the
            // equality operator on the concrete type, otherwise it goes to the
            // default "object" one :)
            dynamic firstObject = Convert.ChangeType(first, type);
            dynamic secondObject = Convert.ChangeType(second, type);

            // Act
            dynamic notEqual = firstObject != secondObject;

            // Assert
            Assert.True(notEqual);
        }

        // TODO:Disabled due to RuntypeBinder errors while structs are internal
        //[Theory]
        //[MemberData(nameof(AlmostEqualsData))]
        //public void AlmostEquals(object first, object second, Type type, float precision)
        //{
        //    // Arrange
        //    // Cast to the known object types, this is so that we can hit the
        //    // equality operator on the concrete type, otherwise it goes to the
        //    // default "object" one :)
        //    dynamic firstObject = Convert.ChangeType(first, type);
        //    dynamic secondObject = Convert.ChangeType(second, type);

        //    // Act
        //    dynamic almostEqual = firstObject.AlmostEquals(secondObject, precision);

        //    // Assert
        //    Assert.True(almostEqual);
        //}

        // TODO:Disabled due to RuntypeBinder errors while structs are internal
        //[Theory]
        //[MemberData(nameof(AlmostNotEqualsData))]
        //public void AlmostNotEquals(object first, object second, Type type, float precision)
        //{
        //    // Arrange
        //    // Cast to the known object types, this is so that we can hit the
        //    // equality operator on the concrete type, otherwise it goes to the
        //    // default "object" one :)
        //    dynamic firstObject = Convert.ChangeType(first, type);
        //    dynamic secondObject = Convert.ChangeType(second, type);

        //    // Act
        //    dynamic almostEqual = firstObject.AlmostEquals(secondObject, precision);

        //    // Assert
        //    Assert.False(almostEqual);
        //}
    }
}
