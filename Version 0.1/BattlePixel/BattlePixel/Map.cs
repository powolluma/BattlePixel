namespace BattlePixel
{
    public class GameMap
    {
        private readonly TileManager _tileManager;
        private readonly int[,] _mapData;

        public GameMap(TileManager tileManager, int width, int height)
        {
            _tileManager = tileManager;
            _mapData = new int[height, width];
        }

        public void GenerateTestMap()
        {
            int width = _mapData.GetLength(1);
            int height = _mapData.GetLength(0);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    _mapData[y, x] = -1;

            int groundY = height - 1;

            for (int x = 0; x < width; x++)
            {
                _mapData[groundY, x] = 1;
            }
        }

        public void Render()
        {
            for (int y = 0; y < _mapData.GetLength(0); y++)
            {
                for (int x = 0; x < _mapData.GetLength(1); x++)
                {
                    int id = _mapData[y, x];
                    if (id >= 0)
                        _tileManager.PlaceTile(id, x, y);
                }
            }
        }
    }
}