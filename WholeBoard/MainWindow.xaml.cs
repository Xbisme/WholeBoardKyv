
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static WholeBoard.SURF;

namespace WholeBoard
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private SURF surf;
        private List<SURF.FeaturePoint> features1;
        private List<SURF.FeaturePoint> features2;
        private string[] filesName;
        private string[] files;
        public ObservableCollection<Item> Items { get; set; }
        public MainWindow()
        {

            surf = new SURF();
            InitializeComponent();
            CenterWindowOnScreen();
        }

        public class Item
        {
            public string Name { get; set; }
        }
        private void CenterWindowOnScreen()
        {
            double screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
            double screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
            double windowWidth = this.Width;
            double windowHeight = this.Height;
            this.Left = (screenWidth / 2) - (windowWidth / 2);
            this.Top = (screenHeight / 2) - (windowHeight / 2);
        }
        private void Load_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.ValidateNames = false;
            openFileDialog.CheckFileExists = false;
            openFileDialog.CheckPathExists = true;
            string first_totalImage = "Total Image: ";
            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = Path.GetDirectoryName(openFileDialog.FileName);
                selectPath.Text = selectedPath;
                string[] supportedExtensions = { ".jpg", ".png", ".jpeg", ".bmp", ".gif" };
                files = Directory.GetFiles(selectedPath)
                                 .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                                 .ToArray();

                filesName = new string[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    filesName[i] = Path.GetFileName(files[i]);
                }

                int itemCount = files.Length;
                imageItems.ItemsSource = files;
                totalImage.Text = first_totalImage + itemCount.ToString();
            }
        }
        private void Execute_Click(object sender, RoutedEventArgs e)
        {
            scale_box.Text = "100";
            //for(int i = 0; i < files.Length - 1; i+=2) 
            //{
                features1 = Image_Loaded(files[0]);
                features2 = Image_Loaded(files[0+1]);
            //}

            List<DMatch> matches = SURF.MatchFeatures(features1, features2);

            List<DMatch> inliers = RANSAC.RansacFilterMatches(features1, features2, matches);

            Mat outputImage = DrawMatchesOnImage(new Bitmap(files[0]), new Bitmap(files[0 + 1]), features1, features2, inliers, 1);
            Bitmap bitmapSource = BitmapConverter.ToBitmap(outputImage);
            bitmapSource.Save("D:\\KYV\\WholeBoard\\WholeBoard\\Image\\Result\\result1.jpg");
            StitchingImage stitchingImage = new StitchingImage();

            Bitmap newImage = stitchingImage.WrapImage(new Bitmap(files[0]), new Bitmap(files[0 + 1]), inliers, features1, features2);
            newImage.Save("D:\\KYV\\WholeBoard\\WholeBoard\\Image\\Result\\ghep.jpg");

            DateTime currentDateTime = DateTime.Now;
            string formattedDateTime = currentDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            Items = new ObservableCollection<Item>()
            {
                new Item { Name = formattedDateTime }
            };
            DataContext = this;
        }

        private void scale_box_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (double.TryParse(scale_box.Text, out double scaleFactor) && scaleFactor >= 20 && scaleFactor <= 100)
            {
                scaleFactor /= 100;
                UpdateScale(scaleFactor);
            }
        }

        private void UpdateScale(double scaleFactor)
        {
            ScaleTransform scaleTransform = new ScaleTransform(scaleFactor, scaleFactor);
            img.RenderTransform = scaleTransform;
        }

        private void AdjustSize(bool increase)
        {
            if (double.TryParse(scale_box.Text, out double scaleFactor) && scaleFactor >= 20 && scaleFactor <= 100)
            {
                if (increase)
                {
                    scaleFactor += 1;
                }
                else
                {
                    scaleFactor -= 1;
                }

                scaleFactor /= 100;
                scale_box.Text = (scaleFactor * 100).ToString();
                UpdateScale(scaleFactor);
            }
            else
            {
                MessageBox.Show("Failed Value");
            }
        }

        private void Up_Size(object sender, RoutedEventArgs e)
        {
            AdjustSize(true);
        }

        private void Down_Size(object sender, RoutedEventArgs e)
        {
            AdjustSize(false);
        }
        private List<FeaturePoint> Image_Loaded(string imagePath )
        {
            // Load the image
            Bitmap originalImage = new Bitmap(imagePath);
            // Detect features
            List<FeaturePoint>features = surf.DetectFeatures(originalImage);
            // Draw the features on the image

            BitmapSource bitmapSource = DrawFeaturesOnImage(originalImage, features);
            ////img.Source = bitmapSource;

            Bitmap bitmap = surf.GrayScale(originalImage);
            bitmap.Save("D:\\KYV\\WholeBoard\\WholeBoard\\Image\\Result\\" + Path.GetFileName(imagePath));
            string savePath = "D:\\KYV\\WholeBoard\\WholeBoard\\Image\\Result\\r" + Path.GetFileName(imagePath);
            SaveClipboardImageToFile(bitmapSource, savePath);
            return features;
        }
        public Mat DrawMatchesOnImage(Bitmap image1, Bitmap image2, List<FeaturePoint> keypoints1, List<FeaturePoint> keypoints2, List<DMatch> matches, int matchIndex)
        {
            Mat cvImage1 = BitmapToMat(image1);
            Mat cvImage2 = BitmapToMat(image2);

            KeyPoint[] keyPointsArray1 = keypoints1.ConvertAll(kp => new KeyPoint(new Point2f(kp.X, kp.Y), 1f)).ToArray();
            KeyPoint[] keyPointsArray2 = keypoints2.ConvertAll(kp => new KeyPoint(new Point2f(kp.X, kp.Y), 1f)).ToArray();

            Mat outputImage = new Mat();

            if (matchIndex >= 0 && matchIndex < matches.Count)
            {
                FeaturePoint feature1 = keypoints1[matches[matchIndex].QueryIdx];
                FeaturePoint feature2 = keypoints2[matches[matchIndex].TrainIdx];

                Cv2.DrawMatches(cvImage1, keyPointsArray1, cvImage2, keyPointsArray2, new List<DMatch> { matches.ElementAt(matchIndex) }, outputImage,
                    new Scalar(0, 255, 255), new Scalar(255, 0, 0), null, DrawMatchesFlags.NotDrawSinglePoints);
                //Scalar color = new Scalar(0, 0, 255); // Red color

                //// Vẽ điểm keypoint của match cụ thể
                //Cv2.Circle(outputImage, new Point((int)feature1.X, (int)feature1.Y), 5, color, -1);
                //Cv2.Circle(outputImage, new Point((int)feature2.X + cvImage1.Cols, (int)feature2.Y), 5, color, -1);

                //// Vẽ đường nối hai điểm keypoint
                //Cv2.Line(outputImage, new Point((int)feature1.X, (int)feature1.Y), new Point((int)feature2.X + cvImage1.Cols, (int)feature2.Y), color, 2);
            }

            return outputImage;
        }


        private BitmapSource DrawFeaturesOnImage(Bitmap originalImage,List<SURF.FeaturePoint> features)
        {
            // Create a DrawingVisual object 
            DrawingVisual drawingVisual = new DrawingVisual();

            // Render the image with features
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                // Convert the original image to a WPF BitmapSource
                BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    originalImage.GetHbitmap(),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // Draw the original image
                drawingContext.DrawImage(bitmapSource, new System.Windows.Rect(0, 0, bitmapSource.PixelWidth, bitmapSource.PixelHeight));

                // Draw circles or markers at the feature coordinates
                double radius = 1 ; // Adjust the radius as needed

                foreach (SURF.FeaturePoint feature in features)
                {
                    double x = feature.X;
                    double y = feature.Y;

                    // Draw a circle or marker at the feature coordinates
                    SolidColorBrush brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0)); // Red color
                    drawingContext.DrawEllipse(brush, null, new System.Windows.Point(x, y), radius, radius);
                }
            }

            // Convert the DrawingVisual to a BitmapSource
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                originalImage.Width, originalImage.Height, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);

            // Assign the BitmapSource to the Image control
            //img.Source = renderBitmap;
            return renderBitmap;
        }
        public static void SaveClipboardImageToFile(BitmapSource bitmapSource,string filePath)
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }
        public  BitmapSource WrapImage(Mat srcImg, Mat homography, System.Windows.Size outputSize)
        {
            Mat wrappedImg = new Mat();
            Cv2.WarpPerspective(srcImg, wrappedImg, homography, new OpenCvSharp.Size(outputSize.Width, outputSize.Height));

            // Convert wrappedImg to BitmapSource
            return BitmapSourceConverter.ToBitmapSource(wrappedImg);
        }
    }
}

