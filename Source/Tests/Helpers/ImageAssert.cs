using System.Drawing;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.Helpers
{
    public class ImageAssert
    {
        internal const string IMAGE_FILE = @"ControlPanel.png";

        internal static void EqualsToExpectedIcon(string deploymentDirectory, Image favoriteIcon)
        {
            bool areEqual = AreEqual(deploymentDirectory, favoriteIcon);
            Assert.IsTrue(areEqual, "The icon wasnt assigned properly.");
        }

        internal static void DoesntEqualsExpectedIcon(string deploymentDirectory, Image favoriteIcon)
        {
            bool areEqual = AreEqual(deploymentDirectory, favoriteIcon);
            Assert.IsFalse(areEqual, "UpdateIcon cant save favorite.");
        }

        private static bool AreEqual(string deploymentDirectory, Image favoriteIcon)
        {
            string fullIconPath = Path.Combine(deploymentDirectory, IMAGE_FILE);
            using (Image expectedImage = Image.FromFile(fullIconPath))
                return AreEqual(expectedImage, favoriteIcon);
        }

        private static bool AreEqual(Image firstImage, Image secondImage)
        {
            if (firstImage == null || secondImage == null ||
                firstImage.Width != secondImage.Width || firstImage.Height != secondImage.Height)
                return false;

            // PNG serialization is not a stable equality representation: even
            // identical decoded pixels can produce different encoded byte streams.
            using (var first = new Bitmap(firstImage))
            using (var second = new Bitmap(secondImage))
            {
                for (int y = 0; y < first.Height; y++)
                {
                    for (int x = 0; x < first.Width; x++)
                    {
                        if (first.GetPixel(x, y).ToArgb() != second.GetPixel(x, y).ToArgb())
                            return false;
                    }
                }

                return true;
            }
        }
    }
}
