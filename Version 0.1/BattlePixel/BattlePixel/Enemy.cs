using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BattlePixel
{
    public class Enemy
    {
        private readonly Canvas _canvas;
        private readonly Image _image;
        private readonly Dictionary<string, List<CroppedBitmap>> _animations = new Dictionary<string, List<CroppedBitmap>>();

        private string _currentAnimation;
        private int _currentFrame;
        private readonly DispatcherTimer _timer;

        public int RenderSize { get; }

        // basePath — папка со спрайтами врага, например ...\Assets\Characters\Orc\
        // namePrefix — префикс файлов, например "Orc" -> "Orc-Idle.png", "Orc-Attack01.png" и т.д.
        public Enemy(Canvas canvas, string basePath, string namePrefix, int frameSize, int renderSize, bool facingRight = true, int framesPerSecond = 8)
        {
            _canvas = canvas;
            RenderSize = renderSize;

            _image = new Image
            {
                Width = renderSize,
                Height = renderSize,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
            Panel.SetZIndex(_image, 10);

            SetFacing(facingRight);
            _canvas.Children.Add(_image);

            // Подгоните имена файлов/количество кадров под реальные ассеты этого врага.
            // Если файла нет — LoadAnimation просто не добавит эту анимацию (см. try/catch),
            // так что вызовы для ещё не нарисованных анимаций ничего не сломают.
            LoadAnimation("Idle", basePath + namePrefix + "-Idle.png", frameSize, 6);
            LoadAnimation("Walk", basePath + namePrefix + "-Walk.png", frameSize, 8);
            LoadAnimation("Attack_1", basePath + namePrefix + "-Attack01.png", frameSize, 6);
            LoadAnimation("Attack_2", basePath + namePrefix + "-Attack02.png", frameSize, 6);
            LoadAnimation("Hurt", basePath + namePrefix + "-Hurt.png", frameSize, 4);
            LoadAnimation("Death", basePath + namePrefix + "-Death.png", frameSize, 4);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / framesPerSecond) };
            _timer.Tick += Timer_Tick;

            Play("Idle");
        }

        private void SetFacing(bool facingRight)
        {
            _image.RenderTransformOrigin = new Point(0.5, 0.5);
            _image.RenderTransform = new ScaleTransform(facingRight ? -1 : 1, 1);
        }

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
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки анимации врага {name}: {ex.Message}");
            }
        }

        public void Play(string animationName)
        {
            if (!_animations.ContainsKey(animationName)) return;
            if (_currentAnimation == animationName) return;

            _currentAnimation = animationName;
            _currentFrame = 0;
            _image.Source = _animations[_currentAnimation][0];
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_currentAnimation == null || !_animations.ContainsKey(_currentAnimation)) return;

            var frames = _animations[_currentAnimation];
            _currentFrame = (_currentFrame + 1) % frames.Count;
            _image.Source = frames[_currentFrame];
        }

        public void SetPosition(double x, double y)
        {
            Canvas.SetLeft(_image, x);
            Canvas.SetTop(_image, y);
        }
    }
}