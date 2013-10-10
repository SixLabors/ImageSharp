using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using ImageProcessor.Helpers.Extensions;

namespace ImageProcessor.Processors
{
	/// <summary>
	/// Constrains an image to the given dimensions.
	/// </summary>
	public class Constrain : ResizeBase
	{
		private static readonly Regex QueryRegex = new Regex(@"constrain=\d+,\d+", RegexOptions.Compiled);

		public override Regex RegexPattern
		{
			get
			{
				return QueryRegex;
			}
		}


		public override dynamic DynamicParameter { get; set; }
		public override int SortOrder { get; protected set; }
		public override Dictionary<string, string> Settings { get; set; }

		public override int MatchRegexIndex(string queryString)
		{
			int index = 0;

			// Set the sort order to max to allow filtering.
			this.SortOrder = int.MaxValue;

			foreach (Match match in this.RegexPattern.Matches(queryString))
			{
				if (match.Success)
				{
					if (index == 0)
					{
						// Set the index on the first instance only.
						this.SortOrder = match.Index;
						int[] constraints = match.Value.ToPositiveIntegerArray();

						int x = constraints[0];
						int y = constraints[1];

						this.DynamicParameter = new Size(x, y);
					}

					index += 1;
				}
			}

			return this.SortOrder;
		}

		public override Image ProcessImage(ImageFactory factory)
		{

			double constrainedWidth = DynamicParameter.Width;
			double constrainedHeight = DynamicParameter.Height;

			var original = factory.Image;
			double width = original.Width;
			double height = original.Height;

			if (width > constrainedWidth || height > constrainedHeight)
			{

				double constraintRatio = constrainedHeight / constrainedWidth;
				double originalRatio = height / width;

				Size newSize = originalRatio < constraintRatio
								   ? new Size((int)constrainedWidth, 0)
								   : new Size(0, (int)constrainedHeight);

				int defaultMaxWidth;
				int defaultMaxHeight;
				int.TryParse(this.Settings["MaxWidth"], out defaultMaxWidth);
				int.TryParse(this.Settings["MaxHeight"], out defaultMaxHeight);

				return ResizeImage(factory, newSize.Width, newSize.Height, defaultMaxWidth, defaultMaxHeight);
			}
			return factory.Image;
		}
	}
}