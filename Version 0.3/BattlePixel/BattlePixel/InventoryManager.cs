using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    public class InventoryManager
    {
        // Текстура и ресурсы
        private readonly string _spriteSheetPath;          
        private readonly CroppedBitmap _slotTexture;    
        private readonly Random _random = new Random();    

        // Ячейки 
        private const int SlotSourceX = 0;
        private const int SlotSourceY = 79;
        private const int SlotSourceSize = 24;

        // Хранение предметов
        // Список предметов
        private readonly List<(string name, string iconPath)> _items = new List<(string, string)>();
        private const int MaxSlots = 6; 

        // Компоненты ui 
        private UniformGrid _grid;              
        private int _slotRenderSize = 64;       
        private string _itemsFolder;            

        // Свойства
        public IReadOnlyList<(string name, string iconPath)> Items => _items;

        // Конструктор
        public InventoryManager(string inventorySheetPath, string itemsFolder = null)
        {
            _spriteSheetPath = inventorySheetPath;
            _itemsFolder = itemsFolder;

            // Загрузка спрайта
            var sheet = new BitmapImage();
            sheet.BeginInit();
            sheet.UriSource = new Uri(_spriteSheetPath, UriKind.Absolute);
            sheet.CacheOption = BitmapCacheOption.OnLoad;
            sheet.EndInit();

            // Вырезание текстуры пустой ячейки 
            var rect = new Int32Rect(SlotSourceX, SlotSourceY, SlotSourceSize, SlotSourceSize);
            _slotTexture = new CroppedBitmap(sheet, rect);
        }

        // Слоты инвентаря
        public void BuildSlots(UniformGrid grid, int slotRenderSize = 64)
        {
            _grid = grid;
            _slotRenderSize = slotRenderSize;
            RefreshGrid(); 
        }

        
        // Добавить айтем
        public bool TryAddItem(string itemName)
        {
            if (_items.Count >= MaxSlots) return false; 

            string iconPath = ResolveIconPath(itemName);
            _items.Add((itemName, iconPath));
            RefreshGrid(); 
            return true;
        }

        // Удаление айтема
        public void RemoveItem(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            _items.RemoveAt(index);
            RefreshGrid(); 
        }

        // Добавить или заменить
        public void AddOrReplaceItem(string itemName)
        {
            string iconPath = ResolveIconPath(itemName);

            if (_items.Count < MaxSlots)
            {
                _items.Add((itemName, iconPath));
            }
            else
            {
                int replaceIndex = _random.Next(_items.Count);
                _items[replaceIndex] = (itemName, iconPath);
            }
            RefreshGrid(); 
        }

        // Поиск пути к иконке
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

        // Обновление ui
        private void RefreshGrid()
        {
            if (_grid == null) return;
            _grid.Children.Clear(); 

            int total = _grid.Columns * _grid.Rows; 

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

                // Проверка на пустой слот
                if (i < _items.Count && _items[i].iconPath != null)
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(_items[i].iconPath, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();

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
                    catch { /* Иконка не найдена */ }
                }

                _grid.Children.Add(cell); 
            }
        }
    }
}