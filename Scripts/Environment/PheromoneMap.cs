using Godot;
using System;

public partial class PheromoneMap : Node2D
{
    // References
    private Environment _environment;

    // Pheromone types
    public enum PheromoneType
    {
        Home = 0,  // Blue - leading back to home
        Food = 1   // Red - leading to food
    }

    // Pheromone grid properties
    private float[,] _homePheromoneGrid; // Grid for home pheromones
    private float[,] _foodPheromoneGrid; // Grid for food pheromones

    // Pheromone parameters
    [Export] public float EvaporationRate = 0.01f;  // How quickly pheromones evaporate
    [Export] public float DiffusionRate = 0.05f;    // How much pheromones spread to neighboring cells
    [Export] public float MaxPheromone = 1.0f;      // Maximum pheromone value

    // Visuals
    private ImageTexture _pheromoneTextureHome;
    private ImageTexture _pheromoneTextureFood;
    private Image _imageHome;
    private Image _imageFood;

    // Called when the node enters the scene tree
    public override void _Ready()
    {
        GD.Print("PheromoneMap _Ready called");

        // Get reference to environment node
        _environment = GetNode<Environment>("../Environment");

        if (_environment == null)
        {
            GD.PrintErr("Failed to get Environment node reference!");
            return;
        }

        // Wait a frame to ensure environment is initialized
        CallDeferred(nameof(Initialize));
    }

    // Initialize after environment is ready
    private void Initialize()
    {
        GD.Print("Initializing PheromoneMap");

        // Initialize pheromone grids
        InitializePheromoneGrids();

        // Create textures for visualization
        CreatePheromoneTextures();

        // Initial update of the textures
        UpdatePheromoneTextures();
    }

