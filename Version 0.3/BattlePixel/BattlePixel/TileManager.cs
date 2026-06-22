using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    public class TileManager
    {
        private readonly Canvas _canvas;                                    
        private readonly Dictionary<int, CroppedBitmap> _tileCache = new Dictionary<int, CroppedBitmap>(); // Кэш загруженных тайлов 

        public int SourceTileSize { get; }     
        public int RenderTileSize { get; }     

        // Конструктор
        public TileManager(Canvas canvas, int sourceTileSize = 32, int renderTileSize = 32)
        {
            _canvas = canvas;
            SourceTileSize = sourceTileSize;
            RenderTileSize = renderTileSize;
            LoadTiles(); //Загрузка и разбивка спрайт-листа
        }

        // Загрузка спрайта
        private void LoadTiles()
        {
            try
            {
                // Загрузка спрайта
                var uri = new Uri("pack://application:,,,/Assets/Tiles/Grassland_Terrain_47Tiles.png", UriKind.Absolute);
                var mainTileset = new BitmapImage(uri);

                int tilesPerRow = mainTileset.PixelWidth / SourceTileSize; // Количество тайлов в строке

                // Цикл: проход по тайлам
                for (int i = 0; i < 47; i++)
                {
                    int col = i % tilesPerRow; // Колонка 
                    int row = i / tilesPerRow; // Строка 

                    // Проверка помещения в изображение
                    if (row * SourceTileSize + SourceTileSize > mainTileset.PixelHeight)
                        continue;

                    // Вырезание одного тайла 
                    var rect = new Int32Rect(col * SourceTileSize, row * SourceTileSize, SourceTileSize, SourceTileSize);
                    _tileCache[i] = new CroppedBitmap(mainTileset, rect);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки тайлов: " + ex.Message);
            }
        }

        //  Размещение тайла
        public void PlaceTile(int tileId, int gridX, int gridY)
        {
            // Проверка ID тайла
            if (tileId < 0 || !_tileCache.ContainsKey(tileId))
                return;

            // Создание изображения для тайла
            var image = new Image
            {
                Source = _tileCache[tileId],
                Width = RenderTileSize,
                Height = RenderTileSize,
                SnapsToDevicePixels = true
            };

            // Включение пиксельной графики 
            RenderOptions.SetBitmapScalingMode(image, System.Windows.Media.BitmapScalingMode.NearestNeighbor);

            // Позиционирование 
            Canvas.SetLeft(image, gridX * RenderTileSize);
            Canvas.SetTop(image, gridY * RenderTileSize);
            _canvas.Children.Add(image);
        }
    }
}