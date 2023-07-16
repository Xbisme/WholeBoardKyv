
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Drawing.Color;

namespace WholeBoard
{
    public class SURF
    {
        public List<FeaturePoint> DetectFeatures(Bitmap image)
        {
            ////Convert to grayscale
            //Bitmap bitmap = GrayScale(image);

            List<FeaturePoint> interestPoints = FindInterestPoints(image);

            // Perform SURF descriptor extraction for each interest point

            foreach (var point in interestPoints)
            {
                point.Descriptor = ExtractSURFDescriptor(image, point);
            }

            // Return the list of feature points
            return interestPoints;

        }
        // Gray Scale
        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)

        {
            var bitmapData = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return bitmapSource;
        }
        private BitmapSource ConvertToGrayscale(BitmapSource source)
        {
            FormatConvertedBitmap grayscaleBitmap = new FormatConvertedBitmap();
            grayscaleBitmap.BeginInit();
            grayscaleBitmap.Source = source;
            grayscaleBitmap.DestinationFormat = PixelFormats.Gray8;
            grayscaleBitmap.EndInit();

            return grayscaleBitmap;
        }
        private Bitmap ConvertBitmapSourceToBitmap(BitmapSource source)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(stream);

                Bitmap bitmap = new Bitmap(stream);

                // Clone the bitmap to ensure it can be safely disposed
                return (Bitmap)bitmap.Clone();
            }
        }
        public Bitmap GrayScale(Bitmap image)
        {
            BitmapSource bitmapSource = ConvertBitmapToBitmapSource(image);
            bitmapSource = ConvertToGrayscale(bitmapSource);

            //Convert BitmapSource to Bitmap
            Bitmap bitmap = ConvertBitmapSourceToBitmap(bitmapSource);
            return bitmap;
        }
        // end Gray Scale
        private List<FeaturePoint> FindInterestPoints(Bitmap image)
        {
            ////Convert to grayscale
            //Bitmap bitmap = GrayScale(image);
            // Compute the Difference of Gaussians
            double[,] dog = ComputeDifferenceOfGaussians(image);

            // Apply non-maximum suppression to find local extrema
            List<FeaturePoint> interestPoints = ApplyNonMaximumSuppression(dog);


            return interestPoints;
        }

        private List<FeaturePoint> ApplyNonMaximumSuppression(double[,] dog)

        {
            List<FeaturePoint> interestPoints = new List<FeaturePoint>();

            int width = dog.GetLength(0);
            int height = dog.GetLength(1);
            //Debug.WriteLine("Features: ");
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    double centerValue = dog[x, y];

                    // Check if the center value is a local extremum
                    if (IsLocalExtremum(dog, x, y))
                    {
                        FeaturePoint point = new FeaturePoint { X = x, Y = y, Response = centerValue };
                        //Debug.WriteLine(point);
                        interestPoints.Add(point);
                    }
                }
            }
            
            return interestPoints;
        }

        private bool IsLocalExtremum(double[,] dog, int x, int y)
        {
            double centerValue = dog[x, y];

            // Check if the center value is greater than or less than all neighboring values
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    if (dog[x + i, y + j] > centerValue || dog[x + i, y + j] < centerValue)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private double[,] ComputeDifferenceOfGaussians(Bitmap image)
        {
            ////Convert to grayscale
            //image = GrayScale(image);
            // Define the scales for the Gaussian kernels
            double sigma1 = 0.5;
            double sigma2 = 1.0;

            // Compute the blurred images using Gaussian smoothing
            double[,] blurred1 = ApplyGaussianSmoothing(image, sigma1);
            double[,] blurred2 = ApplyGaussianSmoothing(image, sigma2);

            // Compute the Difference of Gaussians (DoG)
            int width = image.Width;
            int height = image.Height;
            double[,] dog = new double[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    dog[x, y] = blurred2[x, y] - blurred1[x, y];
                }
            }

            return dog;
        }

        private double[,] ApplyGaussianSmoothing(Bitmap image, double sigma)
        {
            //string imagePath = "D:\\KYV\\WholeBoard\\WholeBoard\\Image\\Test\\example.jpg";

            //// Load the image
            //image = new Bitmap(imagePath);
            // Define the size of the Gaussian kernel (odd number)
            int kernelSize = (int)(3 * sigma) * 2 + 1;

            // Compute the Gaussian kernel
            double[,] kernel = ComputeGaussianKernel(sigma, kernelSize);

            // Apply Gaussian smoothing to the image
            int width = image.Width;
            int height = image.Height;
            double[,] smoothed = new double[width, height];

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

                    smoothed[x, y] = sum;
                }
            }

            return smoothed;
        }



        private double[,] ComputeGaussianKernel(double sigma, int size)
        {
            double[,] kernel = new double[size, size];
            double sum = 0.0;

            int center = size / 2;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = x - center;
                    int dy = y - center;

                    //double exponent = -(dx * dx + dy * dy) / (2 * sigma * sigma);
                    ////double value = continue from previous response:

                    //double value = Math.Exp(exponent) / (2 * Math.PI * sigma * sigma);
                    double exponent = ((x * x) + (y * y)) / (2 * sigma * sigma);
                    double value = Math.Exp(-exponent) / (2 * Math.PI * sigma * sigma);
                    kernel[x, y] = value;
                    sum += value;
                }
            }
            //Debug.WriteLine("Kernel value: ");
            // Normalize the kernel
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    kernel[x, y] /= sum;
                    //Debug.WriteLine(kernel);
                }
            }
            return kernel;
        }
        private double[] ExtractSURFDescriptor(Bitmap image, FeaturePoint point)
        {
            // Convert the image to grayscale
            Bitmap grayImage = GrayScale(image);

            // Extract the neighborhood around the interest point
            double[,] neighborhood = ExtractNeighborhood(grayImage, point.X, point.Y, 16);

            // Compute the SURF descriptor for the neighborhood
            double[] descriptor = ComputeSURFDescriptor(neighborhood);

            return descriptor;
        }
         
        private double[,] ExtractNeighborhood(Bitmap image, double x, double y, int neighborhoodSize)
        {
            // Calculate the neighborhood bounds based on the point coordinates and neighborhood size
            int halfSize = neighborhoodSize / 2;
            int startX = (int)Math.Max(0, x - halfSize);
            int startY = (int)Math.Max(0, y - halfSize);
            int endX = (int)Math.Min(image.Width - 1, x + halfSize);
            int endY = (int)Math.Min(image.Height - 1, y + halfSize);

            // Extract the neighborhood pixels into a separate matrix
            double[,] neighborhood = new double[endX - startX + 1, endY - startY + 1];

            for (int j = startY; j <= endY; j++)
            {
                for (int i = startX; i <= endX; i++)
                {
                    Color pixelColor = image.GetPixel(i, j);
                    neighborhood[i - startX, j - startY] = pixelColor.R;
                }
            }

            return neighborhood;
        }

        private double[] ComputeSURFDescriptor(double[,] neighborhood)
        {
            // Determine the dimensions of the neighborhood
            int width = neighborhood.GetLength(0);
            int height = neighborhood.GetLength(1);

            // Flatten the neighborhood into a 1D array
            double[] flattenedNeighborhood = new double[width * height];
            int index = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    flattenedNeighborhood[index] = neighborhood[x, y];
                    index++;
                }
            }

            // Compute the SURF descriptor from the flattened neighborhood
            double[] descriptor = new double[128];
            // Compute the histogram of gradient orientations
            ComputeHistogramOfGradientOrientations(flattenedNeighborhood, descriptor);

            // Normalize the descriptor
            NormalizeDescriptor(descriptor);

            return descriptor;
        }

        // Function to compute the histogram of gradient orientations
        private void ComputeHistogramOfGradientOrientations(double[] flattenedNeighborhood, double[] descriptor)
        {
            // Calculate gradients
            double[] gradients = CalculateGradients(flattenedNeighborhood);

            // Calculate orientations and magnitudes
            double[] orientations = CalculateOrientations(gradients);
            double[] magnitudes = CalculateMagnitudes(gradients);

            // Accumulate orientations into the descriptor
            int binSize = 360 / 128;

            for (int i = 0; i < orientations.Length; i++)
            {
                int binIndex = (int)(orientations[i] / binSize);
                descriptor[binIndex] += magnitudes[i];
            }
        }

        // Function to calculate gradients using central differences
        private double[] CalculateGradients(double[] flattenedNeighborhood)
        {
            int size = flattenedNeighborhood.Length;
            double[] gradients = new double[size];

            for (int i = 1; i < size - 1; i++)
            {
                gradients[i] = flattenedNeighborhood[i + 1] - flattenedNeighborhood[i - 1];
            }

            return gradients;
        }

        // Function to calculate orientations from gradients
        private double[] CalculateOrientations(double[] gradients)
        {
            int size = gradients.Length;
            double[] orientations = new double[size];

            for (int i = 0; i < size; i++)
            {
                orientations[i] = (Math.Atan2(gradients[i], 1.0) * 180.0 / Math.PI + 360.0) % 360.0;
            }

            return orientations;
        }

        // Function to calculate magnitudes from gradients
        private double[] CalculateMagnitudes(double[] gradients)
        {
            int size = gradients.Length;
            double[] magnitudes = new double[size];

            for (int i = 0; i < size; i++)
            {
                magnitudes[i] = Math.Sqrt(Math.Pow(gradients[i], 2) + 1.0);
            }

            return magnitudes;
        }

        // Function to normalize the descriptor
        private void NormalizeDescriptor(double[] descriptor)
        {
            double sum = 0.0;

            for (int i = 0; i < 128; i++)
            {
                sum += descriptor[i];
            }

            if (sum > 0.0)
            {
                for (int i = 0; i < 128; i++)
                {
                    descriptor[i] /= sum;
                }
            }
        }
        public class FeaturePoint
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Scale { get; set; }
            public double Orientation { get; set; }
            public double[] Descriptor { get; set; }
            public double Response { get; set; }
        }

    }
}