    // Initialize the pheromone grids
    private void InitializePheromoneGrids()
    {
        Vector2I gridSize = _environment.GridSize;

        // Initialize home pheromone grid
        _homePheromoneGrid = new float[gridSize.X, gridSize.Y];

        // Initialize food pheromone grid
        _foodPheromoneGrid = new float[gridSize.X, gridSize.Y];

        // Set all values to 0
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                _homePheromoneGrid[x, y] = 0.0f;
                _foodPheromoneGrid[x, y] = 0.0f;
            }
        }
    }

    // Create textures for pheromone visualization
    private void CreatePheromoneTextures()
    {
        Vector2I gridSize = _environment.GridSize;

        GD.Print($"Creating pheromone textures with grid size: {gridSize}");

        // Create image for home pheromones (blue) - using the environment grid size
        _imageHome = Image.CreateEmpty(gridSize.X, gridSize.Y, false, Image.Format.Rgba8);
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                _imageHome.SetPixel(x, y, new Color(0, 0, 0, 0));
            }
        }
        _pheromoneTextureHome = ImageTexture.CreateFromImage(_imageHome);

        // Create image for food pheromones (red) - using the environment grid size
        _imageFood = Image.CreateEmpty(gridSize.X, gridSize.Y, false, Image.Format.Rgba8);
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                _imageFood.SetPixel(x, y, new Color(0, 0, 0, 0));
            }
        }
        _pheromoneTextureFood = ImageTexture.CreateFromImage(_imageFood);

        GD.Print($"Pheromone textures created. Texture size: {_pheromoneTextureHome.GetSize()}");
    }

    // Update pheromones (evaporation and diffusion)
    public override void _Process(double delta)
    {
        // Only update pheromones every few frames to save performance
        _updateTimer += (float)delta;
        if (_updateTimer >= _updateInterval)
        {
            UpdatePheromones(_updateTimer);
            UpdatePheromoneTextures();
            QueueRedraw();
            _updateTimer = 0;
        }
    }

    // Add timer variables for controlling update frequency
    private float _updateTimer = 0;
    private float _updateInterval = 0.05f; // Update 20 times per second

    // Update pheromone values with evaporation and diffusion
    private void UpdatePheromones(float delta)
    {
        Vector2I gridSize = _environment.GridSize;

        // Calculate evaporation amount for this frame
        float evaporationAmount = EvaporationRate * delta;

        // Create new grids for diffusion calculation
        float[,] newHomeGrid = new float[gridSize.X, gridSize.Y];
        float[,] newFoodGrid = new float[gridSize.X, gridSize.Y];

        // Process evaporation and diffusion
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                // Skip if this is a wall
                if (_environment.GetCellType(new Vector2I(x, y)) == Environment.CellType.Wall)
                {
                    continue;
                }

                // Process home pheromones
                float homeValue = _homePheromoneGrid[x, y];
                homeValue = Mathf.Max(0.0f, homeValue - evaporationAmount);

                // Process food pheromones
                float foodValue = _foodPheromoneGrid[x, y];
                foodValue = Mathf.Max(0.0f, foodValue - evaporationAmount);

                // Apply diffusion
                SpreadPheromone(newHomeGrid, x, y, homeValue);
                SpreadPheromone(newFoodGrid, x, y, foodValue);
            }
        }

        // Update the grids with the new values
        _homePheromoneGrid = newHomeGrid;
        _foodPheromoneGrid = newFoodGrid;
    }

    // Spread pheromone value to surrounding cells
    private void SpreadPheromone(float[,] grid, int x, int y, float value)
    {
        // Amount to keep at current cell
        float keepAmount = 1.0f - DiffusionRate;

        // Update current cell
        grid[x, y] += value * keepAmount;

        // Amount to spread to each neighbor
        float spreadAmount = (value * DiffusionRate) / 4.0f;

        // Spread to neighbors (von Neumann neighborhood)
        Vector2I[] neighbors = new Vector2I[]
        {
            new Vector2I(x + 1, y),
            new Vector2I(x - 1, y),
            new Vector2I(x, y + 1),
            new Vector2I(x, y - 1)
        };

        foreach (Vector2I neighbor in neighbors)
        {
            if (_environment.IsValidGridPosition(neighbor) &&
                _environment.GetCellType(neighbor) != Environment.CellType.Wall)
            {
                grid[neighbor.X, neighbor.Y] += spreadAmount;
            }
        }
    }

    // Update pheromone texture visualizations
    private void UpdatePheromoneTextures()
    {
        Vector2I gridSize = _environment.GridSize;

        // Create new images each time to avoid potential issues
        Image newImageHome = Image.CreateEmpty(gridSize.X, gridSize.Y, false, Image.Format.Rgba8);
        Image newImageFood = Image.CreateEmpty(gridSize.X, gridSize.Y, false, Image.Format.Rgba8);

        // Update pixels
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                // Skip walls
                if (_environment.GetCellType(new Vector2I(x, y)) == Environment.CellType.Wall)
                {
                    newImageHome.SetPixel(x, y, new Color(0, 0, 0, 0));
                    newImageFood.SetPixel(x, y, new Color(0, 0, 0, 0));
                    continue;
                }

                // Set home pheromone pixel (blue)
                float homeValue = _homePheromoneGrid[x, y];
                if (homeValue > 0.01f)
                {
                    float homeAlpha = Mathf.Min(1.0f, homeValue);
                    newImageHome.SetPixel(x, y, new Color(0, 0, 1, homeAlpha));
                }
                else
                {
                    newImageHome.SetPixel(x, y, new Color(0, 0, 0, 0));
                }

                // Set food pheromone pixel (red)
                float foodValue = _foodPheromoneGrid[x, y];
                if (foodValue > 0.01f)
                {
                    float foodAlpha = Mathf.Min(1.0f, foodValue);
                    newImageFood.SetPixel(x, y, new Color(1, 0, 0, foodAlpha));
                }
                else
                {
                    newImageFood.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
        }

        // Update textures with the new image data
        _imageHome = newImageHome;
        _imageFood = newImageFood;
        _pheromoneTextureHome = ImageTexture.CreateFromImage(newImageHome);
        _pheromoneTextureFood = ImageTexture.CreateFromImage(newImageFood);
    }

    // Alternative drawing approach - draw pheromones directly as rectangles
    private void DrawPheromoneGrids()
    {
        Vector2I gridSize = _environment.GridSize;
        Vector2I cellSize = _environment.CellSize;

        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                Vector2I cellPos = new Vector2I(x, y);
                Vector2 worldPos = new Vector2(x * cellSize.X, y * cellSize.Y);

                // Skip walls
                if (_environment.GetCellType(cellPos) == Environment.CellType.Wall)
                {
                    continue;
                }

                // Draw home pheromone (blue)
                float homeValue = _homePheromoneGrid[x, y];
                if (homeValue > 0.01f)
                {
                    float homeAlpha = Mathf.Min(0.5f, homeValue);
                    DrawRect(new Rect2(worldPos, cellSize), new Color(0, 0, 1, homeAlpha), true);
                }

                // Draw food pheromone (red)
                float foodValue = _foodPheromoneGrid[x, y];
                if (foodValue > 0.01f)
                {
                    float foodAlpha = Mathf.Min(0.5f, foodValue);
                    DrawRect(new Rect2(worldPos, cellSize), new Color(1, 0, 0, foodAlpha), true);
                }
            }
        }
    }

    // Draw the pheromone textures or grids
    public override void _Draw()
    {
        // Try both approaches - uncomment the one that works better

        // Approach 1: Draw using textures
        DrawPheromoneTextures();

        // Approach 2: Draw directly using rectangles
        // DrawPheromoneGrids();
    }

    // Draw the pheromone textures using TextureRect
    private void DrawPheromoneTextures()
    {
        Vector2I gridSize = _environment.GridSize;
        Vector2I cellSize = _environment.CellSize;

        // Calculate the actual size of the grid in pixels
        Vector2 fullSize = new Vector2(gridSize.X * cellSize.X, gridSize.Y * cellSize.Y);

        // Create a rect that covers the entire environment area
        Rect2 destinationRect = new Rect2(Vector2.Zero, fullSize);

        // Only draw if textures are valid
        if (_pheromoneTextureHome != null)
        {
            // Draw home pheromone texture (blue) stretched to cover the environment
            DrawTextureRect(_pheromoneTextureHome, destinationRect, false, new Color(1, 1, 1, 0.3f));
        }

        if (_pheromoneTextureFood != null)
        {
            // Draw food pheromone texture (red) stretched to cover the environment
            DrawTextureRect(_pheromoneTextureFood, destinationRect, false, new Color(1, 1, 1, 0.3f));
        }
    }

    // Add pheromone at a specific grid position
    public void AddPheromone(Vector2I gridPos, PheromoneType type, float amount)
    {
        if (_environment.IsValidGridPosition(gridPos))
        {
            if (type == PheromoneType.Home)
            {
                _homePheromoneGrid[gridPos.X, gridPos.Y] =
                    Mathf.Min(MaxPheromone, _homePheromoneGrid[gridPos.X, gridPos.Y] + amount);
            }
            else
            {
                _foodPheromoneGrid[gridPos.X, gridPos.Y] =
                    Mathf.Min(MaxPheromone, _foodPheromoneGrid[gridPos.X, gridPos.Y] + amount);
            }
        }
    }

    // Get pheromone value at a specific grid position
    public float GetPheromone(Vector2I gridPos, PheromoneType type)
    {
        if (_environment.IsValidGridPosition(gridPos))
        {
            if (type == PheromoneType.Home)
            {
                return _homePheromoneGrid[gridPos.X, gridPos.Y];
            }
            else
            {
                return _foodPheromoneGrid[gridPos.X, gridPos.Y];
            }
        }
        return 0.0f;
    }
}