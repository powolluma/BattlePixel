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
    /// Состояния врага
    /// </summary>
    public enum EnemyState { Idle, Walk, Attack, Hurt, Death }

    /// <summary>
    /// Класс врага. Управляет анимацией, характеристиками, AI поведением и боем.
    /// Враги автоматически преследуют игрока в радиусе обнаружения и атакуют в радиусе атаки.
    /// Характеристики масштабируются в зависимости от уровня.
    /// </summary>
    public class Enemy
    {
        // ==================== КОМПОНЕНТЫ ОТОБРАЖЕНИЯ ====================
        private readonly Canvas _canvas;                              // Канвас для рендеринга
        private readonly Image _image;                               // Изображение врага
        private readonly Dictionary<string, List<CroppedBitmap>> _animations =
            new Dictionary<string, List<CroppedBitmap>>();           // Словарь анимаций: имя -> кадры

        private string _currentAnimation;    // Текущая анимация
        private int _currentFrame;           // Текущий кадр
        private readonly DispatcherTimer _animTimer; // Таймер анимации
        private readonly DispatcherTimer _hitTimer;  // Таймер нанесения урона
        private bool _isPlayingDeathOnce;    // Флаг однократного проигрывания смерти

        // ==================== ХАРАКТЕРИСТИКИ ====================
        public int MaxHp { get; private set; }        // Максимальное здоровье
        public int Hp { get; private set; }           // Текущее здоровье
        public int Damage { get; private set; }       // Урон
        public double MoveSpeed { get; private set; } // Скорость движения (пикселей за тик)
        public double DetectRange { get; private set; } // Радиус обнаружения игрока
        public double AttackRange { get; private set; } // Радиус атаки

        // ==================== ПОЗИЦИЯ ====================
        public double X { get; private set; }         // Мировая координата X
        public double Y { get; private set; }         // Мировая координата Y

        public int RenderSize { get; }                // Размер отображения

        // ==================== СОСТОЯНИЯ ====================
        public EnemyState State { get; private set; } = EnemyState.Idle;
        private bool _isAttacking;           // Флаг выполнения атаки
        private bool _isHurt;                // Флаг получения урона
        public bool IsDead { get; private set; } // Флаг смерти

        // ==================== СОБЫТИЯ ====================
        public event Action<Enemy> OnDeath;   // Срабатывает при смерти врага
        public event Action OnAttackHit;      // Срабатывает при успешной атаке

        // ==================== ТАЙМЕРЫ СОСТОЯНИЙ ====================
        private readonly DispatcherTimer _attackTimer;      // Длительность атаки
        private readonly DispatcherTimer _hurtTimer;        // Длительность получения урона
        private readonly DispatcherTimer _attackCooldown;   // Перезарядка атаки
        private bool _attackOnCooldown;          // Флаг перезарядки

        // ================================================================
        //  КОНСТРУКТОР
        //  Загружает спрайт-листы врага и настраивает характеристики
        //  в зависимости от уровня игры
        // ================================================================
        public Enemy(Canvas canvas, string basePath, string namePrefix,
                     int frameSize, int renderSize, int level = 1, bool facingRight = true,
                     int framesPerSecond = 8)
        {
            _canvas = canvas;
            RenderSize = renderSize;

            // Масштабирование характеристик в зависимости от уровня
            MaxHp = 50 + (level - 1) * 25;        // HP растет с уровнем
            Hp = MaxHp;
            Damage = 10 + (level - 1) * 5;        // Урон растет с уровнем
            MoveSpeed = 1.5 + (level - 1) * 0.3;  // Скорость растет с уровнем
            DetectRange = 300;                    // Фиксированный радиус обнаружения
            AttackRange = renderSize * 0.4;       // Радиус атаки зависит от размера

            // Создание изображения на канвасе
            _image = new Image
            {
                Width = renderSize,
                Height = renderSize,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
            Panel.SetZIndex(_image, 10);
            SetFacing(facingRight); // Установка направления взгляда
            _canvas.Children.Add(_image);

            // Загрузка анимаций из спрайт-листов
            LoadAnimation("Idle", basePath + namePrefix + "-Idle.png", frameSize, 6);
            LoadAnimation("Walk", basePath + namePrefix + "-Walk.png", frameSize, 8);
            LoadAnimation("Attack_1", basePath + namePrefix + "-Attack01.png", frameSize, 6);
            LoadAnimation("Attack_2", basePath + namePrefix + "-Attack02.png", frameSize, 6);
            LoadAnimation("Hurt", basePath + namePrefix + "-Hurt.png", frameSize, 4);
            LoadAnimation("Death", basePath + namePrefix + "-Death.png", frameSize, 4);

            // Таймер анимации
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / framesPerSecond) };
            _animTimer.Tick += AnimTimer_Tick;

            // Таймер атаки: 6 кадров при 8fps ≈ 750 мс
            _attackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _attackTimer.Tick += (s, e) =>
            {
                _attackTimer.Stop();
                _isAttacking = false;
                _attackOnCooldown = true;
                _attackCooldown.Start(); // Запуск перезарядки после атаки
                if (!IsDead) SetState(EnemyState.Idle);
            };

            // Перезарядка атаки: 1200 мс
            _attackCooldown = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _attackCooldown.Tick += (s, e) => { _attackCooldown.Stop(); _attackOnCooldown = false; };

            // Таймер получения урона: 4 кадра ≈ 400 мс
            _hurtTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _hurtTimer.Tick += (s, e) => { _hurtTimer.Stop(); _isHurt = false; if (!IsDead) SetState(EnemyState.Idle); };

            // Таймер нанесения урона: срабатывает через 350 мс после начала атаки
            _hitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _hitTimer.Tick += (s, e) => { _hitTimer.Stop(); OnAttackHit?.Invoke(); };

            // Начальное состояние
            Play("Idle");
        }

        // ================================================================
        //  НАСТРОЙКА НАПРАВЛЕНИЯ ВЗГЛЯДА
        //  Зеркально отражает спрайт врага
        // ================================================================
        private void SetFacing(bool facingRight)
        {
            _image.RenderTransformOrigin = new Point(0.5, 0.5);
            _image.RenderTransform = new ScaleTransform(facingRight ? -1 : 1, 1);
        }

        // ================================================================
        //  ЗАГРУЗКА АНИМАЦИЙ ИЗ СПРАЙТ-ЛИСТА
        //  Разбивает спрайт-лист на отдельные кадры
        // ================================================================
        private void LoadAnimation(string name, string filePath, int frameSize, int frameCount)
        {
            try
            {
                var sheet = new BitmapImage();
                sheet.BeginInit();
                sheet.UriSource = new Uri(filePath, UriKind.Absolute);
                sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.EndInit();

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
        //  AI ЛОГИКА
        //  Вызывается из игрового цикла каждый кадр
        //  Определяет поведение врага: преследование, атака или ожидание
        // ================================================================
        public void Update(double playerX, double playerY)
        {
            // Враг не может действовать, если мертв, получает урон или атакует
            if (IsDead || _isHurt || _isAttacking) return;

            double dx = playerX - X;          // Разница по X между врагом и игроком
            double dist = Math.Abs(dx);       // Расстояние до игрока

            // Атака, если игрок в радиусе атаки и атака не на перезарядке
            if (dist <= AttackRange && !_attackOnCooldown)
            {
                StartAttack();
                return;
            }

            // Преследование, если игрок в радиусе обнаружения
            if (dist <= DetectRange)
            {
                double dir = dx > 0 ? 1 : -1; // Направление движения
                SetPosition(X + dir * MoveSpeed, Y);
                SetFacing(dir < 0); // Враг смотрит в сторону игрока
                SetState(EnemyState.Walk);
            }
            else
            {
                SetState(EnemyState.Idle); // Ожидание
            }
        }

        // ================================================================
        //  БОЕВЫЕ МЕТОДЫ
        // ================================================================

        /// <summary>
        /// Начать атаку. Запускает анимацию и таймеры.
        /// </summary>
        public void StartAttack()
        {
            if (IsDead || _isAttacking) return;
            _isAttacking = true;
            SetState(EnemyState.Attack);
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
            if (IsDead) return;
            Hp = Math.Max(0, Hp - amount);
            if (Hp == 0) { Die(); return; }
            _isHurt = true;
            SetState(EnemyState.Hurt);
            _hurtTimer.Stop();
            _hurtTimer.Start();
        }

        /// <summary>
        /// Смерть. Останавливает все действия, запускает анимацию смерти
        /// и удаляет врага с канваса через 1.5 секунды.
        /// </summary>
        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            _attackTimer.Stop();
            _hurtTimer.Stop();
            _hitTimer.Stop();
            SetState(EnemyState.Death);

            // Таймер для удаления спрайта с канваса после окончания анимации смерти
            var removeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            removeTimer.Tick += (s, e) =>
            {
                removeTimer.Stop();
                _canvas.Children.Remove(_image);
                OnDeath?.Invoke(this); // Уведомление о смерти
            };
            removeTimer.Start();
        }

        // ================================================================
        //  ПОЗИЦИОНИРОВАНИЕ И СТОЛКНОВЕНИЯ
        // ================================================================

        /// <summary>
        /// Установка позиции врага на канвасе.
        /// </summary>
        public void SetPosition(double x, double y)
        {
            X = x;
            Y = y;
            Canvas.SetLeft(_image, x);
            Canvas.SetTop(_image, y);
        }

        /// <summary>
        /// Получить ограничивающий прямоугольник для проверки столкновений.
        /// Используется сжатый размер для более точных попаданий.
        /// </summary>
        public Rect GetBounds()
        {
            return new Rect(X + RenderSize * 0.2, Y + RenderSize * 0.2,
                            RenderSize * 0.6, RenderSize * 0.6);
        }

        // ================================================================
        //  УПРАВЛЕНИЕ СОСТОЯНИЯМИ И АНИМАЦИЯМИ
        // ================================================================

        /// <summary>
        /// Установка состояния врага.
        /// </summary>
        private void SetState(EnemyState newState)
        {
            if (State == newState) return;
            State = newState;
            switch (newState)
            {
                case EnemyState.Idle: Play("Idle"); break;
                case EnemyState.Walk: Play("Walk"); break;
                case EnemyState.Attack: Play("Attack_1"); break;
                case EnemyState.Hurt: Play("Hurt"); break;
                case EnemyState.Death: Play("Death"); break;
            }
        }

        /// <summary>
        /// Запуск анимации по имени.
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
        /// Анимация смерти проигрывается один раз и останавливается на последнем кадре.
        /// Остальные анимации циклические.
        /// </summary>
        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            if (_currentAnimation == null || !_animations.ContainsKey(_currentAnimation)) return;
            var frames = _animations[_currentAnimation];

            // Анимация смерти: один раз до конца, затем остановка
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
    }
}