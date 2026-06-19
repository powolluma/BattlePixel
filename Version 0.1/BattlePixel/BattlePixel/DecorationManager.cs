using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    // Описание одного типа декорации: путь к файлу + реальный размер картинки в пикселях.
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

    public class DecorationManager
    {
        private readonly Canvas _canvas;
        private readonly Random _random = new Random();

        public DecorationManager(Canvas canvas)
        {
            _canvas = canvas;
        }

        // groundY — Y-координата верхней границы пола (groundRow * renderTileSize),
        // декорация ставится так, чтобы её низ совпадал с полом.
        public void PlaceOnGround(DecorationInfo decoration, double x, double groundY)
        {
            var image = new Image
            {
                Source = LoadBitmap(decoration.FilePath),
                Width = decoration.Width,
                Height = decoration.Height,
                SnapsToDevicePixels = true
            };

            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

            Canvas.SetLeft(image, x);
            Canvas.SetTop(image, groundY - decoration.Height);
            Panel.SetZIndex(image, 0); // декорации — задний план
            _canvas.Children.Add(image);
        }

        // Расставляет несколько декораций одного типа случайно в заданном диапазоне x.
        public void ScatterRandom(List<DecorationInfo> variants, int count, double minX, double maxX, double groundY)
        {
            for (int i = 0; i < count; i++)
            {
                var variant = variants[_random.Next(variants.Count)];
                double x = minX + _random.NextDouble() * (maxX - minX);
                PlaceOnGround(variant, x, groundY);
            }
        }

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