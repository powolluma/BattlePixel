using System;
using System.Windows.Media;

namespace BattlePixel
{
    public class SoundManager
    {
        // Музыка
        private readonly MediaPlayer _musicPlayer = new MediaPlayer();
        private readonly string[] _tracks;
        private int _currentTrack = 0;
        private double _musicVolume = 0.5;

        // ЗВуки
        private double _soundVolume = 0.5;

        private static string A(string rel) => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "SoundAndMusic", rel);

        // игрок
        private readonly string _playerAttackSound = A("attack_1.wav");
        private readonly string _playerDamageSound = A("damage_1.wav");
        private readonly string _playerWalkSound = A("walk_1.wav");

        // враг
        private readonly string _enemyAttackSound = A("attack_2.wav");
        private readonly string _enemyDamageSound = A("damage_2.wav");
        private readonly string _enemyWalkSound = A("walk_2.wav");

        // Конструктор
        public SoundManager()
        {
            _tracks = new[]
            {
                A("track_1.mp3"),
                A("track_2.mp3")
            };

            // Следующий после окончания
            _musicPlayer.MediaEnded += (s, e) =>
            {
                _currentTrack = (_currentTrack + 1) % _tracks.Length;
                PlayCurrentTrack();
            };
        }

        // Старт музыки
        public void StartMusic()
        {
            _currentTrack = 0;
            PlayCurrentTrack();
        }

        // Проигрывание текущего
        private void PlayCurrentTrack()
        {
            try
            {
                var uri = new Uri(_tracks[_currentTrack], UriKind.Absolute);
                _musicPlayer.Open(uri);
                _musicPlayer.Volume = _musicVolume;
                _musicPlayer.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка воспроизведения: {ex.Message}");
            }
        }

        //Остановка музыки
        public void StopMusic()
        {
            _musicPlayer.Stop();
        }

        // Регулировка музыки
        public void SetMusicVolume(double volume) 
        {
            _musicVolume = Math.Max(0, Math.Min(1, volume));
            _musicPlayer.Volume = _musicVolume;
        }

        // Звуки
        public void SetSoundVolume(double volume) // 0.0 - 1.0
        {
            _soundVolume = Math.Max(0, Math.Min(1, volume));
        }

        // Запуск звуков при определенной анимации тайла
        public void PlayPlayerAttack() => PlaySfx(_playerAttackSound);
        public void PlayPlayerDamage() => PlaySfx(_playerDamageSound);
        public void PlayPlayerWalk() => PlaySfx(_playerWalkSound);

        public void PlayEnemyAttack() => PlaySfx(_enemyAttackSound);
        public void PlayEnemyDamage() => PlaySfx(_enemyDamageSound);
        public void PlayEnemyWalk() => PlaySfx(_enemyWalkSound);

        // Запуск звуков
        private void PlaySfx(string path)
        {
            try
            {
                var player = new MediaPlayer();
                player.Volume = _soundVolume;
                player.Open(new Uri(path, UriKind.Absolute));
                player.MediaEnded += (s, e) => player.Close();
                player.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка звуков: {ex.Message}");
            }
        }
    }
}