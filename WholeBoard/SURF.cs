
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;
using OpenCvSharp;
using System.Linq;
using System.Diagnostics;
using System.IO;
using OpenCvSharp.Flann;

namespace WholeBoard
{

    public class SURF
    {
        private static readonly string LogFilePath = "log.txt";
        public List<FeaturePoint> DetectFeatures(Bitmap image)
        {
            ////Convert to grayscale
            //Bitmap bitmap = GrayScale(image);

            //List<FeaturePoint> interestPoints = FindInterestPoints(image);

            //this new
            double[,] hessianResponses = ComputeHessianResponses(image);
            List<FeaturePoint> interestPoints = ApplyNonMaximumSuppression(hessianResponses);
            


            // Perform SURF descriptor extraction for each interest point

            ComputeSURFDescriptors(image, interestPoints);
            

            // Return the list of feature points
            return interestPoints;

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

        public BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)

        {
            var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                            System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(bitmapData.Width, bitmapData.Height,
                                                   bitmap.HorizontalResolution, bitmap.VerticalResolution,
                                                   PixelFormats.Gray16, null,
                                                   bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return bitmapSource;
        }

        private List<FeaturePoint> FindInterestPoints(Bitmap image)
        {
            ////Convert to grayscale
            //Bitmap bitmap = GrayScale(image);
            // Compute the Difference of Gaussians
            // Assign unique IDs to the feature points
            int currentId = 0;
            double[,] dog = ComputeDifferenceOfGaussians(image);

            // Apply non-maximum suppression to find local extrema
            List<FeaturePoint> interestPoints = ApplyNonMaximumSuppression(dog);
            foreach (FeaturePoint point in interestPoints)
            {
                point.Id = currentId;
                currentId++;
            }
            return interestPoints;
        }

        private double[,] ComputeHessianResponses(Bitmap image)
        {
            Bitmap grayscaleImage = GrayScale(image);
            int width = grayscaleImage.Width;
            int height = grayscaleImage.Height;
            double[,] hessianResponses = new double[width, height];

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    double dx = grayscaleImage.GetPixel(x + 1, y).R - grayscaleImage.GetPixel(x - 1, y).R;
                    double dy = grayscaleImage.GetPixel(x, y + 1).R - grayscaleImage.GetPixel(x, y - 1).R;
                    double dxx = grayscaleImage.GetPixel(x + 1, y).R - 2 * grayscaleImage.GetPixel(x, y).R + grayscaleImage.GetPixel(x - 1, y).R;
                    double dyy = grayscaleImage.GetPixel(x, y + 1).R - 2 * grayscaleImage.GetPixel(x, y).R + grayscaleImage.GetPixel(x, y - 1).R;
                    double dxy = (grayscaleImage.GetPixel(x + 1, y + 1).R - grayscaleImage.GetPixel(x - 1, y + 1).R -
                                  grayscaleImage.GetPixel(x + 1, y - 1).R + grayscaleImage.GetPixel(x - 1, y - 1).R) / 4.0;

                    // Compute the determinant of the Hessian matrix for the point (x, y)
                    double determinant = dxx * dyy - dxy * dxy;

                    // Compute the trace of the Hessian matrix for the point (x, y)
                    double trace = dxx + dyy;

                    // Compute the response value using the Harris corner detection formula (k is a constant, usually between 0.04 and 0.06)
                    double k = 0.04;
                    hessianResponses[x, y] = determinant - k * Math.Pow(trace, 2);
                }
            }

            return hessianResponses;
        }

