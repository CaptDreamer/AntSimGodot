using Godot;
using System;
using System.Collections.Generic;

public partial class Ant : CharacterBody2D
{
    // References
    private Environment _environment;
    private PheromoneMap _pheromoneMap;

    // Movement properties
    [Export] public float MoveSpeed = 50.0f;
    [Export] public float SteerStrength = 2.0f;
    [Export] public float WanderStrength = 0.5f;

    // Sensing properties
    [Export] public float SensorAngleDegrees = 45.0f;
    [Export] public float SensorOffsetDst = 8.0f;
    [Export] public float SensorSize = 1.0f;

    // State
    public bool CarryingFood = false;
    private List<Vector2I> _foodSource = new List<Vector2I>();
    private Vector2I _homePosition;
    private Vector2 _velocity;
    private Vector2 _homeDir;

    // Visualization
    [Export] public Color AntColor = new Color(0.0f, 0.0f, 0.0f);
    [Export] public Color CarryingFoodColor = new Color(0.7f, 0.5f, 0.0f);

    // Called when the node enters the scene tree
    public override void _Ready()
    {
        // Set initial heading to a random direction
        float randomAngle = (float)(GD.Randf() * Mathf.Pi * 2);
        _velocity = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)).Normalized() * MoveSpeed;
        Rotation = randomAngle;
    }

    // Physics process for movement
    public override void _PhysicsProcess(double delta)
    {
        // Only process if references are set
        if (_environment == null || _pheromoneMap == null)
            return;

        if (_homePosition == Vector2I.Zero)
        {
            _homePosition = _environment.GetHomePosition();
        }

        // Calculate steering
        Vector2 steeringForce = CalculateSteering((float)delta);
        _velocity += steeringForce * SteerStrength * (float)delta;

        // Normalize and set speed
        if (_velocity.LengthSquared() > 0.01f)
        {
            _velocity = _velocity.Normalized() * MoveSpeed;
        }

        // Apply movement
        Velocity = _velocity * (float)delta;
        MoveAndSlide();

        // Update rotation to match velocity
        Rotation = Mathf.Atan2(_velocity.Y, _velocity.X);

        // Check for collision with food or home
        CheckInteraction();

        // Update appearance based on state
        UpdateAppearance();
    }

    // Calculate steering forces
    private Vector2 CalculateSteering(float delta)
    {
        Vector2 desiredDirection = Vector2.Zero;

        // Determine which pheromone to follow based on state
        PheromoneMap.PheromoneType pheromoneToFollow = CarryingFood
            ? PheromoneMap.PheromoneType.Home
            : PheromoneMap.PheromoneType.Food;

        // Get information from sensors
        SensorInfo sensorInfo = Sense(pheromoneToFollow);

        // Detect walls and avoid them
        Vector2 wallAvoidanceForce = GetWallAvoidanceForce();
        desiredDirection += wallAvoidanceForce * 4.0f;  // High priority for wall avoidance

        // Follow pheromone trail
        if (sensorInfo.centerValue > 0.01f || sensorInfo.leftValue > 0.01f || sensorInfo.rightValue > 0.01f)
        {
            // Determine steering direction based on sensor values
            if (sensorInfo.centerValue > sensorInfo.leftValue && sensorInfo.centerValue > sensorInfo.rightValue)
            {
                // Center has strongest pheromone, continue forward
                desiredDirection += _velocity.Normalized();
            }
            else if (sensorInfo.leftValue > sensorInfo.rightValue)
            {
                // Left has strongest pheromone, steer left
                desiredDirection += _velocity.Rotated(-SensorAngleDegrees * Mathf.Pi / 180.0f).Normalized();
            }
            else
            {
                // Right has strongest pheromone, steer right
                desiredDirection += _velocity.Rotated(SensorAngleDegrees * Mathf.Pi / 180.0f).Normalized();
            }

            // Scale by strongest value
            float strongestValue = Mathf.Max(sensorInfo.centerValue, Mathf.Max(sensorInfo.leftValue, sensorInfo.rightValue));
            desiredDirection *= strongestValue * 2.0f;  // Stronger influence for stronger trails
        }
        else
        {
            // No pheromone trail found, add random wander
            desiredDirection += GetWanderForce() * WanderStrength;

            // If carrying food and lost, try to head home
            if (CarryingFood)
            {
                // Calculate vector to home
                Vector2 homeWorldPos = _environment.GridToWorld(_homePosition) +
                                     new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);
                Vector2 toHome = (homeWorldPos - Position).Normalized();

                // Add home-seeking force when lost
                float homeInfluence = 0.5f;
                desiredDirection += toHome * homeInfluence;
            }
        }

        // Calculate steering force to reach desired direction
        Vector2 steeringForce = Vector2.Zero;
        if (desiredDirection.LengthSquared() > 0.01f)
        {
            steeringForce = (desiredDirection.Normalized() * MoveSpeed - _velocity);
        }

        return steeringForce;
    }

    // Sense the environment using three points
    private SensorInfo Sense(PheromoneMap.PheromoneType pheromoneType)
    {
        // Get forward direction
        Vector2 forwardDir = _velocity.Normalized();

        // Calculate sensor positions
        Vector2 leftSensorDir = forwardDir.Rotated(-SensorAngleDegrees * Mathf.Pi / 180.0f);
        Vector2 rightSensorDir = forwardDir.Rotated(SensorAngleDegrees * Mathf.Pi / 180.0f);

        Vector2 leftSensorPos = Position + leftSensorDir * SensorOffsetDst;
        Vector2 centerSensorPos = Position + forwardDir * SensorOffsetDst;
        Vector2 rightSensorPos = Position + rightSensorDir * SensorOffsetDst;

        // Sample pheromone values at sensor positions
        float leftValue = SamplePheromone(leftSensorPos, pheromoneType);
        float centerValue = SamplePheromone(centerSensorPos, pheromoneType);
        float rightValue = SamplePheromone(rightSensorPos, pheromoneType);

        return new SensorInfo
        {
            leftValue = leftValue,
            centerValue = centerValue,
            rightValue = rightValue
        };
    }

    // Sample pheromone value at world position
    private float SamplePheromone(Vector2 worldPos, PheromoneMap.PheromoneType pheromoneType)
    {
        Vector2I gridPos = _environment.WorldToGrid(worldPos);

        // Check if valid position
        if (!_environment.IsValidGridPosition(gridPos))
            return 0.0f;

        // Check if wall
        if (_environment.GetCellType(gridPos) == Environment.CellType.Wall)
            return 0.0f;

        // Return pheromone value
        return _pheromoneMap.GetPheromone(gridPos, pheromoneType);
    }

    // Calculate random wander force
    private Vector2 GetWanderForce()
    {
        // Generate random angle
        float randomAngle = (float)((GD.Randf() * 2.0f - 1.0f) * Mathf.Pi);

        // Create random direction vector
        return new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
    }

    // Calculate wall avoidance force
    private Vector2 GetWallAvoidanceForce()
    {
        // Look for walls ahead at various angles
        Vector2 forwardDir = _velocity.Normalized();
        float rayLength = 10.0f;
        Vector2 avoidanceForce = Vector2.Zero;

        // Check forward, left and right for walls
        for (int i = -1; i <= 1; i++)
        {
            float angle = i * Mathf.Pi / 4.0f;  // -45, 0, 45 degrees
            Vector2 dir = forwardDir.Rotated(angle);
            Vector2 rayEnd = Position + dir * rayLength;
            Vector2I gridPos = _environment.WorldToGrid(rayEnd);

            bool isWall = _environment.IsValidGridPosition(gridPos) &&
                         _environment.GetCellType(gridPos) == Environment.CellType.Wall;

            if (isWall)
            {
                // Create force away from wall, stronger for middle ray
                float forceMagnitude = (i == 0) ? 1.0f : 0.5f;
                Vector2 awayFromWall = -dir.Normalized() * forceMagnitude;

                // Also add perpendicular component to slide along walls
                Vector2 perpendicular = new Vector2(-dir.Y, dir.X) * forceMagnitude;
                avoidanceForce += (awayFromWall + perpendicular);
            }
        }

        return avoidanceForce;
    }

    // Check for interaction with food or home
    private void CheckInteraction()
    {
        Vector2I gridPos = _environment.WorldToGrid(Position);

        // Check for valid position
        if (!_environment.IsValidGridPosition(gridPos))
        {
            ResetToHome();
            return;
        }

        // Check cell type for interaction
        Environment.CellType cellType = _environment.GetCellType(gridPos);

        switch (cellType)
        {
            case Environment.CellType.Food:
                if (!CarryingFood)
                {
                    // Pick up food
                    CarryingFood = true;

                    // Remember this food source
                    if (!_foodSource.Contains(gridPos))
                    {
                        _foodSource.Add(gridPos);
                    }

                    // Deposit strong food pheromone
                    _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Food, 1.0f);

                    // Turn around
                    _velocity = -_velocity;
                }
                break;

            case Environment.CellType.Home:
                if (CarryingFood)
                {
                    // Drop off food
                    CarryingFood = false;

                    // Deposit strong home pheromone
                    _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Home, 1.0f);

                    // If we know where food is, head back there
                    if (_foodSource.Count > 0)
                    {
                        // Turn toward food source
                        Vector2I foodPos = _foodSource[0];
                        Vector2 foodWorldPos = _environment.GridToWorld(foodPos) +
                                            new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);
                        _velocity = (foodWorldPos - Position).Normalized() * MoveSpeed;
                    }
                    else
                    {
                        // Turn around
                        _velocity = -_velocity;
                    }
                }
                break;

            case Environment.CellType.Wall:
                // Bounce off wall
                _velocity = GetWallAvoidanceForce().Normalized() * MoveSpeed;
                break;
        }
    }

    // Deposit pheromone trail
    private void DepositPheromone()
    {
        Vector2I gridPos = _environment.WorldToGrid(Position);

        if (!_environment.IsValidGridPosition(gridPos))
            return;

        if (CarryingFood)
        {
            // When carrying food, deposit FOOD pheromone (trail to food)
            _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Food, 0.1f);
        }
        else
        {
            // When not carrying food, deposit HOME pheromone (trail to home)
            _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Home, 0.1f);
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
        float randomAngle = (float)(GD.Randf() * Mathf.Pi * 2.0);
        _velocity = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)).Normalized() * MoveSpeed;

        // Reset state
        CarryingFood = false;
    }

    // Update appearance based on state
    private void UpdateAppearance()
    {
        Modulate = CarryingFood ? CarryingFoodColor : AntColor;
        QueueRedraw();
    }

    // Set references to other nodes (called by AntManager)
    public void SetReferences(Environment environment, PheromoneMap pheromoneMap)
    {
        _environment = environment;
        _pheromoneMap = pheromoneMap;
        _homePosition = _environment.GetHomePosition();
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

        DrawColoredPolygon(points, Modulate);
    }

    // Process
    public override void _Process(double delta)
    {
        // Deposit pheromone every frame
        DepositPheromone();
    }

    // Helper class for sensor information
    private class SensorInfo
    {
        public float leftValue;
        public float centerValue;
        public float rightValue;
    }
}