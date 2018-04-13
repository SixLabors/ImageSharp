// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Memory
{


    public static class TestStructs
    {
        public struct Foo : IEquatable<Foo>
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
                var result = new Foo[size];
                for (int i = 0; i < size; i++)
                {
                    result[i] = new Foo(i + 1, i + 1);
                }
                return result;
            }

            public override bool Equals(object obj) => obj is Foo foo && this.Equals(foo);

            public bool Equals(Foo other) => this.A.Equals(other.A) && this.B.Equals(other.B);

            public override int GetHashCode()
            {
                int hashCode = -1817952719;
                hashCode = hashCode * -1521134295 + base.GetHashCode();
                hashCode = hashCode * -1521134295 + this.A.GetHashCode();
                hashCode = hashCode * -1521134295 + this.B.GetHashCode();
                return hashCode;
            }

            public override string ToString() => $"({this.A},{this.B})";
        }


        /// <summary>
        /// sizeof(AlignedFoo) == sizeof(long)
        /// </summary>
        public unsafe struct AlignedFoo : IEquatable<AlignedFoo>
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

            public override bool Equals(object obj) => obj is AlignedFoo foo && this.Equals(foo);

            public bool Equals(AlignedFoo other) => this.A.Equals(other.A) && this.B.Equals(other.B);

            internal static AlignedFoo[] CreateArray(int size)
            {
                var result = new AlignedFoo[size];
                for (int i = 0; i < size; i++)
                {
                    result[i] = new AlignedFoo(i + 1, i + 1);
                }
                return result;
            }

            public override int GetHashCode()
            {
                int hashCode = -1817952719;
                hashCode = hashCode * -1521134295 + base.GetHashCode();
                hashCode = hashCode * -1521134295 + this.A.GetHashCode();
                hashCode = hashCode * -1521134295 + this.B.GetHashCode();
                return hashCode;
            }
        }
    }
}