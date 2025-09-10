using System.Windows.Media;

namespace MusicalChart
{
    public class Track
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Brush ColorPath { get; set; }

        public Track(double x, double y, Color color)
        {
            X = x;
            Y = y;
            ColorPath = new SolidColorBrush(color);
        }
    }
}