// <copyright file="ColorDefinitionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;

    using ImageSharp.Colors.Spaces;
    using Xunit;
    public class ColorDefinitionTests
    {
        public static IEnumerable<string[]> ColorNames => typeof(NamedColors<Color>).GetTypeInfo().GetFields().Select(x => new[] { x.Name });

        [Theory]
        [MemberData(nameof(ColorNames))]
        public void AllColorsAreOnGenericAndBaseColor(string name)
        {
            FieldInfo generic = typeof(NamedColors<Color>).GetTypeInfo().GetField(name);
            FieldInfo specific = typeof(Color).GetTypeInfo().GetField(name);

            Assert.NotNull(specific);
            Assert.NotNull(generic);
            Assert.True(specific.Attributes.HasFlag(FieldAttributes.Public), "specific must be public");
            Assert.True(specific.Attributes.HasFlag(FieldAttributes.Static), "specific must be static");
            Assert.True(generic.Attributes.HasFlag(FieldAttributes.Public), "generic must be public");
            Assert.True(generic.Attributes.HasFlag(FieldAttributes.Static), "generic must be static");
            Color expected = (Color)generic.GetValue(null);
            Color actual = (Color)specific.GetValue(null);
            Assert.Equal(expected, actual);
        }
    }
}
