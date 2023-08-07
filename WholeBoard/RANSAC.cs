using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Features2D;
using static WholeBoard.SURF;
using Point = System.Drawing.Point;

namespace WholeBoard
{
    public class RANSAC
    {
        private static readonly string LogFilePath = "log.txt";
        public static Mat ComputeHomography(List<float[]> keyPoints1, List<float[]> keyPoints2)
        {
            float[] srcPointsArray = keyPoints1.SelectMany(kp => new float[] { kp[0], kp[1] }).ToArray();
            float[] dstPointsArray = keyPoints2.SelectMany(kp => new float[] { kp[0], kp[1] }).ToArray();

            Mat srcPoints = new Mat(keyPoints1.Count, 1, MatType.CV_32FC2, srcPointsArray);
            Mat dstPoints = new Mat(keyPoints2.Count, 1, MatType.CV_32FC2, dstPointsArray);

            return Cv2.FindHomography(srcPoints, dstPoints, HomographyMethods.Ransac);
        }



        public static List<DMatch> RansacFilterMatches(List<FeaturePoint> keypoints1, List<FeaturePoint> keypoints2, List<DMatch> matches, double ransacThreshold = 3.0)
        {
            Logger.LogMethodCall(nameof(RansacFilterMatches));
            List<DMatch> goodMatches = new List<DMatch>();
            Logger.LogMessage("RansacFilterMatches process started.");
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int maxInliers = 0;
            List<DMatch> bestInliers = new List<DMatch>();
            int maxIterations = 100;

            for (int i = 0; i < maxIterations; i++)
            {
                Debug.WriteLine(i);
                // Randomly select a subset of matches (e.g., 4 matches) to estimate the transformation
                List<DMatch> randomMatches = matches.OrderBy(x => Guid.NewGuid()).Take(4).ToList();

                // Get the keypoint coordinates for the selected matches
                List<float[]> srcPointsList = new List<float[]>();
                List<float[]> dstPointsList = new List<float[]>();
                foreach (DMatch match in randomMatches)
                {
                    FeaturePoint feature1 = keypoints1[match.QueryIdx];
                    FeaturePoint feature2 = keypoints2[match.TrainIdx];
                    srcPointsList.Add(new[] { feature1.X, feature1.Y });
                    dstPointsList.Add(new[] { feature2.X, feature2.Y });
                }

                // Compute the homography using the selected matches
                Mat homography = ComputeHomography(srcPointsList, dstPointsList);

                // Evaluate the number of inliers using the estimated homography and the RANSAC threshold
                List<DMatch> inliers = new List<DMatch>();
                foreach (DMatch match in matches)
                {
                    FeaturePoint feature1 = keypoints1[match.QueryIdx];
                    FeaturePoint feature2 = keypoints2[match.TrainIdx];

                    float[] srcPoint = new[] { feature1.X, feature1.Y };
                    float[] dstPoint = new[] { feature2.X, feature2.Y };

                    // Convert the float[] to Mat for homography transformation
                    Mat srcPtMat = new Mat(1, 1, MatType.CV_32FC2, srcPoint);
                    Mat transformedPtMat = new Mat();
                    Cv2.PerspectiveTransform(srcPtMat, transformedPtMat, homography);

                    // Get the transformed point
                    Point2f transformedPt = transformedPtMat.Get<Point2f>(0, 0);

                    // Separate the X and Y components
                    float transformedX = transformedPt.X;
                    float transformedY = transformedPt.Y;

                    // Calculate the distance between the dstPoint and the transformed point
                    double distance = Math.Sqrt(Math.Pow(dstPoint[0] - transformedX, 2) + Math.Pow(dstPoint[1] - transformedY, 2));

                    if (distance < ransacThreshold)
                    {
                        inliers.Add(match);
                    }
                }

                // Update the best set of inliers
                if (inliers.Count > maxInliers)
                {
                    maxInliers = inliers.Count;
                    bestInliers = inliers;
                }
            }
            stopwatch.Stop();
            Logger.LogMessage($"RansacFilterMatches process finished. Execution time: {stopwatch.Elapsed}");
            Logger.LogMethodEnd(nameof(RansacFilterMatches));

            return bestInliers;
        }
        private Mat WrapImage(Bitmap image, Mat homography)
        {
            Mat cvImage = SURF.BitmapToMat(image);
            Mat warpedImage = new Mat();
            Cv2.WarpPerspective(cvImage, warpedImage, homography, new OpenCvSharp.Size(image.Width, image.Height));

            return warpedImage;
        }

        private Bitmap CombineImages(Bitmap image1, Mat image2)
        {
            Bitmap bitmap2 = MatToBitmap(image2);

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
        private Bitmap MatToBitmap(Mat mat)
        {
            var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat);

            return bitmap;
        }
    }
}
