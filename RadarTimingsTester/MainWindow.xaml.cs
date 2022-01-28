using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RadarTimingsTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public enum WeaponClass
        {
            Knife,
            Pistol,
            Smg,
            Rifle
        }
        private enum DrawingMode { Freehand, Waypoint }
        public static Dictionary<WeaponClass, int> RunSpeeds;

        private class PathData
        {
            public SolidColorBrush Color;
            public List<Line> Lines = new List<Line>();

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                var distance = GetLineDistance(Lines);

                foreach (var kvp in RunSpeeds)
                {
                    _ = sb.Append($"    {kvp.Key}: {distance / kvp.Value:0.00s}");
                }
                return sb.ToString();
            }
        }

        private readonly string VERSION = "0.4";

        private bool pathDrawOngoing = false;
        private Point currentPoint = new Point();
        private double currentTime = 0d;
        private SolidColorBrush currentColor = new SolidColorBrush();
        private BitmapSource generalImage;
        private DrawingMode drawingMode = DrawingMode.Freehand;
        private int selectedIndex = -1;
        private object selectedObj = null;

        private List<PathData> paths = new List<PathData>();

        //Map Data
        private static float mapScale = 1;

        public MainWindow()
        {
            InitializeComponent();

            PopulateSpeeds();
            //Not currently using, but may in the future.
            DrawOptions.Items.Add("Freehand");
            DrawOptions.Items.Add("Waypoint");
            DrawOptions.SelectedIndex = 0;

            MainWin.Title = "Radar Timings Tester (RTT) [Version " + VERSION +"]";
        }

        private void PopulateSpeeds()
        {
            RunSpeeds = new Dictionary<WeaponClass, int>();
            RunSpeeds.Add(WeaponClass.Knife, 250);
            RunSpeeds.Add(WeaponClass.Pistol, 240);
            RunSpeeds.Add(WeaponClass.Smg, 230);
            RunSpeeds.Add(WeaponClass.Rifle, 220);
        }

        private void LoadMapData(string path)
        {
            if (File.Exists(path))
            {
                string[] txtData = File.ReadAllLines(path);

                foreach (string line in txtData)
                {
                    if (line.Contains("scale"))
                    {
                        string val = line.Split('"')[3];
                        val = val.Replace('.', ',');
                        mapScale = float.Parse(val);
                        break;
                    }
                }
            }
        }

        private void MainCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && !pathDrawOngoing)
            {
                currentTime = 0d;
                currentPoint = Mouse.GetPosition(MainCanvas);
 
                // Define parameters used to create the BitmapSource.
                PixelFormat pf = PixelFormats.Bgr32;
                int width = 1;
                int height = 1;
                int rawStride = (width * pf.BitsPerPixel + 7) / 8;
                byte[] rawImage = new byte[rawStride * height];
            
                // Initialize the image with data.
                Random value = new Random();
                value.NextBytes(rawImage);
            
                // Create a BitmapSource.
                generalImage = BitmapSource.Create(width, height,
                    10, 10, pf, null,
                    rawImage, rawStride);
            
                //Format is Bgr, reversed of normal, so I enter it backwards
                currentColor = new SolidColorBrush(Color.FromRgb(rawImage[2], rawImage[1], rawImage[0]));

                paths.Add(new PathData()
                {
                    Color = currentColor,
                    Lines = new List<Line>()
                });

                pathDrawOngoing = true;
            }
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && pathDrawOngoing)
            {
                Line line = new Line();

                var newPoint = Mouse.GetPosition(MainCanvas);

                line.Stroke = currentColor;
                line.StrokeThickness = 3;
                line.X1 = currentPoint.X;
                line.Y1 = currentPoint.Y;
                line.X2 = newPoint.X;
                line.Y2 = newPoint.Y;

                currentTime += CalculateTime(CorrectedDistance(line), WeaponClass.Knife);

                PathInfoLabel.Content = $"Current Distance: {currentTime.ToString("0.00s")}";

                currentPoint = newPoint;
            
                paths[paths.Count - 1].Lines.Add(new Line()
                {
                    Stroke = currentColor,
                    StrokeThickness = 3,
                    X1 = line.X1,
                    X2 = line.X2,
                    Y1 = line.Y1,
                    Y2 = line.Y2
                });
            
                MainCanvas.Children.Add(line);
            }
        }

        private void MainCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && paths.Count > 0 && pathDrawOngoing)
            {
                pathDrawOngoing = false;
                PathInfoLabel.Content = string.Empty;

                StackPanel panel = new StackPanel()
                {
                    Orientation = Orientation.Horizontal
                };

                Image myImage = new Image();
                myImage.Width = 10;
                myImage.Source = generalImage;

                panel.Children.Add(myImage);
                panel.Children.Add(new TextBlock() { Text = paths[paths.Count - 1].ToString() });
                ListOfLines.Items.Add(panel);
            }
        }

        private void ListOfLines_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var view = (sender as ListView);

            if (view.SelectedItem != null)
            {
                selectedObj = view.SelectedItem;

                int index = view.SelectedIndex;
                selectedIndex = index;

                if(selectedIndex != -1)
                {
                    MainCanvas.Children.Clear();

                    //Outline selected path
                    var halfOfTotalDistance = GetLineDistance(paths[selectedIndex].Lines) / 2;

                    var distanceTravelled = 0d;
                    for (int i = 0; i < paths[selectedIndex].Lines.Count; i++)
                    {
                        Line outline = new Line();
                        outline.X1 = paths[selectedIndex].Lines[i].X1;
                        outline.X2 = paths[selectedIndex].Lines[i].X2;
                        outline.Y1 = paths[selectedIndex].Lines[i].Y1;
                        outline.Y2 = paths[selectedIndex].Lines[i].Y2;
                        outline.Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255));

                        outline.StrokeThickness = 6;

                        if (distanceTravelled <= halfOfTotalDistance)
                        {
                            distanceTravelled += CorrectedDistance(outline);
                            if (distanceTravelled > halfOfTotalDistance)
                            {
                                Ellipse midCircle = new Ellipse() {
                                    Width = 20, 
                                    Height = 20,
                                    Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                                    Margin = new Thickness (outline.X1 - 10, outline.Y1 - 10, 0, 0)
                                };
                                MainCanvas.Children.Add(midCircle);
                            }
                        }

                        MainCanvas.Children.Add(outline);
                    }
                }

                RedrawLines(false);
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (selectedIndex != -1)
            {
                ListOfLines.Items.Remove(selectedObj);
                paths.RemoveAt(selectedIndex);

                MainCanvas.Children.Clear();

                RedrawLines();

                selectedIndex = -1;
                selectedObj = null;
            }
        }

        private void RedrawLines(bool clear = true)
        {
            if(clear)
            {
                MainCanvas.Children.Clear();
            }

            for (int i = 0; i < paths.Count; i++)
            {
                for (int j = 0; j < paths[i].Lines.Count; j++)
                {
                    MainCanvas.Children.Add(paths[i].Lines[j]);
                }
            }
        }

        private void LoadMapButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "dds files (*.dds)|*.dds|png files (*.png)|*.png|All files (*.*)|*.*";
            fileDialog.RestoreDirectory = true;
            if(fileDialog.ShowDialog() == true)
            {
                paths.Clear();
                MainCanvas.Children.Clear();
                ListOfLines.Items.Clear();

                string path = fileDialog.FileName;
                string ext = System.IO.Path.GetExtension(path);

                //Manually load the image data into a byte[]. We do this so we can close the stream without holding onto the reference.
                Stream imageStreamSource = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] bytes = new byte[imageStreamSource.Length + 10];
                int toRead = (int)imageStreamSource.Length;
                int alreadyRead = 0;
                do
                {
                    int n = imageStreamSource.Read(bytes, alreadyRead, 10);
                    alreadyRead += n;
                    toRead -= n;
                } while (toRead > 0);
                imageStreamSource.Close();

                //Do that shit with whatever and make it show up. You get it.
                using(var stream = new MemoryStream(bytes))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    ImageBrush b = new ImageBrush(bitmap);
                    MainCanvas.Background = b;
                }

                string txtPath = path.Replace("_radar" + ext, ".txt");
                int layerN = 1;
                while(txtPath.Contains("_layer"))
                {
                    txtPath = txtPath.Replace("_layer" + layerN, string.Empty);
                    layerN++;

                    if(layerN > 50)
                    {
                        break; //Infinite loop safety check
                    }
                }

                //In case someone adds an invalid image it wont be able to find the .txt
                //Might provide a manual method of passing the .txt in the future
                if(File.Exists(txtPath) && txtPath.Contains(".txt"))
                {
                    LoadMapData(txtPath);
                    ErrorText.Visibility = Visibility.Hidden;
                }
                else
                {
                    ErrorText.Visibility = Visibility.Visible;
                    ErrorText.Text = "Could not find .txt file associated with radar. Timings will not be accurate. Please report this to 7ark.";
                }
            }
        }

        //Static utility functions
        public static double Distance(double x1, double y1, double x2, double y2)
        {
            double xPoints = Math.Pow((x2 - x1), 2.0);
            double yPoints = Math.Pow((y2 - y1), 2.0);

            return Math.Sqrt(xPoints + yPoints);
        }
        public static double CorrectedDistance(Line line)
        {
            return CorrectedDistance(line.X1, line.X2, line.Y1, line.Y2);
        }
        public static double CorrectedDistance(double x1, double y1, double x2, double y2)
        {
            float scale = mapScale;

            //Getting this, since a normal radar is 1024 x 1024, but this one is only 600 x 600
            float mod = 1024f / 600f;

            float correction = mod * scale;

            return Distance(x1 * correction, x2 * correction, y1 * correction, y2 * correction);
        }

        public static double GetLineDistance(List<Line> lines)
        {
            double totalDistance = 0;

            foreach(var line in lines)
            {
                //Adjusting everything to world position, or close enough to where its mostly accurate
                totalDistance += CorrectedDistance(line);
            }
            return totalDistance;
        }

        private void RemoveAll_Click(object sender, RoutedEventArgs e)
        {
            paths.Clear();
            ListOfLines.Items.Clear();
            RedrawLines();
            selectedIndex = -1;
            selectedObj = null;
        }
        public static double CalculateTime(double distance, WeaponClass wc) 
        {
            //Adjust to get timings when running with a knife, maybe I'll add more weapons in the future.
            return distance / RunSpeeds[wc];
        }
    }
}
