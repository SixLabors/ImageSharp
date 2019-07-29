// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    public class TestType<T> : IXunitSerializable
    {
        public TestType()
        {
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
        }

        public void Serialize(IXunitSerializationInfo info)
        {
        }

        public override string ToString()
        {
            return $"Type<{typeof(T).Name}>";
        }
    }
}
