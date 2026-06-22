using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    public class DecorationInfo
    {
        public string FilePath { get; }
        public int Width { get; }    
        public int Height { get; }    

        public DecorationInfo(string filePath, int width, int height)
        {
            FilePath = filePath;
            Width = width;
            Height = height;
        }
    }

    // Менеджер декораций
    public class DecorationManager
    {
        private readonly Canvas _canvas;   
        private readonly Random _random = new Random(); 

        public DecorationManager(Canvas canvas)
        {
            _canvas = canvas;
        }

        // Размещение декораций
        public void PlaceOnGround(DecorationInfo decoration, double x, double groundY)
        {
            // Создание изображения
            var image = new Image
            {
                Source = LoadBitmap(decoration.FilePath),
                Width = decoration.Width,
                Height = decoration.Height,
                SnapsToDevicePixels = true
            };

            // Включение пиксельной графики 
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

            Canvas.SetLeft(image, x);
            Canvas.SetTop(image, groundY - decoration.Height);
            Panel.SetZIndex(image, 0); // Декорации на заднем плане 
            _canvas.Children.Add(image);
        }

        //  Случайное размещение
        public void ScatterRandom(List<DecorationInfo> variants, int count, double minX, double maxX, double groundY)
        {
            for (int i = 0; i < count; i++)
            {
                var variant = variants[_random.Next(variants.Count)];
                double x = minX + _random.NextDouble() * (maxX - minX);
                PlaceOnGround(variant, x, groundY);
            }
        }

        //  Загрузка изображения
        private BitmapImage LoadBitmap(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; 
            bitmap.EndInit();
            return bitmap;
        }
    }
}