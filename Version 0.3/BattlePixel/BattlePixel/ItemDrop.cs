using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    public class ItemDrop
    {
        // Компоненты отображения
        private readonly Canvas _canvas;   
        private readonly Image _image;      
        
        // Свойства
        public string ItemName { get; }  
        public bool IsCoin { get; }         
        public double X { get; }            
        public double Y { get; }           
        public bool Collected { get; private set; } 
        public DateTime SpawnTime { get; }   

        // Компоненты отображения
        private const double Size = 32;           
        private const double CoinRenderSize = 32 / 1.5; 
        private const int CoinFrameSize = 80;      

        // Конструктор
        public ItemDrop(Canvas canvas, string itemPath, string itemName, double x, double y, bool isCoin = false)
        {
            _canvas = canvas;
            ItemName = itemName;
            IsCoin = isCoin;
            X = x;
            Y = y;
            SpawnTime = DateTime.UtcNow;

            ImageSource finalSource = null;

            // Загрузка текстуры дропа
            try
            {
                var sheet = new BitmapImage();
                sheet.BeginInit();
                sheet.UriSource = new Uri(itemPath, UriKind.Absolute);
                sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.EndInit();

                // Для монет первый кадр 
                if (isCoin)
                {
                    if (sheet.PixelWidth >= CoinFrameSize && sheet.PixelHeight >= CoinFrameSize)
                    {
                        var rect = new Int32Rect(0, 0, CoinFrameSize, CoinFrameSize);
                        finalSource = new CroppedBitmap(sheet, rect);
                    }
                    else
                    {
                        finalSource = sheet;
                    }
                }
                else
                {
                    finalSource = sheet;
                }
            }
            catch (Exception ex)
            {
                finalSource = null;
            }

            // Определение размера отображения
            double renderSize = isCoin ? CoinRenderSize : Size;

            // Создание изображения
            _image = new Image
            {
                Source = finalSource,
                Width = renderSize,
                Height = renderSize,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);

            // Размещение над землей
            Panel.SetZIndex(_image, 8);
            Canvas.SetLeft(_image, x);
            Canvas.SetTop(_image, y);
            _canvas.Children.Add(_image);
        }

        // Методы взаимодействия
        
        // Помогалка (хитбоксы)
        public Rect GetBounds() => new Rect(X, Y, Size, Size);

        // Подобрать предмет
        public void Collect()
        {
            if (Collected) return;
            Collected = true;
            _canvas.Children.Remove(_image);
        }
    }

    public class DropManager
    {
        // Компоненты
        private readonly Canvas _canvas;             
        private readonly Random _rnd = new Random();  
        private readonly List<string> _itemPaths;     
        private readonly double _groundY;            

        // Шанс выпадения
        private const double ItemDropChance = 0.35;   
        private const double CoinDropChance = 0.60;   
        private const double ItemSize = 32;           

        // Список предметов
        public List<ItemDrop> Drops { get; } = new List<ItemDrop>();

        // Конструктор
        public DropManager(Canvas canvas, string itemsFolder, double groundY)
        {
            _canvas = canvas;
            _groundY = groundY;

            // Загрузка всех файлов 
            _itemPaths = new List<string>();
            if (System.IO.Directory.Exists(itemsFolder))
                _itemPaths.AddRange(System.IO.Directory.GetFiles(itemsFolder, "*.png"));
        }

        // Создание предмета
        public void SpawnDrop(double x, string coinsIconPath)
        {
            double dropY = _groundY - ItemSize; 

            // Выпадение монет
            if (_rnd.NextDouble() < CoinDropChance)
            {
                var coin = new ItemDrop(_canvas, coinsIconPath, "coins:1", x, dropY, isCoin: true);
                Drops.Add(coin);
            }

            // Выпадение предмета 
            if (_rnd.NextDouble() < ItemDropChance && _itemPaths.Count > 0)
            {
                string path = _itemPaths[_rnd.Next(_itemPaths.Count)];
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                var drop = new ItemDrop(_canvas, path, name, x + 36, dropY, isCoin: false);
                Drops.Add(drop);
            }
        }

        // Проверка подбора предмета
        public void CheckPickup(Player player, InventoryManager inventory)
        {
            foreach (var drop in Drops)
            {
                if (drop.Collected) continue;

                // Автоподбор через секунду 
                bool timedOut = (DateTime.UtcNow - drop.SpawnTime).TotalSeconds >= 1.0;

                // Проверка столкновения с игроком
                bool playerCollision = player.GetBounds().IntersectsWith(drop.GetBounds());

                if (timedOut || playerCollision)
                {
                    drop.Collect(); // Сбор дропа

                    if (drop.IsCoin)
                    {
                        // Добавление монет 
                        string[] parts = drop.ItemName.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[1], out int amount))
                            player.Coins += amount;
                    }
                    else
                    {
                        // Добавление предмета в инвентарь
                        inventory.AddOrReplaceItem(drop.ItemName);
                        // за подбор предмета 2% к урону и здоровью
                        player.ApplySkillUpgrade(0.02, 0.02);
                    }
                }
            }

            // Удаление собранных дропов из списка
            Drops.RemoveAll(d => d.Collected);
        }
    }
}