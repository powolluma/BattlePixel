using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BattlePixel
{
    /// <summary>
    /// Состояния игрового персонажа
    /// </summary>
    public enum PlayerState { Idle, Walk, Attack, Hurt, Death }

    /// <summary>
    /// Класс игрока. Управляет анимацией, характеристиками, состояниями и боем.
    /// Персонаж использует спрайт-листы с кадрами для каждого действия.
    /// </summary>
    public class Player
    {
        // ==================== КОМПОНЕНТЫ ОТОБРАЖЕНИЯ ====================
        private Canvas _canvas;   // Канвас для рендеринга (пересоздается при смене уровня)
        private readonly Image _image; // Изображение персонажа

        // Словарь анимаций: имя -> список кадров (CroppedBitmap)
        private readonly Dictionary<string, List<CroppedBitmap>> _animations = new Dictionary<string, List<CroppedBitmap>>();

        private string _currentAnimation; // Имя текущей проигрываемой анимации
        private int _currentFrame;        // Текущий кадр анимации
        private readonly DispatcherTimer _animTimer; // Таймер для смены кадров

        // ==================== ХАРАКТЕРИСТИКИ ====================
        public int MaxHp { get; private set; }  // Максимальное здоровье
        public int Hp { get; private set; }     // Текущее здоровье
        public int Damage { get; private set; } // Базовый урон
        public int Coins { get; set; }          // Количество монет

        // ==================== ПОЗИЦИЯ НА КАРТЕ ====================
        public double X { get; private set; }   // Мировая координата X
        public double Y { get; private set; }   // Мировая координата Y

        public int RenderSize { get; }          // Размер отображения персонажа в пикселях

        // ==================== СОСТОЯНИЯ ====================
        public PlayerState State { get; private set; } = PlayerState.Idle;
        private bool _isAttacking;     // Флаг выполнения атаки
        private bool _isHurt;          // Флаг получения урона
        private bool _isDead;          // Флаг смерти
        private bool _isPlayingDeathOnce; // Флаг однократного проигрывания анимации смерти

        // ==================== МУЛЬТИПЛИКАТОРЫ НАВЫКОВ ====================
        public double DamageMultiplier { get; set; } = 1.0;   // Множитель урона от навыков
        public double MaxHpMultiplier { get; set; } = 1.0;    // Множитель здоровья от навыков

        // ==================== СОБЫТИЯ ====================
        public event Action OnAttackHit;       // Срабатывает в середине анимации атаки
        public event Action OnDeathFinished;   // Срабатывает после завершения анимации смерти

        // ==================== ТАЙМЕРЫ СОСТОЯНИЙ ====================
        private readonly DispatcherTimer _attackTimer; // Таймер длительности атаки
        private readonly DispatcherTimer _hurtTimer;   // Таймер длительности получения урона
        private readonly DispatcherTimer _hitTimer;    // Таймер момента нанесения урона

        // ================================================================
        //  КОНСТРУКТОР
        //  Загружает спрайт-листы, настраивает анимации и таймеры
        // ================================================================
        public Player(Canvas canvas, int frameSize, int renderSize, int framesPerSecond = 8)
        {
            _canvas = canvas;
            RenderSize = renderSize;

            // Начальные характеристики
            MaxHp = 100;
            Hp = MaxHp;
            Damage = 20;
            Coins = 0;

            // Создание изображения на канвасе
            _image = new Image
            {
                Width = renderSize,
                Height = renderSize,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
            Panel.SetZIndex(_image, 10);
            _canvas.Children.Add(_image);

            // Загрузка анимаций из спрайт-листов
            LoadAnimation("Idle", GetAssetPath("Soldier-Idle.png"), 100, 6);
            LoadAnimation("Walk", GetAssetPath("Soldier-Walk.png"), 100, 8);
            LoadAnimation("Attack_1", GetAssetPath("Soldier-Attack01.png"), 100, 6);
            LoadAnimation("Attack_2", GetAssetPath("Soldier-Attack02.png"), 100, 6);
            LoadAnimation("Hurt", GetAssetPath("Soldier-Hurt.png"), 100, 4);
            LoadAnimation("Death", GetAssetPath("Soldier-Death.png"), frameSize, 4);

            // Таймер анимации
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / framesPerSecond) };
            _animTimer.Tick += AnimTimer_Tick;

            // Таймер атаки: длится 6 кадров при 8fps ≈ 750 мс
            _attackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _attackTimer.Tick += (s, e) => { _attackTimer.Stop(); _isAttacking = false; SetState(PlayerState.Walk); };

            // Таймер нанесения урона: срабатывает через 350 мс после начала атаки
            _hitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _hitTimer.Tick += (s, e) => { _hitTimer.Stop(); OnAttackHit?.Invoke(); };

            // Таймер получения урона: 4 кадра ≈ 500 мс
            _hurtTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _hurtTimer.Tick += (s, e) => { _hurtTimer.Stop(); _isHurt = false; if (!_isDead) SetState(PlayerState.Walk); };

            // Начальное состояние
            Play("Idle");
        }

        // ================================================================
        //  ЗАГРУЗКА СПРАЙТ-ЛИСТА
        //  Разбивает спрайт-лист на отдельные кадры по ширине кадра
        // ================================================================
        private string GetAssetPath(string fileName)
        {
            // Путь к ресурсам в папке Assets проекта
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return System.IO.Path.Combine(baseDir, "Assets", "Characters", "Soldier", fileName);
        }

        private void LoadAnimation(string name, string filePath, int frameSize, int frameCount)
        {
            try
            {
                // Загрузка спрайт-листа
                var sheet = new BitmapImage();
                sheet.BeginInit();
                sheet.UriSource = new Uri(filePath, UriKind.Absolute);
                sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.EndInit();

                // Разбивка на кадры
                var frames = new List<CroppedBitmap>();
                for (int i = 0; i < frameCount; i++)
                {
                    var rect = new Int32Rect(i * frameSize, 0, frameSize, frameSize);
                    frames.Add(new CroppedBitmap(sheet, rect));
                }
                _animations[name] = frames;
            }
            catch { /* Файл не найден - анимация будет пропущена */ }
        }

        // ================================================================
        //  БОЕВЫЕ МЕТОДЫ
        // ================================================================

        /// <summary>
        /// Начать атаку. Запускает анимацию и таймеры.
        /// </summary>
        public void StartAttack()
        {
            if (_isDead || _isAttacking) return;
            _isAttacking = true;
            SetState(PlayerState.Attack);
            _attackTimer.Stop();
            _attackTimer.Start();

            _hitTimer.Stop();
            _hitTimer.Start();
        }

        /// <summary>
        /// Получить урон. Уменьшает HP и запускает анимацию получения урона.
        /// </summary>
        public void TakeDamage(int amount)
        {
            if (_isDead) return;
            Hp = Math.Max(0, Hp - amount);
            if (Hp == 0)
            {
                Die();
                return;
            }
            _isHurt = true;
            SetState(PlayerState.Hurt);
            _hurtTimer.Stop();
            _hurtTimer.Start();
        }

        /// <summary>
        /// Лечение. Восстанавливает HP до максимума.
        /// </summary>
        public void Heal(int amount)
        {
            Hp = Math.Min(MaxHp, Hp + amount);
        }

        /// <summary>
        /// Смерть. Останавливает все действия и запускает анимацию смерти.
        /// </summary>
        public void Die()
        {
            if (_isDead) return;
            _isDead = true;
            _attackTimer.Stop();
            _hurtTimer.Stop();
            _hitTimer.Stop();
            SetState(PlayerState.Death);
            OnDeathFinished?.Invoke();
        }

        // ==================== СВОЙСТВА СОСТОЯНИЙ ====================
        public bool IsDead => _isDead;
        public bool IsAttacking => _isAttacking;

        // ================================================================
        //  МЕТОДЫ СБРОСА СОСТОЯНИЯ
        // ================================================================

        /// <summary>
        /// Сброс при переходе на новый уровень. Сохраняет характеристики.
        /// </summary>
        public void ResetForNewLevel()
        {
            _isDead = false;
            _isAttacking = false;
            _isHurt = false;
            _isPlayingDeathOnce = false;
            _attackTimer.Stop();
            _hurtTimer.Stop();

            // Пересчет характеристик с учетом мультипликаторов навыков
            int newMax = (int)(100 * MaxHpMultiplier);
            if (newMax != MaxHp)
            {
                MaxHp = newMax;
                Hp = MaxHp;
            }
            else
            {
                Hp = MaxHp;
            }
            Damage = (int)(20 * DamageMultiplier);

            _currentAnimation = null; // Принудительный перезапуск анимации
            SetState(PlayerState.Walk);
        }

        /// <summary>
        /// Возрождение после смерти. Восстанавливает здоровье до максимума.
        /// </summary>
        public void RespawnKeepStats()
        {
            _isDead = false;
            _isAttacking = false;
            _isHurt = false;
            _isPlayingDeathOnce = false;
            _attackTimer.Stop();
            _hurtTimer.Stop();
            _hitTimer.Stop();

            Hp = MaxHp; // Полное восстановление здоровья

            _currentAnimation = null;
            SetState(PlayerState.Walk);
        }

        /// <summary>
        /// Применить улучшение от навыка. Увеличивает мультипликаторы.
        /// </summary>
        public void ApplySkillUpgrade(double dmgBonus, double hpBonus)
        {
            DamageMultiplier += dmgBonus;
            MaxHpMultiplier += hpBonus;
            Damage = (int)(20 * DamageMultiplier);
            int newMax = (int)(100 * MaxHpMultiplier);
            int diff = newMax - MaxHp;
            MaxHp = newMax;
            Hp = Math.Min(MaxHp, Hp + diff);
        }

        // ================================================================
        //  ПОЗИЦИОНИРОВАНИЕ И РЕНДЕРИНГ
        // ================================================================

        /// <summary>
        /// Установка позиции персонажа на канвасе.
        /// </summary>
        public void SetPosition(double x, double y)
        {
            X = x;
            Y = y;
            Canvas.SetLeft(_image, x);
            Canvas.SetTop(_image, y);
        }

        /// <summary>
        /// Перемещение на новый канвас при смене уровня.
        /// </summary>
        public void ReaddToCanvas(Canvas newCanvas)
        {
            _canvas = newCanvas;
            if (!newCanvas.Children.Contains(_image))
                newCanvas.Children.Add(_image);
        }

        // ================================================================
        //  УПРАВЛЕНИЕ СОСТОЯНИЯМИ И АНИМАЦИЯМИ
        // ================================================================

        /// <summary>
        /// Установка состояния персонажа.
        /// </summary>
        private void SetState(PlayerState newState)
        {
            if (State == newState) return;
            State = newState;
            switch (newState)
            {
                case PlayerState.Idle: Play("Idle"); break;
                case PlayerState.Walk: Play("Walk"); break;
                case PlayerState.Attack: Play("Attack_1"); break;
                case PlayerState.Hurt: Play("Hurt"); break;
                case PlayerState.Death: Play("Death"); break;
            }
        }

        //Запуск анимации ходьбы (если не атакует, не ранен и не мертв).
        public void PlayWalk() { if (!_isAttacking && !_isHurt && !_isDead) SetState(PlayerState.Walk); }

        //Запуск анимации ожидания.
        public void PlayIdle() { if (!_isAttacking && !_isHurt && !_isDead) SetState(PlayerState.Idle); }

        /// <summary>
        /// Запуск анимации по имени. Циклическое проигрывание, кроме анимации смерти.
        /// </summary>
        private void Play(string animName)
        {
            if (!_animations.ContainsKey(animName)) return;
            if (_currentAnimation == animName) return;

            _currentAnimation = animName;
            _currentFrame = 0;
            _image.Source = _animations[_currentAnimation][0];
            _isPlayingDeathOnce = (animName == "Death");

            _animTimer.Stop();
            _animTimer.Start();
        }

        /// <summary>
        /// Таймер смены кадров анимации.
        /// </summary>
        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            if (_currentAnimation == null || !_animations.ContainsKey(_currentAnimation)) return;
            var frames = _animations[_currentAnimation];

            // Анимация смерти проигрывается один раз и останавливается на последнем кадре
            if (_isPlayingDeathOnce)
            {
                if (_currentFrame >= frames.Count - 1)
                {
                    _animTimer.Stop();
                    return;
                }
                _currentFrame++;
                _image.Source = frames[_currentFrame];
                return;
            }

            // Циклическое проигрывание остальных анимаций
            _currentFrame = (_currentFrame + 1) % frames.Count;
            _image.Source = frames[_currentFrame];
        }

        // ================================================================
        //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ================================================================

        /// <summary>
        /// Получить ограничивающий прямоугольник персонажа для проверки столкновений.
        /// Используется сжатый размер для более точных попаданий.
        /// </summary>
        public Rect GetBounds()
        {
            return new Rect(X + RenderSize * 0.2, Y + RenderSize * 0.2,
                            RenderSize * 0.6, RenderSize * 0.6);
        }
    }
}