using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using static WholeBoard.SURF;
using Point = System.Drawing.Point;

namespace WholeBoard
{
    public class StitchingImage
    {
        public Bitmap WrapImage (Bitmap image1, Bitmap image2, List<DMatch> filteredMatches, List<FeaturePoint> keypoints1, List<FeaturePoint> keypoints2) 
        {
            // Prepare matched keypoints for computing homography
            List<float[]> matchedKeyPoints1 = new List<float[]>();
            List<float[]> matchedKeyPoints2 = new List<float[]>();

            foreach (DMatch match in filteredMatches)
            {
                FeaturePoint feature1 = keypoints1[match.QueryIdx];
                FeaturePoint feature2 = keypoints2[match.TrainIdx];

                matchedKeyPoints1.Add(new[] { feature1.X, feature1.Y });
                matchedKeyPoints2.Add(new[] { feature2.X, feature2.Y });
            }

            // Compute homography using RANSAC filtered matches
            Mat homography = ComputeHomography(matchedKeyPoints1, matchedKeyPoints2);
            Debug.WriteLine("Homo is: " +  homography.Size());
            // Wrap the first image using the computed homography
            Mat wrappedImage = WrapImages(image1, homography);

            // Combine the wrapped image and the second image
            Bitmap combinedImage = CombineImages(image2, wrappedImage);

            return combinedImage;
        }
        private Mat ComputeHomography(List<float[]> matchedKeyPoints1, List<float[]> matchedKeyPoints2)
        {

            float[] srcPointsArray = matchedKeyPoints1.SelectMany(kp => new float[] { kp[0], kp[1] }).ToArray();
            float[] dstPointsArray = matchedKeyPoints2.SelectMany(kp => new float[] { kp[0], kp[1] }).ToArray();

            Mat srcPoints = new Mat(matchedKeyPoints1.Count, 2, MatType.CV_32FC1, srcPointsArray);
            Mat dstPoints = new Mat(matchedKeyPoints2.Count, 2, MatType.CV_32FC1, dstPointsArray);

            return Cv2.FindHomography(srcPoints, dstPoints, HomographyMethods.Ransac);
        }
        private Mat WrapImages(Bitmap image, Mat homography)
        {
            Mat cvImage = BitmapToMat(image);
            Mat warpedImage = new Mat();
            Cv2.WarpPerspective(cvImage, warpedImage, homography, new OpenCvSharp.Size(image.Width, image.Height));

            return warpedImage;
        }

        private Bitmap CombineImages(Bitmap image1, Mat image2)
        {
            Bitmap bitmap2 = BitmapConverter.ToBitmap(image2);

            int maxWidth = Math.Max(image1.Width, image2.Width);
            int totalHeight = image1.Height + image2.Height;

            Bitmap combinedImage = new Bitmap(maxWidth, totalHeight);

            using (Graphics g = Graphics.FromImage(combinedImage))
            {
                g.DrawImage(image1, new Point(0, 0));
                g.DrawImage(bitmap2, new Point(0, image1.Height));
            }

            return combinedImage;
        }
    }
}
