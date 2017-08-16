// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ColorDefinitionTests
    {
        public static TheoryData<string> ColorNames
        {
            get
            {
                var result = new TheoryData<string>();
                foreach (string name in typeof(NamedColors<Rgba32>).GetTypeInfo().GetFields().Select(x =>  x.Name ))
                {
                    result.Add(name);
                }
                return result;
            }
        }

        [Theory]
        [MemberData(nameof(ColorNames))]
        public void AllColorsAreOnGenericAndBaseColor(string name)
        {
            FieldInfo generic = typeof(NamedColors<Rgba32>).GetTypeInfo().GetField(name);
            FieldInfo specific = typeof(Rgba32).GetTypeInfo().GetField(name);

            Assert.NotNull(specific);
            Assert.NotNull(generic);
            Assert.True(specific.Attributes.HasFlag(FieldAttributes.Public), "specific must be public");
            Assert.True(specific.Attributes.HasFlag(FieldAttributes.Static), "specific must be static");
            Assert.True(generic.Attributes.HasFlag(FieldAttributes.Public), "generic must be public");
            Assert.True(generic.Attributes.HasFlag(FieldAttributes.Static), "generic must be static");
            Rgba32 expected = (Rgba32)generic.GetValue(null);
            Rgba32 actual = (Rgba32)specific.GetValue(null);
            Assert.Equal(expected, actual);
        }
    }
}