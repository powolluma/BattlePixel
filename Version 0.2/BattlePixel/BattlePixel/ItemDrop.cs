using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BattlePixel
{
    /// <summary>
    /// Представляет предмет или монету, выпадающую на землю после убийства врага.
    /// Автоматически подбирается при столкновении с игроком или через 1 секунду.
    /// </summary>
    public class ItemDrop
    {
        // ==================== КОМПОНЕНТЫ ОТОБРАЖЕНИЯ ====================
        private readonly Canvas _canvas;    // Канвас для рендеринга
        private readonly Image _image;      // Изображение дропа

        // ==================== СВОЙСТВА ====================
        public string ItemName { get; }      // Имя предмета или "coins:количество"
        public bool IsCoin { get; }          // Является ли дроп монетой
        public double X { get; }             // Мировая X-координата
        public double Y { get; }             // Мировая Y-координата
        public bool Collected { get; private set; } // Флаг подбора
        public DateTime SpawnTime { get; }   // Время появления

        // ==================== КОНСТАНТЫ ОТОБРАЖЕНИЯ ====================
        private const double Size = 32;              // Размер предмета
        private const double CoinRenderSize = 32 / 1.5; // Размер монеты (уменьшен)
        private const int CoinFrameSize = 80;        // Размер кадра монеты в спрайт-листе

        // ================================================================
        //  КОНСТРУКТОР
        //  Загружает и отображает дроп на канвасе
        // ================================================================
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
                if (!System.IO.File.Exists(itemPath))
                    Debug.WriteLine($"[ItemDrop] ФАЙЛ НЕ НАЙДЕН: {itemPath}");

                var sheet = new BitmapImage();
                sheet.BeginInit();
                sheet.UriSource = new Uri(itemPath, UriKind.Absolute);
                sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.EndInit();

                Debug.WriteLine($"[ItemDrop] Загружен лист: {itemPath}, size={sheet.PixelWidth}x{sheet.PixelHeight}, isCoin={isCoin}");

                // Для монет берем первый кадр из спрайт-листа
                if (isCoin)
                {
                    if (sheet.PixelWidth >= CoinFrameSize && sheet.PixelHeight >= CoinFrameSize)
                    {
                        var rect = new Int32Rect(0, 0, CoinFrameSize, CoinFrameSize);
                        finalSource = new CroppedBitmap(sheet, rect);
                    }
                    else
                    {
                        Debug.WriteLine($"[ItemDrop] Лист монеты меньше {CoinFrameSize}px, используем целиком");
                        finalSource = sheet;
                    }
                }
                else
                {
                    // Для предметов используем всю текстуру
                    finalSource = sheet;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ItemDrop] ОШИБКА загрузки {itemPath}: {ex.Message}");
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

            // Размещение на канвасе (над землей, но под персонажами)
            Panel.SetZIndex(_image, 8);
            Canvas.SetLeft(_image, x);
            Canvas.SetTop(_image, y);
            _canvas.Children.Add(_image);
        }

        // ================================================================
        //  МЕТОДЫ ВЗАИМОДЕЙСТВИЯ
        // ================================================================

        /// <summary>
        /// Получить ограничивающий прямоугольник для проверки столкновений
        /// </summary>
        public Rect GetBounds() => new Rect(X, Y, Size, Size);

        /// <summary>
        /// Подобрать предмет (удалить с канваса и отметить как собранный)
        /// </summary>
        public void Collect()
        {
            if (Collected) return;
            Collected = true;
            _canvas.Children.Remove(_image);
        }
    }

    /// <summary>
    /// Менеджер выпадающих предметов.
    /// Управляет созданием дропа после смерти врагов, его отображением
    /// и автоматическим подбором игроком через определенное время.
    /// </summary>
    public class DropManager
    {
        // ==================== КОМПОНЕНТЫ ====================
        private readonly Canvas _canvas;              // Канвас для рендеринга
        private readonly Random _rnd = new Random();  // Генератор случайных чисел
        private readonly List<string> _itemPaths;     // Пути к текстурам предметов
        private readonly double _groundY;             // Y-координата уровня земли

        // ==================== КОНСТАНТЫ ШАНСА ВЫПАДЕНИЯ ====================
        private const double ItemDropChance = 0.35;   // Шанс выпадения предмета (35%)
        private const double CoinDropChance = 0.60;   // Шанс выпадения монет (60%)
        private const double ItemSize = 32;           // Размер предмета

        // ==================== СПИСОК ДРОПА ====================
        public List<ItemDrop> Drops { get; } = new List<ItemDrop>();

        // ================================================================
        //  КОНСТРУКТОР
        //  Загружает все текстуры предметов из папки Assets/Items
        // ================================================================
        public DropManager(Canvas canvas, string itemsFolder, double groundY)
        {
            _canvas = canvas;
            _groundY = groundY;

            // Загрузка всех .png файлов из папки с предметами
            _itemPaths = new List<string>();
            if (System.IO.Directory.Exists(itemsFolder))
                _itemPaths.AddRange(System.IO.Directory.GetFiles(itemsFolder, "*.png"));
        }

        // ================================================================
        //  СОЗДАНИЕ ДРОПА
        //  После смерти врага создает монеты и/или предмет на земле
        // ================================================================
        public void SpawnDrop(double x, string coinsIconPath)
        {
            double dropY = _groundY - ItemSize; // Размещение на уровне земли

            // Выпадение монет
            if (_rnd.NextDouble() < CoinDropChance)
            {
                var coin = new ItemDrop(_canvas, coinsIconPath, "coins:1", x, dropY, isCoin: true);
                Drops.Add(coin);
            }

            // Выпадение предмета (если есть текстуры и сработал шанс)
            if (_rnd.NextDouble() < ItemDropChance && _itemPaths.Count > 0)
            {
                string path = _itemPaths[_rnd.Next(_itemPaths.Count)];
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                var drop = new ItemDrop(_canvas, path, name, x + 36, dropY, isCoin: false);
                Drops.Add(drop);
            }
        }

        // ================================================================
        //  ПРОВЕРКА ПОДБОРА
        //  Вызывается из игрового цикла. Проверяет все дропы на:
        //  1) Столкновение с игроком
        //  2) Истечение времени (1 секунда)
        //  При подборе добавляет монеты или предметы в инвентарь
        // ================================================================
        public void CheckPickup(Player player, InventoryManager inventory)
        {
            foreach (var drop in Drops)
            {
                if (drop.Collected) continue;

                // Автоподбор через 1 секунду после появления
                bool timedOut = (DateTime.UtcNow - drop.SpawnTime).TotalSeconds >= 1.0;

                // Проверка столкновения с игроком
                bool playerCollision = player.GetBounds().IntersectsWith(drop.GetBounds());

                if (timedOut || playerCollision)
                {
                    drop.Collect(); // Сбор дропа

                    if (drop.IsCoin)
                    {
                        // Добавление монет игроку
                        string[] parts = drop.ItemName.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[1], out int amount))
                            player.Coins += amount;
                    }
                    else
                    {
                        // Добавление предмета в инвентарь
                        inventory.AddOrReplaceItem(drop.ItemName);
                        // Бонус за подбор предмета: +2% к урону и здоровью
                        player.ApplySkillUpgrade(0.02, 0.02);
                    }
                }
            }

            // Удаление собранных дропов из списка
            Drops.RemoveAll(d => d.Collected);
        }
    }
}