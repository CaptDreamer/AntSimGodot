using Godot;
using System;

public partial class Ant : CharacterBody2D
{
    // References
    private Environment _environment;
    private PheromoneMap _pheromoneMap;

    // Movement properties
    [Export] public float Speed = 50.0f;
    [Export] public float TurnSpeed = 5.0f;
    [Export] public float WanderStrength = 0.3f;
    [Export] public float SensingAngle = Mathf.Pi / 4.0f;  // 45 degrees
    [Export] public float SensingDistance = 8.0f;

    // State
    public bool CarryingFood = false;

    // Pheromone deposit properties
    [Export] public float DepositInterval = 0.1f;
    [Export] public float DepositAmount = 0.1f;
    private float _depositTimer = 0.0f;

    // Obstacle avoidance
    [Export] public float AvoidRange = 16.0f;
    [Export] public float AvoidForce = 3.0f;

    // Visualization
    private Color _normalColor = new Color(0.0f, 0.0f, 0.0f);
    private Color _carryingFoodColor = new Color(0.7f, 0.5f, 0.0f);

    // Called when the node enters the scene tree
    public override void _Ready()
    {
        // Set random seed for this ant
        Random random = new Random();

        // Randomize initial rotation
        Rotation = (float)(random.NextDouble() * Mathf.Pi * 2.0);

        // Get references to other nodes - these will be set by AntManager when it spawns the ant
        // We'll implement AntManager in the next step
    }

    // Physics process for movement
    public override void _PhysicsProcess(double delta)
    {
        // Only process if references are set
        if (_environment == null || _pheromoneMap == null)
            return;

        // Update pheromone deposit timer
        _depositTimer += (float)delta;

        // Deposit pheromones at intervals
        if (_depositTimer >= DepositInterval)
        {
            DepositPheromone();
            _depositTimer = 0.0f;
        }

        // Determine which pheromone to follow based on state
        var pheromoneTypeToFollow = CarryingFood ?
            PheromoneMap.PheromoneType.Home :
            PheromoneMap.PheromoneType.Food;

        // Calculate steering force based on pheromones
        Vector2 steeringForce = SensePheromones(pheromoneTypeToFollow);

        // Add obstacle avoidance
        Vector2 avoidanceForce = AvoidObstacles();
        steeringForce += avoidanceForce;

        // Add random wander behavior
        Vector2 wanderForce = Wander();
        steeringForce += wanderForce;

        // Apply steering
        ApplySteering(steeringForce, (float)delta);

        // Move the ant
        Vector2 forward = new Vector2(Mathf.Cos(Rotation), Mathf.Sin(Rotation));
        Vector2 motion = forward * Speed * (float)delta;
        Velocity = motion;
        MoveAndSlide();

        // Check for food or home collision
        CheckCollisions();

        // Update appearance based on state
        UpdateAppearance();
    }

    // Sense pheromones to determine steering direction
    private Vector2 SensePheromones(PheromoneMap.PheromoneType pheromoneType)
    {
        // Sample pheromones at 3 points: ahead left, ahead center, ahead right
        Vector2 forward = new Vector2(Mathf.Cos(Rotation), Mathf.Sin(Rotation));
        Vector2 right = forward.Rotated(Mathf.Pi / 2.0f);

        Vector2 centerPos = Position + forward * SensingDistance;
        Vector2 leftPos = Position + forward.Rotated(-SensingAngle) * SensingDistance;
        Vector2 rightPos = Position + forward.Rotated(SensingAngle) * SensingDistance;

        // Convert to grid positions
        Vector2I centerGrid = _environment.WorldToGrid(centerPos);
        Vector2I leftGrid = _environment.WorldToGrid(leftPos);
        Vector2I rightGrid = _environment.WorldToGrid(rightPos);

        // Sample pheromone values
        float centerValue = _pheromoneMap.GetPheromone(centerGrid, pheromoneType);
        float leftValue = _pheromoneMap.GetPheromone(leftGrid, pheromoneType);
        float rightValue = _pheromoneMap.GetPheromone(rightGrid, pheromoneType);

        // Decide turning direction based on strongest pheromone
        Vector2 steeringDirection = Vector2.Zero;

        if (centerValue > leftValue && centerValue > rightValue)
        {
            // Center has strongest pheromone, continue forward
            steeringDirection = forward;
        }
        else if (leftValue > rightValue)
        {
            // Left has strongest pheromone, steer left
            steeringDirection = forward.Rotated(-SensingAngle);
        }
        else if (rightValue > leftValue)
        {
            // Right has strongest pheromone, steer right
            steeringDirection = forward.Rotated(SensingAngle);
        }
        else
        {
            // No strong pheromone, continue forward
            steeringDirection = forward;
        }

        return steeringDirection;
    }

