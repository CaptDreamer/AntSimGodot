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

        // Add test walls
        SetCellType(new Vector2I(10, 10), CellType.Wall);

        GD.Print("Environment ready. Grid size: " + GridSize + ", Cell size: " + CellSize);
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

        // Add test walls in corners to verify drawing
        SetCellType(new Vector2I(0, 0), CellType.Wall);
        SetCellType(new Vector2I(GridSize.X - 1, 0), CellType.Wall);
        SetCellType(new Vector2I(0, GridSize.Y - 1), CellType.Wall);
        SetCellType(new Vector2I(GridSize.X - 1, GridSize.Y - 1), CellType.Wall);

        GD.Print("Grid initialized with test walls in corners");
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
                    if (mouseButton.ButtonIndex == MouseButton.Left)
                    {
                        // Left click to place wall
                        if (Input.IsKeyPressed(Key.Shift))
                        {
                            // With shift held, remove wall
                            if (GetCellType(gridPos) == CellType.Wall)
                            {
                                SetCellType(gridPos, CellType.Empty);
                                EmitSignal(SignalName.WallRemoved, gridPos);
                            }
                        }
                        else
                        {
                            // Place wall
                            if (GetCellType(gridPos) == CellType.Empty)
                            {
                                SetCellType(gridPos, CellType.Wall);
                                EmitSignal(SignalName.WallPlaced, gridPos);
                                GD.Print($"Wall placed at {gridPos}");
                            }
                        }
                    }
                    else if (mouseButton.ButtonIndex == MouseButton.Right)
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
                            // Place food
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
                // Left mouse button held down
                if ((motion.ButtonMask & MouseButtonMask.Left) != 0)
                {
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
                        // Draw walls
                        if (GetCellType(gridPos) == CellType.Empty)
                        {
                            SetCellType(gridPos, CellType.Wall);
                            EmitSignal(SignalName.WallPlaced, gridPos);
                        }
                    }
                }
                // Right mouse button held down
                else if ((motion.ButtonMask & MouseButtonMask.Right) != 0)
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
                        // Draw food
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

        // Draw test walls explicitly regardless of grid state
        DrawRect(new Rect2(GridToWorld(new Vector2I(15, 15)), CellSize), new Color(0, 0, 0, 1), true);
        DrawRect(new Rect2(GridToWorld(new Vector2I(16, 15)), CellSize), new Color(0, 0, 0, 1), true);
        DrawRect(new Rect2(GridToWorld(new Vector2I(17, 15)), CellSize), new Color(0, 0, 0, 1), true);
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
        // Debug - if T key is pressed, output current mouse position details
        if (Input.IsKeyPressed(Key.T))
        {
            Vector2 mousePos = GetGlobalMousePosition();
            Vector2I gridPos = WorldToGrid(mousePos);
            CellType cellType = GetCellType(gridPos);

            GD.Print($"Mouse at {mousePos}, Grid: {gridPos}, Cell type: {cellType}");

            // Force place a wall here regardless of cell state
            _grid[gridPos.X, gridPos.Y] = CellType.Wall;
            QueueRedraw();
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

            //GD.Print($"Cell changed at {gridPos}: {oldType} -> {type}");

            // Verify the change was made
            CellType newType = _grid[gridPos.X, gridPos.Y];
            if (newType != type)
            {
                GD.Print($"ERROR: Cell type not changed correctly! Current value: {newType}");
            }

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
}