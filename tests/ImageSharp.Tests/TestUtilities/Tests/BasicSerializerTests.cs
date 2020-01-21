using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    public class BasicSerializerTests
    {
        class BaseObj : IXunitSerializable
        {
            public double Length { get; set; }
            public string Name { get; set; }
            public int Lives { get; set; }

            public virtual void Deserialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(Length), Length);
                info.AddValue(nameof(Name), Name);
                info.AddValue(nameof(this.Lives), Lives);
            }

            public virtual void Serialize(IXunitSerializationInfo info)
            {
                this.Length = info.GetValue<double>(nameof(Length));
                this.Name = info.GetValue<string>(nameof(Name));
                this.Lives = info.GetValue<int>(nameof(Lives));
            }
        }

        class DerivedObj : BaseObj
        {
            public double Strength { get; set; }

            public override void Deserialize(IXunitSerializationInfo info)
            {
                this.Strength = info.GetValue<double>(nameof(Strength));
                base.Deserialize(info);
            }

            public override void Serialize(IXunitSerializationInfo info)
            {
                base.Serialize(info);
                info.AddValue(nameof(Strength), Strength);
            }
        }

        [Fact]
        public void SerializeDeserialize_ShouldPreserveValues()
        {
            var obj = new DerivedObj() {Length = 123.1, Name = "Lol123!", Lives = 7, Strength = 4.8};

            string str = BasicSerializer.Serialize(obj);
            BaseObj mirrorBase = BasicSerializer.Deserialize<BaseObj>(str);

            DerivedObj mirror = Assert.IsType<DerivedObj>(mirrorBase);
            Assert.Equal(obj.Length, mirror.Length);
            Assert.Equal(obj.Name, mirror.Name);
            Assert.Equal(obj.Lives, mirror.Lives);
            Assert.Equal(obj.Strength, mirror.Strength);
        }
    }
}
