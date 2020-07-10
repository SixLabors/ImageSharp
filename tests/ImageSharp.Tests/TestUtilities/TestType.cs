// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
