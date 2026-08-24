// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.Memory.DiscontiguousBuffers;

public partial class MemoryGroupTests
{
    public class CopyTo : MemoryGroupTestsBase
    {
        [Fact]
        public void GroupToSpan_StridedSource_DoesNotRequireTrailingPadding()
        {
            using MemoryGroup<int> src = this.CreateTestGroup(totalLength: 7, bufferLength: 4, fillSequence: true);
            int[] trg = new int[6];

            src.CopyTo(
                sourceStride: 4,
                trg,
                targetStride: 3,
                width: 3,
                height: 2);

            Assert.Equal(new[] { 1, 2, 3, 5, 6, 7 }, trg);
        }

        [Fact]
        public void GroupToGroup_StridedCopy_DoesNotRequireTrailingPadding()
        {
            using MemoryGroup<int> src = this.CreateTestGroup(totalLength: 11, bufferLength: 5, fillSequence: true);
            using MemoryGroup<int> trg = this.CreateTestGroup(totalLength: 13, bufferLength: 6, fillSequence: false);

            src.CopyTo(
                sourceStride: 4,
                trg,
                targetStride: 5,
                width: 3,
                height: 3);

            Assert.Equal(1, GetElementAtLinearIndex(trg, 0));
            Assert.Equal(2, GetElementAtLinearIndex(trg, 1));
            Assert.Equal(3, GetElementAtLinearIndex(trg, 2));
            Assert.Equal(5, GetElementAtLinearIndex(trg, 5));
            Assert.Equal(6, GetElementAtLinearIndex(trg, 6));
            Assert.Equal(7, GetElementAtLinearIndex(trg, 7));
            Assert.Equal(9, GetElementAtLinearIndex(trg, 10));
            Assert.Equal(10, GetElementAtLinearIndex(trg, 11));
            Assert.Equal(11, GetElementAtLinearIndex(trg, 12));
        }

        [Fact]
        public void GroupToSpan_StridedSource_HeightOne()
        {
            using MemoryGroup<int> src = this.CreateTestGroup(totalLength: 3, bufferLength: 2, fillSequence: true);
            int[] trg = new int[3];

            src.CopyTo(
                sourceStride: 8,
                trg,
                targetStride: 3,
                width: 3,
                height: 1);

            Assert.Equal(new[] { 1, 2, 3 }, trg);
        }

        private static int GetElementAtLinearIndex(MemoryGroup<int> group, int index)
        {
            int pos = 0;
            for (MemoryGroupIndex i = group.MinIndex(); i < group.MaxIndex(); i += 1, pos++)
            {
                if (pos == index)
                {
                    return group.GetElementAt(i);
                }
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
