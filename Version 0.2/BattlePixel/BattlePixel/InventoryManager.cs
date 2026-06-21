using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    /// <summary>
    /// Менеджер инвентаря. Управляет отображением и хранением предметов игрока.
    /// Инвентарь состоит из ограниченного количества слотов (MaxSlots = 6).
    /// Каждый слот может содержать один предмет с иконкой.
    /// </summary>
    public class InventoryManager
    {
        // ==================== РЕСУРСЫ И ТЕКСТУРЫ ====================
        private readonly string _spriteSheetPath;           // Путь к спрайт-листу инвентаря
        private readonly CroppedBitmap _slotTexture;        // Текстура пустой ячейки
        private readonly Random _random = new Random();     // Генератор для случайной замены

        // Константы расположения текстуры ячейки в спрайт-листе
        private const int SlotSourceX = 0;
        private const int SlotSourceY = 79;
        private const int SlotSourceSize = 24;

        // ==================== ХРАНЕНИЕ ПРЕДМЕТОВ ====================
        // Список предметов: имя и путь к иконке
        private readonly List<(string name, string iconPath)> _items =
            new List<(string, string)>();
        private const int MaxSlots = 6; // Максимальное количество слотов

        // ==================== UI КОМПОНЕНТЫ ====================
        private UniformGrid _grid;              // Сетка для отображения слотов
        private int _slotRenderSize = 64;       // Размер слота в пикселях
        private string _itemsFolder;            // Папка с иконками предметов

        // ==================== СВОЙСТВА ====================
        public IReadOnlyList<(string name, string iconPath)> Items => _items;

        // ================================================================
        //  КОНСТРУКТОР
        //  Загружает текстуру слота из спрайт-листа инвентаря
        // ================================================================
        public InventoryManager(string inventorySheetPath, string itemsFolder = null)
        {
            _spriteSheetPath = inventorySheetPath;
            _itemsFolder = itemsFolder;

            // Загрузка спрайт-листа инвентаря
            var sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.UriSource = new Uri(_spriteSheetPath, UriKind.Absolute);
            sheet.CacheOption = BitmapCacheOption.OnLoad;
            sheet.EndInit();

            // Вырезание текстуры пустой ячейки из спрайт-листа
            var rect = new Int32Rect(SlotSourceX, SlotSourceY, SlotSourceSize, SlotSourceSize);
            _slotTexture = new CroppedBitmap(sheet, rect);
        }

        // ================================================================
        //  ПОСТРОЕНИЕ СЛОТОВ ИНВЕНТАРЯ
        //  Создает сетку с пустыми ячейками и отображает их на UI
        // ================================================================
        public void BuildSlots(UniformGrid grid, int slotRenderSize = 64)
        {
            _grid = grid;
            _slotRenderSize = slotRenderSize;
            RefreshGrid(); // Обновление отображения
        }

        // ================================================================
        //  УПРАВЛЕНИЕ ПРЕДМЕТАМИ
        // ================================================================

        /// <summary>
        /// Добавить предмет в инвентарь.
        /// Возвращает true, если предмет успешно добавлен.
        /// </summary>
        public bool TryAddItem(string itemName)
        {
            if (_items.Count >= MaxSlots) return false; // Инвентарь полон

            string iconPath = ResolveIconPath(itemName);
            _items.Add((itemName, iconPath));
            RefreshGrid(); // Обновление отображения
            return true;
        }

        /// <summary>
        /// Удалить предмет из инвентаря по индексу.
        /// </summary>
        public void RemoveItem(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            _items.RemoveAt(index);
            RefreshGrid(); // Обновление отображения
        }

        /// <summary>
        /// Добавить или заменить предмет в инвентаре.
        /// Если есть свободное место - добавляет новый предмет.
        /// Если места нет - случайным образом заменяет существующий предмет.
        /// </summary>
        public void AddOrReplaceItem(string itemName)
        {
            string iconPath = ResolveIconPath(itemName);

            if (_items.Count < MaxSlots)
            {
                // Есть свободное место - добавляем
                _items.Add((itemName, iconPath));
            }
            else
            {
                // Нет места - заменяем случайный предмет
                int replaceIndex = _random.Next(_items.Count);
                _items[replaceIndex] = (itemName, iconPath);
            }
            RefreshGrid(); // Обновление отображения
        }

        // ================================================================
        //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ================================================================

        /// <summary>
        /// Поиск пути к иконке предмета.
        /// Сначала ищет файл с расширением .png, затем без расширения.
        /// </summary>
        private string ResolveIconPath(string itemName)
        {
            if (string.IsNullOrEmpty(_itemsFolder)) return null;

            // Поиск файла с расширением .png
            string pathPng = System.IO.Path.Combine(_itemsFolder, itemName + ".png");
            if (System.IO.File.Exists(pathPng)) return pathPng;

            // Поиск файла без расширения
            string pathRaw = System.IO.Path.Combine(_itemsFolder, itemName);
            return System.IO.File.Exists(pathRaw) ? pathRaw : null;
        }

        // ================================================================
        //  ОБНОВЛЕНИЕ UI
        //  Перестраивает все слоты инвентаря на основе текущего списка предметов
        // ================================================================
        private void RefreshGrid()
        {
            if (_grid == null) return;
            _grid.Children.Clear(); // Очистка старых ячеек

            int total = _grid.Columns * _grid.Rows; // Общее количество слотов

            // Создание каждого слота
            for (int i = 0; i < total; i++)
            {
                // Контейнер для слота
                var cell = new Grid
                {
                    Width = _slotRenderSize,
                    Height = _slotRenderSize,
                    Margin = new Thickness(2)
                };

                // Фоновая текстура пустой ячейки
                var slotImg = new Image
                {
                    Source = _slotTexture,
                    Width = _slotRenderSize,
                    Height = _slotRenderSize,
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetBitmapScalingMode(slotImg, BitmapScalingMode.NearestNeighbor);
                cell.Children.Add(slotImg);

                // Если слот занят предметом - отображаем иконку поверх
                if (i < _items.Count && _items[i].iconPath != null)
                {
                    try
                    {
                        // Загрузка иконки предмета
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(_items[i].iconPath, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();

                        // Иконка занимает 70% размера слота
                        double iconSize = _slotRenderSize * 0.70;
                        var itemImg = new Image
                        {
                            Source = bmp,
                            Width = iconSize,
                            Height = iconSize,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            SnapsToDevicePixels = true
                        };
                        RenderOptions.SetBitmapScalingMode(itemImg, BitmapScalingMode.NearestNeighbor);
                        cell.Children.Add(itemImg);
                    }
                    catch { /* Иконка не найдена - оставляем слот пустым */ }
                }

                _grid.Children.Add(cell); // Добавление слота в сетку
            }
        }
    }
}