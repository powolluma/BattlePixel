using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BattlePixel
{
    // Состояния врага
    public enum EnemyState { 
        Idle, 
        Walk, 
        Attack, 
        Hurt, 
        Death 
    }

    public class Enemy
    {
        // Компоненты отображения
        private readonly Canvas _canvas;                             
        private readonly Image _image;                               
        private readonly Dictionary<string, List<CroppedBitmap>> _animations = new Dictionary<string, List<CroppedBitmap>>();  //Словарь (изображение - кадр)        

        private string _currentAnimation;   
        private int _currentFrame;           
        private readonly DispatcherTimer _animTimer; 
        private readonly DispatcherTimer _hitTimer; 
        private bool _isPlayingDeathOnce;    

        // Характеристики
        public int MaxHp { get; private set; }     
        public int Hp { get; private set; }          
        public int Damage { get; private set; }       
        public double MoveSpeed { get; private set; } 
        public double DetectRange { get; private set; } 
        public double AttackRange { get; private set; } 

        // Позиция
        public double X { get; private set; }        
        public double Y { get; private set; }         
        public int RenderSize { get; }              

        // Состояния
        public EnemyState State { get; private set; } = EnemyState.Idle;
        private bool _isAttacking;           
        private bool _isHurt;              
        public bool IsDead { get; private set; } 

        // События
        public event Action<Enemy> OnDeath;  
        public event Action OnAttackHit;     

        // Таймеры состояний
        private readonly DispatcherTimer _attackTimer;    
        private readonly DispatcherTimer _hurtTimer;       
        private readonly DispatcherTimer _attackCooldown;   
        private bool _attackOnCooldown;          

        // Конструктор
        public Enemy(Canvas canvas, string basePath, string namePrefix,
                     int frameSize, int renderSize, int level = 1, bool facingRight = true,
                     int framesPerSecond = 8)
        {
            _canvas = canvas;
            RenderSize = renderSize;

            // Масштабирование характеристик
            MaxHp = 50 + (level - 1) * 25;        
            Hp = MaxHp;
            Damage = 10 + (level - 1) * 5;        
            MoveSpeed = 1.5 + (level - 1) * 0.3;  
            DetectRange = 300;                   
            AttackRange = renderSize * 0.4;      

            // Создание изображения 
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

            LoadAnimation("Idle", basePath + namePrefix + "-Idle.png", frameSize, 6);
            LoadAnimation("Walk", basePath + namePrefix + "-Walk.png", frameSize, 8);
            LoadAnimation("Attack_1", basePath + namePrefix + "-Attack01.png", frameSize, 6);
            LoadAnimation("Attack_2", basePath + namePrefix + "-Attack02.png", frameSize, 6);
            LoadAnimation("Hurt", basePath + namePrefix + "-Hurt.png", frameSize, 4);
            LoadAnimation("Death", basePath + namePrefix + "-Death.png", frameSize, 4);

            // Таймер анимации
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / framesPerSecond) };
            _animTimer.Tick += AnimTimer_Tick;

            // Таймер атаки
            _attackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _attackTimer.Tick += (s, e) =>
            {
                _attackTimer.Stop();
                _isAttacking = false;
                _attackOnCooldown = true;
                _attackCooldown.Start();
                if (!IsDead) SetState(EnemyState.Idle);
            };

            // Перезарядка атаки
            _attackCooldown = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _attackCooldown.Tick += (s, e) => { _attackCooldown.Stop(); _attackOnCooldown = false; };

            // Таймер получения урона
            _hurtTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _hurtTimer.Tick += (s, e) => { _hurtTimer.Stop(); _isHurt = false; if (!IsDead) SetState(EnemyState.Idle); };

            // Таймер нанесения урона
            _hitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _hitTimer.Tick += (s, e) => { _hitTimer.Stop(); OnAttackHit?.Invoke(); };

            // Начальное состояние
            Play("Idle");
        }

        //  Настройка направления взгляда
        private void SetFacing(bool facingRight)
        {
            _image.RenderTransformOrigin = new Point(0.5, 0.5);
            _image.RenderTransform = new ScaleTransform(facingRight ? -1 : 1, 1);
        }

        //  Загрузка анимаций
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
            catch { /* Файл не найден */ }
        }

        //  Логика
        public void Update(double playerX, double playerY)
        {
            if (IsDead || _isHurt || _isAttacking) return;

            double dx = playerX - X;         
            double dist = Math.Abs(dx);      

            // Атака, если игрок в радиусе атаки и атака не на перезарядке
            if (dist <= AttackRange && !_attackOnCooldown)
            {
                StartAttack();
                return;
            }

            // Преследование
            if (dist <= DetectRange)
            {
                double dir = dx > 0 ? 1 : -1; 
                SetPosition(X + dir * MoveSpeed, Y);
                SetFacing(dir < 0); // Враг смотрит в сторону игрока
                SetState(EnemyState.Walk);
            }
            else
            {
                SetState(EnemyState.Idle); 
            }
        }

        // Боевка
        // Атака
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

        // Получить урон
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

        // Смерть
        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            _attackTimer.Stop();
            _hurtTimer.Stop();
            _hitTimer.Stop();
            SetState(EnemyState.Death);

            // Таймер для удаления спрайта 
            var removeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            removeTimer.Tick += (s, e) =>
            {
                removeTimer.Stop();
                _canvas.Children.Remove(_image);
                OnDeath?.Invoke(this); 
            };
            removeTimer.Start();
        }

        //  Позиция и столкновение
        public void SetPosition(double x, double y)
        {
            X = x;
            Y = y;
            Canvas.SetLeft(_image, x);
            Canvas.SetTop(_image, y);
        }

        // Вспомогательная штучка (хитбоксы)
        public Rect GetBounds()
        {
            return new Rect(X + RenderSize * 0.2, Y + RenderSize * 0.2,
                            RenderSize * 0.6, RenderSize * 0.6);
        }

        // Управление состояниями и анимациями
        // Установка состояния врага
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

        // Запуск анимации
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

        // Таймер смены кадров анимации
        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            if (_currentAnimation == null || !_animations.ContainsKey(_currentAnimation)) return;
            var frames = _animations[_currentAnimation];

            // Анимация смерти
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

            // Остальные анимации
            _currentFrame = (_currentFrame + 1) % frames.Count;
            _image.Source = frames[_currentFrame];
        }
    }
}