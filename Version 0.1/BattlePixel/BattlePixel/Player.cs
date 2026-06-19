using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BattlePixel
{
    public class Player
    {
        private readonly Canvas _canvas;
        private readonly Image _image;
        private readonly Dictionary<string, List<CroppedBitmap>> _animations = new Dictionary<string, List<CroppedBitmap>>();

        private string _currentAnimation;
        private int _currentFrame;
        private readonly DispatcherTimer _timer;

        public int RenderSize { get; }

        public Player(Canvas canvas, int frameSize, int renderSize, int framesPerSecond = 8)
        {
            _canvas = canvas;
            RenderSize = renderSize;

            _image = new Image
            {
                Width = renderSize,
                Height = renderSize,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(_image, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
            Panel.SetZIndex(_image, 10); 
            _canvas.Children.Add(_image);

            //Анимации
            LoadAnimation("Idle", "C:\\Users\\black\\source\\repos\\BattlePixel\\BattlePixel\\Assets\\Characters\\Soldier\\Soldier-Idle.png", 100, 6);
            LoadAnimation("Walk", "C:\\Users\\black\\source\\repos\\BattlePixel\\BattlePixel\\Assets\\Characters\\Soldier\\Soldier-Walk.png", 100, 8);
            LoadAnimation("Attack_1", "C:\\Users\\black\\source\\repos\\BattlePixel\\BattlePixel\\Assets\\Characters\\Soldier\\Soldier-Attack01.png", 100, 6);
            LoadAnimation("Attack_2", "C:\\Users\\black\\source\\repos\\BattlePixel\\BattlePixel\\Assets\\Characters\\Soldier\\Soldier-Attack02.png", 100, 6);
            LoadAnimation("Hurt", "C:\\Users\\black\\source\\repos\\BattlePixel\\BattlePixel\\Assets\\Characters\\Soldier\\Soldier-Hurt.png", 100, 4);
            LoadAnimation("Death", "C:\\Users\\black\\source\\repos\\BattlePixel\\BattlePixel\\Assets\\Characters\\Soldier\\Soldier-Death.png", frameSize, 4);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.0 / framesPerSecond)
            };
            _timer.Tick += Timer_Tick;

            Play("Idle");
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
                MessageBox.Show($"Ошибка загрузки анимации {name}: {ex.Message}");
            }
        }

        public void Play(string animationName)
        {
            if (!_animations.ContainsKey(animationName))
                return;

            if (_currentAnimation == animationName)
                return; // уже играет, не сбрасываем кадр

            _currentAnimation = animationName;
            _currentFrame = 0;
            _image.Source = _animations[_currentAnimation][0];
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_currentAnimation == null || !_animations.ContainsKey(_currentAnimation))
                return;

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