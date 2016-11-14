using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using ImageSharp.Formats;
using Xunit.Abstractions;

namespace ImageSharp.Tests.Formats.Jpg
{
    public class UtilityTestClassBase
    {
        public UtilityTestClassBase(ITestOutputHelper output)
        {
            Output = output;
        }

        protected ITestOutputHelper Output { get; }

        // ReSharper disable once InconsistentNaming
        public static float[] Create8x8FloatData()
        {
            float[] result = new float[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[i * 8 + j] = i * 10 + j;
                }
            }
            return result;
        }

      


        // ReSharper disable once InconsistentNaming
        public static int[] Create8x8IntData()
        {
            int[] result = new int[64];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[i * 8 + j] = i * 10 + j;
                }
            }
            return result;
        }

        internal void Print8x8Data<T>(MutableSpan<T> data) => Print8x8Data(data.Data);

        internal void Print8x8Data<T>(T[] data)
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

        internal void PrintLinearData<T>(T[] data) => PrintLinearData(new MutableSpan<T>(data), data.Length);

        internal void PrintLinearData<T>(MutableSpan<T> data, int count = -1)
        {
            if (count < 0) count = data.TotalCount;

            StringBuilder bld = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                bld.Append($"{data[i],3} ");
            }
            Output.WriteLine(bld.ToString());
        }

        protected void Measure(int times, Action action, [CallerMemberName] string operationName = null)
        {
            Output.WriteLine($"{operationName} X {times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < times; i++)
            {
                action();
            }

            sw.Stop();
            Output.WriteLine($"{operationName} finished in {sw.ElapsedMilliseconds} ms");
        }
    }
}