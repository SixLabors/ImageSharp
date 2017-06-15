using System;
using System.Numerics;
using ImageSharp;

namespace Scratch
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var image = Image.Load(@"C:\Users\tocso\Desktop\92d8758c-471c-11e7-81d8-62ec301b092a (1).jpg")) {

                //image.Fill(Rgba32.Beige, b => b.AddBezier(Vector2.UnitX, Vector2.UnitX, Vector2.UnitX, Vector2.UnitX));

                image.Resize(640, 480)
                    .Save(@"C:\Users\tocso\Desktop\92d8758c-471c-11e7-81d8-62ec301b092a (1)-saved.jpg");
            }
        }
    }
}