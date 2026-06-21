using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    /// <summary>
    /// Ветка навыков в дереве навыков.
    /// Содержит угол направления и массив путей к иконкам навыков.
    /// Каждая ветка представляет собой линию навыков, исходящую из центра под определенным углом.
    /// </summary>
    public class SkillBranch
    {
        public double AngleDegrees { get; } // Угол направления ветки (в градусах)
        public string[] IconPaths { get; }  // Пути к иконкам навыков в ветке (от ближнего к дальнему)

        public SkillBranch(double angleDegrees, string[] iconPaths)
        {
            AngleDegrees = angleDegrees;
            IconPaths = iconPaths;
        }
    }

    /// <summary>
    /// Определение одного навыка для прокачки.
    /// Содержит название, стоимость в монетах, бонусы к урону и HP,
    /// а также состояние (открыт/закрыт) и путь к иконке.
    /// </summary>
    public class SkillDefinition
    {
        public string Name { get; set; }                // Название навыка
        public int Cost { get; set; } = 10;             // Стоимость в монетах
        public double DamageBonus { get; set; } = 0.0;  // Бонус к урону (0.1 = +10%)
        public double HpBonus { get; set; } = 0.0;      // Бонус к здоровью (0.1 = +10%)
        public bool Unlocked { get; set; } = false;     // Открыт ли навык
        public string IconPath { get; set; }            // Путь к иконке навыка
    }

    /// <summary>
    /// Менеджер дерева навыков. Отвечает за построение, отображение
    /// и взаимодействие с деревом навыков игрока.
    /// Навыки организованы в ветки, исходящие из центра под разными углами.
    /// Прокачка навыка происходит по двойному клику на иконке.
    /// </summary>
    public class SkillTreeManager
    {
        // ==================== РЕСУРСЫ ====================
        private readonly CroppedBitmap _slotTexture; // Текстура фоновой ячейки навыка

        // Константы расположения текстуры ячейки в спрайт-листе
        private const int SlotSourceX = 0;
        private const int SlotSourceY = 79;
        private const int SlotSourceSize = 24;

        // ==================== ДАННЫЕ ====================
        // Список всех навыков в порядке их расположения в дереве
        public List<SkillDefinition> Skills { get; } = new List<SkillDefinition>();

        // ==================== СОБЫТИЯ ====================
        // Событие, вызываемое при прокачке навыка
        public event Action<SkillDefinition> OnSkillUnlocked;

        // ==================== ОБРАБОТКА ДВОЙНОГО КЛИКА ====================
        private DateTime _lastClickTime = DateTime.MinValue; // Время последнего клика
        private Image _lastClickedImage;                     // Последний кликнутый элемент
        private const double DoubleClickMs = 400;            // Максимальный интервал между кликами

        // ================================================================
        //  КОНСТРУКТОР
        //  Загружает текстуру ячейки для отображения слотов навыков
        // ================================================================
        public SkillTreeManager(string inventorySheetPath)
        {
            // Загрузка спрайт-листа инвентаря
            var sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.UriSource = new Uri(inventorySheetPath, UriKind.Absolute);
            sheet.CacheOption = BitmapCacheOption.OnLoad;
            sheet.EndInit();

            // Вырезание текстуры ячейки
            _slotTexture = new CroppedBitmap(sheet,
                new Int32Rect(SlotSourceX, SlotSourceY, SlotSourceSize, SlotSourceSize));
        }

        // Перегрузка конструктора для совместимости с оригиналом
        public SkillTreeManager(Canvas _, string inventorySheetPath) : this(inventorySheetPath) { }

        // ================================================================
        //  ПОСТРОЕНИЕ ДЕРЕВА НАВЫКОВ
        //  Создает дерево навыков на канвасе с центральной ячейкой
        //  и ветками, расходящимися под заданными углами
        // ================================================================
        public void BuildTree(Canvas canvas, List<SkillBranch> branches,
            double centerX, double centerY,
            double slotRenderSize = 64, double spacing = 80, double startRadius = 70)
        {
            canvas.Children.Clear(); // Очистка предыдущего дерева
            Skills.Clear();          // Очистка списка навыков

            // Центральная ячейка (пассивная, без навыка)
            AddSlot(canvas, centerX - slotRenderSize / 2, centerY - slotRenderSize / 2, slotRenderSize, null, null);

            // Построение каждой ветки
            foreach (var branch in branches)
            {
                // Преобразование угла в радианы для математических расчетов
                double rad = branch.AngleDegrees * Math.PI / 180.0;
                double dirX = Math.Cos(rad); // Направление по X
                double dirY = Math.Sin(rad); // Направление по Y

                // Создание навыков в ветке (от ближнего к дальнему)
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
                        Cost = 10 + i * 5, // Стоимость растет с каждым уровнем: 10, 15, 20, 25
                        // Автоматическое определение типа бонуса по названию
                        DamageBonus = rawName.Contains("slash") || rawName.Contains("dragon") ? 0.1 + i * 0.05 : 0,
                        HpBonus = rawName.Contains("heal") || rawName.Contains("plant") ? 0.1 + i * 0.05 : 0,
                    };
                    Skills.Add(def);

                    // Добавление слота с навыком на канвас
                    AddSlot(canvas, x, y, slotRenderSize, iconPath, def);
                }
            }
        }

        // ================================================================
        //  ДОБАВЛЕНИЕ СЛОТА НА КАНВАС
        //  Создает визуальный слот с иконкой навыка
        //  и обрабатывает двойной клик для прокачки
        // ================================================================
        private void AddSlot(Canvas canvas, double x, double y, double size, string iconPath, SkillDefinition def)
        {
            // --- Фоновая ячейка ---
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

            // Если нет иконки - выходим (центральная ячейка)
            if (iconPath == null) return;

            // --- Иконка навыка ---
            double iconSize = size * 0.7; // Иконка занимает 70% слота
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
                    Opacity = def.Unlocked ? 1.0 : 0.45, // Неоткрытые навыки полупрозрачные
                    ToolTip = $"{def.Name}\nCost: {def.Cost} coins\n" + // Подсказка с информацией
                                         (def.DamageBonus > 0 ? $"+{def.DamageBonus * 100:0}% DMG\n" : "") +
                                         (def.HpBonus > 0 ? $"+{def.HpBonus * 100:0}% HP" : "")
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

            // --- Обработка двойного клика для прокачки ---
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

                    // Обновление прозрачности после проверки (если не хватило монет, навык останется закрытым)
                    capturedImg.Opacity = capturedDef.Unlocked ? 1.0 : 0.45;
                }
            };
        }
    }
}