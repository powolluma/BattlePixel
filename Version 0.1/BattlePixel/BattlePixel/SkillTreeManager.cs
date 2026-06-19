using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    // Одна ветка дерева навыков: направление (угол) + 4 иконки скиллов
    public class SkillBranch
    {
        public double AngleDegrees { get; }
        public string[] IconPaths { get; } // ровно 4 пути

        public SkillBranch(double angleDegrees, string[] iconPaths)
        {
            AngleDegrees = angleDegrees;
            IconPaths = iconPaths;
        }
    }

    public class SkillTreeManager
    {
        private readonly Canvas _canvas;
        private readonly CroppedBitmap _slotTexture;

        // Та же ячейка, что и в инвентаре (левый верхний коричневый вариант Inventory.png)
        private const int SlotSourceX = 0;
        private const int SlotSourceY = 79;
        private const int SlotSourceSize = 24;

        public SkillTreeManager(string inventorySheetPath)
        {
            var sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.UriSource = new Uri(inventorySheetPath, UriKind.Absolute);
            sheet.CacheOption = BitmapCacheOption.OnLoad;
            sheet.EndInit();

            var rect = new Int32Rect(SlotSourceX, SlotSourceY, SlotSourceSize, SlotSourceSize);
            _slotTexture = new CroppedBitmap(sheet, rect);
        }

        public SkillTreeManager(Canvas canvas, string inventorySheetPath) : this(inventorySheetPath)
        {
            _canvas = canvas;
        }

        public void BuildTree(Canvas canvas, List<SkillBranch> branches,
            double centerX, double centerY,
            double slotRenderSize = 64, double spacing = 80, double startRadius = 70)
        {
            canvas.Children.Clear();

            // Центральная ячейка-ядро дерева
            AddSlot(canvas, centerX - slotRenderSize / 2, centerY - slotRenderSize / 2, slotRenderSize, null);

            foreach (var branch in branches)
            {
                double rad = branch.AngleDegrees * Math.PI / 180.0;
                double dirX = Math.Cos(rad);
                double dirY = Math.Sin(rad);

                for (int i = 0; i < branch.IconPaths.Length; i++)
                {
                    double radius = startRadius + i * spacing;
                    double x = centerX + dirX * radius - slotRenderSize / 2;
                    double y = centerY + dirY * radius - slotRenderSize / 2;

                    AddSlot(canvas, x, y, slotRenderSize, branch.IconPaths[i]);
                }
            }
        }

        private void AddSlot(Canvas canvas, double x, double y, double size, string iconPath)
        {
            var slotImage = new Image
            {
                Source = _slotTexture,
                Width = size,
                Height = size,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(slotImage, BitmapScalingMode.NearestNeighbor);

            Canvas.SetLeft(slotImage, x);
            Canvas.SetTop(slotImage, y);
            Panel.SetZIndex(slotImage, 0);
            canvas.Children.Add(slotImage);

            if (iconPath == null)
                return;

            double iconSize = size * 0.7;

            var skillBitmap = new BitmapImage();
            skillBitmap.BeginInit();
            skillBitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
            skillBitmap.CacheOption = BitmapCacheOption.OnLoad;
            skillBitmap.EndInit();

            var iconImage = new Image
            {
                Source = skillBitmap,
                Width = iconSize,
                Height = iconSize,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(iconImage, BitmapScalingMode.NearestNeighbor);

            Canvas.SetLeft(iconImage, x + (size - iconSize) / 2);
            Canvas.SetTop(iconImage, y + (size - iconSize) / 2);
            Panel.SetZIndex(iconImage, 1);
            canvas.Children.Add(iconImage);
        }
    }
}