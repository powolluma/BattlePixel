using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    /// <summary>
    /// Компонент полосы здоровья, отображаемый над персонажами.
    /// Использует сегментированную шкалу из спрайт-листа Health.png.
    /// Есть два варианта цвета: зеленый для игрока и оранжевый для врагов.
    /// Полоса состоит из 6 кадров: от полностью заполненной до пустой.
    /// </summary>
    public class HealthBar
    {
        // ==================== КОМПОНЕНТЫ ОТОБРАЖЕНИЯ ====================
        private readonly Canvas _canvas;        // Канвас для рендеринга
        private readonly Image _image;          // Изображение полосы здоровья
        private readonly List<CroppedBitmap> _frames; // Кадры полосы [0]=полная, [5]=пустая

        private double _renderWidth;   // Ширина рендеринга
        private double _renderHeight;  // Высота рендеринга

        // ==================== КОНСТАНТЫ СПРАЙТ-ЛИСТА ====================
        // Координаты сегментной шкалы в Health.png (256x240)
        // Зеленая - для игрока (Y=148), оранжевая - для врагов (Y=116)
        private const int FrameSourceWidth = 27;   // Ширина одного кадра
        private const int FrameSourceHeight = 8;   // Высота одного кадра
        private const int FrameStartX = 66;        // Начальная X-координата первого кадра
        private const int FrameStepX = 32;         // Шаг между кадрами по X
        private const int FrameCount = 6;          // Всего 6 кадров (100%, 80%, 60%, 40%, 20%, 0%)
        private const int GreenY = 148;            // Y-координата зеленой шкалы (для игрока)
        private const int OrangeY = 116;           // Y-координата оранжевой шкалы (для врагов)

        // ================================================================
        //  КОНСТРУКТОР
        //  Загружает спрайт-лист, выбирает нужный цвет и создает кадры
        // ================================================================
        public HealthBar(Canvas canvas, string healthSheetPath, bool isGreen,
            double renderWidth = 80, double renderHeight = 16)
        {
            _canvas = canvas;
            _renderWidth = renderWidth;
            _renderHeight = renderHeight;
            _frames = new List<CroppedBitmap>();

            // Загрузка спрайт-листа и вырезание кадров
            try
            {
                var sheet = new BitmapImage();
                sheet.BeginInit();
                sheet.UriSource = new Uri(healthSheetPath, UriKind.Absolute);
                sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.EndInit();

                // Выбор строки в зависимости от цвета
                int rowY = isGreen ? GreenY : OrangeY;
                for (int i = 0; i < FrameCount; i++)
                {
                    int x = FrameStartX + i * FrameStepX;
                    var rect = new Int32Rect(x, rowY, FrameSourceWidth, FrameSourceHeight);
                    _frames.Add(new CroppedBitmap(sheet, rect));
                }
            }
            catch { /* Спрайт не найден - полоса останется пустой */ }

            // Создание изображения для отображения
            _image = new Image
            {
                Width = renderWidth,
                Height = renderHeight,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
            if (_frames.Count > 0) _image.Source = _frames[0]; // Начальное состояние - 100%

            // Полоса здоровья отображается поверх персонажей
            Panel.SetZIndex(_image, 21);
            _canvas.Children.Add(_image);
        }

        // ================================================================
        //  ПОЗИЦИОНИРОВАНИЕ
        //  Размещает полосу здоровья над персонажем (с небольшим смещением)
        // ================================================================
        public void SetPosition(double centerX, double topY, double offsetBelow = 4)
        {
            // Центрирование по X относительно персонажа
            double left = centerX - _renderWidth / 2;
            // Размещение над головой персонажа с отступом
            double top = topY + offsetBelow;

            Canvas.SetLeft(_image, left);
            Canvas.SetTop(_image, top);
        }

        // ================================================================
        //  ОБНОВЛЕНИЕ ЗДОРОВЬЯ
        //  Вычисляет процент здоровья и выбирает соответствующий кадр
        // ================================================================
        public void UpdateHealth(int current, int max)
        {
            if (_frames.Count == 0) return;

            // Вычисление процента здоровья (от 0 до 1)
            double ratio = max > 0 ? Math.Max(0, Math.Min(1, (double)current / max)) : 0;

            // Преобразование процента в индекс кадра:
            // 100% -> кадр 0, 0% -> кадр 5 (FrameCount - 1)
            int frameIndex = FrameCount - 1 - (int)Math.Round(ratio * (FrameCount - 1));
            frameIndex = Math.Max(0, Math.Min(FrameCount - 1, frameIndex));

            // Установка соответствующего кадра
            _image.Source = _frames[frameIndex];
        }

        // ================================================================
        //  УПРАВЛЕНИЕ ВИДИМОСТЬЮ
        // ================================================================

        /// <summary>
        /// Удалить полосу здоровья с канваса
        /// </summary>
        public void Remove()
        {
            _canvas.Children.Remove(_image);
        }

        /// <summary>
        /// Показать/скрыть полосу здоровья
        /// </summary>
        public void SetVisible(bool visible)
        {
            _image.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}