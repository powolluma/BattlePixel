using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace BattlePixel
{
    public partial class MainWindow : Window
    {
        private TileManager _tileManager;
        
        private GameMap _gameMap;
        
        private Player _player;
        
        private readonly List<Enemy> _enemies = new List<Enemy>();
        
        private DecorationManager _decorationManager;
        
        private InventoryManager _inventoryManager;
        private Player _inventoryPlayer;
        private bool _inventoryOpen;
        
        private SkillTreeManager _skillTreeManager;
        private bool _skillTreeOpen;

        private HealthBar _playerHealthBar;
        private readonly List<HealthBar> _enemyHealthBars = new List<HealthBar>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _tileManager = new TileManager(GameCanvas, sourceTileSize: 32, renderTileSize: 64);

            //Размер тайлов в длину (26)
            _gameMap = new GameMap(_tileManager, 26, 9);

            _gameMap.GenerateTestMap();
            _gameMap.Render();

            // Персонаж: кадр в паке 32x32, на экране рисуем 64x64 (тот же масштаб, что и тайлы)
            _player = new Player(GameCanvas, frameSize: 100, renderSize: 250);

            int groundRow = 9 - 1; // height - 1 из GameMap
            double playerX = 50;
            double playerY = groundRow * 64 - 142;

            _player.SetPosition(playerX, playerY);

            const string healthSheetPath = @"C:\Users\black\source\repos\BattlePixel\BattlePixel\Assets\Health\Health.png";
            _playerHealthBar = new HealthBar(GameCanvas, healthSheetPath, isGreen: true, renderWidth: 80, renderHeight: 16);
            _playerHealthBar.SetPosition(centerX: playerX + 125, topY: playerY, offsetAboveHead: 2); // 125 = половина renderSize (250/2)

            // Несколько орков, равномерно расставленных по карте
            double[] enemyPositions = { 500, 800, 1100, 1400 };

            foreach (double enemyX in enemyPositions)
            {
                var enemy = new Enemy(
                    GameCanvas,
                    basePath: @"C:\Users\black\source\repos\BattlePixel\BattlePixel\Assets\Characters\Orc\",
                    namePrefix: "Orc",
                    frameSize: 100,
                    renderSize: 250,
                    facingRight: true);

                double enemyY = groundRow * 64 - 142;

                enemy.SetPosition(enemyX, enemyY);
                
                var enemyHealthBar = new HealthBar(GameCanvas, healthSheetPath, isGreen: false, renderWidth: 80, renderHeight: 16);
                enemyHealthBar.SetPosition(centerX: enemyX + 125, topY: enemyY, offsetAboveHead: 2); // 125 = половина renderSize (250/2)
                _enemyHealthBars.Add(enemyHealthBar);

                _enemies.Add(enemy);
            }

            // --- Декорации ---
            // groundY — верхняя граница пола (низ декораций будет упираться именно в эту линию)
            double groundY = groundRow * 64;

            _decorationManager = new DecorationManager(GameCanvas);

            const string decorationFolder = @"C:\Users\black\source\repos\BattlePixel\BattlePixel\Assets\Decoration\";

            // Портал — фиксированно справа, чуть отступив от правого края карты (1664).
            var portal = new DecorationInfo(decorationFolder + "GrassLand_Entry_3.png", width: 32, height: 64); // x2 от 16x32
            _decorationManager.PlaceOnGround(portal, x: 1664 - 32 - 40, groundY); // отступ 40px от края

            // Цветы — рандомно по всей карте (кроме зоны прямо перед игроком, x < 200, чтобы не мешали обзору старта)
            var flowers = new List<DecorationInfo>
            {
                new DecorationInfo(decorationFolder + "GrassLand_Flower_1.png", width: 32, height: 32), // x2 от 16x16
                new DecorationInfo(decorationFolder + "GrassLand_Flower_2.png", width: 32, height: 32),
                new DecorationInfo(decorationFolder + "GrassLand_Flower_3.png", width: 32, height: 32)
            };
            _decorationManager.ScatterRandom(flowers, count: 8, minX: 200, maxX: 1600, groundY: groundY);

            // Трава — рандомно по всей карте
            var tallGrass = new List<DecorationInfo>
            {
                new DecorationInfo(decorationFolder + "GrassLand_TallGrass_1.png", width: 32, height: 32), // x2 от 16x16
                new DecorationInfo(decorationFolder + "GrassLand_TallGrass_2.png", width: 32, height: 32)
            };
            _decorationManager.ScatterRandom(tallGrass, count: 8, minX: 200, maxX: 1600, groundY: groundY);

            // Камни — рандомно по всей карте (Stone_3 побольше, остальные поменьше)
            var stones = new List<DecorationInfo>
            {
                new DecorationInfo(decorationFolder + "GrassLand_Stone_1.png", width: 32, height: 32),  // x2 от 16x16
                new DecorationInfo(decorationFolder + "GrassLand_Stone_2.png", width: 32, height: 32),  // x2 от 16x16
                new DecorationInfo(decorationFolder + "GrassLand_Stone_3.png", width: 64, height: 32)   // x2 от 32x16
            };
            _decorationManager.ScatterRandom(stones, count: 6, minX: 200, maxX: 1600, groundY: groundY);

            _inventoryManager = new InventoryManager(@"C:\Users\black\source\repos\BattlePixel\BattlePixel\Assets\Inventory\Inventory.png");

            _inventoryManager.BuildSlots(InventoryGrid, slotRenderSize: 48);

            // Персонаж в инвентаре — отдельный экземпляр, та же idle-анимация
            _inventoryPlayer = new Player(InventoryCharacterCanvas, frameSize: 100, renderSize: 240);
            _inventoryPlayer.SetPosition(10, 10);

            // --- Древо навыков ---
            const string skillsFolder = @"C:\Users\black\source\repos\BattlePixel\BattlePixel\Assets\Skills\";

            _skillTreeManager = new SkillTreeManager(
                @"C:\Users\black\source\repos\BattlePixel\BattlePixel\Assets\Inventory\Inventory.png");

            var branches = new List<SkillBranch>
    {
        new SkillBranch(angleDegrees: -90,  iconPaths: new[]
        {
            skillsFolder + "dragon_charges_skill_icon.png",
            skillsFolder + "dragon_roar_skill_icon.png",
            skillsFolder + "dragon_tail_icon.png",
            skillsFolder + "dragon_wing_skill_icon.png"
        }),
        new SkillBranch(angleDegrees: -38.6, iconPaths: new[]
        {
            skillsFolder + "fireskillicon2.png",
            skillsFolder + "fireskillicon3.png",
            skillsFolder + "fireskillicon4.png",
            skillsFolder + "fireskillicon.png"
        }),
        new SkillBranch(angleDegrees: 12.9, iconPaths: new[]
        {
            skillsFolder + "healingskillicon2.png",
            skillsFolder + "healingskillicon3.png",
            skillsFolder + "healingskillicon4.png",
            skillsFolder + "healingskillicon.png"
        }),
        new SkillBranch(angleDegrees: 64.3, iconPaths: new[]
        {
            skillsFolder + "Iceskillicon2.png",
            skillsFolder + "Iceskillicon3.png",
            skillsFolder + "Iceskillicon4.png",
            skillsFolder + "Iceskillicon.png"
        }),
        new SkillBranch(angleDegrees: 115.7, iconPaths: new[]
        {
            skillsFolder + "plantskillicon2.png",
            skillsFolder + "plantskillicon3.png",
            skillsFolder + "plantskillicon4.png",
            skillsFolder + "plantskillicon.png"
        }),
        new SkillBranch(angleDegrees: 167.1, iconPaths: new[]
        {
            skillsFolder + "Poisonskillicon2.png",
            skillsFolder + "Poison skillicon3.png",
            skillsFolder + "Poisonskillicon4.png",
            skillsFolder + "Poisonskillicon.png"
        }),
        new SkillBranch(angleDegrees: -141.4, iconPaths: new[]
        {
            skillsFolder + "slashskillicon2.png",
            skillsFolder + "slashskillicon3.png",
            skillsFolder + "slashskillicon4.png",
            skillsFolder + "slashskillicon.png"
        })
    };

            _skillTreeManager.BuildTree(SkillTreeCanvas, branches,
                centerX: 260, centerY: 260,
                slotRenderSize: 44, spacing: 55, startRadius: 50);

            this.KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.I)
            {
                ToggleInventory();
            }
            else if (e.Key == Key.O)
            {
                ToggleSkillTree();
            }
        }

        private void ToggleInventory()
        {
            _inventoryOpen = !_inventoryOpen;
            InventoryOverlay.Visibility = _inventoryOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleSkillTree()
        {
            _skillTreeOpen = !_skillTreeOpen;
            SkillTreeOverlay.Visibility = _skillTreeOpen ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}   