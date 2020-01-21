using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    public class BasicSerializerTests
    {
        class TestObj : IXunitSerializable
        {
            public double Length { get; set; }
            public string Name { get; set; }
            public int Lives { get; set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(Length), Length);
                info.AddValue(nameof(Name), Name);
                info.AddValue(nameof(this.Lives), Lives);
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                this.Length = info.GetValue<double>(nameof(Length));
                this.Name = info.GetValue<string>(nameof(Name));
                this.Lives = info.GetValue<int>(nameof(Lives));
            }
        }

        [Fact]
        public void SerializeDeserialize_ShouldPreserveValues()
        {
            var obj = new TestObj() {Length = 123, Name = "Lol123!", Lives = 7};

            string str = BasicSerializer.Serialize(obj);
            TestObj mirror = BasicSerializer.Deserialize<TestObj>(str);

            Assert.Equal(obj.Length, mirror.Length);
            Assert.Equal(obj.Name, mirror.Name);
            Assert.Equal(obj.Lives, mirror.Lives);
        }
    }
}
