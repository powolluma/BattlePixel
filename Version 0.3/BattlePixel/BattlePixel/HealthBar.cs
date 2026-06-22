using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    public class HealthBar
    {
        // Компоннты отображения
        private readonly Canvas _canvas;       
        private readonly Image _image;        
        private readonly List<CroppedBitmap> _frames; // Кадры полосы

        private double _renderWidth;  
        private double _renderHeight; 

        // Координаты 

        private const int FrameSourceWidth = 27;   // Ширина одного кадра
        private const int FrameSourceHeight = 8;   // Высота одного кадра
        private const int FrameStartX = 66;        // Начальная X-координата первого кадра
        private const int FrameStepX = 32;         // Шаг между кадрами по X
        private const int FrameCount = 6;         
        private const int GreenY = 148;            // Y зеленой шкалы
        private const int OrangeY = 116;           // Y оранжевой шкалы

        // Конструктор
        public HealthBar(Canvas canvas, string healthSheetPath, bool isGreen,
            double renderWidth = 80, double renderHeight = 16)
        {
            _canvas = canvas;
            _renderWidth = renderWidth;
            _renderHeight = renderHeight;
            _frames = new List<CroppedBitmap>();

            // Загрузка спрайта
            try
            {
                var sheet = new BitmapImage();
                sheet.BeginInit();
                sheet.UriSource = new Uri(healthSheetPath, UriKind.Absolute);
                sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.EndInit();

                // Выбор строки 
                int rowY = isGreen ? GreenY : OrangeY;
                for (int i = 0; i < FrameCount; i++)
                {
                    int x = FrameStartX + i * FrameStepX;
                    var rect = new Int32Rect(x, rowY, FrameSourceWidth, FrameSourceHeight);
                    _frames.Add(new CroppedBitmap(sheet, rect));
                }
            }
            catch { /* Спрайт не найден */ }

            // Создание изображения
            _image = new Image
            {
                Width = renderWidth,
                Height = renderHeight,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
            if (_frames.Count > 0) _image.Source = _frames[0]; // Начальное состояние - 100%

            Panel.SetZIndex(_image, 21);
            _canvas.Children.Add(_image);
        }

        // Позиция
        public void SetPosition(double centerX, double topY, double offsetBelow = 4)
        {
            double left = centerX - _renderWidth / 2;
            double top = topY + offsetBelow;

            Canvas.SetLeft(_image, left);
            Canvas.SetTop(_image, top);
        }

        // Обновление здоровья
        public void UpdateHealth(int current, int max)
        {
            if (_frames.Count == 0) return;

            double ratio = max > 0 ? Math.Max(0, Math.Min(1, (double)current / max)) : 0;

            int frameIndex = FrameCount - 1 - (int)Math.Round(ratio * (FrameCount - 1));
            frameIndex = Math.Max(0, Math.Min(FrameCount - 1, frameIndex));

            // Установка  кадра
            _image.Source = _frames[frameIndex];
        }

        // Управление видимостью
        public void Remove()
        {
            _canvas.Children.Remove(_image);
        }

        // показать или скрыть полосу здоровья
        public void SetVisible(bool visible)
        {
            _image.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}