        private List<FeaturePoint> ApplyNonMaximumSuppression(double[,] hessianResponses, double threshold = 0.01)
        {
            List<FeaturePoint> interestPoints = new List<FeaturePoint>();
            int width = hessianResponses.GetLength(0);
            int height = hessianResponses.GetLength(1);

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    double centerValue = hessianResponses[x, y];

                    if (centerValue > threshold)
                    {
                        if (IsLocalMaximum(hessianResponses, x, y))
                        {
                            interestPoints.Add(new FeaturePoint { X = x, Y = y, Response = centerValue });
                        }
                    }
                }
            }

            return interestPoints;
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
        private bool IsLocalExtremum(double[,] dog, int x, int y)
        {
            double centerValue = dog[x, y];

            // Check if the center value is greater than or equal to ALL neighboring values
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    if (i == 0 && j == 0) // Skip the center pixel
                        continue;

                    if (dog[x + i, y + j] >= centerValue)
                    {
                        return true; // Not a local extremum
                    }
                }
            }

            return false; // It's a local extremum
        }


        private double[,] ComputeDifferenceOfGaussians(Bitmap image)
        {
            //Convert to grayscale
            image = GrayScale(image);

            // Define the scales for the LoG kernels
            double sigma1 = 1;
            double sigma2 = 2;

            // Compute the LoG filtered images
            double[,] filtered1 = ApplyLaplacianOfGaussian(image, sigma1);
            double[,] filtered2 = ApplyLaplacianOfGaussian(image, sigma2);

            // Compute the Difference of Gaussians (DoG)
            int width = image.Width;
            int height = image.Height;
            double[,] dog = new double[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dog[x, y] = filtered2[x, y] - filtered1[x, y];
                }
            }

            return dog;
        }

        private double[,] ApplyLaplacianOfGaussian(Bitmap image, double sigma)
        {
            int kernelSize = (int)(6 * sigma) + 1; // The size of the LoG kernel should be an odd number

            // Compute the Laplacian of Gaussian (LoG) kernel
            double[,] kernel = ComputeLaplacianOfGaussianKernel(sigma, kernelSize);

            // Apply LoG filtering to the image
            int width = image.Width;
            int height = image.Height;
            double[,] filteredImage = new double[width, height];

            BitmapData imageData = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            int stride = imageData.Stride;
            int bytesPerPixel = 4; // Assuming 32bppArgb pixel format (4 bytes per pixel)
            byte[] pixelData = new byte[stride * height];
            Marshal.Copy(imageData.Scan0, pixelData, 0, pixelData.Length);

            image.UnlockBits(imageData);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double sum = 0.0;

                    for (int j = -kernelSize / 2; j <= kernelSize / 2; j++)
                    {
                        for (int i = -kernelSize / 2; i <= kernelSize / 2; i++)
                        {
                            int pixelX = x + i;
                            int pixelY = y + j;

                            if (pixelX >= 0 && pixelX < width && pixelY >= 0 && pixelY < height)
                            {
                                int pixelIndex = (pixelY * stride) + (pixelX * bytesPerPixel);
                                byte pixelValue = pixelData[pixelIndex]; // Assuming grayscale image (R, G, B channels have the same value)

                                sum += kernel[j + kernelSize / 2, i + kernelSize / 2] * pixelValue;
                            }
                        }
                    }

                    filteredImage[x, y] = sum;
                }
            }

            return filteredImage;
        }


        private double[,] ComputeLaplacianOfGaussianKernel(double sigma, int size)
        {
            double[,] kernel = new double[size, size];

            int center = size / 2;
            double sigmaSquared = sigma * sigma;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = x - center;
                    int dy = y - center;

                    double exponent = (dx * dx + dy * dy) / (2 * sigmaSquared);
                    double value = (1.0 - exponent) * Math.Exp(-exponent) / (Math.PI * sigmaSquared * sigmaSquared);
                    kernel[x, y] = value;
                }
            }

            // Normalize the kernel
            double sum = kernel.Cast<double>().Sum();
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    kernel[x, y] /= sum;
                }
            }

            return kernel;
        }

        public void ComputeSURFDescriptors(Bitmap bitmap, List<FeaturePoint> keypoints)
        {
            
            Bitmap image = GrayScale(bitmap);
            Mat cvImage = BitmapToMat(image);

           
            //if (cvImage.Channels() > 1)
            //{
            //    cvImage = cvImage.CvtColor(ColorConversionCodes.BGRA2GRAY);
            //}

            KeyPoint[] keyPointsArray = keypoints.Select(kp => new KeyPoint(kp.X, kp.Y,1)).ToArray();
            var surf = OpenCvSharp.XFeatures2D.SURF.Create(100);
            Mat descriptors = new Mat();
            //KeyPoint[] newPoint = surf.Detect(cvImage);
            surf.Compute(cvImage, ref keyPointsArray, descriptors);

            for (int i = 0; i < keyPointsArray.Length; i++)
            {
                keypoints[i].Descriptor = descriptors.Row(i);
            }
        }
        public static Mat BitmapToMat(Bitmap bitmap)
        {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);

            Mat mat = new Mat(bitmap.Height, bitmap.Width, MatType.CV_8UC4, bmpData.Scan0);

            bitmap.UnlockBits(bmpData);

            return mat;
        }
        // Matching Features
        public static List<DMatch> MatchFeatures(List<FeaturePoint> features1, List<FeaturePoint> features2, double maxDistanceThreshold = 10)
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
            var flannIndexParams = new KDTreeIndexParams();
            var flannSearchParams = new SearchParams {};

            // Create FLANN matcher
            using (var matcher = new FlannBasedMatcher(flannIndexParams, flannSearchParams))
            {
                // Match descriptors
                var matches = matcher.Match(descriptors1, descriptors2);

                // Apply the distance threshold to filter out poor matches
                foreach (var match in matches)
                {
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
        private static double CalculateDescriptorDistance(Mat descriptor1, Mat descriptor2)
        {
            // Assuming both descriptors are of type CV_32F
            double distanceSquared = 0.0;

            for (int i = 0; i < descriptor1.Rows; i++)
            {
                double diff = descriptor1.At<float>(i) - descriptor2.At<float>(i);
                distanceSquared += diff * diff;
            }

            return Math.Sqrt(distanceSquared);
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
            public Mat Descriptor { get; set; }
            public double Response { get; set; }
            //public double TransformedX { get; set; }
            //public double TransformedY { get; set; }
        }

    }
}
