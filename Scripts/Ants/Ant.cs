using Godot;
using System;
using System.Collections.Generic;

public partial class Ant : CharacterBody2D
{
    // References
    private Environment _environment;
    private PheromoneMap _pheromoneMap;

    // Sensor node references
    private Node2D _sensorsNode;
    private Marker2D _leftSensor;
    private Marker2D _centerSensor;
    private Marker2D _rightSensor;
    private Node2D _leftVisualizer;
    private Node2D _centerVisualizer;
    private Node2D _rightVisualizer;

    // Movement properties
    [Export] public float MoveSpeed = 50.0f;
    [Export] public float SteerStrength = 2.0f;
    [Export] public float WanderStrength = 0.5f;

    // Pheromone sensing
    [Export] public bool FollowPheromones = true;
    [Export] public float PheromoneInfluence = 2.0f;
    [Export] public bool FollowGradient = true; // Whether to follow gradient or just strongest pheromone

    // Pheromone deposition
    [Export] public bool DepositPheromones = true;
    [Export] public float PheromoneAmount = 0.3f;
    [Export] public float PheromoneDepositRate = 0.1f; // Seconds between deposits
    private float _pheromoneTimer = 0.0f;

    // Direct navigation thresholds
    [Export] public int DirectNavigationDistance = 1; // If within this many cells of target, go directly to it

    // Sensor properties
    [Export] public float SensorDistance = 12.0f;
    [Export] public float SensorAngle = 35.0f; // Degrees

    // Selection and debugging
    public bool IsSelected { get; private set; } = false;
    private static Ant _selectedAnt = null;

    // State
    public bool CarryingFood = false;
    private Vector2I _homePosition;
    private Vector2 _velocity;
    private List<Vector2I> _knownFoodPositions = new List<Vector2I>();

    // Visualization
    [Export] public Color AntColor = new Color(0.0f, 0.0f, 0.0f);
    [Export] public Color CarryingFoodColor = new Color(0.7f, 0.5f, 0.0f);
    [Export] public Color SelectedColor = new Color(0.0f, 1.0f, 1.0f);
    private Color _sensorColor = Colors.Yellow;

