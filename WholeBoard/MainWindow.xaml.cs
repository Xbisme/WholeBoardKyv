
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WholeBoard
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SURF surf;
        private Bitmap originalImage;
        private List<SURF.FeaturePoint> features;
        public ObservableCollection<Item> Items { get; set; }
        public MainWindow()
        {

            InitializeComponent();
            surf = new SURF();
        }

        public class Item
        {
            public string? Name { get; set; }
        }
        private void Load_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.ValidateNames = false;
            openFileDialog.CheckFileExists = false;
            openFileDialog.CheckPathExists = true;
            String first_totalImage = "Total Image: ";
            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = Path.GetDirectoryName(openFileDialog.FileName);
                selectPath.Text = selectedPath;
                string[] files = Directory.GetFiles(selectedPath, "*.png");
                int itemCount = files.Length;
                imageItems.ItemsSource = files;
                totalImage.Text = first_totalImage + itemCount.ToString();
            }
        }
        private void Execute_Click(object sender, RoutedEventArgs e)
        {
            scale_box.Text = "100";
            //img.Source = new BitmapImage(new Uri("C:\\Users\\acer\\Pictures\\Nitro\\2.jpg"));


            //Load_Image();
            Image_Loaded( sender,  e);
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
        private void Image_Loaded(object sender, RoutedEventArgs e)
        {
        
            
            string imagePath = "C:\\Users\\acer\\Pictures\\Nitro\\2.jpg";

            // Load the image
            originalImage = new Bitmap(imagePath);

            // Detect features
            features = surf.DetectFeatures(originalImage);

            //// Draw the features on the image
            //DrawFeaturesOnImage();

        }

        private void DrawFeaturesOnImage()
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
                drawingContext.DrawImage(bitmapSource, new Rect(0, 0, bitmapSource.PixelWidth, bitmapSource.PixelHeight));

                // Draw circles or markers at the feature coordinates
                int radius = 5; // Adjust the radius as needed

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
            img.Source = renderBitmap;
        }
    }
}
