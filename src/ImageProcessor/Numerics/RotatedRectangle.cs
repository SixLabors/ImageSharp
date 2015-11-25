namespace ImageProcessor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// A rotated rectangle. Adapted from the excellent sample. TODO: Refactor into a struct.
    /// <see href="http://www.xnadevelopment.com/tutorials/rotatedrectanglecollisions/rotatedrectanglecollisions.shtml"/>
    /// </summary>
    public class RotatedRectangle
    {
        public Rectangle CollisionRectangle;
        public float Rotation;
        public Vector2 Origin;

        public RotatedRectangle(Rectangle theRectangle, float theInitialRotation)
        {
            CollisionRectangle = theRectangle;
            Rotation = theInitialRotation;

            //Calculate the Rectangles origin. We assume the center of the Rectangle will
            //be the point that we will be rotating around and we use that for the origin
            Origin = new Vector2((int)theRectangle.Width / 2f, (int)theRectangle.Height / 2f);
        }

        /// <summary>
        /// Used for changing the X and Y position of the RotatedRectangle
        /// </summary>
        /// <param name="theXPositionAdjustment"></param>
        /// <param name="theYPositionAdjustment"></param>
        public void ChangePosition(int theXPositionAdjustment, int theYPositionAdjustment)
        {
            CollisionRectangle.X += theXPositionAdjustment;
            CollisionRectangle.Y += theYPositionAdjustment;
        }

        /// <summary>
        /// Determines if the specfied point is contained within the rectangular region defined by
        /// this <see cref="RotatedRectangle"/>.
        /// </summary>
        /// <param name="x">The x-coordinate of the given point.</param>
        /// <param name="y">The y-coordinate of the given point.</param>
        /// <returns>The <see cref="bool"/></returns>
        public bool Contains(int x, int y)
        {
            Rectangle rectangle = new Rectangle(x, y, 1, 1);
            return this.Intersects(new RotatedRectangle(rectangle, 0.0f));
        }

        /// <summary>
        /// This intersects method can be used to check a standard XNA framework Rectangle
        /// object and see if it collides with a Rotated Rectangle object
        /// </summary>
        /// <param name="rectangle"></param>
        /// <returns></returns>
        public bool Intersects(Rectangle rectangle)
        {
            return this.Intersects(new RotatedRectangle(rectangle, 0.0f));
        }

        /// <summary>
        /// Check to see if two Rotated Rectangls have collided
        /// </summary>
        /// <param name="theRectangle"></param>
        /// <returns></returns>
        public bool Intersects(RotatedRectangle theRectangle)
        {
            //Calculate the Axis we will use to determine if a collision has occurred
            //Since the objects are rectangles, we only have to generate 4 Axis (2 for
            //each rectangle) since we know the other 2 on a rectangle are parallel.
            List<Vector2> aRectangleAxis = new List<Vector2>
            {
                this.UpperRightCorner() - this.UpperLeftCorner(),
                this.UpperRightCorner() - this.LowerRightCorner(),
                theRectangle.UpperLeftCorner() - theRectangle.LowerLeftCorner(),
                theRectangle.UpperLeftCorner() - theRectangle.UpperRightCorner()
            };

            //Cycle through all of the Axis we need to check. If a collision does not occur
            //on ALL of the Axis, then a collision is NOT occurring. We can then exit out 
            //immediately and notify the calling function that no collision was detected. If
            //a collision DOES occur on ALL of the Axis, then there is a collision occurring
            //between the rotated rectangles. We know this to be true by the Seperating Axis Theorem
            return aRectangleAxis.All(aAxis => this.IsAxisCollision(theRectangle, aAxis));
        }

        /// <summary>
        /// Determines if a collision has occurred on an Axis of one of the
        /// planes parallel to the Rectangle
        /// </summary>
        /// <param name="theRectangle"></param>
        /// <param name="aAxis"></param>
        /// <returns></returns>
        private bool IsAxisCollision(RotatedRectangle theRectangle, Vector2 aAxis)
        {
            //Project the corners of the Rectangle we are checking on to the Axis and
            //get a scalar value of that project we can then use for comparison
            List<int> aRectangleAScalars = new List<int>
            {
                this.GenerateScalar(theRectangle.UpperLeftCorner(), aAxis),
                this.GenerateScalar(theRectangle.UpperRightCorner(), aAxis),
                this.GenerateScalar(theRectangle.LowerLeftCorner(), aAxis),
                this.GenerateScalar(theRectangle.LowerRightCorner(), aAxis)
            };

            //Project the corners of the current Rectangle on to the Axis and
            //get a scalar value of that project we can then use for comparison
            List<int> aRectangleBScalars = new List<int>
            {
                this.GenerateScalar(this.UpperLeftCorner(), aAxis),
                this.GenerateScalar(this.UpperRightCorner(), aAxis),
                this.GenerateScalar(this.LowerLeftCorner(), aAxis),
                this.GenerateScalar(this.LowerRightCorner(), aAxis)
            };

            //Get the Maximum and Minium Scalar values for each of the Rectangles
            int aRectangleAMinimum = aRectangleAScalars.Min();
            int aRectangleAMaximum = aRectangleAScalars.Max();
            int aRectangleBMinimum = aRectangleBScalars.Min();
            int aRectangleBMaximum = aRectangleBScalars.Max();

            //If we have overlaps between the Rectangles (i.e. Min of B is less than Max of A)
            //then we are detecting a collision between the rectangles on this Axis
            if (aRectangleBMinimum <= aRectangleAMaximum && aRectangleBMaximum >= aRectangleAMaximum)
            {
                return true;
            }
            else if (aRectangleAMinimum <= aRectangleBMaximum && aRectangleAMaximum >= aRectangleBMaximum)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Generates a scalar value that can be used to compare where corners of 
        /// a rectangle have been projected onto a particular axis. 
        /// </summary>
        /// <param name="corner"></param>
        /// <param name="axis"></param>
        /// <returns></returns>
        private int GenerateScalar(Vector2 corner, Vector2 axis)
        {
            // Using the formula for Vector projection. Take the corner being passed in
            // and project it onto the given Axis
            float numerator = Vector2.Dot(corner, axis); //(theRectangleCorner.X * theAxis.X) + (theRectangleCorner.Y * theAxis.Y);
            float denominator = Vector2.Dot(axis, axis); //(theAxis.X * theAxis.X) + (theAxis.Y * theAxis.Y);
            float aDivisionResult = numerator / denominator;

            Vector2 projected = new Vector2(aDivisionResult * axis.X, aDivisionResult * axis.Y);

            // Now that we have our projected Vector, calculate a scalar of that projection
            // that can be used to more easily do comparisons
            float scalar = Vector2.Dot(axis, projected);
            return (int)scalar;
        }

        /// <summary>
        /// Rotate a point from a given location and adjust using the Origin we
        /// are rotating around
        /// </summary>
        /// <param name="thePoint"></param>
        /// <param name="theOrigin"></param>
        /// <param name="theRotation"></param>
        /// <returns></returns>
        private Vector2 RotatePoint(Vector2 thePoint, Vector2 theOrigin, float theRotation)
        {
            Vector2 aTranslatedPoint = new Vector2
            {
                X = (float)(theOrigin.X
                    + (thePoint.X - theOrigin.X) * Math.Cos(theRotation)
                    - (thePoint.Y - theOrigin.Y) * Math.Sin(theRotation)),
                Y = (float)(theOrigin.Y
                    + (thePoint.Y - theOrigin.Y) * Math.Cos(theRotation)
                    + (thePoint.X - theOrigin.X) * Math.Sin(theRotation))
            };
            return aTranslatedPoint;
        }

        public Vector2 UpperLeftCorner()
        {
            Vector2 aUpperLeft = new Vector2(this.CollisionRectangle.Left, this.CollisionRectangle.Top);
            aUpperLeft = this.RotatePoint(aUpperLeft, aUpperLeft + this.Origin, this.Rotation);
            return aUpperLeft;
        }

        public Vector2 UpperRightCorner()
        {
            Vector2 aUpperRight = new Vector2(this.CollisionRectangle.Right, this.CollisionRectangle.Top);
            aUpperRight = this.RotatePoint(aUpperRight, aUpperRight + new Vector2(-this.Origin.X, this.Origin.Y), this.Rotation);
            return aUpperRight;
        }

        public Vector2 LowerLeftCorner()
        {
            Vector2 aLowerLeft = new Vector2(this.CollisionRectangle.Left, this.CollisionRectangle.Bottom);
            aLowerLeft = this.RotatePoint(aLowerLeft, aLowerLeft + new Vector2(this.Origin.X, -this.Origin.Y), this.Rotation);
            return aLowerLeft;
        }

        public Vector2 LowerRightCorner()
        {
            Vector2 aLowerRight = new Vector2(this.CollisionRectangle.Right, this.CollisionRectangle.Bottom);
            aLowerRight = this.RotatePoint(aLowerRight, aLowerRight + new Vector2(-this.Origin.X, -this.Origin.Y), this.Rotation);
            return aLowerRight;
        }

        public int X => this.CollisionRectangle.X;

        public int Y => this.CollisionRectangle.Y;

        public int Width => this.CollisionRectangle.Width;

        public int Height => this.CollisionRectangle.Height;
    }
}
