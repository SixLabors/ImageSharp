
namespace ImageSharp.Tests.Drawing.Text
{
    using ImageSharp.Drawing;
    using SixLabors.Fonts;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using Xunit;

    public class GlyphBuilderTests
    {
        [Fact]
        public void OriginUsed()
        {
            // Y axis is inverted as it expects to be drawing for bottom left
            GlyphBuilder fullBuilder = new GlyphBuilder(new System.Numerics.Vector2(10, 99));
            IGlyphRenderer builder = fullBuilder;

            builder.BeginGlyph();
            builder.BeginFigure();
            builder.MoveTo(new Vector2(0, 0));
            builder.LineTo(new Vector2(0, 10)); // becomes 0, -10

            builder.CubicBezierTo(
                new Vector2(15, 15), // control point -  will not be in the final point collection
                new Vector2(15, 10), // control point -  will not be in the final point collection
                new Vector2(10, 10));// becomes 10, -10

            builder.QuadraticBezierTo(
                new Vector2(10, 5), // control point -  will not be in the final point collection
                new Vector2(10, 0));

            builder.EndFigure();
            builder.EndGlyph();

            System.Collections.Immutable.ImmutableArray<Vector2> points = fullBuilder.Paths.Single().Flatten().Single().Points;

            Assert.Contains(new Vector2(10, 99), points);
            Assert.Contains(new Vector2(10, 109), points);
            Assert.Contains(new Vector2(20, 99), points);
            Assert.Contains(new Vector2(20, 109), points);
        }

        [Fact]
        public void EachGlypeCausesNewPath()
        {
            // Y axis is inverted as it expects to be drawing for bottom left
            GlyphBuilder fullBuilder = new GlyphBuilder();
            IGlyphRenderer builder = fullBuilder;
            for (int i = 0; i < 10; i++)
            {
                builder.BeginGlyph();
                builder.BeginFigure();
                builder.MoveTo(new Vector2(0, 0));
                builder.LineTo(new Vector2(0, 10)); // becomes 0, -10
                builder.LineTo(new Vector2(10, 10));// becomes 10, -10
                builder.LineTo(new Vector2(10, 0));
                builder.EndFigure();
                builder.EndGlyph();
            }

            Assert.Equal(10, fullBuilder.Paths.Count());
        }
    }
}
