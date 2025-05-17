using Godot;
using System;
using System.Collections.Generic;

public partial class Environment : Node2D
{
    // Grid properties
    [Export] public Vector2I GridSize = new Vector2I(128, 72);
    [Export] public Vector2I CellSize = new Vector2I(8, 8);

    // Cell type enum
    public enum CellType
    {
        Empty = 0,
        Wall = 1,
        Food = 2,
        Home = 3
    }

    // The grid data
    private CellType[,] _grid;

    // Colors for different cell types - making walls much darker for visibility
    private Color _wallColor = new Color(0.1f, 0.1f, 0.1f);
    private Color _foodColor = new Color(0.0f, 0.7f, 0.0f);
    private Color _homeColor = new Color(0.7f, 0.0f, 0.0f);

    // Pheromone placement
    private PheromoneMap _pheromoneMap;
    private PheromoneMap.PheromoneType _selectedPheromoneType = PheromoneMap.PheromoneType.Food;
    [Export] public float PheromoneStrength = 1.0f;
    [Export] public bool PlacingPheromones = false;

    // Pheromone brush size (in cells)
    [Export] public int PheromoneSize = 1;

    // Debug mode
    [Export] public bool DebugMode = false;
    [Export] public bool ShowGrid = false;

    // Signals
    [Signal] public delegate void FoodPlacedEventHandler(Vector2I position);
    [Signal] public delegate void WallPlacedEventHandler(Vector2I position);
    [Signal] public delegate void FoodRemovedEventHandler(Vector2I position);
    [Signal] public delegate void WallRemovedEventHandler(Vector2I position);
    [Signal] public delegate void PheromoneTypeChangedEventHandler(PheromoneMap.PheromoneType type);

    // Called when the node enters the scene tree
    public override void _Ready()
    {
        // Get reference to the pheromone map
        _pheromoneMap = GetNode<PheromoneMap>("../PheromoneMap");
        if (_pheromoneMap == null)
        {
            GD.PrintErr("Failed to get PheromoneMap reference!");
        }

        // Initialize the grid
        InitializeGrid();

        // Create boundary walls
        CreateBoundaryWalls();

        GD.Print("Environment ready. Grid size: " + GridSize + ", Cell size: " + CellSize);
        GD.Print("Left-click to place walls, right-click to place food. Hold Shift+click to remove.");
        GD.Print("Press 1 to select food pheromone (red), 2 to select home pheromone (blue).");
        GD.Print("Press P to toggle pheromone placement mode. Use mouse wheel to adjust pheromone size.");
    }

    // Initialize the grid with empty cells
    private void InitializeGrid()
    {
        _grid = new CellType[GridSize.X, GridSize.Y];

        // Initialize all cells as empty
        for (int x = 0; x < GridSize.X; x++)
        {
            for (int y = 0; y < GridSize.Y; y++)
            {
                _grid[x, y] = CellType.Empty;
            }
        }

        // Set home location at center
        Vector2I homePos = new Vector2I(GridSize.X / 2, GridSize.Y / 2);
        SetCellType(homePos, CellType.Home);

        GD.Print("Grid initialized with home at center");
    }

    // Create boundary walls around the entire grid
    public void CreateBoundaryWalls()
    {
        // Add walls on the top and bottom edges
        for (int x = 0; x < GridSize.X; x++)
        {
            SetCellType(new Vector2I(x, 0), CellType.Wall);
            SetCellType(new Vector2I(x, GridSize.Y - 1), CellType.Wall);
        }

        // Add walls on the left and right edges
        for (int y = 0; y < GridSize.Y; y++)
        {
            SetCellType(new Vector2I(0, y), CellType.Wall);
            SetCellType(new Vector2I(GridSize.X - 1, y), CellType.Wall);
        }

        GD.Print("Boundary walls created");
    }

