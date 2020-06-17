using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RadarTimingsTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private enum DrawingMode { Freehand, Waypoint }
        private const double RUN_SPEED_WITH_KNIFE = 250;
        private class PathData
        {
            public SolidColorBrush color;
            public List<Line> lines = new List<Line>();
            private double time = 0;
            public double Time { get { return time; } }

            public void CalculateTime()
            {
                float scale = mapScale;

                //Getting this, since a normal radar is 1024 x 1024, but this one is only 600 x 600
                float mod = 1024f / 600f;

                double totalDistance = 0;
                for (int i = 0; i < lines.Count; i++)
                {
                    //Adjusting everything to world position, or close enough to where its mostly accurate
                    double x1 = (lines[i].X1 * mod * scale) + mapPosX;
                    double x2 = (lines[i].X2 * mod * scale) + mapPosX;
                    double y1 = (lines[i].Y1 * mod * scale) + mapPosY;
                    double y2 = (lines[i].Y2 * mod * scale) + mapPosY;
                    double dist = Distance(x1, y1, x2, y2);
                    totalDistance += dist;
                }

                //Adjust to get timings when running with a knife, maybe I'll add more weapons in the future.
                time = totalDistance / RUN_SPEED_WITH_KNIFE;
            }

            public override string ToString()
            {
                return "    Time: " + time.ToString("0.0000s");
            }
        }

        private bool ready = false;
        private Point currentPoint = new Point();
        private SolidColorBrush currentColor = new SolidColorBrush();
        private BitmapSource generalImage;
        private DrawingMode drawingMode = DrawingMode.Freehand;
        private int selectedIndex = -1;
        private object selectedObj = null;

        private List<PathData> paths = new List<PathData>();

        //Map Data
        private static float mapPosX = 10;
        private static float mapPosY = 10;
        private static float mapScale = 1;

        public MainWindow()
        {
            InitializeComponent();

            //Not currently using, but may in the future.
            DrawOptions.Items.Add("Freehand");
            DrawOptions.Items.Add("Waypoint");
            DrawOptions.SelectedIndex = 0;
        }

        private void LoadMapData(string path)
        {
            if (File.Exists(path))
            {
                List<string> test = new List<string>();
                string[] txtData = File.ReadAllLines(path);

                foreach (string line in txtData)
                {
                    if (line.Contains("pos_x"))
                    {
                        string val = line.Split('"')[3];
                        mapPosX = ConvertFromStringToFloat(val);
                    }
                    if (line.Contains("pos_y"))
                    {
                        string val = line.Split('"')[3];
                        mapPosY = ConvertFromStringToFloat(val);
                    }
                    if (line.Contains("scale"))
                    {
                        string val = line.Split('"')[3];
                        mapScale = ConvertFromStringToFloat(val);
                    }
                }
            }
        }

        /// <summary>
        /// In some cases System.Convert.ToSingle wasn't working properly, so I handle it myself
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private float ConvertFromStringToFloat(string val)
        {
            if(!val.Contains("."))
            {
                return System.Convert.ToInt32(val);
            }

            float result = 0;
            string[] halfs = val.Split('.');
            result = 0;
            result += System.Convert.ToInt32(halfs[0]);

            int index = halfs[1].Length - 1;
            while (halfs[1].Length > 0 && halfs[1][halfs[1].Length - 1] == '0')
            {
                halfs[1] = halfs[1].Substring(0, halfs[1].Length - 1);
            }

            if(halfs[1].Length > 0)
            {
                result += System.Convert.ToInt32(halfs[1]) * 0.01f;
            }

            return result;
        }

        private void MainCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                currentPoint = Mouse.GetPosition(MainCanvas);
            
                bool bad = true;
                int safety = 0;
            
                while(bad)
                {
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
            
                    bad = false;
            
                    //Check to make sure we get a unique-ish color
                    int index = GetIndexFromColor(currentColor);
                    if(index == -1)
                    {
                        break;
                    }
            
                    safety++;
                    if(safety > 1000)
                    {
                        break;
                    }
                }
            
                if (!ready)
                {
                    paths.Add(new PathData()
                    {
                        color = currentColor,
                        lines = new List<Line>()
                    });
                }
                ready = true;
            }
        }

        private void MainCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && ready)
            {
                Line line = new Line();
            
                line.Stroke = currentColor;
                line.StrokeThickness = 3;
                line.X1 = currentPoint.X;
                line.Y1 = currentPoint.Y;
                line.X2 = Mouse.GetPosition(MainCanvas).X;
                line.Y2 = Mouse.GetPosition(MainCanvas).Y;
            
                currentPoint = Mouse.GetPosition(MainCanvas);
            
                paths[paths.Count - 1].lines.Add(new Line()
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
            if (paths.Count > 0)
            {
                ready = false;

                paths[paths.Count - 1].CalculateTime();

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

        private int GetIndexFromColor(SolidColorBrush color)
        {
            int range = 10;
            for (int i = 0; i < paths.Count; i++)
            {
                bool weGood = false;
                for (int j = 0; j < range*range + 1; j++)
                {
                    if(paths[i].color.Color.R == color.Color.R + (j - range))
                    {
                        weGood = true;
                        break;
                    }
                }
                if(!weGood)
                {
                    continue;
                }
                for (int j = 0; j < range * range + 1; j++)
                {
                    if (paths[i].color.Color.G == color.Color.G + (j - range))
                    {
                        weGood = true;
                        break;
                    }
                }
                if (!weGood)
                {
                    continue;
                }
                for (int j = 0; j < range * range + 1; j++)
                {
                    if (paths[i].color.Color.B == color.Color.B + (j - range))
                    {
                        weGood = true;
                        break;
                    }
                }
                if (!weGood)
                {
                    continue;
                }
                return i;
            }
            return -1;
        }

        private int GetIndexFromTime(double time)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                if(Math.Abs(paths[i].Time - time) < 0.0001f)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ListOfLines_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = (sender as ListView).SelectedItem;
            if (item != null)
            {
                selectedObj = item;

                TextBlock block = (item as StackPanel).Children[1] as TextBlock;

                //Do a bunch of bullshit to get the time. Hoping time is unique enough of an identifier. If I get complaints/issues with this, I could probably try another method.
                int index = GetIndexFromTime(System.Convert.ToDouble(block.Text.Replace("s", string.Empty).Replace("Time:", string.Empty).Trim()));
                selectedIndex = index;

                if(selectedIndex != -1)
                {
                    MainCanvas.Children.Clear();

                    //Outline selected path
                    for (int i = 0; i < paths[selectedIndex].lines.Count; i++)
                    {
                        Line outline = new Line();
                        outline.X1 = paths[selectedIndex].lines[i].X1;
                        outline.X2 = paths[selectedIndex].lines[i].X2;
                        outline.Y1 = paths[selectedIndex].lines[i].Y1;
                        outline.Y2 = paths[selectedIndex].lines[i].Y2;
                        outline.Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                        outline.StrokeThickness = 6;

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
                for (int j = 0; j < paths[i].lines.Count; j++)
                {
                    MainCanvas.Children.Add(paths[i].lines[j]);
                }
            }
        }

        private void LoadMapButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "png files (*.png)|*.png|All files (*.*)|*.*";
            fileDialog.RestoreDirectory = true;
            if(fileDialog.ShowDialog() == true)
            {
                paths.Clear();
                MainCanvas.Children.Clear();
                ListOfLines.Items.Clear();

                string path = fileDialog.FileName;
                string ext = System.IO.Path.GetExtension(path);
                ImageBrush imageBrush = new ImageBrush();
                imageBrush.ImageSource = new BitmapImage(new Uri(path));

                MainCanvas.Background = imageBrush;

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
    }
}
