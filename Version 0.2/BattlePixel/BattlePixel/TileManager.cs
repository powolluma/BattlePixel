using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    /// <summary>
    /// Менеджер тайлов. Отвечает за загрузку, кэширование и размещение
    /// тайлов из спрайт-листа на игровой карте.
    /// Использует пиксельную графику с ближайшим соседом для масштабирования.
    /// </summary>
    public class TileManager
    {
        private readonly Canvas _canvas;                                    // Канвас для рендеринга тайлов
        private readonly Dictionary<int, CroppedBitmap> _tileCache =        // Кэш загруженных тайлов
            new Dictionary<int, CroppedBitmap>();

        public int SourceTileSize { get; }     // Размер тайла в исходной текстуре (в пикселях)
        public int RenderTileSize { get; }     // Размер тайла при рендеринге (в пикселях)

        // ================================================================
        //  КОНСТРУКТОР
        //  Загружает спрайт-лист и разбивает его на отдельные тайлы
        // ================================================================
        public TileManager(Canvas canvas, int sourceTileSize = 32, int renderTileSize = 32)
        {
            _canvas = canvas;
            SourceTileSize = sourceTileSize;
            RenderTileSize = renderTileSize;
            LoadTiles(); // Загрузка и разбивка спрайт-листа
        }

        // ================================================================
        //  ЗАГРУЗКА СПРАЙТ-ЛИСТА
        //  Загружает файл Grassland_Terrain_47Tiles.png и разбивает его
        //  на отдельные тайлы размером SourceTileSize x SourceTileSize
        //  (47 тайлов в одном файле)
        // ================================================================
        private void LoadTiles()
        {
            try
            {
                // Загрузка спрайт-листа из ресурсов приложения (pack URI)
                var uri = new Uri("pack://application:,,,/Assets/Tiles/Grassland_Terrain_47Tiles.png", UriKind.Absolute);
                var mainTileset = new BitmapImage(uri);

                int tilesPerRow = mainTileset.PixelWidth / SourceTileSize; // Количество тайлов в строке

                // Проход по всем 47 тайлам
                for (int i = 0; i < 47; i++)
                {
                    int col = i % tilesPerRow; // Колонка в спрайт-листе
                    int row = i / tilesPerRow; // Строка в спрайт-листе

                    // Проверка, что тайл помещается в изображение
                    if (row * SourceTileSize + SourceTileSize > mainTileset.PixelHeight)
                        continue;

                    // Вырезание одного тайла из спрайт-листа
                    var rect = new Int32Rect(col * SourceTileSize, row * SourceTileSize, SourceTileSize, SourceTileSize);
                    _tileCache[i] = new CroppedBitmap(mainTileset, rect);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки тайлов: " + ex.Message);
            }
        }

        // ================================================================
        //  РАЗМЕЩЕНИЕ ТАЙЛА НА КАРТЕ
        //  Создает изображение тайла и размещает его в указанной
        //  сеточной позиции на канвасе
        // ================================================================
        public void PlaceTile(int tileId, int gridX, int gridY)
        {
            // Проверка валидности ID тайла
            if (tileId < 0 || !_tileCache.ContainsKey(tileId))
                return;

            // Создание элемента изображения для тайла
            var image = new Image
            {
                Source = _tileCache[tileId],
                Width = RenderTileSize,
                Height = RenderTileSize,
                SnapsToDevicePixels = true
            };

            // Включение пиксельной графики (без сглаживания)
            RenderOptions.SetBitmapScalingMode(image, System.Windows.Media.BitmapScalingMode.NearestNeighbor);

            // Позиционирование на канвасе по сетке
            Canvas.SetLeft(image, gridX * RenderTileSize);
            Canvas.SetTop(image, gridY * RenderTileSize);
            _canvas.Children.Add(image);
        }
    }
}