    // Place pheromone at the given grid position
    private void PlacePheromoneAtPosition(Vector2I gridPos)
    {
        if (_pheromoneMap == null || !IsValidGridPosition(gridPos))
            return;

        // Skip walls
        if (GetCellType(gridPos) == CellType.Wall)
            return;

        // Place pheromone with the current brush size
        for (int x = -PheromoneSize + 1; x < PheromoneSize; x++)
        {
            for (int y = -PheromoneSize + 1; y < PheromoneSize; y++)
            {
                Vector2I brushPos = new Vector2I(gridPos.X + x, gridPos.Y + y);

                // Check if within grid and not a wall
                if (IsValidGridPosition(brushPos) && GetCellType(brushPos) != CellType.Wall)
                {
                    // Calculate distance from center of brush for gradient effect
                    float distance = gridPos.DistanceTo(brushPos);
                    float distanceFactor = 1.0f - (distance / PheromoneSize);
                    if (distanceFactor <= 0)
                        continue;

                    // Scale strength by distance from center
                    float strength = PheromoneStrength * distanceFactor;

                    // Deposit pheromone
                    _pheromoneMap.AddPheromone(brushPos, _selectedPheromoneType, strength);
                }
            }
        }
    }

    // Process direct input
    public override void _UnhandledInput(InputEvent @event)
    {
        // Handle pheromone type selection
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            // 1 key selects food pheromone (red)
            if (keyEvent.Keycode == Key.Key1)
            {
                _selectedPheromoneType = PheromoneMap.PheromoneType.Food;
                GD.Print("Selected Food pheromone (RED)");
                EmitSignal(SignalName.PheromoneTypeChanged, (int)_selectedPheromoneType);
            }
            // 2 key selects home pheromone (blue)
            else if (keyEvent.Keycode == Key.Key2)
            {
                _selectedPheromoneType = PheromoneMap.PheromoneType.Home;
                GD.Print("Selected Home pheromone (BLUE)");
                EmitSignal(SignalName.PheromoneTypeChanged, (int)_selectedPheromoneType);
            }
            // P key toggles pheromone placement mode
            else if (keyEvent.Keycode == Key.P)
            {
                PlacingPheromones = !PlacingPheromones;
                GD.Print($"Pheromone placement mode: {(PlacingPheromones ? "ON" : "OFF")}");
            }
        }

        // Handle mouse wheel for pheromone size
        if (@event is InputEventMouseButton mouseWheel && PlacingPheromones)
        {
            if (mouseWheel.ButtonIndex == MouseButton.WheelUp)
            {
                PheromoneSize = Mathf.Clamp(PheromoneSize + 1, 1, 10);
                GD.Print($"Pheromone brush size: {PheromoneSize}");
            }
            else if (mouseWheel.ButtonIndex == MouseButton.WheelDown)
            {
                PheromoneSize = Mathf.Clamp(PheromoneSize - 1, 1, 10);
                GD.Print($"Pheromone brush size: {PheromoneSize}");
            }
        }

        // Handle mouse button for placing pheromones or food/walls
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            Vector2I gridPos = WorldToGrid(GetGlobalMousePosition());

