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
        private readonly Dictionary<int, CroppedBitmap> _tileCache = new Dictionary<int, CroppedBitmap>();

        public int SourceTileSize { get; }

        public int RenderTileSize { get; }

        public TileManager(Canvas canvas, int sourceTileSize = 32, int renderTileSize = 32)
        {
            _canvas = canvas;
            SourceTileSize = sourceTileSize;
            RenderTileSize = renderTileSize;
            LoadTiles();
        }

        private void LoadTiles()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/Tiles/Grassland_Terrain_47Tiles.png", UriKind.Absolute);
                var mainTileset = new BitmapImage(uri);

                int tilesPerRow = mainTileset.PixelWidth / SourceTileSize;

                for (int i = 0; i < 47; i++)
                {
                    int col = i % tilesPerRow;
                    int row = i / tilesPerRow;

                    if (row * SourceTileSize + SourceTileSize > mainTileset.PixelHeight)
                        continue;

                    var rect = new Int32Rect(col * SourceTileSize, row * SourceTileSize, SourceTileSize, SourceTileSize);
                    _tileCache[i] = new CroppedBitmap(mainTileset, rect);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки тайлов: " + ex.Message);
            }
        }

        public void PlaceTile(int tileId, int gridX, int gridY)
        {
            if (tileId < 0 || !_tileCache.ContainsKey(tileId))
                return;

            var image = new Image
            {
                Source = _tileCache[tileId],
                Width = RenderTileSize,
                Height = RenderTileSize,
                SnapsToDevicePixels = true
            };

            RenderOptions.SetBitmapScalingMode(image, System.Windows.Media.BitmapScalingMode.NearestNeighbor);

            Canvas.SetLeft(image, gridX * RenderTileSize);
            Canvas.SetTop(image, gridY * RenderTileSize);
            _canvas.Children.Add(image);
        }
    }
}