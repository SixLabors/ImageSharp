using System.Numerics;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;

namespace ImageSharp.Tests.Formats.Jpg
{
    public class DctTests : UtilityTestClassBase
    {
        public DctTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void Mennyi()
        {
            Output.WriteLine(Vector.IsHardwareAccelerated.ToString());
            Output.WriteLine(Vector<float>.Count.ToString());
        }

        [Fact]
        public void CheckTestData()
        {
            var data = Create8x8FloatData();

            Print8x8Data(data);
        }

        [Fact]
        public void Load_Store()
        {
            var data = Create8x8FloatData();

            var m = ReferenceDCT.Load(data, 1, 1);
            m = Matrix4x4.Transpose(m);

            ReferenceDCT.Store(m, data, 4, 4);

            Print8x8Data(data);
        }

        [Fact]
        public void Transpose8x8()
        {
            var data = Create8x8FloatData();

            Span<float> result = new Span<float>(64);

            ReferenceDCT.Transpose8x8(data, result);

            Print8x8Data(result.Data);
        }

        [Fact]
        public void Transpose8x8_Inplace()
        {
            var data = Create8x8FloatData();

            ReferenceDCT.Transpose8x8(data);

            Print8x8Data(data);
        }

        
    }
}