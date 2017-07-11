using System;
using System.Collections.Generic;
using System.Text;
using ImageSharp.PixelFormats;
using Xunit.Abstractions;

namespace ImageSharp.Tests.TestUtilities
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
