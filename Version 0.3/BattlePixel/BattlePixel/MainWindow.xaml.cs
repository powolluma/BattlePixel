using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BattlePixel
{
    // Все состояния игры
    public enum GameState
    {
        MainMenu, // Главное меню
        Playing,  // Игра идёт
        Paused,   // Пауза
        Dead,     // Экран смерти
        Settings  // Настройки
    }

    public partial class MainWindow : Window
    {
        // Менеджеры
        private TileManager _tileManager;               // Тайлы, карта
        private GameMap _gameMap;                       // Карта
        private DecorationManager _decorationManager;   // Декорации
        private InventoryManager _inventoryManager;     // Инвентарь
        private SkillTreeManager _skillTreeManager;     // Древо навыков
        private DropManager _dropManager;               // Выпадение айтемов
        private SoundManager _soundManager;             // Звуки

        // Сущности
        private Player _player;                                                     // Персонаж
        private Player _inventoryPlayer;                                            // Персонаж в инвентаре
        private readonly List<Enemy> _enemies = new List<Enemy>();                  // Список врагов
        private readonly List<HealthBar> _enemyHealthBars = new List<HealthBar>();  // Список полос здоровья врагов
        private HealthBar _playerHealthBar;                                         // Список полос здоровья персонажа

        // UI
        private bool _inventoryOpen;                            // Открытый инвентарь 
        private bool _skillTreeOpen;                            // Открытое древо навыков
        private GameState _gameState = GameState.MainMenu;      // Меню 
        private GameState _settingsReturnTo;                    // После закрытия настроек
        private DateTime _lastWalkSoundTime = DateTime.MinValue;// Хранение времени проигрывания звука

        // Массив файлов заднего фона
        private readonly string[] _backgrounds = new[]
        {
            "GrassLand_Background_Guide.png",
            "origbig_1.png",
            "origbig_2.png",
            "origbig_3.png",
            "origbig_4.png",
            "origbig_5.png"
        };

        // Начальный уровень 
        private int _level = 1;

        // Игровой цикл (Таймер)
        private readonly DispatcherTimer _gameLoop = new DispatcherTimer { 
            Interval = TimeSpan.FromMilliseconds(16) // Интервал срабатывания (60 Фпс)
        };

        // Константы 
        private const int MapCols = 26;             // Ширина
        private const int MapRows = 9;              // Высота
        private const int TileRender = 64;          // Размер тайла
        private const int CharRender = 250;         // Размер персонажа
        private const double MoveSpeed = 2.5;       // Скорость движения персонажа
        private const double AttackRange = 110.0;   // Дистанция атаки

        // Пути к ассетам
        private static string A(string rel) => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", rel); // Относительный путь - полный путь
        private string HealthSheet => A(@"Health\Health.png");
        private string InvSheet => A(@"Inventory\Inventory.png");
        private string ItemsDir => A("Items");
        private string SkillsDir => A("Skills");
        private string OrcDir => A(@"Characters\Orc\");
        private string DecoDir => A(@"Decoration\");
        private string CoinsIcon => A(@"Items\coin_spin-Sheet.png");
        
        private double _portalWorldX;   // Координата портала
        private double _groundY;        // Координата Y

        //Конструктор
        public MainWindow()
        {
            InitializeComponent();
        }

        // Событие загрузки окна
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.KeyDown += MainWindow_KeyDown;           // Реакция на клавиши 
            _gameLoop.Tick += GameLoop_Tick;              // Игровой цикл
            _soundManager = new SoundManager();           // Звуковая система
            BtnContinue.IsEnabled = SaveManager.HasSave;  // Сохранение
            LoadSettings();
            SetState(GameState.MainMenu);   
            _soundManager.StartMusic();
        }

        // Управление состоянием игры
        private void SetState(GameState newState)
        {
            _gameState = newState;
            // Скрыть
            MainMenuOverlay.Visibility = Visibility.Collapsed;
            PauseOverlay.Visibility = Visibility.Collapsed;
            DeathOverlay.Visibility = Visibility.Collapsed;
            SettingsOverlay.Visibility = Visibility.Collapsed;
            HudPanel.Visibility = Visibility.Collapsed;

            switch (newState)
            {
                case GameState.MainMenu:
                    MainMenuOverlay.Visibility = Visibility.Visible;
                    _gameLoop.Stop();
                    break;
                case GameState.Playing:
                    HudPanel.Visibility = Visibility.Visible;
                    _gameLoop.Start();
                    break;
                case GameState.Paused:
                    HudPanel.Visibility = Visibility.Visible;
                    PauseOverlay.Visibility = Visibility.Visible;
                    _gameLoop.Stop();
                    break;
                case GameState.Dead:
                    DeathOverlay.Visibility = Visibility.Visible;
                    _gameLoop.Stop();
                    break;
                case GameState.Settings:
                    SettingsOverlay.Visibility = Visibility.Visible;
                    _gameLoop.Stop();
                    break;
            }
        }

        // Обновление заднего фона
        private void UpdateBackground()
        {
            int index = (_level - 1) % _backgrounds.Length; 
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Backgrounds", _backgrounds[index]);  // Сборка полного пути
            
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();                   // Работа с изображениями в WPF
                bmp.BeginInit();                                                            
                bmp.UriSource = new Uri(path, UriKind.Absolute);                            // Указание относительного пути
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;    // Загрузка изображения
                bmp.EndInit();
                BackgroundImage.Source = bmp;   // Присваивание изображения
            }
            catch{}
        }

        //Инициализация уровня
        private void InitLevel(bool freshStart = false)
        {
            GameCanvas.Children.Clear();   
            _enemies.Clear();
            _enemyHealthBars.Clear();

            _tileManager = new TileManager(GameCanvas, sourceTileSize: 32, renderTileSize: TileRender);
            _gameMap = new GameMap(_tileManager, MapCols, MapRows);
            _gameMap.GenerateTestMap();     
            _gameMap.Render();  

            int groundRow = MapRows - 1;                
            double groundY = groundRow * TileRender;    
            _groundY = groundY;
            double charY = groundRow * TileRender - 142;    // Персонаж и враг
            double startX = 50; 

            // Игрок 
            if (_player == null || freshStart)
            {
                // Новая игра
                if (_player != null)
                    ResetPlayerCompletely();
                _player = new Player(GameCanvas, frameSize: 100, renderSize: CharRender);
                _player.OnAttackHit += Player_OnAttackHit;
                _player.OnDeathFinished += Player_OnDeathFinished;
            }
            else
            {
                _player.ReaddToCanvas(GameCanvas); // Добавить перса
                _player.ResetForNewLevel();        // Сброс здоровья, сохранение прокачки
            }
            _player.SetPosition(startX, charY);

            // Шкала здоровья игрока 
            _playerHealthBar?.Remove();
            _playerHealthBar = new HealthBar(GameCanvas, HealthSheet, isGreen: true, renderWidth: 80, renderHeight: 16);
            _playerHealthBar.SetPosition(startX + CharRender / 2.0, charY + 165, 0);
            _playerHealthBar.UpdateHealth(_player.Hp, _player.MaxHp);

            // Враги 
            double[] enemyXs = { 500, 800, 1100, 1400 };
            foreach (double ex in enemyXs)
            {
                var enemy = new Enemy(GameCanvas, basePath: OrcDir, namePrefix: "Orc", frameSize: 100, renderSize: CharRender, level: _level, facingRight: true);
                enemy.SetPosition(ex, charY);
                enemy.OnDeath += Enemy_OnDeath;
                enemy.OnAttackHit += () => Enemy_OnAttackHit(enemy);
                var hb = new HealthBar(GameCanvas, HealthSheet, isGreen: false, renderWidth: 80, renderHeight: 16);
                hb.SetPosition(ex + CharRender / 2.0, charY + 165, 0);
                hb.UpdateHealth(enemy.Hp, enemy.MaxHp);
                _enemies.Add(enemy);
                _enemyHealthBars.Add(hb);
            }

            // Декорации 
            _portalWorldX = MapCols * TileRender - 32 - 40;
            _decorationManager = new DecorationManager(GameCanvas);
            var portal = new DecorationInfo(DecoDir + "GrassLand_Entry_3.png", 32, 64);
            _decorationManager.PlaceOnGround(portal, x: _portalWorldX, groundY);

            var flowers = new List<DecorationInfo>
            {
                new DecorationInfo(DecoDir + "GrassLand_Flower_1.png", 32, 32),
                new DecorationInfo(DecoDir + "GrassLand_Flower_2.png", 32, 32),
                new DecorationInfo(DecoDir + "GrassLand_Flower_3.png", 32, 32),
            };
            _decorationManager.ScatterRandom(flowers, 8, 200, 1600, groundY);

            var tallGrass = new List<DecorationInfo>
            {
                new DecorationInfo(DecoDir + "GrassLand_TallGrass_1.png", 32, 32),
                new DecorationInfo(DecoDir + "GrassLand_TallGrass_2.png", 32, 32),
            };
            _decorationManager.ScatterRandom(tallGrass, 8, 200, 1600, groundY);

            var stones = new List<DecorationInfo>
            {
                new DecorationInfo(DecoDir + "GrassLand_Stone_1.png", 32, 32),
                new DecorationInfo(DecoDir + "GrassLand_Stone_2.png", 32, 32),
                new DecorationInfo(DecoDir + "GrassLand_Stone_3.png", 64, 32),
            };
            _decorationManager.ScatterRandom(stones, 6, 200, 1600, groundY);

            // Инвентарь и древо навыков
            if (_inventoryManager == null || freshStart)
            {
                _inventoryManager = new InventoryManager(InvSheet, ItemsDir);
                _inventoryManager.BuildSlots(InventoryGrid, slotRenderSize: 48);
            }
            if (_inventoryPlayer == null || freshStart)
            {
                if (_inventoryPlayer != null)
                    InventoryCharacterCanvas.Children.Clear();
                _inventoryPlayer = new Player(InventoryCharacterCanvas, frameSize: 100, renderSize: 240);
                _inventoryPlayer.SetPosition(10, 10);
            }
            if (_skillTreeManager == null || freshStart)
            {
                _skillTreeManager = new SkillTreeManager(InvSheet);
                _skillTreeManager.OnSkillUnlocked += OnSkillUnlocked;
                BuildSkillTree();
            }

            _dropManager = new DropManager(GameCanvas, ItemsDir, _groundY);

            UpdateBackground();
            UpdateHUD();
        }

        // Полный сброс игрока
        private void ResetPlayerCompletely()
        {
            _player = null;
            _inventoryManager = null;
            _inventoryPlayer = null;
            _skillTreeManager = null;
            _level = 1;
        }

        // Основной игровой цикл
        private void GameLoop_Tick(object sender, EventArgs e)
        {
            if (_player == null || _player.IsDead || _gameState != GameState.Playing) return;

            AutoMovePlayer();
            UpdateEnemies();
            UpdateCamera();
            UpdateHealthBars();
            _dropManager?.CheckPickup(_player, _inventoryManager);
            UpdateHUD();
        }

        // Автодвижение игрока
        private void AutoMovePlayer()
        {
            if (_player.IsAttacking) return;

            // Проверка дистанции до врага
            Enemy target = FindNearestEnemyAhead();
            if (target != null) 
            {
                double dist = target.X - _player.X;
                if (dist <= AttackRange)
                {
                    _soundManager?.PlayPlayerAttack();
                    _player.StartAttack();
                    return;
                }
            }

            _player.SetPosition(_player.X + MoveSpeed, _player.Y);
            _player.PlayWalk();

            // Расчет следующего звука ходьбы
            if ((DateTime.UtcNow - _lastWalkSoundTime).TotalMilliseconds > 400)
            {
                _soundManager?.PlayPlayerWalk();
                _lastWalkSoundTime = DateTime.UtcNow;
            }

            // Проверка входа в портал
            const double portalWidth = 32;
            double portalCenterX = _portalWorldX + portalWidth / 2.0;
            double playerCenterX = _player.X + CharRender / 2.0;
            if (playerCenterX >= portalCenterX) EnterPortal();
        }

        // Нахождение первого врага
        private Enemy FindNearestEnemyAhead()
        {
            Enemy nearest = null;
            double nearestD = double.MaxValue;
            foreach (var en in _enemies)
            {
                if (en.IsDead) continue;
                double dx = en.X - _player.X;
                if (dx >= -50 && dx < nearestD) { nearestD = dx; nearest = en; }
            }
            return nearest;
        }

        // Обновление врагов
        private void UpdateEnemies()
        {
            foreach (var en in _enemies)
            {
                if (!en.IsDead)
                {
                    var stateBefore = en.State;     // Сохранение состояния
                    en.Update(_player.X, _player.Y);
                    // Звук шагов при Walk
                    if (en.State == EnemyState.Walk && stateBefore != EnemyState.Walk)
                        _soundManager?.PlayEnemyWalk();
                }
            }
        }

        // Обновление камеры
        private void UpdateCamera()
        {
            double viewW = 800;                                             // Видимая область в ширину
            double targetX = _player.X + CharRender / 2.0 - viewW / 2.0;    // Позиция для центирования по персонажу
            double maxX = MapCols * TileRender - viewW;                     // Максимальное смещение камеры
            double offsetX = Math.Max(0, Math.Min(targetX, maxX));          // Ограничение кармеры
            WorldTransform.X = -offsetX;                                    // Сдвиг мира
        }

        // Обновление шкалы здоровья
        private void UpdateHealthBars()
        {
            const double barOffset = 70;
            _playerHealthBar?.SetPosition(_player.X + CharRender / 2.0, _player.Y + barOffset, 0);
            _playerHealthBar?.UpdateHealth(_player.Hp, _player.MaxHp);

            // Цикл для шкалы здоровья врагов
            for (int i = 0; i < _enemies.Count && i < _enemyHealthBars.Count; i++)
            {
                var en = _enemies[i];
                var hb = _enemyHealthBars[i];
                if (en.IsDead) { hb.SetVisible(false); continue; }
                hb.SetVisible(true);
                hb.SetPosition(en.X + CharRender / 2.0, en.Y + barOffset, 0);
                hb.UpdateHealth(en.Hp, en.MaxHp);
            }
        }

        // Переход в портал
        private void EnterPortal()
        {
            _level++;
            if (_dropManager != null)
                foreach (var d in _dropManager.Drops) d.Collect();

            AutoSave();     
            InitLevel();    
        }

        // События в бою

        // Атака игрока
        private void Player_OnAttackHit()
        {
            foreach (var en in _enemies)
            {
                if (en.IsDead) continue;
                if (_player.GetBounds().IntersectsWith(en.GetBounds())) // Проверка пересечения хитбоксов (координат)
                {
                    en.TakeDamage(_player.Damage);
                    _soundManager?.PlayEnemyDamage();
                }
            }
        }

        // Атака врага
        private void Enemy_OnAttackHit(Enemy attacker)
        {
            if (attacker.IsDead || _player.IsDead) return;
            if (attacker.GetBounds().IntersectsWith(_player.GetBounds()))
            {
                _player.TakeDamage(attacker.Damage);
                _soundManager?.PlayPlayerDamage();
            }
        }

        // Смерть врага
        private void Enemy_OnDeath(Enemy dead)
        {
            int idx = _enemies.IndexOf(dead);
            if (idx >= 0 && idx < _enemyHealthBars.Count)
                _enemyHealthBars[idx].Remove(); // Удаление шкалы здоровья врага

            _dropManager?.SpawnDrop(dead.X, CoinsIcon);
        }

        // Смерть персонажа
        private void Player_OnDeathFinished()
        {
            // Проигрывание анимации
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (s, e) => { t.Stop(); SetState(GameState.Dead); };
            t.Start();
        }

        // Разблокировка скиллов
        private void OnSkillUnlocked(SkillDefinition skill)
        {
            if (_player.Coins < skill.Cost) { skill.Unlocked = false; return; }
            _player.Coins -= skill.Cost;
            const double base_ = 0.03;
            _player.ApplySkillUpgrade(skill.DamageBonus + base_, skill.HpBonus + base_);
            UpdateHUD();
        }

        // Обновление HUD
        private void UpdateHUD()
        {
            if (CoinsText != null) CoinsText.Text = $"Coins: {_player?.Coins ?? 0}";
            if (LevelText != null) LevelText.Text = $" | Level: {_level}";
        }

        // Клавиши
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_gameState == GameState.Playing)
                    SetState(GameState.Paused);
                else if (_gameState == GameState.Paused)
                    SetState(GameState.Playing);
                else if (_gameState == GameState.Settings)
                    SetState(_settingsReturnTo);
                return;
            }

            if (_gameState != GameState.Playing) return;

            if (e.Key == Key.I) ToggleInventory();     
            else if (e.Key == Key.O) ToggleSkillTree(); 
        }

        // Кнопки главного меню
        // Новая игра
        private void BtnNewGame_Click(object sender, RoutedEventArgs e)
        {
            SaveManager.Delete();       
            ResetPlayerCompletely();
            InitLevel(freshStart: true);
            SetState(GameState.Playing);
        }

        // Продолжение
        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            var save = SaveManager.Load();
            if (save == null) { BtnNewGame_Click(sender, e); return; }
            ResetPlayerCompletely();
            _level = save.Level;
            InitLevel(freshStart: true);
            ApplySaveToPlayer(save);
            SetState(GameState.Playing);
        }

        // Настройки
        private void BtnMainSettings_Click(object sender, RoutedEventArgs e)
        {
            _settingsReturnTo = GameState.MainMenu;
            SetState(GameState.Settings);
        }

        // Выход из игры
        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Кнопки паузы
        // Продолжить
        private void BtnPauseResume_Click(object sender, RoutedEventArgs e)
        {
            CloseOverlays();
            SetState(GameState.Playing);
        }

        // Настройки
        private void BtnPauseSettings_Click(object sender, RoutedEventArgs e)
        {
            _settingsReturnTo = GameState.Paused;
            SetState(GameState.Settings);
        }

        // Начать заново
        private void BtnPauseRestart_Click(object sender, RoutedEventArgs e)
        {
            SaveManager.Delete();
            ResetPlayerCompletely();
            InitLevel(freshStart: true);
            SetState(GameState.Playing);
        }

        // Выход из игры
        private void BtnPauseExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Кнопки экрана смерти
        // Начать заново
        private void BtnDeathRestart_Click(object sender, RoutedEventArgs e)
        {
            SaveManager.Delete();
            ResetPlayerCompletely();
            InitLevel(freshStart: true);
            SetState(GameState.Playing);
        }

        // Выход из игры
        private void BtnDeathExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }


        // Перерождение
        private void BtnDeathRebirth_Click(object sender, RoutedEventArgs e)
        {
            double savedDamageMultiplier = _player?.DamageMultiplier ?? 1.0;
            double savedMaxHpMultiplier = _player?.MaxHpMultiplier ?? 1.0;
            int savedCoins = _player?.Coins ?? 0;

            var savedInventory = _inventoryManager != null ? new List<string>(_inventoryManager.Items.Select(i => i.name)) : new List<string>();

            var savedSkills = _skillTreeManager != null
                ? new List<string>(_skillTreeManager.Skills
                    .Where(s => s.Unlocked)
                    .Select(s => s.Name))
                : new List<string>();

            ResetPlayerCompletely(); 
            InitLevel(freshStart: true);

            _player.DamageMultiplier = savedDamageMultiplier;
            _player.MaxHpMultiplier = savedMaxHpMultiplier;
            _player.Coins = savedCoins;
            _player.ResetForNewLevel(); 

            foreach (var name in savedInventory)
                _inventoryManager?.TryAddItem(name);

            if (_skillTreeManager != null)
                foreach (var skill in _skillTreeManager.Skills)
                    skill.Unlocked = savedSkills.Contains(skill.Name);

            UpdateHUD();
            SetState(GameState.Playing);
        }

        // Настройки

        // Регулировка музыки
        private void MusicSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MusicPct != null)
                MusicPct.Text = $"{(int)e.NewValue}%";
            _soundManager?.SetMusicVolume(e.NewValue / 100.0);
        }

        // Регулировка звуков
        private void SoundSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SoundPct != null)
                SoundPct.Text = $"{(int)e.NewValue}%";
            _soundManager?.SetSoundVolume(e.NewValue / 100.0);
        }

        // Назад
        private void BtnSettingsBack_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            SetState(_settingsReturnTo);

            if (_settingsReturnTo == GameState.Paused)
                PauseOverlay.Visibility = Visibility.Visible;
            else if (_settingsReturnTo == GameState.MainMenu)
                MainMenuOverlay.Visibility = Visibility.Visible;
        }

        // Загрузка настроек
        private void LoadSettings()
        {
            var save = SaveManager.Load();
            if (save == null) return;

            MusicSlider.Value = save.MusicVolume * 100;
            SoundSlider.Value = save.SoundVolume * 100;
            _soundManager?.SetMusicVolume(save.MusicVolume);
            _soundManager?.SetSoundVolume(save.SoundVolume);
        }

        // Сохранение настроек
        private void SaveSettings()
        {
            var save = SaveManager.Load() ?? new SaveData();
            save.MusicVolume = MusicSlider.Value / 100.0;
            save.SoundVolume = SoundSlider.Value / 100.0;
            SaveManager.Save(save);
        }

        // Автосохранение 
        private void AutoSave()
        {
            if (_player == null) return;

            var save = new SaveData
            {
                Level = _level,
                Hp = _player.Hp,
                MaxHp = _player.MaxHp,
                Damage = _player.Damage,
                Coins = _player.Coins,
                DamageMultiplier = _player.DamageMultiplier,
                MaxHpMultiplier = _player.MaxHpMultiplier,
                MusicVolume = MusicSlider.Value / 100.0,
                SoundVolume = SoundSlider.Value / 100.0,
            };

            if (_inventoryManager != null)
                foreach (var (name, _) in _inventoryManager.Items)
                    save.InventoryItems.Add(name);

            if (_skillTreeManager != null)
                foreach (var skill in _skillTreeManager.Skills)
                    if (skill.Unlocked)
                        save.UnlockedSkills.Add(skill.Name);

            SaveManager.Save(save); 
            BtnContinue.IsEnabled = true;
        }

        // Применение данных сохранения к игроку и инвентарю
        private void ApplySaveToPlayer(SaveData save)
        {
            if (_player == null || save == null) return;

            _player.DamageMultiplier = save.DamageMultiplier;
            _player.MaxHpMultiplier = save.MaxHpMultiplier;
            _player.Coins = save.Coins;
            _player.ResetForNewLevel(); 

            if (_inventoryManager != null)
                foreach (var name in save.InventoryItems)
                    _inventoryManager.TryAddItem(name);

            if (_skillTreeManager != null)
                foreach (var skill in _skillTreeManager.Skills)
                    skill.Unlocked = save.UnlockedSkills.Contains(skill.Name);
            UpdateHUD();
        }

        // Закрытие оверлеев
        private void CloseOverlays()
        {
            _inventoryOpen = false;
            _skillTreeOpen = false;
            InventoryOverlay.Visibility = Visibility.Collapsed;
            SkillTreeOverlay.Visibility = Visibility.Collapsed;
        }

        // Открытие инвентаря
        private void ToggleInventory()
        {
            _inventoryOpen = !_inventoryOpen;
            InventoryOverlay.Visibility = _inventoryOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        // Открытие древа навыков
        private void ToggleSkillTree()
        {
            _skillTreeOpen = !_skillTreeOpen;
            SkillTreeOverlay.Visibility = _skillTreeOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        // Построение древа навыков
        private void BuildSkillTree()
        {
            string s = SkillsDir + "\\";
            var branches = new List<SkillBranch>
            {
                new SkillBranch(-90, new[] { s+"dragon_charges_skill_icon.png", s+"dragon_roar_skill_icon.png", s+"dragon_tail_icon.png", s+"dragon_wing_skill_icon.png" }),
                new SkillBranch(-38.6, new[] { s+"fireskillicon2.png", s+"fireskillicon3.png", s+"fireskillicon4.png", s+"fireskillicon.png" }),
                new SkillBranch( 12.9, new[] { s+"healingskillicon2.png", s+"healingskillicon3.png", s+"healingskillicon4.png", s+"healingskillicon.png" }),
                new SkillBranch( 64.3, new[] { s+"Iceskillicon2.png", s+"Iceskillicon3.png", s+"Iceskillicon4.png", s+"Iceskillicon.png" }),
                new SkillBranch(115.7, new[] { s+"plantskillicon2.png", s+"plantskillicon3.png", s+"plantskillicon4.png", s+"plantskillicon.png" }),
                new SkillBranch(167.1, new[] { s+"Poisonskillicon2.png", s+"Poison skillicon3.png", s+"Poisonskillicon4.png", s+"Poisonskillicon.png" }),
                new SkillBranch(-141.4, new[] { s+"slashskillicon2.png", s+"slashskillicon3.png", s+"slashskillicon4.png", s+"slashskillicon.png" }),
            };

            _skillTreeManager.BuildTree(SkillTreeCanvas, branches,
                centerX: 260, centerY: 260, slotRenderSize: 44, spacing: 55, startRadius: 50);
        }
    }
}