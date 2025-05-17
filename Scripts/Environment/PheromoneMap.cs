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

    // Pheromone grid properties - completely separate grids
    private float[,] _homePheromoneGrid; // Grid for home pheromones
    private float[,] _foodPheromoneGrid; // Grid for food pheromones
    private float[,] _homePheromoneAge;  // Age tracking for home pheromones
    private float[,] _foodPheromoneAge;  // Age tracking for food pheromones

    // Pheromone parameters
    [Export] public float EvaporationRate = 0.002f;  // Very slow evaporation
    [Export] public float MaxPheromone = 1.0f;      // Maximum pheromone value
    [Export] public float PheromoneThreshold = 0.05f; // Threshold below which pheromones are removed
    [Export] public float MaxAge = 100.0f;          // Maximum age tracking (in seconds)
    [Export] public float DiffusionRate = 0.02f;     // Gentler diffusion for player-created trails

    // Visualization options
    [Export] public bool ShowGradients = true;
    [Export] public bool HighlightPheromones = true; // Makes pheromones more visible
    [Export] public bool BlendPheromones = false;    // If true, blend pheromones in visualization (separate logic still)

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

        // Initialize pheromone grids - completely separate for each type
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

        // Toggle pheromone highlight with H key
        if (Input.IsKeyPressed(Key.H))
        {
            HighlightPheromones = !HighlightPheromones;
            GD.Print($"Pheromone highlighting: {(HighlightPheromones ? "ON" : "OFF")}");
            UpdatePheromoneTextures();
            QueueRedraw();
        }

        // Toggle gradient view with G key
        if (Input.IsKeyPressed(Key.G))
        {
            ShowGradients = !ShowGradients;
            GD.Print($"Pheromone gradient visualization: {(ShowGradients ? "ON" : "OFF")}");
            UpdatePheromoneTextures();
            QueueRedraw();
        }

        // Toggle pheromone blending with B key
        if (Input.IsKeyPressed(Key.B))
        {
            BlendPheromones = !BlendPheromones;
            GD.Print($"Pheromone blending visualization: {(BlendPheromones ? "ON" : "OFF")}");
            UpdatePheromoneTextures();
            QueueRedraw();
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

    // Clear only a specific type of pheromone
    public void ClearPheromoneType(PheromoneType type)
    {
        Vector2I gridSize = _environment.GridSize;

        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                if (type == PheromoneType.Home)
                {
                    _homePheromoneGrid[x, y] = 0.0f;
                    _homePheromoneAge[x, y] = 0.0f;
                }
                else
                {
                    _foodPheromoneGrid[x, y] = 0.0f;
                    _foodPheromoneAge[x, y] = 0.0f;
                }
            }
        }

        UpdatePheromoneTextures();
        QueueRedraw();
        GD.Print($"Cleared all {type} pheromones");
    }

    // Update pheromone values with evaporation and age tracking
    private void UpdatePheromones(float delta)
    {
        Vector2I gridSize = _environment.GridSize;

        // Create temporary grids for diffusion calculation - separate for each type
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

        // Main update loop for pheromones - process SEPARATELY for each type
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

                // Process HOME pheromones
                _homePheromoneGrid[x, y] = Math.Max(0.0f, tempHomeGrid[x, y] - evaporationAmount);
                if (_homePheromoneGrid[x, y] > PheromoneThreshold)
                {
                    _homePheromoneAge[x, y] = Math.Min(_homePheromoneAge[x, y] + delta, MaxAge);
                }
                else
                {
                    _homePheromoneGrid[x, y] = 0.0f;
                    _homePheromoneAge[x, y] = 0.0f;
                }

                // Process FOOD pheromones (completely independently)
                _foodPheromoneGrid[x, y] = Math.Max(0.0f, tempFoodGrid[x, y] - evaporationAmount);
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

        // Apply diffusion as a separate pass for each pheromone type
        float diffusionAmount = DiffusionRate * delta;

        // Process HOME pheromone diffusion
        ProcessPheromoneTypeDiffusion(PheromoneType.Home, tempHomeGrid, diffusionAmount);

        // Process FOOD pheromone diffusion (completely separate)
        ProcessPheromoneTypeDiffusion(PheromoneType.Food, tempFoodGrid, diffusionAmount);
    }

    // Process diffusion for a specific pheromone type
    private void ProcessPheromoneTypeDiffusion(PheromoneType type, float[,] tempGrid, float diffusionAmount)
    {
        Vector2I gridSize = _environment.GridSize;

        for (int x = 0; x < gridSize.X; x++)
        {
            for (int y = 0; y < gridSize.Y; y++)
            {
                // Skip if this is a wall or empty
                if (_environment.GetCellType(new Vector2I(x, y)) == Environment.CellType.Wall ||
                    tempGrid[x, y] < PheromoneThreshold)
                {
                    continue;
                }

                // Count valid neighbors
                int validNeighbors = 0;
                float diffuseTotal = 0.0f;

                // Check neighbors (use 4-way diffusion for clearer trail formation)
                int[] dx = { 0, 1, 0, -1 }; // 4-way diffusion: right, down, left, up
                int[] dy = { 1, 0, -1, 0 };

                for (int i = 0; i < 4; i++)
                {
                    int neighborX = x + dx[i];
                    int neighborY = y + dy[i];

                    // Check if valid position and not a wall
                    if (neighborX >= 0 && neighborX < gridSize.X &&
                        neighborY >= 0 && neighborY < gridSize.Y &&
                        _environment.GetCellType(new Vector2I(neighborX, neighborY)) != Environment.CellType.Wall)
                    {
                        validNeighbors++;

                        // Diffuse from higher to lower concentrations to maintain gradient
                        if (tempGrid[x, y] > tempGrid[neighborX, neighborY])
                        {
                            float diff = tempGrid[x, y] - tempGrid[neighborX, neighborY];
                            float transferAmount = diff * diffusionAmount;

                            // Update the appropriate grid based on type
                            if (type == PheromoneType.Home)
                            {
                                _homePheromoneGrid[neighborX, neighborY] += transferAmount;
                            }
                            else
                            {
                                _foodPheromoneGrid[neighborX, neighborY] += transferAmount;
                            }

                            diffuseTotal += transferAmount;
                        }
                    }
                }

                // Reduce the source cell by the amount diffused
                if (validNeighbors > 0)
                {
                    // Update the appropriate grid based on type
                    if (type == PheromoneType.Home)
                    {
                        _homePheromoneGrid[x, y] = Math.Max(0.0f, _homePheromoneGrid[x, y] - diffuseTotal);
                        // Cap pheromone values at maximum
                        _homePheromoneGrid[x, y] = Math.Min(_homePheromoneGrid[x, y], MaxPheromone);
                    }
                    else
                    {
                        _foodPheromoneGrid[x, y] = Math.Max(0.0f, _foodPheromoneGrid[x, y] - diffuseTotal);
                        // Cap pheromone values at maximum
                        _foodPheromoneGrid[x, y] = Math.Min(_foodPheromoneGrid[x, y], MaxPheromone);
                    }
                }
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

        // Update pixels with enhanced visualization
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

                // Render HOME pheromones (BLUE)
                float homeValue = _homePheromoneGrid[x, y];
                if (homeValue > PheromoneThreshold)
                {
                    // Normalize for better visibility and increase intensity if highlighting is on
                    float normalizationFactor = HighlightPheromones ? 0.5f : 0.7f;
                    float intensity = Mathf.Clamp(homeValue / (maxHome * normalizationFactor), 0.0f, 1.0f);

                    // Increase opacity for better visibility when highlighting
                    float opacity = HighlightPheromones ? 0.9f : 0.8f;

                    Color homeColor;

                    if (ShowGradients)
                    {
                        // Age-based gradient visualization
                        float ageNormalized = _homePheromoneAge[x, y] / MaxAge;
                        if (ageNormalized < 0.3f)
                            homeColor = new Color(0.0f, 0.0f, 1.0f, intensity * opacity); // Pure Blue (newer)
                        else if (ageNormalized < 0.6f)
                            homeColor = new Color(0.0f, 0.5f, 1.0f, intensity * opacity); // Blue-Cyan (medium)
                        else
                            homeColor = new Color(0.0f, 0.8f, 1.0f, intensity * opacity); // Cyan (older)
                    }
                    else
                    {
                        // Simple BLUE color for HOME pheromones - brighter for better visibility
                        homeColor = new Color(0.3f, 0.3f, 1.0f, intensity * opacity);
                    }

                    newImageHome.SetPixel(x, y, homeColor);
                }
                else
                {
                    newImageHome.SetPixel(x, y, new Color(0, 0, 0, 0));
                }

                // Render FOOD pheromones (RED)
                float foodValue = _foodPheromoneGrid[x, y];
                if (foodValue > PheromoneThreshold)
                {
                    // Normalize for better visibility and increase intensity if highlighting is on
                    float normalizationFactor = HighlightPheromones ? 0.5f : 0.7f;
                    float intensity = Mathf.Clamp(foodValue / (maxFood * normalizationFactor), 0.0f, 1.0f);

                    // Increase opacity for better visibility when highlighting
                    float opacity = HighlightPheromones ? 0.9f : 0.8f;

                    Color foodColor;

                    if (ShowGradients)
                    {
                        // Age-based gradient visualization
                        float ageNormalized = _foodPheromoneAge[x, y] / MaxAge;
                        if (ageNormalized < 0.3f)
                            foodColor = new Color(1.0f, 0.0f, 0.0f, intensity * opacity); // Pure Red (newer)
                        else if (ageNormalized < 0.6f)
                            foodColor = new Color(1.0f, 0.5f, 0.0f, intensity * opacity); // Orange (medium)
                        else
                            foodColor = new Color(1.0f, 0.8f, 0.0f, intensity * opacity); // Yellow-Orange (older)
                    }
                    else
                    {
                        // Simple RED color for FOOD pheromones - brighter for better visibility
                        foodColor = new Color(1.0f, 0.3f, 0.3f, intensity * opacity);
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

    // Add pheromone at a specific grid position - does NOT overwrite the other type
    public void AddPheromone(Vector2I gridPos, PheromoneType type, float amount)
    {
        if (_environment.IsValidGridPosition(gridPos))
        {
            if (type == PheromoneType.Home)
            {
                // Only affect HOME pheromones, leave FOOD pheromones untouched

                // Reset age when adding fresh pheromone
                _homePheromoneAge[gridPos.X, gridPos.Y] = 0.0f;

                // Add pheromone value
                _homePheromoneGrid[gridPos.X, gridPos.Y] =
                    Mathf.Min(MaxPheromone, _homePheromoneGrid[gridPos.X, gridPos.Y] + amount);
            }
            else // PheromoneType.Food
            {
                // Only affect FOOD pheromones, leave HOME pheromones untouched

                // Reset age when adding fresh pheromone
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

    // Get the dominant pheromone type at a specific grid position
    public PheromoneType GetDominantPheromoneType(Vector2I gridPos)
    {
        if (_environment.IsValidGridPosition(gridPos))
        {
            float homeValue = _homePheromoneGrid[gridPos.X, gridPos.Y];
            float foodValue = _foodPheromoneGrid[gridPos.X, gridPos.Y];

            return (foodValue > homeValue) ? PheromoneType.Food : PheromoneType.Home;
        }
        return PheromoneType.Home; // Default
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

    // Set pheromone values directly - used for player controls
    public void SetPheromoneValue(Vector2I gridPos, PheromoneType type, float value)
    {
        if (_environment.IsValidGridPosition(gridPos))
        {
            if (type == PheromoneType.Home)
            {
                // Only modify HOME pheromones
                _homePheromoneGrid[gridPos.X, gridPos.Y] = Mathf.Clamp(value, 0.0f, MaxPheromone);
                _homePheromoneAge[gridPos.X, gridPos.Y] = 0.0f; // Reset age
            }
            else
            {
                // Only modify FOOD pheromones
                _foodPheromoneGrid[gridPos.X, gridPos.Y] = Mathf.Clamp(value, 0.0f, MaxPheromone);
                _foodPheromoneAge[gridPos.X, gridPos.Y] = 0.0f; // Reset age
            }
        }
    }

    // Sample pheromone value for ant sensors at world position
    public float SamplePheromone(Vector2 worldPos, PheromoneType type)
    {
        Vector2I gridPos = _environment.WorldToGrid(worldPos);

        // Check if valid position
        if (!_environment.IsValidGridPosition(gridPos))
            return 0.0f;

        // Check if wall
        if (_environment.GetCellType(gridPos) == Environment.CellType.Wall)
            return 0.0f;

        // Return the specific pheromone value requested
        return GetPheromone(gridPos, type);
    }

    // Create a test pattern of pheromones (useful for debugging)
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