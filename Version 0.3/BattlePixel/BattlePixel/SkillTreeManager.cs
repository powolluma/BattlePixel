using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    public class SkillBranch
    {
        public double AngleDegrees { get; } // Угол направления ветки 
        public string[] IconPaths { get; }  

        public SkillBranch(double angleDegrees, string[] iconPaths)
        {
            AngleDegrees = angleDegrees;
            IconPaths = iconPaths;
        }
    }

    public class SkillDefinition
    {
        public string Name { get; set; }               
        public int Cost { get; set; } = 10;            
        public double DamageBonus { get; set; } = 0.0; 
        public double HpBonus { get; set; } = 0.0;     
        public bool Unlocked { get; set; } = false;    
        public string IconPath { get; set; }          
    }

    public class SkillTreeManager
    {
        // Ресурсыы
        private readonly CroppedBitmap _slotTexture; // Текстура фоновой ячейки навыка

        // расположения текстуры 
        private const int SlotSourceX = 0;
        private const int SlotSourceY = 79;
        private const int SlotSourceSize = 24;

        // Список всех навыков 
        public List<SkillDefinition> Skills { get; } = new List<SkillDefinition>();

        // Событие при прокачке навыка
        public event Action<SkillDefinition> OnSkillUnlocked;

        // Обработка двойного клика
        private DateTime _lastClickTime = DateTime.MinValue; // Время последнего клика
        private Image _lastClickedImage;                     // Последний кликнутый элемент
        private const double DoubleClickMs = 400;            // Максимальный интервал между кликами

        // Конструктор
        public SkillTreeManager(string inventorySheetPath)
        {
            // Загрузка спрайта
            var sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.UriSource = new Uri(inventorySheetPath, UriKind.Absolute);
            sheet.CacheOption = BitmapCacheOption.OnLoad;
            sheet.EndInit();

            // Вырезание текстуры ячейки
            _slotTexture = new CroppedBitmap(sheet,
                new Int32Rect(SlotSourceX, SlotSourceY, SlotSourceSize, SlotSourceSize));
        }

        // Перегрузка конструктора 
        public SkillTreeManager(Canvas _, string inventorySheetPath) : this(inventorySheetPath) { }

        // Построение древа
        public void BuildTree(Canvas canvas, List<SkillBranch> branches,
            double centerX, double centerY,
            double slotRenderSize = 64, double spacing = 80, double startRadius = 70)
        {
            canvas.Children.Clear(); 
            Skills.Clear();         

            // Центральная ячейка
            AddSlot(canvas, centerX - slotRenderSize / 2, centerY - slotRenderSize / 2, slotRenderSize, null, null);

            // Построение каждой ветки
            foreach (var branch in branches)
            {
                double rad = branch.AngleDegrees * Math.PI / 180.0;
                double dirX = Math.Cos(rad); // X
                double dirY = Math.Sin(rad); // Y

                // Создание навыков в ветке 
                for (int i = 0; i < branch.IconPaths.Length; i++)
                {
                    // Расчет позиции слота
                    double radius = startRadius + i * spacing;
                    double x = centerX + dirX * radius - slotRenderSize / 2;
                    double y = centerY + dirY * radius - slotRenderSize / 2;

                    // Автоматическое создание определения навыка
                    string iconPath = branch.IconPaths[i];
                    string rawName = System.IO.Path.GetFileNameWithoutExtension(iconPath ?? "");
                    var def = new SkillDefinition
                    {
                        Name = rawName,
                        IconPath = iconPath,
                        Cost = 10 + i * 5, 
                        DamageBonus = rawName.Contains("slash") || rawName.Contains("dragon") ? 0.1 + i * 0.05 : 0,
                        HpBonus = rawName.Contains("heal") || rawName.Contains("plant") ? 0.1 + i * 0.05 : 0,
                    };
                    Skills.Add(def);
                    AddSlot(canvas, x, y, slotRenderSize, iconPath, def);
                }
            }
        }

        // Добавление слота
        private void AddSlot(Canvas canvas, double x, double y, double size, string iconPath, SkillDefinition def)
        {
            var slotImg = new Image
            {
                Source = _slotTexture,
                Width = size,
                Height = size,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(slotImg, BitmapScalingMode.NearestNeighbor);
            Canvas.SetLeft(slotImg, x);
            Canvas.SetTop(slotImg, y);
            Panel.SetZIndex(slotImg, 0);
            canvas.Children.Add(slotImg);

            if (iconPath == null) return;

            double iconSize = size * 0.7; 
            Image iconImg = null;

            try
            {
                // Загрузка иконки навыка
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(iconPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();

                // Создание изображения иконки
                iconImg = new Image
                {
                    Source = bmp,
                    Width = iconSize,
                    Height = iconSize,
                    SnapsToDevicePixels = true,
                    Opacity = def.Unlocked ? 1.0 : 0.45, 
                };
                RenderOptions.SetBitmapScalingMode(iconImg, BitmapScalingMode.NearestNeighbor);
            }
            catch
            {
                return; // Иконка не загружена - выходим
            }

            // Позиционирование иконки
            Canvas.SetLeft(iconImg, x + (size - iconSize) / 2);
            Canvas.SetTop(iconImg, y + (size - iconSize) / 2);
            Panel.SetZIndex(iconImg, 1);
            canvas.Children.Add(iconImg);

            // Обработка двойного клика 
            var capturedDef = def;
            var capturedImg = iconImg;
            iconImg.MouseLeftButtonDown += (s, e) =>
            {
                // Определение двойного клика
                var now = DateTime.UtcNow;
                bool isDouble = (now - _lastClickTime).TotalMilliseconds < DoubleClickMs
                                && _lastClickedImage == capturedImg;
                _lastClickTime = now;
                _lastClickedImage = capturedImg;

                // Прокачка при двойном клике на неоткрытом навыке
                if (isDouble && !capturedDef.Unlocked)
                {
                    capturedDef.Unlocked = true;
                    OnSkillUnlocked?.Invoke(capturedDef);

                    // Обновление прозрачности после проверки
                    capturedImg.Opacity = capturedDef.Unlocked ? 1.0 : 0.45;
                }
            };
        }
    }
}