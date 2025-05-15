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

    // Debug mode
    [Export] public bool DebugMode = false;
    [Export] public bool ShowGrid = false;

    // Signals
    [Signal] public delegate void FoodPlacedEventHandler(Vector2I position);
    [Signal] public delegate void WallPlacedEventHandler(Vector2I position);
    [Signal] public delegate void FoodRemovedEventHandler(Vector2I position);
    [Signal] public delegate void WallRemovedEventHandler(Vector2I position);

    // Called when the node enters the scene tree for the first time
    public override void _Ready()
    {
        // Initialize the grid
        InitializeGrid();

        // Create boundary walls
        CreateBoundaryWalls();

        GD.Print("Environment ready. Grid size: " + GridSize + ", Cell size: " + CellSize);
        GD.Print("Left-click to place walls, right-click to place food. Hold Shift+click to remove.");
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

    // Process direct input
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.Pressed)
            {
                Vector2I gridPos = WorldToGrid(GetGlobalMousePosition());

                if (IsValidGridPosition(gridPos))
                {
                    // Only use right mouse button now for food placement/removal
                    if (mouseButton.ButtonIndex == MouseButton.Right)
                    {
                        // Right click to place food
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
        }

        // Handle continuous drawing when mouse is held down
        if (@event is InputEventMouseMotion motion && motion.ButtonMask != 0)
        {
            Vector2I gridPos = WorldToGrid(GetGlobalMousePosition());

            if (IsValidGridPosition(gridPos))
            {
                // Only handle right mouse button for food placement/removal
                if ((motion.ButtonMask & MouseButtonMask.Right) != 0)
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
    }

    // Helper methods for grid operations

    // Convert grid position to world position
    public Vector2 GridToWorld(Vector2I gridPos)
    {
        return new Vector2(gridPos.X * CellSize.X, gridPos.Y * CellSize.Y);
    }

    // Called every frame
    public override void _Process(double delta)
    {
        // Toggle grid visibility with G key
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