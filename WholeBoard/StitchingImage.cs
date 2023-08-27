using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.XFeatures2D;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using static WholeBoard.SURF;
using Size = OpenCvSharp.Size;

namespace WholeBoard
{
    public class StitchingImage
    {
        public Bitmap WrapImage(Bitmap image1, Bitmap image2, List<DMatch> filteredMatches, List<FeaturePoint> keypoints1, List<FeaturePoint> keypoints2)
        {
            // Prepare matched keypoints for computing homography
            Mat image_1 = image1.ToMat();
            Mat image_2 = image2.ToMat();

            List<Point2f> matchedKeyPoints1 = new List<Point2f>();
            List<Point2f> matchedKeyPoints2 = new List<Point2f>();
            filteredMatches.Sort((m1, m2) => m1.Distance.CompareTo(m2.Distance));
            foreach (DMatch match in filteredMatches)
            {
                FeaturePoint feature1 = keypoints1[match.QueryIdx];
                FeaturePoint feature2 = keypoints2[match.TrainIdx];
                matchedKeyPoints1.Add(new Point2f(feature1.X, feature1.Y));
                matchedKeyPoints2.Add(new Point2f(feature2.X, feature2.Y));
            }

            // Draw Matches
            //for (int i = 0; i < filteredMatches.Count; i+=50)
            //{

            //    Mat outputImage = DrawMatchesOnImage(image1, image2, keypoints1, keypoints2, filteredMatches, i,i+50);
            //    Bitmap bitmapSource = outputImage.ToBitmap();
            //    bitmapSource.Save("D:\\KYV\\WholeBoard\\WholeBoard\\Image\\Result\\result" + i + ".jpg");
            //}
            
            //Caculate Homography matrix
            Mat homography = ComputeHomography(matchedKeyPoints1, matchedKeyPoints2);

            // Init Size of Output Image
            OpenCvSharp.Size size = new Size(image1.Width + image2.Width, image1.Height);

            // Using Homography matrix to find the output image
            Mat result = new Mat(size, MatType.CV_8U);
            Cv2.WarpPerspective(image_1, result, homography, size);
            result.ToBitmap().Save("D:\\KYV\\WholeBoard\\WholeBoard\\Image\\Result\\homo.jpg");
            Cv2.Add(result[new Rect(image1.Width, 0, image2.Width, image2.Height)], image_2, result[new Rect(image1.Width, 0, image2.Width, image2.Height)]);

            return result.ToBitmap();
        }
        private Mat ComputeHomography(List<Point2f> matchedKeyPoints1, List<Point2f> matchedKeyPoints2)
        {
            Mat srcPoints = new Mat(matchedKeyPoints1.Count, 2, MatType.CV_32FC1);
            Mat dstPoints = new Mat(matchedKeyPoints2.Count, 2, MatType.CV_32FC1);

            for (int i = 0; i < matchedKeyPoints1.Count; i++)
            {
                srcPoints.Set(i, 0, matchedKeyPoints1[i].X);
                srcPoints.Set(i, 1, matchedKeyPoints1[i].Y);

                dstPoints.Set(i, 0, matchedKeyPoints2[i].X);
                dstPoints.Set(i, 1, matchedKeyPoints2[i].Y);
            }

            return Cv2.FindHomography(srcPoints, dstPoints, HomographyMethods.Ransac);
        }
        public Mat DrawMatchesOnImage(Bitmap image1, Bitmap image2, List<FeaturePoint> keypoints1, List<FeaturePoint> keypoints2, List<DMatch> matches, int start, int end)
        {
            Mat cvImage1 = image1.ToMat();
            Mat cvImage2 = image2.ToMat();

            KeyPoint[] keyPointsArray1 = keypoints1.ConvertAll(kp => new KeyPoint(new Point2f(kp.X, kp.Y), 1f)).ToArray();
            KeyPoint[] keyPointsArray2 = keypoints2.ConvertAll(kp => new KeyPoint(new Point2f(kp.X, kp.Y), 1f)).ToArray();

            Mat outputImage = new Mat();
            List<DMatch> newMatches = new List<DMatch>();
            for(int i = start; i < end; i++)
            {
                for(int j = 0; j < end - start; j++)
                {
                    int matchIndex = i;
                    if (matchIndex >= 0 && matchIndex < matches.Count)
                    {
                        newMatches.Insert(j, matches.ElementAt(matchIndex));
                        FeaturePoint feature1 = keypoints1[newMatches[j].QueryIdx];
                        FeaturePoint feature2 = keypoints2[newMatches[j].TrainIdx];
                    }
                

                    
                }
            }
            Cv2.DrawMatches(cvImage1, keyPointsArray1, cvImage2, keyPointsArray2, newMatches, outputImage,
                        new Scalar(0, 255, 255), new Scalar(255, 0, 0), null, DrawMatchesFlags.NotDrawSinglePoints);
            return outputImage;
        }
    }
}
