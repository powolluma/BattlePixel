namespace BattlePixel
{
    public class GameMap
    {
        private readonly TileManager _tileManager; 
        private readonly int[,] _mapData;          

        // Конструктор
        public GameMap(TileManager tileManager, int width, int height)
        {
            _tileManager = tileManager;
            _mapData = new int[height, width]; // Инициализация массива карты
        }

        // Генерация карты
        public void GenerateTestMap()
        {
            int width = _mapData.GetLength(1);  // колонки
            int height = _mapData.GetLength(0); // строки

            // Заполнение карты пустыми ячейками 
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    _mapData[y, x] = -1;

            int groundY = height - 1; 

            // Заполнение землей
            for (int x = 0; x < width; x++)
            {
                _mapData[groundY, x] = 1; // Тайл земли
            }
        }

        // Рендеринг
        public void Render()
        {
            for (int y = 0; y < _mapData.GetLength(0); y++) 
            {
                for (int x = 0; x < _mapData.GetLength(1); x++) 
                {
                    int id = _mapData[y, x]; // айди тайла 
                    if (id >= 0) 
                        _tileManager.PlaceTile(id, x, y); 
                }
            }
        }
    }
}