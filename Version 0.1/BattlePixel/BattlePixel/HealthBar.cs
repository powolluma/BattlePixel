using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    public class HealthBar
    {
        private readonly Image _image;

        // Чуть расширили рамку кропа, чтобы не срезать угловые пиксели рамки полоски
        private const int GreenX = 5, GreenY = 36, BarWidth = 39, BarHeight = 9;
        private const int RedX = 5, RedY = 68;

        public HealthBar(Canvas canvas, string healthSheetPath, bool isGreen,
            double renderWidth = 80, double renderHeight = 18)
        {
            var sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.UriSource = new Uri(healthSheetPath, UriKind.Absolute);
            sheet.CacheOption = BitmapCacheOption.OnLoad;
            sheet.EndInit();

            var rect = isGreen
                ? new Int32Rect(GreenX, GreenY, BarWidth, BarHeight)
                : new Int32Rect(RedX, RedY, BarWidth, BarHeight);

            var cropped = new CroppedBitmap(sheet, rect);

            _image = new Image
            {
                Source = cropped,
                Width = renderWidth,
                Height = renderHeight,
                SnapsToDevicePixels = true
            };

            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
            Panel.SetZIndex(_image, 20);
            canvas.Children.Add(_image);
        }

        public void SetPosition(double centerX, double topY, double offsetAboveHead = 2)
        {
            Canvas.SetLeft(_image, centerX - _image.Width / 2);
            Canvas.SetTop(_image, topY - _image.Height - offsetAboveHead);
        }
    }
}