    // Called when the node enters the scene tree
    public override void _Ready()
    {
        // Get sensor node references
        _sensorsNode = GetNode<Node2D>("Sensors");
        _leftSensor = GetNode<Marker2D>("Sensors/LeftSensor");
        _centerSensor = GetNode<Marker2D>("Sensors/CenterSensor");
        _rightSensor = GetNode<Marker2D>("Sensors/RightSensor");

        // Get sensor visualizer references
        _leftVisualizer = GetNode<Node2D>("Sensors/LeftSensor/Visualizer");
        _centerVisualizer = GetNode<Node2D>("Sensors/CenterSensor/Visualizer");
        _rightVisualizer = GetNode<Node2D>("Sensors/RightSensor/Visualizer");

        // Set initial heading to a random direction
        float randomAngle = (float)(GD.Randf() * Mathf.Pi * 2);
        _velocity = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)).Normalized() * MoveSpeed;
        Rotation = randomAngle;

        // Configure sensors properly
        ConfigureSensors();

        // Hide all sensor visualizers initially
        HideSensorVisualizers();
    }

    // Configure sensors with proper positions
    private void ConfigureSensors()
    {
        // Update sensor positions
        _centerSensor.Position = new Vector2(SensorDistance, 0); // Forward

        // Calculate left and right sensor positions based on angle
        float leftAngleRad = -SensorAngle * Mathf.Pi / 180.0f;
        float rightAngleRad = SensorAngle * Mathf.Pi / 180.0f;

        Vector2 leftPos = new Vector2(
            SensorDistance * Mathf.Cos(leftAngleRad),
            SensorDistance * Mathf.Sin(leftAngleRad)
        );

        Vector2 rightPos = new Vector2(
            SensorDistance * Mathf.Cos(rightAngleRad),
            SensorDistance * Mathf.Sin(rightAngleRad)
        );

        _leftSensor.Position = leftPos;
        _rightSensor.Position = rightPos;
    }

    // Show/hide sensor visualizers
    private void ShowSensorVisualizers(bool show = true)
    {
        _leftVisualizer.Visible = show;
        _centerVisualizer.Visible = show;
        _rightVisualizer.Visible = show;
    }

    // Hide all sensor visualizers
    private void HideSensorVisualizers()
    {
        ShowSensorVisualizers(false);
    }

    // Toggle selection state
    public void ToggleSelection()
    {
        // If this ant is already selected, deselect it
        if (IsSelected)
        {
            IsSelected = false;
            _selectedAnt = null;
        }
        // Otherwise, select this ant and deselect any other
        else
        {
            if (_selectedAnt != null)
            {
                _selectedAnt.IsSelected = false;
            }

            IsSelected = true;
            _selectedAnt = this;

            // Print basic state information
            GD.Print($"Ant #{GetInstanceId()}: Position: {Position}, Carrying Food: {CarryingFood}");
            GD.Print($"Depositing Pheromones: {DepositPheromones}, Following Pheromones: {FollowPheromones}, Following Gradient: {FollowGradient}");

            // Show sensor readings if following pheromones
            if (FollowPheromones)
            {
                PheromoneMap.PheromoneType typeToFollow = CarryingFood ?
                    PheromoneMap.PheromoneType.Home : PheromoneMap.PheromoneType.Food;

                Vector2I gridPos = _environment.WorldToGrid(Position);
                float pheromoneValue = _pheromoneMap.GetPheromone(gridPos, typeToFollow);
                GD.Print($"Following {typeToFollow} pheromones. Current value: {pheromoneValue:F2}");

                // Update sensor color based on what we're following
                _sensorColor = CarryingFood ?
                    new Color(0.0f, 0.5f, 1.0f, 0.7f) :   // Blue for home pheromones
                    new Color(1.0f, 0.5f, 0.0f, 0.7f);    // Red for food pheromones
            }
        }

        // Update appearance
        UpdateAppearance();
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

        // Calculate steering based on direct navigation, pheromones, and wandering
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

        // Set rotation to match velocity direction
        Rotation = Mathf.Atan2(_velocity.Y, _velocity.X);

        // Deposit pheromones
        if (DepositPheromones)
        {
            _pheromoneTimer += (float)delta;
            if (_pheromoneTimer >= PheromoneDepositRate)
            {
                DepositPheromone();
                _pheromoneTimer = 0.0f;
            }
        }

        // Check for collision with food or home
        CheckInteraction();

        // Update appearance based on state
        UpdateAppearance();
    }

    // Deposit pheromone at current position
    private void DepositPheromone()
    {
        Vector2I gridPos = _environment.WorldToGrid(Position);

        // Skip if not a valid position or if it's a wall
        if (!_environment.IsValidGridPosition(gridPos) ||
            _environment.GetCellType(gridPos) == Environment.CellType.Wall)
            return;

        // Determine pheromone type and strength based on state
        if (CarryingFood)
        {
            // When carrying food, deposit FOOD pheromones (red)
            // These help other ants find food sources
            _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Food, PheromoneAmount);

            if (IsSelected)
                GD.Print($"Deposited FOOD pheromone at {gridPos}");
        }
        else
        {
            // When seeking food, deposit HOME pheromones (blue)
            // These help ants carrying food find the way back home
            _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Home, PheromoneAmount);

            if (IsSelected)
                GD.Print($"Deposited HOME pheromone at {gridPos}");
        }
    }

    // Calculate steering forces with direct navigation, pheromone following, and wandering
    private Vector2 CalculateSteering(float delta)
    {
        Vector2 desiredDirection = Vector2.Zero;
        Vector2I currentGridPos = _environment.WorldToGrid(Position);

        // Wall avoidance - high priority
        Vector2 wallAvoidanceForce = GetWallAvoidanceForce();
        desiredDirection += wallAvoidanceForce * 4.0f;

        // NEW: Check if we're close to our target for direct navigation
        bool usingDirectNavigation = false;

        if (CarryingFood)
        {
            // If carrying food, check if we're close to home
            int distanceToHome = (int)currentGridPos.DistanceTo(_homePosition);

            if (distanceToHome <= DirectNavigationDistance)
            {
                // We're close to home, navigate directly there
                Vector2 homeWorldPos = _environment.GridToWorld(_homePosition) +
                                     new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);
                Vector2 toHome = (homeWorldPos - Position).Normalized();

                // Strong direct guidance
                desiredDirection += toHome * 3.0f;
                usingDirectNavigation = true;

                if (IsSelected)
                    GD.Print($"Close to home! Navigating directly there (distance: {distanceToHome})");
            }
        }
        else
        {
            // If seeking food, check if we're close to any food
            List<Vector2I> foodCells = _environment.GetCellsOfType(Environment.CellType.Food);
            Vector2I closestFood = Vector2I.Zero;
            int closestDistance = int.MaxValue;

            // Find the closest food cell
            foreach (Vector2I foodPos in foodCells)
            {
                int distance = (int)currentGridPos.DistanceTo(foodPos);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestFood = foodPos;
                }
            }

            // If we're close to a food cell, go directly there
            if (closestDistance <= DirectNavigationDistance && closestFood != Vector2I.Zero)
            {
                Vector2 foodWorldPos = _environment.GridToWorld(closestFood) +
                                     new Vector2(_environment.CellSize.X / 2, _environment.CellSize.Y / 2);
                Vector2 toFood = (foodWorldPos - Position).Normalized();

                // Strong direct guidance
                desiredDirection += toFood * 3.0f;
                usingDirectNavigation = true;

                // Remember this food position
                if (!_knownFoodPositions.Contains(closestFood))
                    _knownFoodPositions.Add(closestFood);

                if (IsSelected)
                    GD.Print($"Close to food! Navigating directly there (distance: {closestDistance})");
            }
        }

        // Pheromone following - only if enabled and not using direct navigation
        if (FollowPheromones && !usingDirectNavigation)
        {
            // Determine which pheromone type to follow
            PheromoneMap.PheromoneType typeToFollow = CarryingFood ?
                PheromoneMap.PheromoneType.Home :  // When carrying food, follow HOME pheromones back to nest
                PheromoneMap.PheromoneType.Food;   // When seeking food, follow FOOD pheromones to food

            // Get sensor readings for this pheromone type
            SensorReadings readings = GetPheromoneReadings(typeToFollow);

            // Check if there are significant pheromones detected
            if (readings.centerValue > 0.05f || readings.leftValue > 0.05f || readings.rightValue > 0.05f)
            {
                if (FollowGradient)
                {
                    // IMPROVED GRADIENT FOLLOWING:
                    // Sample pheromone at current position to compare with sensors
                    float currentValue = _pheromoneMap.GetPheromone(currentGridPos, typeToFollow);

                    // Calculate gradient directions and strengths
                    Vector2 centerGradient = GetGradientDirection(readings.centerValue, currentValue, Rotation);
                    Vector2 leftGradient = GetGradientDirection(readings.leftValue, currentValue,
                                                              Rotation - (SensorAngle * Mathf.Pi / 180.0f));
                    Vector2 rightGradient = GetGradientDirection(readings.rightValue, currentValue,
                                                               Rotation + (SensorAngle * Mathf.Pi / 180.0f));

                    // Combine gradient directions, weighted by their strengths
                    Vector2 gradientDirection = centerGradient + leftGradient + rightGradient;

                    if (gradientDirection.LengthSquared() > 0.01f)
                    {
                        // Normalize and apply the gradient direction
                        desiredDirection += gradientDirection.Normalized() * PheromoneInfluence;

                        if (IsSelected)
                            GD.Print($"Following gradient - Center: {readings.centerValue:F2}, Left: {readings.leftValue:F2}, Right: {readings.rightValue:F2}");
                    }
                    else
                    {
                        // Fallback to strongest sensor if gradient is too weak
                        FollowStrongestSensor(readings, desiredDirection);
                    }
                }
                else
                {
                    // Original method: Just follow the strongest sensor
                    FollowStrongestSensor(readings, desiredDirection);
                }

                // Very slight wandering when following pheromones
                desiredDirection += GetWanderForce() * (WanderStrength * 0.2f);
            }
            else
            {
                // No significant pheromone detected - full random wandering
                desiredDirection += GetWanderForce() * WanderStrength;

                if (IsSelected)
                    GD.Print("No pheromones detected, wandering");
            }
        }
        else if (!usingDirectNavigation)
        {
            // Not following pheromones or using direct navigation - just wander randomly
            desiredDirection += GetWanderForce() * WanderStrength;
        }

        // Calculate steering force to reach desired direction
        Vector2 steeringForce = Vector2.Zero;
        if (desiredDirection.LengthSquared() > 0.01f)
        {
            steeringForce = (desiredDirection.Normalized() * MoveSpeed - _velocity);
        }

        return steeringForce;
    }

    // Helper method to follow the strongest sensor
    private void FollowStrongestSensor(SensorReadings readings, Vector2 desiredDirection)
    {
        // Determine which direction has the strongest pheromone
        if (readings.centerValue >= readings.leftValue && readings.centerValue >= readings.rightValue)
        {
            // Center has strongest - continue forward
            Vector2 forwardDir = new Vector2(Mathf.Cos(Rotation), Mathf.Sin(Rotation));
            desiredDirection += forwardDir * PheromoneInfluence;

            if (IsSelected)
                GD.Print("Following center direction (strongest)");
        }
        else if (readings.leftValue > readings.rightValue)
        {
            // Left has strongest - steer left
            float leftAngle = Rotation - (SensorAngle * Mathf.Pi / 180.0f);
            Vector2 leftDir = new Vector2(Mathf.Cos(leftAngle), Mathf.Sin(leftAngle));
            desiredDirection += leftDir * PheromoneInfluence;

            if (IsSelected)
                GD.Print("Following left direction (strongest)");
        }
        else
        {
            // Right has strongest - steer right
            float rightAngle = Rotation + (SensorAngle * Mathf.Pi / 180.0f);
            Vector2 rightDir = new Vector2(Mathf.Cos(rightAngle), Mathf.Sin(rightAngle));
            desiredDirection += rightDir * PheromoneInfluence;

            if (IsSelected)
                GD.Print("Following right direction (strongest)");
        }
    }

    // Helper method to calculate gradient direction vector
    private Vector2 GetGradientDirection(float sensorValue, float currentValue, float angle)
    {
        // Calculate the gradient (how much stronger/weaker the pheromone is at the sensor)
        float gradient = sensorValue - currentValue;

        // Only consider directions where pheromone increases
        if (gradient > 0.02f) // Small threshold to avoid noise
        {
            // Direction vector for this sensor
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            // Weight by gradient strength
            return direction * gradient * PheromoneInfluence;
        }

        return Vector2.Zero;
    }

    // Read pheromone values from sensors
    private SensorReadings GetPheromoneReadings(PheromoneMap.PheromoneType pheromoneType)
    {
        // Get global positions of sensors
        Vector2 centerPos = _centerSensor.GlobalPosition;
        Vector2 leftPos = _leftSensor.GlobalPosition;
        Vector2 rightPos = _rightSensor.GlobalPosition;

        // Sample pheromone values
        float centerValue = _pheromoneMap.SamplePheromone(centerPos, pheromoneType);
        float leftValue = _pheromoneMap.SamplePheromone(leftPos, pheromoneType);
        float rightValue = _pheromoneMap.SamplePheromone(rightPos, pheromoneType);

        // Update sensor color based on what we're following
        if (CarryingFood)
            _sensorColor = new Color(0.0f, 0.5f, 1.0f, 0.7f); // Blue for home pheromones
        else
            _sensorColor = new Color(1.0f, 0.5f, 0.0f, 0.7f); // Red for food pheromones

        return new SensorReadings
        {
            centerValue = centerValue,
            leftValue = leftValue,
            rightValue = rightValue
        };
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

                    // When finding food, deposit a stronger food pheromone to mark the source
                    if (DepositPheromones)
                    {
                        _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Food, PheromoneAmount * 2.0f);
                        if (IsSelected)
                            GD.Print("Found food! Deposited strong FOOD pheromone");
                    }

                    // Remember this food position
                    if (!_knownFoodPositions.Contains(gridPos))
                        _knownFoodPositions.Add(gridPos);

                    // Simply reverse velocity
                    _velocity = -_velocity;
                }
                break;

            case Environment.CellType.Home:
                if (CarryingFood)
                {
                    // Drop off food
                    CarryingFood = false;

                    // When reaching home, deposit a stronger home pheromone to mark it
                    if (DepositPheromones)
                    {
                        _pheromoneMap.AddPheromone(gridPos, PheromoneMap.PheromoneType.Home, PheromoneAmount * 2.0f);
                        if (IsSelected)
                            GD.Print("Dropped off food at home! Deposited strong HOME pheromone");
                    }

                    // Simply reverse velocity
                    _velocity = -_velocity;
                }
                break;

            case Environment.CellType.Wall:
                // Simple wall avoidance
                _velocity = -_velocity + new Vector2(GD.Randf() - 0.5f, GD.Randf() - 0.5f) * 0.5f;
                break;
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

    // Set references to other nodes
    public void SetReferences(Environment environment, PheromoneMap pheromoneMap)
    {
        _environment = environment;
        _pheromoneMap = pheromoneMap;
        _homePosition = _environment.GetHomePosition();
    }

    // Draw the ant and sensor visualizations
    public override void _Draw()
    {
        // Draw triangle for ant
        Vector2[] points = new Vector2[]
        {
            new Vector2(4, 0),   // Front point
            new Vector2(-2, -2), // Back left
            new Vector2(-2, 2)   // Back right
        };

        DrawColoredPolygon(points, Modulate);

        // Draw sensor visualizations if selected and following pheromones
        if (IsSelected && FollowPheromones)
        {
            // Draw sensor positions
            float sensorRadius = 2.0f;

            // Draw circles at sensor positions in local space
            Vector2 leftPos = _leftSensor.Position;
            Vector2 centerPos = _centerSensor.Position;
            Vector2 rightPos = _rightSensor.Position;

            DrawCircle(leftPos, sensorRadius, _sensorColor);
            DrawCircle(centerPos, sensorRadius, _sensorColor);
            DrawCircle(rightPos, sensorRadius, _sensorColor);

            // Draw lines from ant to sensors
            DrawLine(Vector2.Zero, leftPos, _sensorColor, 1.0f);
            DrawLine(Vector2.Zero, centerPos, _sensorColor, 1.0f);
            DrawLine(Vector2.Zero, rightPos, _sensorColor, 1.0f);
        }
    }

    // Process
    public override void _Process(double delta)
    {
        // Toggle pheromone following with F key when selected
        if (IsSelected && Input.IsKeyPressed(Key.F))
        {
            FollowPheromones = !FollowPheromones;
            GD.Print($"Pheromone following: {(FollowPheromones ? "ON" : "OFF")}");

            QueueRedraw(); // Force redraw to update sensor visualization
        }

        // Toggle pheromone deposition with D key when selected
        if (IsSelected && Input.IsKeyPressed(Key.D))
        {
            DepositPheromones = !DepositPheromones;
            GD.Print($"Pheromone deposition: {(DepositPheromones ? "ON" : "OFF")}");
        }

        // Toggle direct navigation distance with N key when selected
        if (IsSelected && Input.IsKeyPressed(Key.N))
        {
            DirectNavigationDistance = (DirectNavigationDistance == 1) ? 2 : 1;
            GD.Print($"Direct navigation distance: {DirectNavigationDistance} cells");
        }

        // Toggle gradient following with G key when selected
        if (IsSelected && Input.IsKeyPressed(Key.G))
        {
            FollowGradient = !FollowGradient;
            GD.Print($"Following gradient: {(FollowGradient ? "ON" : "OFF")}");
        }
    }

    // Simple class to hold sensor readings
    private class SensorReadings
    {
        public float centerValue;
        public float leftValue;
        public float rightValue;
    }
}