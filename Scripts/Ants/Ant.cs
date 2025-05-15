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

    // Selection and debugging
    public bool IsSelected { get; private set; } = false;
    private static Ant _selectedAnt = null;
    private float _debugTimer = 0f;
    private const float DEBUG_INTERVAL = 0.5f; // Only print debug info every half second

    // State
    public bool CarryingFood = false;
    private List<Vector2I> _foodSource = new List<Vector2I>();
    private Vector2I _homePosition;
    private Vector2 _velocity;
    private Vector2 _homeDir;

    // Visualization
    [Export] public Color AntColor = new Color(0.0f, 0.0f, 0.0f);
    [Export] public Color CarryingFoodColor = new Color(0.7f, 0.5f, 0.0f);
    [Export] public Color SelectedColor = new Color(0.0f, 1.0f, 1.0f);

    // Called when the node enters the scene tree
    public override void _Ready()
    {
        // Set initial heading to a random direction
        float randomAngle = (float)(GD.Randf() * Mathf.Pi * 2);
        _velocity = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)).Normalized() * MoveSpeed;
        Rotation = randomAngle;
    }

    // Toggle selection state
    public void ToggleSelection()
    {
        // If this ant is already selected, deselect it
        if (IsSelected)
        {
            IsSelected = false;
            _selectedAnt = null;
            DebugLog("Deselected");
        }
        // Otherwise, select this ant and deselect any other
        else
        {
            if (_selectedAnt != null)
                _selectedAnt.IsSelected = false;

            IsSelected = true;
            _selectedAnt = this;
            DebugLog("Selected");

            // Print current state information
            Vector2I gridPos = _environment.WorldToGrid(Position);
            DebugLog($"Position: {Position}, Grid: {gridPos}, Carrying Food: {CarryingFood}");
            DebugLog($"Velocity: {_velocity}, Speed: {_velocity.Length()}");

            // Print pheromone info at current position
            float homePhero = _pheromoneMap.GetPheromone(gridPos, PheromoneMap.PheromoneType.Home);
            float foodPhero = _pheromoneMap.GetPheromone(gridPos, PheromoneMap.PheromoneType.Food);
            DebugLog($"Pheromones at position - Home: {homePhero:F3}, Food: {foodPhero:F3}");
        }

        // Update appearance
        UpdateAppearance();
    }

    // Helper method for debug logging - only prints for selected ant
    private void DebugLog(string message)
    {
        if (IsSelected)
        {
            GD.Print($"Ant #{GetInstanceId()}: {message}");
        }
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
        // Rotation = Mathf.Atan2(_velocity.Y, _velocity.X);  // REMOVE THIS LINE

        // Instead, rotate the ant gradually toward its velocity direction
        float targetRotation = Mathf.Atan2(_velocity.Y, _velocity.X);
        float rotationDifference = Mathf.AngleDifference(Rotation, targetRotation);
        Rotation += rotationDifference * 0.2f; // Gradually turn toward movement direction

        // Check for collision with food or home
        CheckInteraction();

        // Update appearance based on state
        UpdateAppearance();

        // Update debug info periodically if selected
        if (IsSelected)
        {
            _debugTimer += (float)delta;
            if (_debugTimer >= DEBUG_INTERVAL)
            {
                _debugTimer = 0;

                // Log current position and pheromones
                Vector2I gridPos = _environment.WorldToGrid(Position);
                float homePhero = _pheromoneMap.GetPheromone(gridPos, PheromoneMap.PheromoneType.Home);
                float foodPhero = _pheromoneMap.GetPheromone(gridPos, PheromoneMap.PheromoneType.Food);

                DebugLog($"Position: {gridPos}, Home Phero: {homePhero:F3}, Food Phero: {foodPhero:F3}");

                // Get which pheromone we're following
                PheromoneMap.PheromoneType followingType = CarryingFood ?
                    PheromoneMap.PheromoneType.Home : PheromoneMap.PheromoneType.Food;
                DebugLog($"Following: {followingType} pheromone");

                // Get sensor readings
                SensorInfo info = Sense(followingType);
                DebugLog($"Sensors: L={info.leftValue:F3}, C={info.centerValue:F3}, R={info.rightValue:F3}");
            }
        }
    }

    // Calculate steering forces
    private Vector2 CalculateSteering(float delta)
    {
        Vector2 desiredDirection = Vector2.Zero;

        // CRITICAL FIX: This is CORRECT - we follow the gradient that leads to our goal
        // When carrying food: follow HOME pheromones (blue) to get back to nest
        // When seeking food: follow FOOD pheromones (red) to find food sources
        PheromoneMap.PheromoneType pheromoneToFollow = CarryingFood
            ? PheromoneMap.PheromoneType.Home  // Blue leads to home
            : PheromoneMap.PheromoneType.Food; // Red leads to food

        // Debug output to confirm which pheromone we're following
        if (IsSelected)
        {
            DebugLog($"State: {(CarryingFood ? "Carrying Food" : "Seeking Food")}, Following: {pheromoneToFollow}");
        }

        // Get information from sensors
        SensorInfo sensorInfo = Sense(pheromoneToFollow);

        // Detect walls and avoid them - always high priority
        Vector2 wallAvoidanceForce = GetWallAvoidanceForce();
        desiredDirection += wallAvoidanceForce * 4.0f;  // High priority for wall avoidance

        // IMPROVED sensing and response for better trail following
        float maxSensorValue = Mathf.Max(sensorInfo.centerValue,
                                        Mathf.Max(sensorInfo.leftValue, sensorInfo.rightValue));

        // Lower threshold to detect weaker pheromone trails (0.05f -> 0.03f)
        if (maxSensorValue > 0.03f)
        {
            // Calculate forward, left, and right directions
            Vector2 forwardDir = _velocity.Normalized();
            Vector2 leftDir = forwardDir.Rotated(-SensorAngleDegrees * Mathf.Pi / 180.0f);
            Vector2 rightDir = forwardDir.Rotated(SensorAngleDegrees * Mathf.Pi / 180.0f);

            // Simple direction selection based on strongest pheromone
            // INCREASED multiplier for stronger following behavior (3.0f -> 5.0f)
            float pheromoneFollowStrength = 5.0f;

            // Scale strength based on pheromone intensity for more natural movement
            // Stronger pheromones result in more determined following
            float strengthModifier = 1.0f + (maxSensorValue * 2.0f); // Up to 3x stronger when full pheromone strength
            pheromoneFollowStrength *= strengthModifier;

            if (IsSelected)
            {
                DebugLog($"Pheromone strength: {maxSensorValue:F2}, Follow force: {pheromoneFollowStrength:F2}");
            }

            // Determine which direction has the strongest pheromone
            if (sensorInfo.centerValue > sensorInfo.leftValue && sensorInfo.centerValue > sensorInfo.rightValue)
            {
                // Center has strongest pheromone - continue forward
                desiredDirection += forwardDir * pheromoneFollowStrength;
                if (IsSelected) DebugLog("Following center direction");
            }
            else if (sensorInfo.leftValue > sensorInfo.rightValue)
            {
                // Left has strongest pheromone - steer left
                // Make turns slightly sharper for better trail following
                Vector2 turnDir = forwardDir.Rotated(-SensorAngleDegrees * 1.2f * Mathf.Pi / 180.0f);
                desiredDirection += turnDir * pheromoneFollowStrength;
                if (IsSelected) DebugLog("Following left direction");
            }
            else
            {
                // Right has strongest pheromone - steer right
                // Make turns slightly sharper for better trail following
                Vector2 turnDir = forwardDir.Rotated(SensorAngleDegrees * 1.2f * Mathf.Pi / 180.0f);
                desiredDirection += turnDir * pheromoneFollowStrength;
                if (IsSelected) DebugLog("Following right direction");
            }

            // When following a strong trail, reduce random movement to a minimum
            // Only add a tiny amount of wander for natural-looking movement
            desiredDirection += GetWanderForce() * (WanderStrength * 0.1f);
        }
        else
        {
            // No pheromone trail found, add random wander at full strength
            desiredDirection += GetWanderForce() * WanderStrength;
            if (IsSelected) DebugLog("No pheromone detected, wandering");

            // Direct homing/food seeking behavior remains unchanged
            if (CarryingFood)
            {
                // When carrying food and lost, try to head home directly
                Vector2 homeWorldPos = _environment.GridToWorld(_homePosition) +
                                     new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);
                Vector2 toHome = (homeWorldPos - Position).Normalized();

                // Increase home-seeking force when lost (0.8f -> 1.2f)
                desiredDirection += toHome * 1.2f;

                if (IsSelected) DebugLog("Lost with food - heading directly home");
            }
            else if (_foodSource.Count > 0)
            {
                // When seeking food and lost but knowing a food source, head there
                Vector2 foodWorldPos = _environment.GridToWorld(_foodSource[0]) +
                                     new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);
                Vector2 toFood = (foodWorldPos - Position).Normalized();

                // Slightly stronger pull toward known food (0.4f -> 0.6f)
                desiredDirection += toFood * 0.6f;

                if (IsSelected) DebugLog("Lost but knowing food - heading to food source");
            }
        }

        // Calculate steering force to reach desired direction
        Vector2 steeringForce = Vector2.Zero;
        if (desiredDirection.LengthSquared() > 0.01f)
        {
            // We want to steer toward our desired direction
            // This approach creates a force that tries to align our velocity with desired direction
            steeringForce = (desiredDirection.Normalized() * MoveSpeed - _velocity);
        }

        return steeringForce;
    }

    // Sense the environment using three points
    private SensorInfo Sense(PheromoneMap.PheromoneType pheromoneType)
    {
        // IMPORTANT CHANGE: Use the ant's forward direction based on its visual orientation
        // instead of velocity direction
        Vector2 forwardDir = new Vector2(Mathf.Cos(Rotation), Mathf.Sin(Rotation));

        // Calculate sensor positions using a fixed sensing cone in front of the ant
        float sensorAngle = 35.0f; // Degrees - reasonable angle for sensing
        Vector2 leftSensorDir = forwardDir.Rotated(-sensorAngle * Mathf.Pi / 180.0f);
        Vector2 rightSensorDir = forwardDir.Rotated(sensorAngle * Mathf.Pi / 180.0f);

        // Fixed sensor distance
        float sensorDistance = 12.0f;

        // Calculate sensor positions
        Vector2 leftSensorPos = Position + leftSensorDir * sensorDistance;
        Vector2 centerSensorPos = Position + forwardDir * sensorDistance;
        Vector2 rightSensorPos = Position + rightSensorDir * sensorDistance;

        // Sample pheromone values at sensor positions
        float leftValue = SamplePheromone(leftSensorPos, pheromoneType);
        float centerValue = SamplePheromone(centerSensorPos, pheromoneType);
        float rightValue = SamplePheromone(rightSensorPos, pheromoneType);

        return new SensorInfo
        {
            leftValue = leftValue,
            centerValue = centerValue,
            rightValue = rightValue,
            leftPos = leftSensorPos,
            centerPos = centerSensorPos,
            rightPos = rightSensorPos
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

        // Return the SPECIFIC pheromone value requested
        float value = _pheromoneMap.GetPheromone(gridPos, pheromoneType);
        return value;
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
                        _foodSource.Clear(); // Only remember the most recent food source
                        _foodSource.Add(gridPos);
                    }

                    // CRITICAL: Deposit a STRONG food pheromone at the food source
                    // This creates a clear signal for other ants to find food
                    _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Food, 1.0f);

                    DebugLog("Found food! Deposited strong food pheromone and turning around");

                    // Turn around - use direct vector to home for more efficient return
                    Vector2 homeWorldPos = _environment.GridToWorld(_homePosition) +
                                         new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);
                    _velocity = (homeWorldPos - Position).Normalized() * MoveSpeed;
                }
                break;

            case Environment.CellType.Home:
                if (CarryingFood)
                {
                    // Drop off food
                    CarryingFood = false;

                    // Deposit strong home pheromone
                    _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Home, 1.0f);
                    DebugLog("Dropped off food at home! Deposited strong home pheromone");

                    // If we know where food is, head back there with more determination
                    if (_foodSource.Count > 0)
                    {
                        // Turn toward food source directly
                        Vector2I foodPos = _foodSource[0];
                        Vector2 foodWorldPos = _environment.GridToWorld(foodPos) +
                                            new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);
                        _velocity = (foodWorldPos - Position).Normalized() * MoveSpeed;

                        // Add slight randomization to prevent all ants taking exactly the same path
                        _velocity = _velocity.Rotated((float)(GD.Randf() * 0.2f - 0.1f));
                        DebugLog($"Heading back to known food at {foodPos}");
                    }
                    else
                    {
                        // Turn around with some randomization to explore
                        _velocity = _velocity.Rotated((float)(GD.Randf() * Mathf.Pi * 0.5f - Mathf.Pi * 0.25f)) * MoveSpeed;
                        DebugLog("No known food source, exploring");
                    }
                }
                break;

            case Environment.CellType.Wall:
                // Bounce off wall
                _velocity = GetWallAvoidanceForce().Normalized() * MoveSpeed;
                DebugLog("Hit wall, bouncing");
                break;
        }
    }

    // Deposit pheromone trail
    private void DepositPheromone()
    {
        Vector2I gridPos = _environment.WorldToGrid(Position);

        if (!_environment.IsValidGridPosition(gridPos))
            return;

        // Skip if this is a wall
        if (_environment.GetCellType(gridPos) == Environment.CellType.Wall)
            return;

        // ABSOLUTELY CLEAR PHEROMONE DEPOSITION LOGIC:

        // 1. Ants carrying food deposit FOOD pheromones (red)
        // These lead TO food sources (for food-seeking ants to follow)
        if (CarryingFood)
        {
            // When carrying food, deposit FOOD pheromone (red)
            // The strength is higher when further from home to create a gradient
            Vector2 homeWorldPos = _environment.GridToWorld(_homePosition) +
                                 new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);
            float distanceToHome = Position.DistanceTo(homeWorldPos);
            float maxDistance = 300.0f;

            // Strength increases with distance from home (stronger near food)
            float strength = 0.3f * Mathf.Clamp(distanceToHome / maxDistance, 0.3f, 1.0f);

            // CRITICAL: Deposit FOOD (red) pheromone that other ants will follow TO food
            _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Food, strength);
        }
        // 2. Ants seeking food deposit HOME pheromones (blue)
        // These lead TO home (for ants carrying food to follow)
        else
        {
            // When searching for food, deposit HOME pheromone (blue)
            // The strength is lower when further from home to create a gradient
            Vector2 homeWorldPos = _environment.GridToWorld(_homePosition) +
                                 new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);
            float distanceToHome = Position.DistanceTo(homeWorldPos);
            float maxDistance = 300.0f;

            // Strength decreases with distance from home (stronger near home)
            float strength = 0.3f * (1.0f - Mathf.Clamp(distanceToHome / maxDistance, 0.0f, 0.7f));

            // CRITICAL: Deposit HOME (blue) pheromone that ants carrying food will follow TO home
            _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Home, strength);
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
        DebugLog("Reset to home position");
    }

    // Update appearance based on state
    private void UpdateAppearance()
    {
        if (IsSelected)
        {
            // Use a bright highlight color for selected ant
            Modulate = CarryingFood ?
                new Color(1.0f, 0.8f, 0.0f) : // Bright gold when carrying food
                SelectedColor;                 // Bright cyan when not carrying food
        }
        else
        {
            // Normal colors for non-selected ants
            Modulate = CarryingFood ? CarryingFoodColor : AntColor;
        }
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

        // If selected, draw sensor positions and extra debug visualization
        if (IsSelected)
        {
            // Which pheromone type we're following
            PheromoneMap.PheromoneType type = CarryingFood ?
                PheromoneMap.PheromoneType.Home : PheromoneMap.PheromoneType.Food;

            // Get sensor info
            SensorInfo info = Sense(type);

            // Draw circles at sensor positions
            float sensorRadius = 2.0f;

            // Drawing in local space, so adjust positions
            DrawCircle(info.leftPos - Position, sensorRadius, Colors.Yellow);
            DrawCircle(info.centerPos - Position, sensorRadius, Colors.Yellow);
            DrawCircle(info.rightPos - Position, sensorRadius, Colors.Yellow);

            // Draw line to indicate velocity (current direction)
            DrawLine(Vector2.Zero, _velocity.Normalized() * 10, Colors.Green, 1.5f);

            // Draw steering force if we have a method to access it
            Vector2 steeringForce = CalculateSteering(0.016f); // Use a typical delta time
            DrawLine(Vector2.Zero, steeringForce.Normalized() * 15, Colors.Red, 2.0f);
        }
    }

    // Process
    public override void _Process(double delta)
    {
        // Deposit pheromone every frame
        DepositPheromone();

        // Debug keys only work if ant is selected
        if (IsSelected)
        {
            // Debug key: Press F to force deposit food pheromone at current location
            if (Input.IsKeyPressed(Key.F))
            {
                Vector2I gridPos = _environment.WorldToGrid(Position);
                _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Food, 1.0f);
                DebugLog($"DEBUG: Forced food pheromone deposit at {gridPos}");
            }

            // Debug key: Press H to force deposit home pheromone at current location
            if (Input.IsKeyPressed(Key.H))
            {
                Vector2I gridPos = _environment.WorldToGrid(Position);
                _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Home, 1.0f);
                DebugLog($"DEBUG: Forced home pheromone deposit at {gridPos}");
            }

            // Debug key: Press T to output current position and sensor readings
            if (Input.IsKeyPressed(Key.T))
            {
                Vector2I gridPos = _environment.WorldToGrid(Position);
                float foodValue = _pheromoneMap.GetPheromone(gridPos, PheromoneMap.PheromoneType.Food);
                float homeValue = _pheromoneMap.GetPheromone(gridPos, PheromoneMap.PheromoneType.Home);
                DebugLog($"Position: {gridPos} - Food pheromone: {foodValue:F3}, Home pheromone: {homeValue:F3}");

                // Test sensor values for both pheromone types
                SensorInfo foodInfo = Sense(PheromoneMap.PheromoneType.Food);
                SensorInfo homeInfo = Sense(PheromoneMap.PheromoneType.Home);

                DebugLog($"Food sensors - Left: {foodInfo.leftValue:F3}, Center: {foodInfo.centerValue:F3}, Right: {foodInfo.rightValue:F3}");
                DebugLog($"Home sensors - Left: {homeInfo.leftValue:F3}, Center: {homeInfo.centerValue:F3}, Right: {homeInfo.rightValue:F3}");
            }

            // Debug key: Press R to toggle visual rays for wall detection
            if (Input.IsActionJustPressed("ui_home") || Input.IsKeyPressed(Key.R)) // R key or Home key
            {
                _showRays = !_showRays;
                DebugLog($"Wall detection rays {(_showRays ? "shown" : "hidden")}");
                QueueRedraw();
            }
        }
    }

    // Helper class for sensor information
    private class SensorInfo
    {
        public float leftValue;
        public float centerValue;
        public float rightValue;
        public Vector2 leftPos;
        public Vector2 centerPos;
        public Vector2 rightPos;
    }

    // Debug visualization
    private bool _showRays = false;
}