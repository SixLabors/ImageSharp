﻿// <copyright file="ColorEqualityTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Colors
{
    using System;
    using System.Numerics;
    using Xunit;

    using ImageSharp.Colors.Spaces;

    /// <summary>
    /// Test implementations of IEquatable and IAlmostEquatable in our colorspaces
    /// </summary>
    public class ColorSpaceEqualityTests
    {
        public static readonly TheoryData<IColorVector> EmptyData =
            new TheoryData<IColorVector>
                {
                    CieLab.Empty,
                    CieLch.Empty,
                    CieXyz.Empty,
                    CieXyy.Empty,
                    HunterLab.Empty,
                    Lms.Empty,
                    LinearRgb.Empty,
                    Rgb.Empty,
                    Hsl.Empty
                };

        public static readonly TheoryData<object, object, Type> EqualityData =
           new TheoryData<object, object, Type>
               {
               { new CieLab(Vector3.One), new CieLab(Vector3.One), typeof(CieLab) },
               { new CieLch(Vector3.One), new CieLch(Vector3.One), typeof(CieLch) },
               { new CieXyz(Vector3.One), new CieXyz(Vector3.One), typeof(CieXyz) },
               { new CieXyy(Vector3.One), new CieXyy(Vector3.One), typeof(CieXyy) },
               { new HunterLab(Vector3.One), new HunterLab(Vector3.One), typeof(HunterLab) },
               { new Lms(Vector3.One), new Lms(Vector3.One), typeof(Lms) },
               { new LinearRgb(Vector3.One), new LinearRgb(Vector3.One), typeof(LinearRgb) },
               { new Rgb(Vector3.One), new Rgb(Vector3.One), typeof(Rgb) },
               { new Hsl(Vector3.One), new Hsl(Vector3.One), typeof(Hsl) },
           };

        public static readonly TheoryData<object, object, Type> NotEqualityDataNulls =
            new TheoryData<object, object, Type>
                {
                // Valid object against null
               { new CieLab(Vector3.One), null, typeof(CieLab) },
               { new CieLch(Vector3.One), null, typeof(CieLch) },
               { new CieXyz(Vector3.One), null, typeof(CieXyz) },
               { new CieXyy(Vector3.One), null, typeof(CieXyy) },
               { new HunterLab(Vector3.One), null, typeof(HunterLab) },
               { new Lms(Vector3.One), null, typeof(Lms) },
               { new LinearRgb(Vector3.One), null, typeof(LinearRgb) },
               { new Rgb(Vector3.One), null, typeof(Rgb) },
               { new Hsl(Vector3.One), null, typeof(Hsl) },
            };

        public static readonly TheoryData<object, object, Type> NotEqualityDataDifferentObjects =
           new TheoryData<object, object, Type>
               {
                // Valid objects of different types but not equal
                { new CieLab(Vector3.One), new CieLch(Vector3.Zero), null },
                { new CieXyz(Vector3.One), new HunterLab(Vector3.Zero), null },
                { new Rgb(Vector3.One), new LinearRgb(Vector3.Zero), null },
                { new Rgb(Vector3.One), new Lms(Vector3.Zero), null },
                { new Cmyk(Vector4.One), new Hsl(Vector3.Zero), null },
           };

        public static readonly TheoryData<object, object, Type> NotEqualityData =
           new TheoryData<object, object, Type>
               {
                // Valid objects of the same type but not equal
               { new CieLab(Vector3.One), new CieLab(Vector3.Zero), typeof(CieLab) },
               { new CieLch(Vector3.One), new CieLch(Vector3.Zero), typeof(CieLch) },
               { new CieXyz(Vector3.One), new CieXyz(Vector3.Zero), typeof(CieXyz) },
               { new CieXyy(Vector3.One), new CieXyy(Vector3.Zero), typeof(CieXyy) },
               { new HunterLab(Vector3.One), new HunterLab(Vector3.Zero), typeof(HunterLab) },
               { new Lms(Vector3.One), new Lms(Vector3.Zero), typeof(Lms) },
               { new LinearRgb(Vector3.One), new LinearRgb(Vector3.Zero), typeof(LinearRgb) },
               { new Rgb(Vector3.One), new Rgb(Vector3.Zero), typeof(Rgb) },
               { new Cmyk(Vector4.One), new Cmyk(Vector4.Zero), typeof(Cmyk) },
               { new Hsl(Vector3.One), new Hsl(Vector3.Zero), typeof(Hsl) },
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
                { new CieXyz(380F, 380F, 380F), new CieXyz(380F, 380F, 380F), typeof(CieXyz), 0F },
                { new CieXyz(380F, 380F, 380F), new CieXyz(380.001F, 380F, 380F), typeof(CieXyz), .01F },
                { new CieXyz(380F, 380F, 380F), new CieXyz(380F, 380.001F, 380F), typeof(CieXyz), .01F },
                { new CieXyz(380F, 380F, 380F), new CieXyz(380F, 380F, 380.001F), typeof(CieXyz), .01F },
                { new Cmyk(1, 1, 1, 1), new Cmyk(1, 1, 1, .99F), typeof(Cmyk), .01F },
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
        public void Equality(IColorVector color)
        {
            // Act
            bool equal = color.Vector.Equals(Vector3.Zero);

            // Assert
            Assert.True(equal);
        }

        [Theory]
        [MemberData(nameof(EqualityData))]
        public void Equality(object first, object second, Type type)
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
        public void NotEquality(object first, object second, Type type)
        {
            // Act
            bool equal = first.Equals(second);

            // Assert
            Assert.False(equal);
        }

        [Theory]
        [MemberData(nameof(EqualityData))]
        public void HashCodeEqual(object first, object second, Type type)
        {
            // Act
            bool equal = first.GetHashCode() == second.GetHashCode();

            // Assert
            Assert.True(equal);
        }

        [Theory]
        [MemberData(nameof(NotEqualityDataDifferentObjects))]
        public void HashCodeNotEqual(object first, object second, Type type)
        {
            // Act
            bool equal = first.GetHashCode() == second.GetHashCode();

            // Assert
            Assert.False(equal);
        }

        [Theory]
        [MemberData(nameof(EqualityData))]
        public void EqualityObject(object first, object second, Type type)
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
        public void NotEqualityObject(object first, object second, Type type)
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

        [Theory]
        [MemberData(nameof(EqualityData))]
        public void EqualityOperator(object first, object second, Type type)
        {
            // Arrange
            // Cast to the known object types, this is so that we can hit the
            // equality operator on the concrete type, otherwise it goes to the
            // default "object" one :)
            dynamic firstObject = Convert.ChangeType(first, type);
            dynamic secondObject = Convert.ChangeType(second, type);

            // Act
            dynamic equal = firstObject == secondObject;

            // Assert
            Assert.True(equal);
        }

        [Theory]
        [MemberData(nameof(NotEqualityData))]
        public void NotEqualityOperator(object first, object second, Type type)
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

        [Theory]
        [MemberData(nameof(AlmostEqualsData))]
        public void AlmostEquals(object first, object second, Type type, float precision)
        {
            // Arrange
            // Cast to the known object types, this is so that we can hit the
            // equality operator on the concrete type, otherwise it goes to the
            // default "object" one :)
            dynamic firstObject = Convert.ChangeType(first, type);
            dynamic secondObject = Convert.ChangeType(second, type);

            // Act
            dynamic almostEqual = firstObject.AlmostEquals(secondObject, precision);

            // Assert
            Assert.True(almostEqual);
        }

        [Theory]
        [MemberData(nameof(AlmostNotEqualsData))]
        public void AlmostNotEquals(object first, object second, Type type, float precision)
        {
            // Arrange
            // Cast to the known object types, this is so that we can hit the
            // equality operator on the concrete type, otherwise it goes to the
            // default "object" one :)
            dynamic firstObject = Convert.ChangeType(first, type);
            dynamic secondObject = Convert.ChangeType(second, type);

            // Act
            dynamic almostEqual = firstObject.AlmostEquals(secondObject, precision);

            // Assert
            Assert.False(almostEqual);
        }

    }
}
