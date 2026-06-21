using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BattlePixel
{
    /// <summary>
    /// Главное окно игры. Управляет всеми игровыми системами, циклом обновления,
    /// рендерингом, вводом с клавиатуры и взаимодействием между компонентами.
    /// Реализует базовую логику RPG с боковым скроллингом.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ==================== МЕНЕДЖЕРЫ ИГРОВЫХ СИСТЕМ ====================
        // Отвечают за отдельные аспекты игры: карту, декорации, инвентарь и т.д.
        private TileManager _tileManager;          // Управляет тайлами карты
        private GameMap _gameMap;                  // Генерирует и хранит данные карты
        private DecorationManager _decorationManager; // Размещает декоративные объекты
        private InventoryManager _inventoryManager;   // Управляет инвентарем игрока
        private SkillTreeManager _skillTreeManager;   // Управляет деревом навыков
        private DropManager _dropManager;             // Управляет выпадающими предметами

        // ==================== ИГРОВЫЕ СУЩНОСТИ ====================
        private Player _player;                    // Основной игрок на поле боя
        private Player _inventoryPlayer;           // Копия игрока для отображения в инвентаре
        private readonly List<Enemy> _enemies = new List<Enemy>();          // Список врагов на уровне
        private readonly List<HealthBar> _enemyHealthBars = new List<HealthBar>(); // Полосы здоровья врагов

        private HealthBar _playerHealthBar;        // Полоса здоровья игрока

        // ==================== СОСТОЯНИЯ UI ====================
        private bool _inventoryOpen;               // Открыт ли инвентарь
        private bool _skillTreeOpen;               // Открыто ли дерево навыков

        // ==================== УРОВНИ И ТАЙМЕРЫ ====================
        private int _level = 1;                    // Текущий уровень игры
        private readonly DispatcherTimer _respawnTimer = new DispatcherTimer
        { Interval = TimeSpan.FromSeconds(3) };    // Таймер возрождения после смерти
        private bool _respawnScheduled;            // Флаг запланированного возрождения

        // ==================== ИГРОВОЙ ЦИКЛ ====================
        // Обновляет игру ~60 раз в секунду (16 мс)
        private readonly DispatcherTimer _gameLoop = new DispatcherTimer
        { Interval = TimeSpan.FromMilliseconds(16) };

        // ==================== ИГРОВЫЕ КОНСТАНТЫ ====================
        private const int MapCols = 26;            // Количество колонок на карте
        private const int MapRows = 9;             // Количество рядов на карте
        private const int TileRender = 64;         // Размер рендеринга тайла в пикселях
        private const int CharRender = 250;        // Размер рендеринга персонажа
        private const double MoveSpeed = 2.5;      // Скорость движения игрока
        private const double AttackRange = 110.0;  // Дальность атаки

        // ==================== ПУТИ К РЕСУРСАМ ====================
        private static string Base =>
            System.IO.Path.GetFullPath(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        // При Debug: bin\Debug\net... -> ..\..\.. -> корень проекта
        // При Release: просто BaseDirectory

        private static string A(string rel) =>
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", rel);

        // Пути к ресурсам
        private string HealthSheet => A(@"Health\Health.png");
        private string InvSheet => A(@"Inventory\Inventory.png");
        private string ItemsDir => A("Items");
        private string SkillsDir => A("Skills");
        private string OrcDir => A(@"Characters\Orc\");
        private string DecoDir => A(@"Decoration\");
        private string CoinsIcon => A(@"Items\coin_spin-Sheet.png");

        // ==================== ПОРТАЛ ====================
        private double _portalWorldX;              // Мировая позиция портала по X
        private double _groundY;                   // Y-координата уровня земли

        // ================================================================
        //  КОНСТРУКТОР
        // ================================================================
        public MainWindow()
        {
            InitializeComponent();
        }

        // ================================================================
        //  ОБРАБОТЧИК ЗАГРУЗКИ ОКНА
        //  Инициализирует все игровые системы и запускает игровой цикл
        // ================================================================
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitLevel();                         // Создание уровня

            this.KeyDown += MainWindow_KeyDown; // Подписка на клавиатуру

            _gameLoop.Tick += GameLoop_Tick;    // Запуск игрового цикла
            _gameLoop.Start();

            _respawnTimer.Tick += RespawnTimer_Tick; // Таймер возрождения
        }

        // ================================================================
        //  ИНИЦИАЛИЗАЦИЯ УРОВНЯ
        //  Создает карту, расставляет врагов, декорации, портал и настраивает игрока
        //  Вызывается при старте игры и при переходе на новый уровень
        // ================================================================
        private void InitLevel()
        {
            // --- Очистка предыдущего уровня ---
            GameCanvas.Children.Clear();
            _enemies.Clear();
            _enemyHealthBars.Clear();

            // --- Генерация карты ---
            _tileManager = new TileManager(GameCanvas, sourceTileSize: 32, renderTileSize: TileRender);
            _gameMap = new GameMap(_tileManager, MapCols, MapRows);
            _gameMap.GenerateTestMap();
            _gameMap.Render();

            // --- Расчет позиций ---
            int groundRow = MapRows - 1;
            double groundY = groundRow * TileRender;
            _groundY = groundY;                 // Сохраняем для декораций
            double charY = groundRow * TileRender - 142;  // Y персонажа (поднят над землей)
            double startX = 50;                 // Стартовая позиция по X

            // --- Инициализация игрока ---
            if (_player == null)
            {
                // Первое создание игрока
                _player = new Player(GameCanvas, frameSize: 100, renderSize: CharRender);
                _player.OnAttackHit += Player_OnAttackHit;      // Событие атаки игрока
                _player.OnDeathFinished += Player_OnDeathFinished; // Событие смерти игрока
            }
            else
            {
                // Переиспользование существующего игрока при переходе на новый уровень
                _player.ReaddToCanvas(GameCanvas);
                _player.ResetForNewLevel();     // Сброс состояния для нового уровня
            }
            _player.SetPosition(startX, charY);

            // --- Полоса здоровья игрока ---
            _playerHealthBar?.Remove();
            _playerHealthBar = new HealthBar(GameCanvas, HealthSheet, isGreen: true, renderWidth: 80, renderHeight: 16);
            _playerHealthBar.SetPosition(startX + CharRender / 2.0, charY + 165, 0);
            _playerHealthBar.UpdateHealth(_player.Hp, _player.MaxHp);

            // --- Создание врагов ---
            double[] enemyXs = { 500, 800, 1100, 1400 }; // Позиции врагов
            foreach (double ex in enemyXs)
            {
                var enemy = new Enemy(
                    GameCanvas,
                    basePath: OrcDir,
                    namePrefix: "Orc",
                    frameSize: 100,
                    renderSize: CharRender,
                    level: _level,               // Сложность зависит от уровня
                    facingRight: true);

                enemy.SetPosition(ex, charY);
                enemy.OnDeath += Enemy_OnDeath;             // Событие смерти врага
                enemy.OnAttackHit += () => Enemy_OnAttackHit(enemy); // Событие атаки врага

                // Полоса здоровья врага
                var hb = new HealthBar(GameCanvas, HealthSheet, isGreen: false, renderWidth: 80, renderHeight: 16);
                hb.SetPosition(ex + CharRender / 2.0, charY + 165, 0);
                hb.UpdateHealth(enemy.Hp, enemy.MaxHp);

                _enemies.Add(enemy);
                _enemyHealthBars.Add(hb);
            }

            // --- Декорации ---
            _portalWorldX = MapCols * TileRender - 32 - 40; // Позиция портала
            _decorationManager = new DecorationManager(GameCanvas);

            // Портал (выход на следующий уровень)
            var portal = new DecorationInfo(DecoDir + "GrassLand_Entry_3.png", width: 32, height: 64);
            _decorationManager.PlaceOnGround(portal, x: _portalWorldX, groundY);

            // Цветы
            var flowers = new List<DecorationInfo>
            {
                new DecorationInfo(DecoDir + "GrassLand_Flower_1.png", 32, 32),
                new DecorationInfo(DecoDir + "GrassLand_Flower_2.png", 32, 32),
                new DecorationInfo(DecoDir + "GrassLand_Flower_3.png", 32, 32),
            };
            _decorationManager.ScatterRandom(flowers, count: 8, minX: 200, maxX: 1600, groundY: groundY);

            // Высокая трава
            var tallGrass = new List<DecorationInfo>
            {
                new DecorationInfo(DecoDir + "GrassLand_TallGrass_1.png", 32, 32),
                new DecorationInfo(DecoDir + "GrassLand_TallGrass_2.png", 32, 32),
            };
            _decorationManager.ScatterRandom(tallGrass, count: 8, minX: 200, maxX: 1600, groundY: groundY);

            // Камни
            var stones = new List<DecorationInfo>
            {
                new DecorationInfo(DecoDir + "GrassLand_Stone_1.png", 32, 32),
                new DecorationInfo(DecoDir + "GrassLand_Stone_2.png", 32, 32),
                new DecorationInfo(DecoDir + "GrassLand_Stone_3.png", 64, 32),
            };
            _decorationManager.ScatterRandom(stones, count: 6, minX: 200, maxX: 1600, groundY: groundY);

            // --- Инвентарь (создается один раз при первом запуске) ---
            if (_inventoryManager == null)
            {
                _inventoryManager = new InventoryManager(InvSheet, ItemsDir);
                _inventoryManager.BuildSlots(InventoryGrid, slotRenderSize: 48);
            }

            // Отображение игрока в инвентаре
            if (_inventoryPlayer == null)
            {
                _inventoryPlayer = new Player(InventoryCharacterCanvas, frameSize: 100, renderSize: 240);
                _inventoryPlayer.SetPosition(10, 10);
            }

            // --- Дерево навыков (создается один раз при первом запуске) ---
            if (_skillTreeManager == null)
            {
                _skillTreeManager = new SkillTreeManager(InvSheet);
                _skillTreeManager.OnSkillUnlocked += OnSkillUnlocked; // Обработчик открытия навыка

                // Определение веток навыков с иконками
                string s = SkillsDir + "\\";
                var branches = new List<SkillBranch>
                {
                    new SkillBranch(angleDegrees: -90,    iconPaths: new[] { s+"dragon_charges_skill_icon.png", s+"dragon_roar_skill_icon.png",  s+"dragon_tail_icon.png",    s+"dragon_wing_skill_icon.png" }),
                    new SkillBranch(angleDegrees: -38.6,  iconPaths: new[] { s+"fireskillicon2.png",            s+"fireskillicon3.png",           s+"fireskillicon4.png",      s+"fireskillicon.png" }),
                    new SkillBranch(angleDegrees:  12.9,  iconPaths: new[] { s+"healingskillicon2.png",         s+"healingskillicon3.png",        s+"healingskillicon4.png",   s+"healingskillicon.png" }),
                    new SkillBranch(angleDegrees:  64.3,  iconPaths: new[] { s+"Iceskillicon2.png",             s+"Iceskillicon3.png",            s+"Iceskillicon4.png",       s+"Iceskillicon.png" }),
                    new SkillBranch(angleDegrees: 115.7,  iconPaths: new[] { s+"plantskillicon2.png",           s+"plantskillicon3.png",          s+"plantskillicon4.png",     s+"plantskillicon.png" }),
                    new SkillBranch(angleDegrees: 167.1,  iconPaths: new[] { s+"Poisonskillicon2.png",          s+"Poison skillicon3.png",        s+"Poisonskillicon4.png",    s+"Poisonskillicon.png" }),
                    new SkillBranch(angleDegrees: -141.4, iconPaths: new[] { s+"slashskillicon2.png",           s+"slashskillicon3.png",          s+"slashskillicon4.png",     s+"slashskillicon.png" }),
                };
                _skillTreeManager.BuildTree(SkillTreeCanvas, branches,
                    centerX: 260, centerY: 260,
                    slotRenderSize: 44, spacing: 55, startRadius: 50);
            }

            // --- Менеджер дропа ---
            _dropManager = new DropManager(GameCanvas, ItemsDir, _groundY);

            // --- Обновление HUD ---
            UpdateHUD();
        }

        // ================================================================
        //  ОБРАБОТЧИК СМЕРТИ ИГРОКА
        //  Запускает таймер возрождения, который перезапускает уровень с 1-го
        // ================================================================
        private void Player_OnDeathFinished()
        {
            if (_respawnScheduled) return;
            _respawnScheduled = true;
            _respawnTimer.Stop();
            _respawnTimer.Start();
        }

        // ================================================================
        //  ТАЙМЕР ВОЗРОЖДЕНИЯ
        //  Сбрасывает уровень до 1-го и пересоздает игрока
        // ================================================================
        private void RespawnTimer_Tick(object sender, EventArgs e)
        {
            _respawnTimer.Stop();
            _respawnScheduled = false;

            _level = 1;                 // Сброс на первый уровень
            _player.RespawnKeepStats(); // Оживление игрока с сохранением характеристик
            InitLevel();               // Пересоздание уровня
        }

        // ================================================================
        //  ИГРОВОЙ ЦИКЛ (60 FPS)
        //  Обновляет все игровые объекты, камеру, HUD и проверяет взаимодействия
        // ================================================================
        private void GameLoop_Tick(object sender, EventArgs e)
        {
            if (_player == null || _player.IsDead) return;

            AutoMovePlayer();          // Автоматическое движение игрока
            UpdateEnemies();           // Обновление AI врагов
            UpdateCamera();            // Сдвиг камеры за игроком
            UpdateHealthBars();        // Обновление полос здоровья
            _dropManager?.CheckPickup(_player, _inventoryManager); // Проверка подбора предметов
            UpdateHUD();               // Обновление интерфейса
        }

        // ================================================================
        //  АВТОМАТИЧЕСКОЕ ДВИЖЕНИЕ ИГРОКА
        //  Игрок движется вправо, атакует врагов в радиусе и проверяет достижение портала
        // ================================================================
        private void AutoMovePlayer()
        {
            if (_player.IsAttacking) return; // Игрок атакует - не двигается

            Enemy target = FindNearestEnemyAhead(); // Поиск ближайшего врага

            if (target != null)
            {
                double dist = target.X - _player.X;
                if (dist <= AttackRange)
                {
                    _player.StartAttack(); // Атака, если враг в радиусе
                    return;
                }
            }

            // Движение вперед
            _player.SetPosition(_player.X + MoveSpeed, _player.Y);
            _player.PlayWalk(); // Анимация ходьбы

            // Проверка достижения портала (центр игрока достигает центра портала)
            const double portalWidth = 32;
            double portalCenterX = _portalWorldX + portalWidth / 2.0;
            double playerCenterX = _player.X + CharRender / 2.0;

            if (playerCenterX >= portalCenterX)
                EnterPortal(); // Переход на следующий уровень
        }

        // ================================================================
        //  ПОИСК БЛИЖАЙШЕГО ВРАГА
        //  Находит врага с минимальным положительным смещением по X
        // ================================================================
        private Enemy FindNearestEnemyAhead()
        {
            Enemy nearest = null;
            double nearest_d = double.MaxValue;
            foreach (var en in _enemies)
            {
                if (en.IsDead) continue;
                double dx = en.X - _player.X;
                if (dx >= -50 && dx < nearest_d) // Враг впереди или чуть сзади
                {
                    nearest_d = dx;
                    nearest = en;
                }
            }
            return nearest;
        }

        // ================================================================
        //  ОБНОВЛЕНИЕ AI ВРАГОВ
        //  Каждый враг обновляет свое поведение относительно игрока
        // ================================================================
        private void UpdateEnemies()
        {
            foreach (var en in _enemies)
            {
                if (!en.IsDead) en.Update(_player.X, _player.Y);
            }
        }

        // ================================================================
        //  ОБНОВЛЕНИЕ КАМЕРЫ
        //  Сдвигает WorldLayer так, чтобы игрок оставался в центре
        // ================================================================
        private void UpdateCamera()
        {
            double viewW = 800; // Ширина видимой области
            double targetX = _player.X + CharRender / 2.0 - viewW / 2.0;
            double maxX = MapCols * TileRender - viewW;
            double offsetX = Math.Max(0, Math.Min(targetX, maxX)); // Ограничение движения камеры

            WorldTransform.X = -offsetX; // Сдвиг контейнера с картой
        }

        // ================================================================
        //  ОБНОВЛЕНИЕ ПОЛОС ЗДОРОВЬЯ
        //  Позиционирует полосы над игроком и врагами
        // ================================================================
        private void UpdateHealthBars()
        {
            const double barOffsetFromTop = 70; // Смещение от верха спрайта

            // Полоса здоровья игрока
            _playerHealthBar?.SetPosition(_player.X + CharRender / 2.0, _player.Y + barOffsetFromTop, 0);
            _playerHealthBar?.UpdateHealth(_player.Hp, _player.MaxHp);

            // Полосы здоровья врагов
            for (int i = 0; i < _enemies.Count && i < _enemyHealthBars.Count; i++)
            {
                var en = _enemies[i];
                var hb = _enemyHealthBars[i];

                if (en.IsDead) { hb.SetVisible(false); continue; } // Скрыть полосу мертвого врага

                hb.SetVisible(true);
                hb.SetPosition(en.X + CharRender / 2.0, en.Y + barOffsetFromTop, 0);
                hb.UpdateHealth(en.Hp, en.MaxHp);
            }
        }

        // ================================================================
        //  ПЕРЕХОД ЧЕРЕЗ ПОРТАЛ
        //  Увеличивает уровень и пересоздает уровень
        // ================================================================
        private void EnterPortal()
        {
            _level++;
            // Очистка не подобранного дропа
            if (_dropManager != null)
                foreach (var d in _dropManager.Drops) d.Collect();

            InitLevel(); // Создание следующего уровня
        }

        // ================================================================
        //  ОБРАБОТЧИК АТАКИ ИГРОКА
        //  Наносит урон всем врагам в зоне поражения
        // ================================================================
        private void Player_OnAttackHit()
        {
            foreach (var en in _enemies)
            {
                if (en.IsDead) continue;
                if (_player.GetBounds().IntersectsWith(en.GetBounds()))
                    en.TakeDamage(_player.Damage);
            }
        }

        // ================================================================
        //  ОБРАБОТЧИК АТАКИ ВРАГА
        //  Наносит урон игроку, если тот в зоне поражения
        // ================================================================
        private void Enemy_OnAttackHit(Enemy attacker)
        {
            if (attacker.IsDead || _player.IsDead) return;
            if (attacker.GetBounds().IntersectsWith(_player.GetBounds()))
                _player.TakeDamage(attacker.Damage);
        }

        // ================================================================
        //  ОБРАБОТЧИК СМЕРТИ ВРАГА
        //  Скрывает полосу здоровья и создает дроп
        // ================================================================
        private void Enemy_OnDeath(Enemy dead)
        {
            // Удаление полосы здоровья
            int idx = _enemies.IndexOf(dead);
            if (idx >= 0 && idx < _enemyHealthBars.Count)
                _enemyHealthBars[idx].Remove();

            // Создание дропа (монеты)
            _dropManager?.SpawnDrop(dead.X, CoinsIcon);
        }

        // ================================================================
        //  ОБРАБОТЧИК ОТКРЫТИЯ НАВЫКА
        //  Проверяет стоимость, списывает монеты и применяет бонусы
        // ================================================================
        private void OnSkillUnlocked(SkillDefinition skill)
        {
            if (_player.Coins < skill.Cost)
            {
                skill.Unlocked = false; // Отмена открытия, если недостаточно монет
                return;
            }
            _player.Coins -= skill.Cost;

            // Базовая прокачка (3%) плюс бонус навыка
            const double baseDamageBonus = 0.03;
            const double baseHpBonus = 0.03;
            _player.ApplySkillUpgrade(skill.DamageBonus + baseDamageBonus, skill.HpBonus + baseHpBonus);

            UpdateHUD();
        }

        // ================================================================
        //  ОБНОВЛЕНИЕ HUD (ЭКРАННОГО ИНТЕРФЕЙСА)
        //  Обновляет отображение монет и уровня
        // ================================================================
        private void UpdateHUD()
        {
            if (CoinsText != null) CoinsText.Text = $"Coins: {_player?.Coins ?? 0}";
            if (LevelText != null) LevelText.Text = $"  |  Level: {_level}";
        }

        // ================================================================
        //  ОБРАБОТЧИК КЛАВИАТУРЫ
        //  I - открыть/закрыть инвентарь
        //  O - открыть/закрыть дерево навыков
        // ================================================================
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.I) ToggleInventory();
            else if (e.Key == Key.O) ToggleSkillTree();
        }

        // ================================================================
        //  ПЕРЕКЛЮЧЕНИЕ ИНВЕНТАРЯ
        //  Показывает/скрывает оверлей инвентаря
        // ================================================================
        private void ToggleInventory()
        {
            _inventoryOpen = !_inventoryOpen;
            InventoryOverlay.Visibility = _inventoryOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        // ================================================================
        //  ПЕРЕКЛЮЧЕНИЕ ДЕРЕВА НАВЫКОВ
        //  Показывает/скрывает оверлей навыков
        // ================================================================
        private void ToggleSkillTree()
        {
            _skillTreeOpen = !_skillTreeOpen;
            SkillTreeOverlay.Visibility = _skillTreeOpen ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}