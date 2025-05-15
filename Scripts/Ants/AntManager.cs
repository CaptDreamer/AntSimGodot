using Godot;
using System;
using System.Collections.Generic;

public partial class AntManager : Node2D
{
    // References
    private Environment _environment;
    private PheromoneMap _pheromoneMap;
    private Node2D _antsContainer;

    // Ant properties
    [Export] public PackedScene AntScene;
    [Export] public int NumAnts = 100;
    private List<Ant> _antList = new List<Ant>();

    // Home position (will be set in _Ready)
    private Vector2I _homePos;

    // Called when the node enters the scene tree
    public override void _Ready()
    {
        // Get references to other nodes
        _environment = GetNode<Environment>("../Environment");
        _pheromoneMap = GetNode<PheromoneMap>("../PheromoneMap");
        _antsContainer = GetNode<Node2D>("Ants");

        // Ensure AntScene is set
        if (AntScene == null)
        {
            GD.PrintErr("AntScene not set in AntManager!");
            return;
        }

        // Wait a frame for environment to initialize
        CallDeferred(nameof(Initialize));
    }

    // Initialize after environment is ready
    private void Initialize()
    {
        // Find home position
        FindHomePosition();

        // Connect to environment signals
        _environment.Connect("FoodPlaced", Callable.From<Vector2I>(OnFoodPlaced));
        _environment.Connect("FoodRemoved", Callable.From<Vector2I>(OnFoodRemoved));

        // Create ants
        SpawnAnts();
    }

    // Find the home position in the environment
    private void FindHomePosition()
    {
        _homePos = _environment.GetHomePosition();
    }

    // Spawn the initial ants
    private void SpawnAnts()
    {
        for (int i = 0; i < NumAnts; i++)
        {
            CreateAnt();
        }
    }

    // Create a single ant
    private Ant CreateAnt()
    {
        Ant ant = AntScene.Instantiate<Ant>();
        _antsContainer.AddChild(ant);

        // Set references
        ant.SetReferences(_environment, _pheromoneMap);

        // Position at home
        Vector2 worldPos = _environment.GridToWorld(_homePos);
        ant.Position = worldPos + new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);

        // Random direction
        ant.Rotation = (float)(GD.Randf() * Math.PI * 2.0);

        // Add to list
        _antList.Add(ant);

        return ant;
    }

    // Update ants
    public override void _Process(double delta)
    {
        // Check if any ants have gone out of bounds
        foreach (Ant ant in _antList)
        {
            if (ant == null || !GodotObject.IsInstanceValid(ant))
                continue;

            // If ant is outside the valid area, reset it
            if (!_environment.IsValidGridPosition(_environment.WorldToGrid(ant.Position)))
            {
                ant.ResetToHome();
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            // Get the mouse position
            Vector2 mousePos = GetGlobalMousePosition();

            // Check if we clicked on an ant
            float closestDistance = 10.0f; // Selection radius
            Ant closestAnt = null;

            foreach (Ant ant in _antList)
            {
                if (ant == null || !GodotObject.IsInstanceValid(ant))
                    continue;

                float distance = mousePos.DistanceTo(ant.Position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestAnt = ant;
                }
            }

            // If we found an ant close to the click, toggle its selection
            if (closestAnt != null)
            {
                closestAnt.ToggleSelection();
            }
        }
    }

    // Event handler for food placement
    private void OnFoodPlaced(Vector2I pos)
    {
        // Notify ants of new food source if needed
        // Currently, ants will discover new food through their normal movement
    }

    // Event handler for food removal
    private void OnFoodRemoved(Vector2I pos)
    {
        // Notify ants that food was removed if needed
        // For ants carrying food that was just removed, we might want to reset them
    }
}