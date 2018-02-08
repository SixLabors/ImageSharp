// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests.Memory
{
    using Xunit;

    public static class TestStructs
    {
        public struct Foo
        {
            public int A;

            public double B;

            public Foo(int a, double b)
            {
                this.A = a;
                this.B = b;
            }

            internal static Foo[] CreateArray(int size)
            {
                Foo[] result = new Foo[size];
                for (int i = 0; i < size; i++)
                {
                    result[i] = new Foo(i + 1, i + 1);
                }
                return result;
            }

            public override string ToString() => $"({this.A},{this.B})";
        }


        /// <summary>
        /// sizeof(AlignedFoo) == sizeof(long)
        /// </summary>
        public unsafe struct AlignedFoo
        {
            public int A;

            public int B;

            static AlignedFoo()
            {
                Assert.Equal(sizeof(AlignedFoo), sizeof(long));
            }

            public AlignedFoo(int a, int b)
            {
                this.A = a;
                this.B = b;
            }

            internal static AlignedFoo[] CreateArray(int size)
            {
                AlignedFoo[] result = new AlignedFoo[size];
                for (int i = 0; i < size; i++)
                {
                    result[i] = new AlignedFoo(i + 1, i + 1);
                }
                return result;
            }
        }
    }
}