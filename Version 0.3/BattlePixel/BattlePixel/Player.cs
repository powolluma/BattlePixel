using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BattlePixel
{
    // Состояния игрока
    public enum PlayerState { 
        Idle, 
        Walk, 
        Attack, 
        Hurt, 
        Death 
    }

    public class Player
    {
        private Canvas _canvas;  
        private readonly Image _image; 

        // Словарь анимаций
        private readonly Dictionary<string, List<CroppedBitmap>> _animations = new Dictionary<string, List<CroppedBitmap>>();

        private string _currentAnimation;           // Имя текущей анимации
        private int _currentFrame;                  // Текущий кадр анимации
        private readonly DispatcherTimer _animTimer;// Таймер для смены кадров

        // Характеристики
        public int MaxHp { get; private set; }  
        public int Hp { get; private set; }     
        public int Damage { get; private set; } 
        public int Coins { get; set; }          

        // Позиция на карте
        public double X { get; private set; }   
        public double Y { get; private set; }   

        public int RenderSize { get; }          

        // Состояния
        public PlayerState State { get; private set; } = PlayerState.Idle;
        private bool _isAttacking;     
        private bool _isHurt;          
        private bool _isDead;          
        private bool _isPlayingDeathOnce; 

        // Мультипликаторы навыков
        public double DamageMultiplier { get; set; } = 1.0;   
        public double MaxHpMultiplier { get; set; } = 1.0;    

        // События
        public event Action OnAttackHit;       
        public event Action OnDeathFinished;   

        // Таймеры
        private readonly DispatcherTimer _attackTimer;
        private readonly DispatcherTimer _hurtTimer;  
        private readonly DispatcherTimer _hitTimer;   

        //  Конструктор
        public Player(Canvas canvas, int frameSize, int renderSize, int framesPerSecond = 8)
        {
            _canvas = canvas;
            RenderSize = renderSize;

            MaxHp = 100;
            Hp = MaxHp;
            Damage = 20;
            Coins = 0;

            // Создание изображения 
            _image = new Image
            {
                Width = renderSize,
                Height = renderSize,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);  //Выравнивание изображения по пикселям
            Panel.SetZIndex(_image, 10);
            _canvas.Children.Add(_image);

            LoadAnimation("Idle", GetAssetPath("Soldier-Idle.png"), 100, 6);
            LoadAnimation("Walk", GetAssetPath("Soldier-Walk.png"), 100, 8);
            LoadAnimation("Attack_1", GetAssetPath("Soldier-Attack01.png"), 100, 6);
            LoadAnimation("Attack_2", GetAssetPath("Soldier-Attack02.png"), 100, 6);
            LoadAnimation("Hurt", GetAssetPath("Soldier-Hurt.png"), 100, 4);
            LoadAnimation("Death", GetAssetPath("Soldier-Death.png"), frameSize, 4);

            // Таймер анимации
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / framesPerSecond) };
            _animTimer.Tick += AnimTimer_Tick;

            // Таймер атаки
            _attackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            _attackTimer.Tick += (s, e) => { _attackTimer.Stop(); _isAttacking = false; SetState(PlayerState.Walk); };

            // Таймер нанесения урона
            _hitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _hitTimer.Tick += (s, e) => { _hitTimer.Stop(); OnAttackHit?.Invoke(); };

            // Таймер получения урона
            _hurtTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _hurtTimer.Tick += (s, e) => { _hurtTimer.Stop(); _isHurt = false; if (!_isDead) SetState(PlayerState.Walk); };

            // Начальное состояние
            Play("Idle");
        }

        //  Загрузка спрайт листа
        private string GetAssetPath(string fileName)
        {
            // Путь к ресурсам 
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return System.IO.Path.Combine(baseDir, "Assets", "Characters", "Soldier", fileName);
        }

        // Загрузка анимации
        private void LoadAnimation(string name, string filePath, int frameSize, int frameCount)
        {
            try
            {
                // Загрузка 
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
            catch { /* Файл не найден */ }
        }

        // Боевка
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

        // Получение урона
        public void TakeDamage(int amount)
        {
            if (_isDead) return;
            Hp = Math.Max(0, Hp - amount);
            if (Hp == 0) { Die(); return; }
            _isHurt = true;
            SetState(PlayerState.Hurt);
            _hurtTimer.Stop();
            _hurtTimer.Start();
        }

        // Лечение
        public void Heal(int amount)
        {
            Hp = Math.Min(MaxHp, Hp + amount);
        }

        // Смерть
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

        // Свойства состояний
        public bool IsDead => _isDead;
        public bool IsAttacking => _isAttacking;

        //  Методы сброса состояний
        public void ResetForNewLevel()
        {
            _isDead = false;
            _isAttacking = false;
            _isHurt = false;
            _isPlayingDeathOnce = false;
            _attackTimer.Stop();
            _hurtTimer.Stop();

            // Пересчет характеристик
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

            _currentAnimation = null; 
            SetState(PlayerState.Walk);
        }

        // Возрождение
        public void RespawnKeepStats()
        {
            _isDead = false;
            _isAttacking = false;
            _isHurt = false;
            _isPlayingDeathOnce = false;
            _attackTimer.Stop();
            _hurtTimer.Stop();
            _hitTimer.Stop();

            Hp = MaxHp; 

            _currentAnimation = null;
            SetState(PlayerState.Walk);
        }

        // Улучшение от навыка
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

        // Позиция и рендер
        public void SetPosition(double x, double y)
        {
            X = x;
            Y = y;
            Canvas.SetLeft(_image, x);
            Canvas.SetTop(_image, y);
        }

        // Перемещение между уровнями
        public void ReaddToCanvas(Canvas newCanvas)
        {
            _canvas = newCanvas;
            if (!newCanvas.Children.Contains(_image))
                newCanvas.Children.Add(_image);
        }

        //  Управление состояниями и анимациями
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

        // Ходьба
        public void PlayWalk() { if (!_isAttacking && !_isHurt && !_isDead) SetState(PlayerState.Walk); }

        // Ожидание
        public void PlayIdle() { if (!_isAttacking && !_isHurt && !_isDead) SetState(PlayerState.Idle); }

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

        // Таймер для смены кадров
        private void AnimTimer_Tick(object sender, EventArgs e)
        {
            if (_currentAnimation == null || !_animations.ContainsKey(_currentAnimation)) return;
            var frames = _animations[_currentAnimation];

            // Смерть
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

        // Вспомогательный метод для хитбоксов
        public Rect GetBounds()
        {
            return new Rect(X + RenderSize * 0.2, Y + RenderSize * 0.2,
                            RenderSize * 0.6, RenderSize * 0.6);
        }
    }
}