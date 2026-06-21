using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    /// <summary>
    /// Описание декоративного объекта: путь к файлу текстуры и размеры в пикселях.
    /// Используется для хранения данных о декорациях перед размещением на карте.
    /// </summary>
    public class DecorationInfo
    {
        public string FilePath { get; } // Путь к файлу текстуры
        public int Width { get; }      // Ширина текстуры в пикселях
        public int Height { get; }     // Высота текстуры в пикселях

        public DecorationInfo(string filePath, int width, int height)
        {
            FilePath = filePath;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Менеджер декораций. Отвечает за размещение декоративных объектов
    /// на игровой карте (цветы, камни, трава, порталы и т.д.).
    /// Декорации размещаются на заднем плане и не взаимодействуют с игроком.
    /// </summary>
    public class DecorationManager
    {
        private readonly Canvas _canvas;     // Канвас для размещения декораций
        private readonly Random _random = new Random(); // Генератор случайных чисел

        public DecorationManager(Canvas canvas)
        {
            _canvas = canvas;
        }

        // ================================================================
        //  РАЗМЕЩЕНИЕ ДЕКОРАЦИИ НА ЗЕМЛЕ
        //  Размещает объект так, чтобы его нижняя часть совпадала с уровнем земли
        // ================================================================
        /// <summary>
        /// Размещает декорацию на указанной позиции по X, привязанной к уровню земли.
        /// </summary>
        /// <param name="decoration">Данные о декорации</param>
        /// <param name="x">X-координата размещения</param>
        /// <param name="groundY">Y-координата верхней границы пола (groundRow * tileSize)</param>
        public void PlaceOnGround(DecorationInfo decoration, double x, double groundY)
        {
            // Создание элемента изображения
            var image = new Image
            {
                Source = LoadBitmap(decoration.FilePath),
                Width = decoration.Width,
                Height = decoration.Height,
                SnapsToDevicePixels = true
            };

            // Включение пиксельной графики (без сглаживания)
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

            // Позиционирование: низ изображения совпадает с уровнем земли
            Canvas.SetLeft(image, x);
            Canvas.SetTop(image, groundY - decoration.Height);
            Panel.SetZIndex(image, 0); // Декорации на заднем плане (ниже персонажей)
            _canvas.Children.Add(image);
        }

        // ================================================================
        //  СЛУЧАЙНОЕ РАЗМЕЩЕНИЕ ДЕКОРАЦИЙ
        //  Размещает несколько случайных декораций из списка вариантов
        //  в заданном диапазоне по X
        // ================================================================
        /// <summary>
        /// Размещает случайные декорации в указанном диапазоне координат X.
        /// </summary>
        /// <param name="variants">Список вариантов декораций</param>
        /// <param name="count">Количество размещаемых объектов</param>
        /// <param name="minX">Минимальная X-координата</param>
        /// <param name="maxX">Максимальная X-координата</param>
        /// <param name="groundY">Y-координата уровня земли</param>
        public void ScatterRandom(List<DecorationInfo> variants, int count, double minX, double maxX, double groundY)
        {
            for (int i = 0; i < count; i++)
            {
                // Выбор случайного варианта декорации из списка
                var variant = variants[_random.Next(variants.Count)];
                // Случайная позиция по X в заданном диапазоне
                double x = minX + _random.NextDouble() * (maxX - minX);
                PlaceOnGround(variant, x, groundY);
            }
        }

        // ================================================================
        //  ЗАГРУЗКА ИЗОБРАЖЕНИЯ
        //  Загружает текстуру из файла с кэшированием
        // ================================================================
        private BitmapImage LoadBitmap(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // Кэширование для быстрой загрузки
            bitmap.EndInit();
            return bitmap;
        }
    }
}