            if (IsValidGridPosition(gridPos))
            {
                // Left mouse button for walls OR pheromones
                if (mouseButton.ButtonIndex == MouseButton.Left)
                {
                    if (PlacingPheromones)
                    {
                        // Place pheromone when in pheromone mode
                        PlacePheromoneAtPosition(gridPos);
                    }
                    else
                    {
                        // Place/remove walls when not in pheromone mode
                        // Walls remain deactivated for now - uncomment to enable
                        /*
                        if (Input.IsKeyPressed(Key.Shift))
                        {
                            // With shift held, remove walls
                            if (GetCellType(gridPos) == CellType.Wall)
                            {
                                SetCellType(gridPos, CellType.Empty);
                                EmitSignal(SignalName.WallRemoved, gridPos);
                            }
                        }
                        else
                        {
                            // Don't overwrite food or home
                            if (GetCellType(gridPos) == CellType.Empty)
                            {
                                SetCellType(gridPos, CellType.Wall);
                                EmitSignal(SignalName.WallPlaced, gridPos);
                            }
                        }
                        */
                    }
                }
                // Right mouse button for food
                else if (mouseButton.ButtonIndex == MouseButton.Right && !PlacingPheromones)
                {
                    if (Input.IsKeyPressed(Key.Shift))
                    {
                        // With shift held, remove food
                        if (GetCellType(gridPos) == CellType.Food)
                        {
                            SetCellType(gridPos, CellType.Empty);
                            EmitSignal(SignalName.FoodRemoved, gridPos);
                        }
                    }
                    else
                    {
                        // Don't overwrite home or wall
                        if (GetCellType(gridPos) == CellType.Empty)
                        {
                            SetCellType(gridPos, CellType.Food);
                            EmitSignal(SignalName.FoodPlaced, gridPos);
                        }
                    }
                }
            }
        }

        // Handle continuous drawing when mouse is held down
        if (@event is InputEventMouseMotion motion && motion.ButtonMask != 0)
        {
            Vector2I gridPos = WorldToGrid(GetGlobalMousePosition());

            if (IsValidGridPosition(gridPos))
            {
                // Left mouse button for walls OR pheromones
                if ((motion.ButtonMask & MouseButtonMask.Left) != 0)
                {
                    if (PlacingPheromones)
                    {
                        // Place pheromones when in pheromone mode
                        PlacePheromoneAtPosition(gridPos);
                    }
                    else
                    {
                        // Place/remove walls when not in pheromone mode - disabled for now
                        /*
                        if (Input.IsKeyPressed(Key.Shift))
                        {
                            // Erase walls
                            if (GetCellType(gridPos) == CellType.Wall)
                            {
                                SetCellType(gridPos, CellType.Empty);
                                EmitSignal(SignalName.WallRemoved, gridPos);
                            }
                        }
                        else
                        {
                            // Draw walls - don't overwrite food or home
                            if (GetCellType(gridPos) == CellType.Empty)
                            {
                                SetCellType(gridPos, CellType.Wall);
                                EmitSignal(SignalName.WallPlaced, gridPos);
                            }
                        }
                        */
                    }
                }
                // Right mouse button for food (when not in pheromone mode)
                else if ((motion.ButtonMask & MouseButtonMask.Right) != 0 && !PlacingPheromones)
                {
                    if (Input.IsKeyPressed(Key.Shift))
                    {
                        // Erase food
                        if (GetCellType(gridPos) == CellType.Food)
                        {
                            SetCellType(gridPos, CellType.Empty);
                            EmitSignal(SignalName.FoodRemoved, gridPos);
                        }
                    }
                    else
                    {
                        // Draw food - don't overwrite home or wall
                        if (GetCellType(gridPos) == CellType.Empty)
                        {
                            SetCellType(gridPos, CellType.Food);
                            EmitSignal(SignalName.FoodPlaced, gridPos);
                        }
                    }
                }
            }
        }
    }

    // Override the _Draw method to render the grid
    public override void _Draw()
    {
        // First draw a debugging frame around the entire grid
        Vector2 totalSize = new Vector2(GridSize.X * CellSize.X, GridSize.Y * CellSize.Y);
        DrawRect(new Rect2(Vector2.Zero, totalSize), new Color(1, 1, 1, 0.1f), false);

        for (int x = 0; x < GridSize.X; x++)
        {
            for (int y = 0; y < GridSize.Y; y++)
            {
                Vector2I cellPos = new Vector2I(x, y);
                CellType cellType = GetCellType(cellPos);
                Vector2 worldPos = GridToWorld(cellPos);

                // Draw cell based on type
                switch (cellType)
                {
                    case CellType.Wall:
                        // Draw walls with multiple techniques to ensure visibility
                        DrawRect(new Rect2(worldPos, CellSize), _wallColor, true);
                        DrawRect(new Rect2(worldPos, CellSize), new Color(0, 0, 0, 1), false);
                        break;
                    case CellType.Food:
                        DrawRect(new Rect2(worldPos, CellSize), _foodColor, true);
                        break;
                    case CellType.Home:
                        DrawRect(new Rect2(worldPos, CellSize), _homeColor, true);
                        break;
                }
            }
        }

        // Draw grid lines in debug mode
        if (ShowGrid)
        {
            // Draw horizontal grid lines
            for (int y = 0; y <= GridSize.Y; y++)
            {
                Vector2 start = new Vector2(0, y * CellSize.Y);
                Vector2 end = new Vector2(GridSize.X * CellSize.X, y * CellSize.Y);
                DrawLine(start, end, new Color(0.5f, 0.5f, 0.5f, 0.2f));
            }

            // Draw vertical grid lines
            for (int x = 0; x <= GridSize.X; x++)
            {
                Vector2 start = new Vector2(x * CellSize.X, 0);
                Vector2 end = new Vector2(x * CellSize.X, GridSize.Y * CellSize.Y);
                DrawLine(start, end, new Color(0.5f, 0.5f, 0.5f, 0.2f));
            }
        }

        // Draw pheromone brush preview when in placement mode
        if (PlacingPheromones)
        {
            Vector2I gridPos = WorldToGrid(GetGlobalMousePosition());
            if (IsValidGridPosition(gridPos))
            {
                Vector2 worldPos = GridToWorld(gridPos);

                // Determine color based on selected pheromone type
                Color previewColor = _selectedPheromoneType == PheromoneMap.PheromoneType.Food ?
                    new Color(1.0f, 0.0f, 0.0f, 0.3f) : // Red for food pheromone
                    new Color(0.0f, 0.0f, 1.0f, 0.3f);  // Blue for home pheromone

                // Draw brush preview
                int size = PheromoneSize * 2 - 1;
                int offset = PheromoneSize - 1;
                DrawRect(
                    new Rect2(
                        worldPos.X - (offset * CellSize.X),
                        worldPos.Y - (offset * CellSize.Y),
                        size * CellSize.X,
                        size * CellSize.Y
                    ),
                    previewColor,
                    true
                );

                // Draw brush outline
                DrawRect(
                    new Rect2(
                        worldPos.X - (offset * CellSize.X),
                        worldPos.Y - (offset * CellSize.Y),
                        size * CellSize.X,
                        size * CellSize.Y
                    ),
                    new Color(1.0f, 1.0f, 1.0f, 0.5f),
                    false
                );
            }
        }
    }

    // Called every frame
    public override void _Process(double delta)
    {
        // Queue redraw when in pheromone placement mode to update brush preview
        if (PlacingPheromones)
        {
            QueueRedraw();
        }

        // Toggle grid visibility with I key
        if (Input.IsKeyPressed(Key.I))
        {
            ShowGrid = !ShowGrid;
            QueueRedraw();
            GD.Print($"Grid visibility: {(ShowGrid ? "ON" : "OFF")}");
        }

        // Add random food with F1 key
        if (Input.IsKeyPressed(Key.F1))
        {
            AddRandomFood(10);
        }

        // Add maze pattern with F2 key
        if (Input.IsKeyPressed(Key.F2))
        {
            CreateSimpleMaze();
        }

        // Reset environment with F3 key
        if (Input.IsKeyPressed(Key.F3))
        {
            ClearAllExceptBoundary();
            GD.Print("Environment reset - boundaries preserved");
        }

        // Clear all pheromones with F4 key
        if (Input.IsKeyPressed(Key.F4) && _pheromoneMap != null)
        {
            _pheromoneMap.ClearAllPheromones();
            GD.Print("All pheromones cleared");
        }
    }

    // Convert grid position to world position
    public Vector2 GridToWorld(Vector2I gridPos)
    {
        return new Vector2(gridPos.X * CellSize.X, gridPos.Y * CellSize.Y);
    }

    // Convert world position to grid position
    public Vector2I WorldToGrid(Vector2 worldPos)
    {
        return new Vector2I(
            (int)(worldPos.X / CellSize.X),
            (int)(worldPos.Y / CellSize.Y)
        );
    }

    // Check if a grid position is within bounds
    public bool IsValidGridPosition(Vector2I gridPos)
    {
        return gridPos.X >= 0 && gridPos.X < GridSize.X &&
               gridPos.Y >= 0 && gridPos.Y < GridSize.Y;
    }

    // Get the type of a specific cell
    public CellType GetCellType(Vector2I gridPos)
    {
        if (IsValidGridPosition(gridPos))
        {
            return _grid[gridPos.X, gridPos.Y];
        }
        return (CellType)(-1); // Invalid position
    }

    // Set the type of a specific cell
    public void SetCellType(Vector2I gridPos, CellType type)
    {
        if (IsValidGridPosition(gridPos))
        {
            CellType oldType = _grid[gridPos.X, gridPos.Y];
            _grid[gridPos.X, gridPos.Y] = type;

            QueueRedraw(); // Request redraw of the node
        }
    }

    // Get a list of all cells of a specific type
    public List<Vector2I> GetCellsOfType(CellType type)
    {
        List<Vector2I> cells = new List<Vector2I>();

        for (int x = 0; x < GridSize.X; x++)
        {
            for (int y = 0; y < GridSize.Y; y++)
            {
                if (_grid[x, y] == type)
                {
                    cells.Add(new Vector2I(x, y));
                }
            }
        }

        return cells;
    }

    // Find the home position
    public Vector2I GetHomePosition()
    {
        List<Vector2I> homeCells = GetCellsOfType(CellType.Home);
        return homeCells.Count > 0 ? homeCells[0] : new Vector2I(GridSize.X / 2, GridSize.Y / 2);
    }

    // Add random food to the environment
    public void AddRandomFood(int count)
    {
        Random random = new Random();
        int foodAdded = 0;
        int attempts = 0;

        while (foodAdded < count && attempts < count * 10)
        {
            // Generate a random position (avoiding edges)
            int x = random.Next(2, GridSize.X - 2);
            int y = random.Next(2, GridSize.Y - 2);
            Vector2I pos = new Vector2I(x, y);

            // Only place food on empty cells
            if (GetCellType(pos) == CellType.Empty)
            {
                SetCellType(pos, CellType.Food);
                EmitSignal(SignalName.FoodPlaced, pos);
                foodAdded++;
            }

            attempts++;
        }

        GD.Print($"Added {foodAdded} random food items");
    }

    // Create a simple maze pattern
    public void CreateSimpleMaze()
    {
        Random random = new Random();

        // First clear existing walls (except boundary)
        for (int x = 1; x < GridSize.X - 1; x++)
        {
            for (int y = 1; y < GridSize.Y - 1; y++)
            {
                if (_grid[x, y] == CellType.Wall)
                {
                    SetCellType(new Vector2I(x, y), CellType.Empty);
                }
            }
        }

        // Create horizontal "lanes" every 8-12 cells
        for (int y = 8; y < GridSize.Y - 8; y += random.Next(8, 13))
        {
            int gapPos = random.Next(5, GridSize.X - 5);

            for (int x = 1; x < GridSize.X - 1; x++)
            {
                // Leave a gap for paths
                if (Math.Abs(x - gapPos) <= 2)
                    continue;

                // Don't overwrite home or food
                Vector2I pos = new Vector2I(x, y);
                if (GetCellType(pos) == CellType.Empty)
                {
                    SetCellType(pos, CellType.Wall);
                }
            }
        }

        // Create vertical "lanes" every 8-12 cells
        for (int x = 8; x < GridSize.X - 8; x += random.Next(8, 13))
        {
            int gapPos = random.Next(5, GridSize.Y - 5);

            for (int y = 1; y < GridSize.Y - 1; y++)
            {
                // Leave a gap for paths
                if (Math.Abs(y - gapPos) <= 2)
                    continue;

                // Don't overwrite home or food
                Vector2I pos = new Vector2I(x, y);
                if (GetCellType(pos) == CellType.Empty)
                {
                    SetCellType(pos, CellType.Wall);
                }
            }
        }

        // Add some random walls
        for (int i = 0; i < GridSize.X * GridSize.Y / 50; i++)
        {
            int x = random.Next(3, GridSize.X - 3);
            int y = random.Next(3, GridSize.Y - 3);
            Vector2I pos = new Vector2I(x, y);

            if (GetCellType(pos) == CellType.Empty)
            {
                SetCellType(pos, CellType.Wall);
            }
        }

        GD.Print("Created simple maze pattern");
    }

    // Clear everything except the boundary walls
    public void ClearAllExceptBoundary()
    {
        // Reset grid to empty, preserving boundary walls
        for (int x = 1; x < GridSize.X - 1; x++)
        {
            for (int y = 1; y < GridSize.Y - 1; y++)
            {
                // Exclude the home cell
                Vector2I homePos = GetHomePosition();
                if (x == homePos.X && y == homePos.Y)
                {
                    SetCellType(new Vector2I(x, y), CellType.Home);
                }
                else
                {
                    SetCellType(new Vector2I(x, y), CellType.Empty);
                }
            }
        }

        QueueRedraw();
    }
}