using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    public class InventoryManager
    {
        private readonly string _spriteSheetPath;
        private readonly CroppedBitmap _slotTexture;

        // Координаты ячейки взяты из самого левого верхнего (коричневого) варианта Inventory.png
        private const int SlotSourceX = 0;
        private const int SlotSourceY = 79;
        private const int SlotSourceSize = 24;

        public InventoryManager(string inventorySheetPath)
        {
            _spriteSheetPath = inventorySheetPath;

            var sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.UriSource = new Uri(_spriteSheetPath, UriKind.Absolute);
            sheet.CacheOption = BitmapCacheOption.OnLoad;
            sheet.EndInit();

            var rect = new Int32Rect(SlotSourceX, SlotSourceY, SlotSourceSize, SlotSourceSize);
            _slotTexture = new CroppedBitmap(sheet, rect);
        }

        // Заполняет UniformGrid пустыми ячейками инвентаря
        public void BuildSlots(UniformGrid grid, int slotRenderSize = 64)
        {
            grid.Children.Clear();

            for (int i = 0; i < grid.Columns * grid.Rows; i++)
            {
                var image = new Image
                {
                    Source = _slotTexture,
                    Width = slotRenderSize,
                    Height = slotRenderSize,
                    Margin = new Thickness(2),
                    SnapsToDevicePixels = true
                };

                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                grid.Children.Add(image);
            }
        }
    }
}