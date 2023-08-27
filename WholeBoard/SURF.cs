using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Color = System.Drawing.Color;
using OpenCvSharp;
using System.Linq;
using System.Diagnostics;
using System.IO;
using OpenCvSharp.Flann;
using OpenCvSharp.Extensions;

namespace WholeBoard
{

    public class SURF
    {
        private static readonly string LogFilePath = "log.txt";
        public List<FeaturePoint> DetectFeatures(Bitmap image)
        {
            //Caculate hessianResponses
            double[,] hessianResponses = ComputeHessianResponses(image);

            //Create features point
            List<FeaturePoint> interestPoints = ApplyNonMaximumSuppression(hessianResponses);

            // Perform SURF descriptor extraction for each interest point
            List<FeaturePoint> features = new List<FeaturePoint>();
            features = ComputeSURFDescriptors(image, interestPoints);


            // Return the list of feature points
            return features;

        }
        // Gray Scale

        public Bitmap GrayScale(Bitmap bitmap)
        {
            Bitmap newbitmap = (Bitmap)bitmap.Clone();
            int width = newbitmap.Width;
            int height = newbitmap.Height;

            // Convert the image to grayscale by changing the pixel values
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixelColor = newbitmap.GetPixel(x, y);
                    byte gray = (byte)((pixelColor.R + pixelColor.G + pixelColor.B) / 3);
                    Color grayColor = Color.FromArgb(pixelColor.A, gray, gray, gray);
                    newbitmap.SetPixel(x, y, grayColor);
                }
            }

