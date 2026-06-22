using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BattlePixel
{
    public class SaveData
    {
        public int Level = 1;
        public int Hp = 100;
        public int MaxHp = 100;
        public int Damage = 20;
        public int Coins = 0;
        public double DamageMultiplier = 1.0;
        public double MaxHpMultiplier = 1.0;
        public double MusicVolume = 0.5;
        public double SoundVolume = 0.5;

        public List<string> InventoryItems = new List<string>();
        public List<string> UnlockedSkills = new List<string>();
    }

    public static class SaveManager
    {
        private static readonly string SaveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BattlePixel"); //Путь сохранения 
        private static readonly string SaveFile = Path.Combine(SaveDir, "save.dat");  
        public static bool HasSave => File.Exists(SaveFile); //Проверка сохранения

        // Сохранить 
        public static void Save(SaveData d)
        {
            try
            {
                Directory.CreateDirectory(SaveDir);
                var sb = new StringBuilder(); // Подготовка для сохранения данных
                sb.AppendLine("Level=" + d.Level);
                sb.AppendLine("Hp=" + d.Hp);
                sb.AppendLine("MaxHp=" + d.MaxHp);
                sb.AppendLine("Damage=" + d.Damage);
                sb.AppendLine("Coins=" + d.Coins);
                sb.AppendLine("DamageMultiplier=" + d.DamageMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("MaxHpMultiplier=" + d.MaxHpMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("MusicVolume=" + d.MusicVolume.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("SoundVolume=" + d.SoundVolume.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("Inventory=" + string.Join(",", d.InventoryItems));
                sb.AppendLine("Skills=" + string.Join(",", d.UnlockedSkills));
                File.WriteAllText(SaveFile, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка сохранения: " + ex.Message);
            }
        }

        // Загрузить 
        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(SaveFile)) return null;
                var d = new SaveData();
                var ci = System.Globalization.CultureInfo.InvariantCulture; // Одинаковый формат для любого сохранения

                foreach (string raw in File.ReadAllLines(SaveFile, Encoding.UTF8))
                {
                    int eq = raw.IndexOf('=');  // Уровень
                    if (eq < 0) continue;   
                    string key = raw.Substring(0, eq).Trim(); // чекает до 
                    string val = raw.Substring(eq + 1).Trim();// чекает после 

                    switch (key)
                    {
                        case "Level": int.TryParse(val, out d.Level); break;
                        case "Hp": int.TryParse(val, out d.Hp); break;
                        case "MaxHp": int.TryParse(val, out d.MaxHp); break;
                        case "Damage": int.TryParse(val, out d.Damage); break;
                        case "Coins": int.TryParse(val, out d.Coins); break;
                        case "DamageMultiplier": double.TryParse(val, System.Globalization.NumberStyles.Any, ci, out d.DamageMultiplier); break;
                        case "MaxHpMultiplier": double.TryParse(val, System.Globalization.NumberStyles.Any, ci, out d.MaxHpMultiplier); break;
                        case "MusicVolume": double.TryParse(val, System.Globalization.NumberStyles.Any, ci, out d.MusicVolume); break;
                        case "SoundVolume": double.TryParse(val, System.Globalization.NumberStyles.Any, ci, out d.SoundVolume); break;
                        case "Inventory":
                            d.InventoryItems = new List<string>();
                            if (!string.IsNullOrWhiteSpace(val))
                                d.InventoryItems.AddRange(val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                            break;
                        case "Skills":
                            d.UnlockedSkills = new List<string>();
                            if (!string.IsNullOrWhiteSpace(val))
                                d.UnlockedSkills.AddRange(val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                            break;
                    }
                }
                return d;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка загрузки: " + ex.Message);
                return null;
            }
        }

        // Удалить сохранение 
        public static void Delete()
        {
            try { if (File.Exists(SaveFile)) File.Delete(SaveFile); }
            catch { }
        }
        public static string GetSavePath() => SaveFile;
    }
}
