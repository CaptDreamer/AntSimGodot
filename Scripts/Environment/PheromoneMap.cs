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
    private float[,] _homePheromoneAge;  // Age tracking for home pheromones
    private float[,] _foodPheromoneAge;  // Age tracking for food pheromones

    // Pheromone parameters
    [Export] public float EvaporationRate = 0.01f;  // How quickly pheromones evaporate
    [Export] public float MaxPheromone = 1.0f;      // Maximum pheromone value
    [Export] public float PheromoneThreshold = 0.05f; // Threshold below which pheromones are removed
    [Export] public float MaxAge = 100.0f;          // Maximum age tracking (in seconds)

    // Visuals
    private ImageTexture _pheromoneTextureHome;
    private ImageTexture _pheromoneTextureFood;
    private Image _imageHome;
    private Image _imageFood;

    // Update control
    [Export] public float UpdateInterval = 0.05f; // 20 updates per second
    private float _updateTimer = 0;

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

        // Initialize pheromone grids
        _homePheromoneGrid = new float[gridSize.X, gridSize.Y];
        _foodPheromoneGrid = new float[gridSize.X, gridSize.Y];

        // Initialize age tracking grids
        _homePheromoneAge = new float[gridSize.X, gridSize.Y];
        _foodPheromoneAge = new float[gridSize.X, gridSize.Y];

        // Set all values to 0
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                _homePheromoneGrid[x, y] = 0.0f;
                _foodPheromoneGrid[x, y] = 0.0f;
                _homePheromoneAge[x, y] = 0.0f;
                _foodPheromoneAge[x, y] = 0.0f;
            }
        }
    }

    // Create textures for pheromone visualization
    private void CreatePheromoneTextures()
    {
        Vector2I gridSize = _environment.GridSize;

        GD.Print($"Creating pheromone textures with grid size: {gridSize}");

        // Create image for home pheromones (blue)
        _imageHome = Image.CreateEmpty(gridSize.X, gridSize.Y, false, Image.Format.Rgba8);
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                _imageHome.SetPixel(x, y, new Color(0, 0, 0, 0));
            }
        }
        _pheromoneTextureHome = ImageTexture.CreateFromImage(_imageHome);

        // Create image for food pheromones (red)
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

    // Update pheromones (evaporation only, no diffusion)
    public override void _Process(double delta)
    {
        // Only update pheromones every few frames to save performance
        _updateTimer += (float)delta;
        if (_updateTimer >= UpdateInterval)
        {
            UpdatePheromones(_updateTimer);
            UpdatePheromoneTextures();
            QueueRedraw();
            _updateTimer = 0;
        }
    }

    // Update pheromone values with evaporation and age tracking
    private void UpdatePheromones(float delta)
    {
        Vector2I gridSize = _environment.GridSize;

        // Calculate evaporation amount for this frame
        float evaporationAmount = EvaporationRate * delta;

        // Process evaporation and update ages
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                // Skip if this is a wall
                if (_environment.GetCellType(new Vector2I(x, y)) == Environment.CellType.Wall)
                {
                    _homePheromoneGrid[x, y] = 0.0f;
                    _foodPheromoneGrid[x, y] = 0.0f;
                    _homePheromoneAge[x, y] = 0.0f;
                    _foodPheromoneAge[x, y] = 0.0f;
                    continue;
                }

                // Process home pheromones
                float homeValue = _homePheromoneGrid[x, y];
                if (homeValue > 0.0f)
                {
                    // Apply evaporation
                    homeValue = Mathf.Max(0.0f, homeValue - evaporationAmount);

                    // Update age if pheromone still exists
                    if (homeValue > PheromoneThreshold)
                    {
                        _homePheromoneAge[x, y] += delta;
                        // Cap age at maximum
                        _homePheromoneAge[x, y] = Mathf.Min(_homePheromoneAge[x, y], MaxAge);
                    }
                    else
                    {
                        // Clear small values completely
                        homeValue = 0.0f;
                        _homePheromoneAge[x, y] = 0.0f;
                    }

                    _homePheromoneGrid[x, y] = homeValue;
                }

                // Process food pheromones
                float foodValue = _foodPheromoneGrid[x, y];
                if (foodValue > 0.0f)
                {
                    // Apply evaporation
                    foodValue = Mathf.Max(0.0f, foodValue - evaporationAmount);

                    // Update age if pheromone still exists
                    if (foodValue > PheromoneThreshold)
                    {
                        _foodPheromoneAge[x, y] += delta;
                        // Cap age at maximum
                        _foodPheromoneAge[x, y] = Mathf.Min(_foodPheromoneAge[x, y], MaxAge);
                    }
                    else
                    {
                        // Clear small values completely
                        foodValue = 0.0f;
                        _foodPheromoneAge[x, y] = 0.0f;
                    }

                    _foodPheromoneGrid[x, y] = foodValue;
                }
            }
        }
    }

    // Update pheromone texture visualizations - now with age-based coloring
    private void UpdatePheromoneTextures()
    {
        Vector2I gridSize = _environment.GridSize;

        // Create new images each time
        Image newImageHome = Image.CreateEmpty(gridSize.X, gridSize.Y, false, Image.Format.Rgba8);
        Image newImageFood = Image.CreateEmpty(gridSize.X, gridSize.Y, false, Image.Format.Rgba8);

        // Update pixels with clearer age visualization
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

                // Set home pheromone pixel (blue) - with clearer age gradient
                float homeValue = _homePheromoneGrid[x, y];
                if (homeValue > 0.0f)
                {
                    // Calculate normalized age (0 = new, 1 = oldest)
                    float ageNormalized = _homePheromoneAge[x, y] / MaxAge;

                    // Use age to visually show the gradient - newer is more blue, older more cyan
                    // This makes it easier to see which direction the gradient flows
                    Color homeColor;
                    if (ageNormalized < 0.3f)
                    {
                        homeColor = new Color(0.0f, 0.0f, 1.0f, homeValue * 0.8f); // Newer: Pure Blue
                    }
                    else if (ageNormalized < 0.6f)
                    {
                        homeColor = new Color(0.0f, 0.5f, 1.0f, homeValue * 0.8f); // Medium: Blue-Cyan
                    }
                    else
                    {
                        homeColor = new Color(0.0f, 0.8f, 1.0f, homeValue * 0.8f); // Older: Cyan
                    }

                    newImageHome.SetPixel(x, y, homeColor);
                }
                else
                {
                    newImageHome.SetPixel(x, y, new Color(0, 0, 0, 0));
                }

                // Set food pheromone pixel (red) - with clearer age gradient
                float foodValue = _foodPheromoneGrid[x, y];
                if (foodValue > 0.0f)
                {
                    // Calculate normalized age (0 = new, 1 = oldest)
                    float ageNormalized = _foodPheromoneAge[x, y] / MaxAge;

                    // Use age to visually show the gradient - newer is more red, older more yellow
                    Color foodColor;
                    if (ageNormalized < 0.3f)
                    {
                        foodColor = new Color(1.0f, 0.0f, 0.0f, foodValue * 0.8f); // Newer: Pure Red
                    }
                    else if (ageNormalized < 0.6f)
                    {
                        foodColor = new Color(1.0f, 0.5f, 0.0f, foodValue * 0.8f); // Medium: Orange
                    }
                    else
                    {
                        foodColor = new Color(1.0f, 0.8f, 0.0f, foodValue * 0.8f); // Older: Yellow-Orange
                    }

                    newImageFood.SetPixel(x, y, foodColor);
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

    // Draw the pheromone textures
    public override void _Draw()
    {
        DrawPheromoneTextures();
    }

    // Draw the pheromone textures
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
            // Draw home pheromone texture (blue)
            DrawTextureRect(_pheromoneTextureHome, destinationRect, false, new Color(1, 1, 1, 0.4f));
        }

        if (_pheromoneTextureFood != null)
        {
            // Draw food pheromone texture (red)
            DrawTextureRect(_pheromoneTextureFood, destinationRect, false, new Color(1, 1, 1, 0.4f));
        }
    }

    // Add pheromone at a specific grid position - now resets age on fresh deposit
    public void AddPheromone(Vector2I gridPos, PheromoneType type, float amount)
    {
        if (_environment.IsValidGridPosition(gridPos))
        {
            if (type == PheromoneType.Home)
            {
                // Reset age when adding fresh pheromone or increasing existing
                _homePheromoneAge[gridPos.X, gridPos.Y] = 0.0f;

                // Add pheromone value
                _homePheromoneGrid[gridPos.X, gridPos.Y] =
                    Mathf.Min(MaxPheromone, _homePheromoneGrid[gridPos.X, gridPos.Y] + amount);
            }
            else
            {
                // Reset age when adding fresh pheromone or increasing existing
                _foodPheromoneAge[gridPos.X, gridPos.Y] = 0.0f;

                // Add pheromone value
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

    // Get pheromone age at a specific grid position
    public float GetPheromoneAge(Vector2I gridPos, PheromoneType type)
    {
        if (_environment.IsValidGridPosition(gridPos))
        {
            if (type == PheromoneType.Home)
            {
                return _homePheromoneAge[gridPos.X, gridPos.Y];
            }
            else
            {
                return _foodPheromoneAge[gridPos.X, gridPos.Y];
            }
        }
        return 0.0f;
    }
}