            // Return the modified bitmap
            return newbitmap;
        }

        private double[,] ComputeHessianResponses(Bitmap image)
        {
            int kernelSize = 5;
            double[,] smoothedImage = ApplyLowPassFilter(image, kernelSize);
            Bitmap grayscaleImage = GrayScale(image);
            int width = smoothedImage.GetLength(0);
            int height = smoothedImage.GetLength(1);
            double[,] hessianResponses = new double[width, height];

            Mat grayMat = BitmapConverter.ToMat(grayscaleImage);

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (x < 0 || x >= width || y < 0 || y >= height)
                    {

                        continue;
                    }

                    double dx = grayMat.At<byte>(y, x + 1) - grayMat.At<byte>(y, x - 1);
                    double dy = grayMat.At<byte>(y + 1, x) - grayMat.At<byte>(y - 1, x);
                    double dxx = grayMat.At<byte>(y, x + 1) - 2 * grayMat.At<byte>(y, x) + grayMat.At<byte>(y, x - 1);
                    double dyy = grayMat.At<byte>(y + 1, x) - 2 * grayMat.At<byte>(y, x) + grayMat.At<byte>(y - 1, x);
                    double dxy = (grayMat.At<byte>(y + 1, x + 1) - grayMat.At<byte>(y - 1, x + 1) -
                                  grayMat.At<byte>(y + 1, x - 1) + grayMat.At<byte>(y - 1, x - 1)) / 4.0;

                    // Compute the determinant of the Hessian matrix for the point (x, y)
                    double determinant = dxx * dyy - dxy * dxy;

                    // Compute the trace of the Hessian matrix for the point (x, y)
                    double trace = dxx + dyy;

                    // Compute the response value using the Harris corner detection formula (k is a constant, usually between 0.04 and 0.06)
                    double k = 0.05;

                    hessianResponses[x, y] = determinant - k * Math.Pow(trace, 2);
                }
            }
            return hessianResponses;
        }



        private List<FeaturePoint> ApplyNonMaximumSuppression(double[,] hessianResponses, double threshold = 0.05)
        {
            List<FeaturePoint> interestPoints = new List<FeaturePoint>();
            int width = hessianResponses.GetLength(0);
            int height = hessianResponses.GetLength(1);
            int idCount = 0;
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    double centerValue = hessianResponses[x, y];

                    if (centerValue > threshold)
                    {
                        if (IsLocalMaximum(hessianResponses, x, y))
                        {
                            interestPoints.Add(new FeaturePoint { X = x, Y = y, Response = centerValue, Size = CalculateSize(x, y, hessianResponses), Id = idCount });
                        }
                    }
                    idCount++;
                }
            }

            return interestPoints;
        }

        private float CalculateSize(int x, int y, double[,] hessianResponses)
        {
            float size = (float)Math.Sqrt(hessianResponses[x, y]);

            return size;
        }

        private bool IsLocalMaximum(double[,] hessianResponses, int x, int y)
        {
            double centerValue = hessianResponses[x, y];

            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    if (i == 0 && j == 0)
                        continue;

                    if (hessianResponses[x + i, y + j] >= centerValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        private double[,] ApplyLowPassFilter(Bitmap image, int kernelSize)
        {
            // Create a kernel for low-pass filtering (average kernel)
            double[,] kernel = new double[kernelSize, kernelSize];
            for (int i = 0; i < kernelSize; i++)
            {
                for (int j = 0; j < kernelSize; j++)
                {
                    kernel[i, j] = 1.0 / (kernelSize * kernelSize); // Normalize the kernel
                }
            }

            int width = image.Width;
            int height = image.Height;
            double[,] filteredImage = new double[width, height];

            BitmapData imageData = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            int stride = imageData.Stride;
            int bytesPerPixel = 4;
            byte[] pixelData = new byte[stride * height];
            Marshal.Copy(imageData.Scan0, pixelData, 0, pixelData.Length);

            image.UnlockBits(imageData);

            int radius = kernelSize / 2;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double sum = 0.0;

                    for (int j = -radius; j <= radius; j++)
                    {
                        for (int i = -radius; i <= radius; i++)
                        {
                            int pixelX = x + i;
                            int pixelY = y + j;

                            if (pixelX >= 0 && pixelX < width && pixelY >= 0 && pixelY < height)
                            {
                                int pixelIndex = (pixelY * stride) + (pixelX * bytesPerPixel);
                                byte pixelValue = pixelData[pixelIndex];

                                sum += kernel[j + radius, i + radius] * pixelValue;
                            }
                        }
                    }

                    filteredImage[x, y] = sum;
                }
            }

            return filteredImage;
        }
        private List<FeaturePoint> ComputeSURFDescriptors(Bitmap bitmap, List<FeaturePoint> keypoints)
        {
            Bitmap image = GrayScale(bitmap);
            Mat cvImage = BitmapConverter.ToMat(image);
            KeyPoint[] keyPointsArray = keypoints.Select(kp => new KeyPoint(kp.X, kp.Y, kp.Size, kp.Angle, (float)kp.Response, kp.Octave, kp.Id)).ToArray();
            Debug.WriteLine(keyPointsArray.Length);
            var surf = OpenCvSharp.XFeatures2D.SURF.Create(900);
            Mat descriptors = new Mat();
            ComputeOctaveAndAngle(keyPointsArray, cvImage);
            surf.Compute(cvImage, ref keyPointsArray, descriptors);

            for (int i = 0; i < keyPointsArray.Length; i++)
            {
                keypoints[i].Descriptor = descriptors.Row(i);
                keypoints[i].Octave = keyPointsArray[i].Octave; // Gán octave cho keypoint
                keypoints[i].Angle = keyPointsArray[i].Angle;   // Gán angle cho keypoint
                keypoints[i].Size = keyPointsArray[i].Size;
                keypoints[i].Id = i;
            }

            return keypoints;
        }
        private void ComputeOctaveAndAngle(KeyPoint[] keyPointsArray, Mat grayscaleImage)
        {
            for (int i = 0; i < keyPointsArray.Length; i++)
            {
                int x = (int)Math.Round(keyPointsArray[i].Pt.X);
                int y = (int)Math.Round(keyPointsArray[i].Pt.Y);

                double gradientX = ComputeGradientX(grayscaleImage, x, y);
                double gradientY = ComputeGradientY(grayscaleImage, x, y);

                keyPointsArray[i].Angle = (float)Math.Atan2(gradientY, gradientX);
                keyPointsArray[i].Octave = (int)Math.Floor(Math.Log(keyPointsArray[i].Size, 2));
            }
        }
        private double ComputeGradientX(Mat integralImage, int x, int y)
        {
            // Tính gradient x tại vị trí (x, y) thông qua integral image
            var sum1 = integralImage.At<float>(y - 1, x + 1) - integralImage.At<float>(y - 1, x - 1)
                       + 2 * integralImage.At<float>(y, x + 1) - 2 * integralImage.At<float>(y, x - 1)
                       + integralImage.At<float>(y + 1, x + 1) - integralImage.At<float>(y + 1, x - 1);

            return sum1;
        }

        private double ComputeGradientY(Mat integralImage, int x, int y)
        {
            // Tính gradient y tại vị trí (x, y) thông qua integral image
            var sum2 = integralImage.At<float>(y + 1, x - 1) - integralImage.At<float>(y - 1, x - 1)
                       + 2 * integralImage.At<float>(y + 1, x) - 2 * integralImage.At<float>(y - 1, x)
                       + integralImage.At<float>(y + 1, x + 1) - integralImage.At<float>(y - 1, x + 1);

            return sum2;
        }



        // Matching Features
        public static List<DMatch> MatchFeatures(List<FeaturePoint> features1, List<FeaturePoint> features2, double maxDistanceThreshold = 0.01)
        {
            Logger.LogMethodCall(nameof(MatchFeatures));
            List<DMatch> goodMatches = new List<DMatch>();
            Logger.LogMessage("Matching process started.");
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            // Convert the descriptors to a float array for FLANN search
            Mat descriptors1 = new Mat();
            Mat descriptors2 = new Mat();

            // Combine descriptors from FeaturePoints into matrices
            foreach (var feature in features1)
            {
                descriptors1.PushBack(feature.Descriptor);
            }

            foreach (var feature in features2)
            {
                descriptors2.PushBack(feature.Descriptor);
            }

            // Set FLANN parameters
            IndexParams flannIndexParams = new KDTreeIndexParams();
            SearchParams flannSearchParams = new SearchParams(checks: 50);

            // Create FLANN matcher
            using (var matcher = new FlannBasedMatcher(flannIndexParams, flannSearchParams))
            {
                // Match descriptors
                var matches = matcher.Match(descriptors1, descriptors2);

                // Apply the distance threshold to filter out poor matches
                foreach (var match in matches)
                {
                    //Debug.WriteLine(match.Distance);
                    if (match.Distance < maxDistanceThreshold)
                    {
                        goodMatches.Add(match);
                    }
                }
            }
            stopwatch.Stop();
            Logger.LogMessage($"Matching process finished. Execution time: {stopwatch.Elapsed}");
            Logger.LogMethodEnd(nameof(MatchFeatures));
            return goodMatches;
        }

        private void LogMessage(string message)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine(message);
            }
        }

        private void LogMethodCall(string methodName)
        {
            LogMessage($"Method {methodName} called at {DateTime.Now}.");
        }

        private void LogMethodEnd(string methodName)
        {
            LogMessage($"Method {methodName} ended at {DateTime.Now}.");
        }

        public class FeaturePoint
        {
            public int Id { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Size { get; set; } = 0;
            public float Angle { get; set; } = 0;
            public int Octave { get; set; } = 0;
            public Mat Descriptor { get; set; }
            public double Response { get; set; }
            //public double TransformedX { get; set; }
            //public double TransformedY { get; set; }
        }

    }
}
