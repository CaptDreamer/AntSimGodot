// PheromoneMap.cs - Optimized version with improved visualization

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
    [Export] public float EvaporationRate = 0.002f;  // Very slow evaporation
    [Export] public float MaxPheromone = 1.0f;      // Maximum pheromone value
    [Export] public float PheromoneThreshold = 0.05f; // Threshold below which pheromones are removed
    [Export] public float MaxAge = 100.0f;          // Maximum age tracking (in seconds)
    [Export] public float DiffusionRate = 0.03f;     // Gentler diffusion

    // Visuals
    private ImageTexture _pheromoneTextureHome;
    private ImageTexture _pheromoneTextureFood;
    private Image _imageHome;
    private Image _imageFood;

    // Update control
    [Export] public float UpdateInterval = 0.05f; // 20 updates per second
    private float _updateTimer = 0;

    // Debug
    [Export] public bool DebugMode = false;
    [Export] public bool ShowGradients = true;

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

    // Update pheromones (evaporation, diffusion)
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

        // Debug keys only active in debug mode
        if (DebugMode)
        {
            // Toggle gradient view
            if (Input.IsKeyPressed(Key.G))
            {
                ShowGradients = !ShowGradients;
                GD.Print($"Pheromone gradient visualization: {(ShowGradients ? "ON" : "OFF")}");
                UpdatePheromoneTextures();
                QueueRedraw();
            }

            // Clear all pheromones
            if (Input.IsKeyPressed(Key.C))
            {
                ClearAllPheromones();
                GD.Print("Cleared all pheromones");
            }
        }
    }

    // Clear all pheromones from the map
    public void ClearAllPheromones()
    {
        Vector2I gridSize = _environment.GridSize;

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

        UpdatePheromoneTextures();
        QueueRedraw();
    }

    // Update pheromone values with evaporation and age tracking
    private void UpdatePheromones(float delta)
    {
        Vector2I gridSize = _environment.GridSize;

        // Create temporary grids for diffusion calculation
        float[,] tempHomeGrid = new float[gridSize.X, gridSize.Y];
        float[,] tempFoodGrid = new float[gridSize.X, gridSize.Y];

        // Copy current values to temp grids
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                tempHomeGrid[x, y] = _homePheromoneGrid[x, y];
                tempFoodGrid[x, y] = _foodPheromoneGrid[x, y];
            }
        }

        // Calculate evaporation amount for this frame
        float evaporationAmount = EvaporationRate * delta;

        // Main update loop for pheromones
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

                // Apply evaporation
                _homePheromoneGrid[x, y] = Math.Max(0.0f, tempHomeGrid[x, y] - evaporationAmount);
                _foodPheromoneGrid[x, y] = Math.Max(0.0f, tempFoodGrid[x, y] - evaporationAmount);

                // Update ages
                if (_homePheromoneGrid[x, y] > PheromoneThreshold)
                {
                    _homePheromoneAge[x, y] = Math.Min(_homePheromoneAge[x, y] + delta, MaxAge);
                }
                else
                {
                    _homePheromoneGrid[x, y] = 0.0f;
                    _homePheromoneAge[x, y] = 0.0f;
                }

                if (_foodPheromoneGrid[x, y] > PheromoneThreshold)
                {
                    _foodPheromoneAge[x, y] = Math.Min(_foodPheromoneAge[x, y] + delta, MaxAge);
                }
                else
                {
                    _foodPheromoneGrid[x, y] = 0.0f;
                    _foodPheromoneAge[x, y] = 0.0f;
                }
            }
        }

        // Apply diffusion as a separate pass to maintain gradients better
        float diffusionAmount = DiffusionRate * delta;
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                // Skip if this is a wall or empty
                if (_environment.GetCellType(new Vector2I(x, y)) == Environment.CellType.Wall ||
                    (tempHomeGrid[x, y] < PheromoneThreshold && tempFoodGrid[x, y] < PheromoneThreshold))
                {
                    continue;
                }

                // Count valid neighbors
                int validNeighbors = 0;
                float homeDiffuseTotal = 0.0f;
                float foodDiffuseTotal = 0.0f;

                // Check neighbors
                for (int nx = -1; nx <= 1; nx++)
                {
                    for (int ny = -1; ny <= 1; ny++)
                    {
                        // Skip self
                        if (nx == 0 && ny == 0) continue;

                        // Skip diagonals for simpler diffusion
                        if (nx != 0 && ny != 0) continue;

                        int neighborX = x + nx;
                        int neighborY = y + ny;

                        // Check if valid position and not a wall
                        if (neighborX >= 0 && neighborX < gridSize.X &&
                            neighborY >= 0 && neighborY < gridSize.Y &&
                            _environment.GetCellType(new Vector2I(neighborX, neighborY)) != Environment.CellType.Wall)
                        {
                            validNeighbors++;

                            // For diffusion, we only want to diffuse from higher to lower concentrations
                            // This helps maintain the gradient
                            if (tempHomeGrid[x, y] > tempHomeGrid[neighborX, neighborY])
                            {
                                float diff = tempHomeGrid[x, y] - tempHomeGrid[neighborX, neighborY];
                                float transferAmount = diff * diffusionAmount;
                                _homePheromoneGrid[neighborX, neighborY] += transferAmount;
                                homeDiffuseTotal += transferAmount;
                            }

                            if (tempFoodGrid[x, y] > tempFoodGrid[neighborX, neighborY])
                            {
                                float diff = tempFoodGrid[x, y] - tempFoodGrid[neighborX, neighborY];
                                float transferAmount = diff * diffusionAmount;
                                _foodPheromoneGrid[neighborX, neighborY] += transferAmount;
                                foodDiffuseTotal += transferAmount;
                            }
                        }
                    }
                }

                // Reduce the source cell by the amount diffused
                if (validNeighbors > 0)
                {
                    _homePheromoneGrid[x, y] = Math.Max(0.0f, _homePheromoneGrid[x, y] - homeDiffuseTotal);
                    _foodPheromoneGrid[x, y] = Math.Max(0.0f, _foodPheromoneGrid[x, y] - foodDiffuseTotal);
                }

                // Cap pheromone values at maximum
                _homePheromoneGrid[x, y] = Math.Min(_homePheromoneGrid[x, y], MaxPheromone);
                _foodPheromoneGrid[x, y] = Math.Min(_foodPheromoneGrid[x, y], MaxPheromone);
            }
        }
    }

    // Update pheromone texture visualizations with improved visibility
    private void UpdatePheromoneTextures()
    {
        Vector2I gridSize = _environment.GridSize;

        // Create new images
        Image newImageHome = Image.CreateEmpty(gridSize.X, gridSize.Y, false, Image.Format.Rgba8);
        Image newImageFood = Image.CreateEmpty(gridSize.X, gridSize.Y, false, Image.Format.Rgba8);

        // Find maximum values for better visualization
        float maxHome = 0.01f;
        float maxFood = 0.01f;

        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                maxHome = Math.Max(maxHome, _homePheromoneGrid[x, y]);
                maxFood = Math.Max(maxFood, _foodPheromoneGrid[x, y]);
            }
        }

        // Log maximum values if in debug mode
        if (DebugMode)
        {
            GD.Print($"Max pheromone values - Home: {maxHome:F3}, Food: {maxFood:F3}");
        }

        // Update pixels with clear visualization
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

                // HOME pheromones (BLUE) - lead to home
                float homeValue = _homePheromoneGrid[x, y];
                if (homeValue > PheromoneThreshold)
                {
                    // Normalize for better visibility and increase intensity
                    float intensity = Mathf.Clamp(homeValue / (maxHome * 0.7f), 0.0f, 1.0f);

                    Color homeColor;

                    if (ShowGradients)
                    {
                        // Age-based gradient visualization when enabled
                        float ageNormalized = _homePheromoneAge[x, y] / MaxAge;
                        if (ageNormalized < 0.3f)
                            homeColor = new Color(0.0f, 0.0f, 1.0f, intensity * 0.8f); // Pure Blue (newer)
                        else if (ageNormalized < 0.6f)
                            homeColor = new Color(0.0f, 0.5f, 1.0f, intensity * 0.8f); // Blue-Cyan (medium)
                        else
                            homeColor = new Color(0.0f, 0.8f, 1.0f, intensity * 0.8f); // Cyan (older)
                    }
                    else
                    {
                        // Simple BLUE color for HOME pheromones - very clear and bright
                        homeColor = new Color(0.0f, 0.0f, 1.0f, intensity * 0.8f);
                    }

                    newImageHome.SetPixel(x, y, homeColor);
                }
                else
                {
                    newImageHome.SetPixel(x, y, new Color(0, 0, 0, 0));
                }

                // FOOD pheromones (RED) - lead to food
                float foodValue = _foodPheromoneGrid[x, y];
                if (foodValue > PheromoneThreshold)
                {
                    // Normalize for better visibility and increase intensity
                    float intensity = Mathf.Clamp(foodValue / (maxFood * 0.7f), 0.0f, 1.0f);

                    Color foodColor;

                    if (ShowGradients)
                    {
                        // Age-based gradient visualization when enabled
                        float ageNormalized = _foodPheromoneAge[x, y] / MaxAge;
                        if (ageNormalized < 0.3f)
                            foodColor = new Color(1.0f, 0.0f, 0.0f, intensity * 0.8f); // Pure Red (newer)
                        else if (ageNormalized < 0.6f)
                            foodColor = new Color(1.0f, 0.5f, 0.0f, intensity * 0.8f); // Orange (medium)
                        else
                            foodColor = new Color(1.0f, 0.8f, 0.0f, intensity * 0.8f); // Yellow-Orange (older)
                    }
                    else
                    {
                        // Simple RED color for FOOD pheromones - very clear and bright
                        foodColor = new Color(1.0f, 0.0f, 0.0f, intensity * 0.8f);
                    }

                    newImageFood.SetPixel(x, y, foodColor);
                }
                else
                {
                    newImageFood.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
        }

        // Update textures
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
            DrawTextureRect(_pheromoneTextureHome, destinationRect, false, new Color(1, 1, 1, 0.6f));
        }

        if (_pheromoneTextureFood != null)
        {
            // Draw food pheromone texture (red)
            DrawTextureRect(_pheromoneTextureFood, destinationRect, false, new Color(1, 1, 1, 0.6f));
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
            else // PheromoneType.Food
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

    // Set pheromone values directly - useful for debugging
    public void SetPheromoneValue(Vector2I gridPos, PheromoneType type, float value)
    {
        if (_environment.IsValidGridPosition(gridPos))
        {
            if (type == PheromoneType.Home)
            {
                _homePheromoneGrid[gridPos.X, gridPos.Y] = Mathf.Clamp(value, 0.0f, MaxPheromone);
                _homePheromoneAge[gridPos.X, gridPos.Y] = 0.0f; // Reset age
            }
            else
            {
                _foodPheromoneGrid[gridPos.X, gridPos.Y] = Mathf.Clamp(value, 0.0f, MaxPheromone);
                _foodPheromoneAge[gridPos.X, gridPos.Y] = 0.0f; // Reset age
            }
        }
    }

    // Create a test pattern of pheromones - useful for debugging
    public void CreateTestPattern()
    {
        Vector2I gridSize = _environment.GridSize;
        Vector2I homePos = _environment.GetHomePosition();

        // Clear existing pheromones
        ClearAllPheromones();

        // Create a gradient of HOME pheromones from the home position
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                Vector2I pos = new Vector2I(x, y);

                // Skip walls
                if (_environment.GetCellType(pos) == Environment.CellType.Wall)
                    continue;

                // Calculate distance from home
                float distFromHome = pos.DistanceTo(homePos);
                float maxDist = gridSize.Length() / 2.0f;

                // Create a gradient - stronger near home, weaker further away
                float homeValue = Mathf.Max(0.0f, 1.0f - (distFromHome / maxDist));

                // Set home pheromone if value is significant
                if (homeValue > PheromoneThreshold)
                {
                    SetPheromoneValue(pos, PheromoneType.Home, homeValue * MaxPheromone);
                }
            }
        }

        // Create some test FOOD pheromones
        Vector2I foodTestPos = new Vector2I(homePos.X + gridSize.X / 4, homePos.Y + gridSize.Y / 4);

        // Create a gradient of FOOD pheromones from the test food position
        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                Vector2I pos = new Vector2I(x, y);

                // Skip walls
                if (_environment.GetCellType(pos) == Environment.CellType.Wall)
                    continue;

                // Calculate distance from food test position
                float distFromFood = pos.DistanceTo(foodTestPos);
                float maxDist = gridSize.Length() / 3.0f; // Smaller radius

                // Create a gradient - stronger near food, weaker further away
                float foodValue = Mathf.Max(0.0f, 1.0f - (distFromFood / maxDist));

                // Set food pheromone if value is significant
                if (foodValue > PheromoneThreshold)
                {
                    SetPheromoneValue(pos, PheromoneType.Food, foodValue * MaxPheromone);
                }
            }
        }

        // Update visuals
        UpdatePheromoneTextures();
        QueueRedraw();

        GD.Print("Created test pheromone pattern");
    }
}