    // Random wander behavior to add variability to movement
    private Vector2 Wander()
    {
        // Generate a random angle
        float randomAngle = (float)(GD.Randf() * Mathf.Pi * 2.0 - Mathf.Pi);

        // Create a random direction vector
        Vector2 randomDir = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));

        // Scale by wander strength
        return randomDir * WanderStrength;
    }

    // Check for collisions with walls, food, and home
    private void CheckCollisions()
    {
        // Get current position on grid
        Vector2I gridPos = _environment.WorldToGrid(Position);

        // Check if position is valid
        if (!_environment.IsValidGridPosition(gridPos))
        {
            ResetToHome();
            return;
        }

        // Check cell type
        var cellType = _environment.GetCellType(gridPos);

        switch (cellType)
        {
            case Environment.CellType.Wall:
                // Bounce off walls
                Rotation += Mathf.Pi;
                break;

            case Environment.CellType.Food:
                if (!CarryingFood)
                {
                    // Pick up food
                    CarryingFood = true;

                    // Reverse direction to head back
                    Rotation += Mathf.Pi;
                }
                break;

            case Environment.CellType.Home:
                if (CarryingFood)
                {
                    // Drop off food at home
                    CarryingFood = false;

                    // Reverse direction to head out again
                    Rotation += Mathf.Pi;
                }
                break;
        }
    }

    // Avoid obstacles (walls)
    private Vector2 AvoidObstacles()
    {
        Vector2 forward = new Vector2(Mathf.Cos(Rotation), Mathf.Sin(Rotation));
        Vector2 avoidanceForce = Vector2.Zero;

        // Check for walls in front
        Vector2 forwardPos = Position + forward * AvoidRange;
        Vector2I forwardGrid = _environment.WorldToGrid(forwardPos);

        if (_environment.IsValidGridPosition(forwardGrid) &&
            _environment.GetCellType(forwardGrid) == Environment.CellType.Wall)
        {
            // Generate avoidance force perpendicular to forward direction
            float randomSign = GD.Randf() > 0.5f ? 1.0f : -1.0f;
            avoidanceForce = forward.Rotated(Mathf.Pi / 2.0f * randomSign) * AvoidForce;
        }

        return avoidanceForce;
    }

    // Apply steering force to change direction
    private void ApplySteering(Vector2 steeringForce, float delta)
    {
        if (steeringForce.LengthSquared() > 0.01f)
        {
            // Calculate target rotation
            float targetRotation = Mathf.Atan2(steeringForce.Y, steeringForce.X);

            // Smoothly rotate towards target
            // Implement our own angle wrapping to keep rotation difference in range [-PI, PI]
            float rotationDifference = targetRotation - Rotation;
            rotationDifference = ((rotationDifference + Mathf.Pi) % (Mathf.Pi * 2)) - Mathf.Pi;

            Rotation += rotationDifference * TurnSpeed * delta;
        }
    }

    // Deposit pheromone based on current state
    private void DepositPheromone()
    {
        Vector2I gridPos = _environment.WorldToGrid(Position);

        if (CarryingFood)
        {
            // Returning to home, deposit food pheromone (red)
            _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Food, DepositAmount);
        }
        else
        {
            // Heading out, deposit home pheromone (blue)
            _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Home, DepositAmount);
        }
    }

    // Reset ant to home position
    public void ResetToHome()
    {
        // Find home position
        Vector2I homePos = _environment.GetHomePosition();

        // Convert to world position
        Vector2 worldPos = _environment.GridToWorld(homePos);

        // Center in cell
        Position = worldPos + new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);

        // Random direction
        Rotation = (float)(GD.Randf() * Mathf.Pi * 2.0);

        // Reset state
        CarryingFood = false;
    }

    // Update ant appearance based on state
    private void UpdateAppearance()
    {
        // Change color based on whether carrying food
        Modulate = CarryingFood ? _carryingFoodColor : _normalColor;
    }

    // Set references to other nodes (called by AntManager)
    public void SetReferences(Environment environment, PheromoneMap pheromoneMap)
    {
        _environment = environment;
        _pheromoneMap = pheromoneMap;
    }

    // Draw the ant
    public override void _Draw()
    {
        // Draw triangle for ant
        Vector2[] points = new Vector2[]
        {
            new Vector2(4, 0),      // Front point
            new Vector2(-2, -2),    // Back left
            new Vector2(-2, 2)      // Back right
        };

        Color drawColor = CarryingFood ? _carryingFoodColor : _normalColor;
        DrawColoredPolygon(points, drawColor);
    }
}