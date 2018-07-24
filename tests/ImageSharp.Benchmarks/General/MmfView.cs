using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    

    [Config(typeof(MyConfig))]
    public unsafe class MmfView
    {
        public class MyConfig : ManualConfig
        {
            public MyConfig()
            {
                long lol = Process.GetCurrentProcess().PagedMemorySize64;
                this.Add(Job.DryCore.WithLaunchCount(1).WithWarmupCount(3).WithTargetCount(3));
                this.Add(MemoryDiagnoser.Default);
            }
        }

        [Params(512, 
            128 * 1024, 
            1024 * 1024, 
            16 * 1024 * 1024
            )]
        public int BufferLength { get; set; }

        private int LoopLength => this.BufferLength / 8;

        [Benchmark(Baseline = true)]
        public byte UseArray()
        {
            byte[] data = new byte[this.BufferLength];

            fixed (byte* ptr = data)
            {
                int* lol = (int*)ptr;
                for (int i = 0; i < this.LoopLength; i++)
                {
                    lol[i] = i;
                }
            }

            return data[10];
        }
        
        [Benchmark]
        public byte UseMemoryMappedFile()
        {
            using (var mmf = MemoryMappedFile.CreateNew("hello", this.BufferLength, MemoryMappedFileAccess.ReadWrite))
            {
                using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor())
                {
                    byte* ptr = default;
                    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);

                    int* lol = (int*)ptr;
                    for (int i = 0; i < this.LoopLength; i++)
                    {
                        lol[i] = i;
                    }

                    return ptr[10];
                }
            }
        }
    }
}