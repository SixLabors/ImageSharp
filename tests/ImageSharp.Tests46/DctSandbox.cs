using System.Numerics;
using System.Text;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;

namespace ImageSharp.Tests
{
    public class DctSandbox
    {

        private ITestOutputHelper Output { get; }

        public DctSandbox(ITestOutputHelper output)
        {
            Output = output;
        }

        private float[] CreateTestData()
        {
            float[] result =new float[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[i*8 + j] = i*10 + j;
                }
            }
            return result;
        }

        private void Print(float[] data)
        {
            StringBuilder bld = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    bld.Append($"{data[i * 8 + j],3} ");
                }
                bld.AppendLine();
            }

            Output.WriteLine(bld.ToString());
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
            var data = CreateTestData();

            Print(data);
        }

        [Fact]
        public void Load_Store()
        {
            var data = CreateTestData();

            var m = MagicDCT.Load(data, 1, 1);
            m = Matrix4x4.Transpose(m);

            MagicDCT.Store(m, data, 4, 4);

            Print(data);
        }

        [Fact]
        public void Transpose8x8()
        {
            var data = CreateTestData();

            Span<float> result = new Span<float>(64);

            MagicDCT.Transpose8x8(data, result);

            Print(result.Data);
        }

        [Fact]
        public void Transpose8x8_Inplace()
        {
            var data = CreateTestData();

            MagicDCT.Transpose8x8(data);

            Print(data);
        }
    }
}