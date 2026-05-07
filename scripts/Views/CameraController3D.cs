#nullable enable

using Godot;
using Maze.Model;

namespace Maze.Views;

/// <summary>
/// Frei steuerbare 3D-Kamera fuer die Maze-Ansicht.
/// Bewegung: WASD horizontal in Blickrichtung, QE vertikal in Welt-Y,
/// Shift verdoppelt die Geschwindigkeit. Drehung: RMB + Maus oder Pfeiltasten.
/// Zoom: Mausrad als Dolly entlang der Blickrichtung.
/// </summary>
public partial class CameraController3D : Camera3D
{
    [Export] public float MoveSpeed = 8f;
    [Export] public float SprintMultiplier = 2f;
    [Export] public float MouseSensitivity = 0.003f;
    [Export] public float KeyTurnSpeed = 1.5f;
    [Export] public float ZoomStep = 1.5f;
    [Export] public float ZoomSprintMultiplier = 3f;
    [Export] public float FollowDistance = 4.5f;
    [Export] public float FollowHeight = 3.0f;
    [Export] public float FollowSmoothing = 6.0f;
    [Export] public float FirstPersonForwardOffset = 0.02f;
    [Export] public float FirstPersonSmoothing = 12.0f;

    private float _yaw;
    private float _pitch;
    private bool _mouseLook;
    private Node3D? _followTarget;
    private PlayerCharacter3D? _firstPersonTarget;
    private float _followOrbitYaw;
    private float _followOrbitPitch;
    private float _followOrbitRadius;
    private Vector3 _externalShakeOffset;
    private Vector3 _appliedShakeOffset;

    public bool FollowMode { get; private set; }
    public bool FirstPersonMode { get; private set; }

    public override void _Ready()
    {
        ProcessPriority = 10;
        Vector3 euler = Basis.GetEuler();
        _pitch = euler.X;
        _yaw = euler.Y;
    }

    public override void _Process(double delta)
    {
        RemoveAppliedShakeOffset();

        if (!IsVisibleInTree())
        {
            return;
        }

        if (FollowMode && _followTarget is not null)
        {
            UpdateFollowCamera(delta);
            ApplyExternalShakeOffset();
            return;
        }

        if (FirstPersonMode && _firstPersonTarget is not null)
        {
            HandleKeyboardLook(delta);
            UpdateFirstPersonCamera(delta);
            ApplyExternalShakeOffset();
            return;
        }

        HandleMovement(delta);
        HandleKeyboardLook(delta);
        ApplyRotation();
        ApplyExternalShakeOffset();
    }

    private void HandleMovement(double delta)
    {
        Vector2 input = GetMoveInput();

        Vector3 worldVertical = Vector3.Zero;
        if (Input.IsPhysicalKeyPressed(Key.E)) worldVertical += Vector3.Up;
        if (Input.IsPhysicalKeyPressed(Key.Q)) worldVertical += Vector3.Down;

        if (input == Vector2.Zero && worldVertical == Vector3.Zero)
        {
            return;
        }

        float speed = MoveSpeed;
        if (Input.IsPhysicalKeyPressed(Key.Shift))
        {
            speed *= SprintMultiplier;
        }

        if (input != Vector2.Zero)
        {
            Vector3 moveDirection = GetFreeMoveDirection(input);

            if (moveDirection != Vector3.Zero)
            {
                GlobalPosition += moveDirection * speed * (float)delta;
            }
        }

        if (worldVertical != Vector3.Zero)
        {
            Position += worldVertical.Normalized() * speed * (float)delta;
        }
    }

    private void HandleKeyboardLook(double delta)
    {
        float yawDelta = 0f;
        float pitchDelta = 0f;
        if (Input.IsPhysicalKeyPressed(Key.Left)) yawDelta += KeyTurnSpeed * (float)delta;
        if (Input.IsPhysicalKeyPressed(Key.Right)) yawDelta -= KeyTurnSpeed * (float)delta;
        if (Input.IsPhysicalKeyPressed(Key.Up)) pitchDelta += KeyTurnSpeed * (float)delta;
        if (Input.IsPhysicalKeyPressed(Key.Down)) pitchDelta -= KeyTurnSpeed * (float)delta;

        _yaw += yawDelta;
        _pitch = Mathf.Clamp(_pitch + pitchDelta, -1.4f, 1.4f);
    }

    private void ApplyRotation()
    {
        Basis = Basis.FromEuler(new Vector3(_pitch, _yaw, 0f));
    }

    public Direction GetFacingDirectionForInput()
    {
        Vector2 input = GetMoveInput();

        if (input == Vector2.Zero)
        {
            return Direction.North;
        }

        Vector3 desired = GetGroundMovementDirection(input);
        if (desired == Vector3.Zero)
        {
            return Direction.North;
        }

        return WorldVectorToDirection(desired);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsVisibleInTree())
        {
            return;
        }

        if (FirstPersonMode)
        {
            HandleFirstPersonInput(@event);
            return;
        }

        if (FollowMode)
        {
            HandleFollowInput(@event);
            return;
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                _mouseLook = mouseButton.Pressed;
                Input.MouseMode = mouseButton.Pressed ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
                return;
            }

            if (mouseButton.Pressed && (mouseButton.ButtonIndex == MouseButton.WheelUp || mouseButton.ButtonIndex == MouseButton.WheelDown))
            {
                float step = ZoomStep;
                if (Input.IsPhysicalKeyPressed(Key.Shift))
                {
                    step *= ZoomSprintMultiplier;
                }

                Vector3 direction = mouseButton.ButtonIndex == MouseButton.WheelUp ? Vector3.Forward : Vector3.Back;
                Translate(direction * step);
                return;
            }
        }

        if (@event is InputEventMouseMotion motion && _mouseLook)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -1.4f, 1.4f);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut && _mouseLook)
        {
            _mouseLook = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public void FitToMaze(global::Maze.Model.Maze maze)
    {
        float width = maze.Width;
        float height = maze.Height;
        float centerX = width / 2f;
        float centerZ = height / 2f;
        float fitHeight = Mathf.Max(width, height) * 0.8f;

        Position = new Vector3(centerX, fitHeight, centerZ + fitHeight * 0.7f);
        LookAt(new Vector3(centerX, 0f, centerZ), Vector3.Up);

        Vector3 euler = Basis.GetEuler();
        _pitch = euler.X;
        _yaw = euler.Y;
    }

    public void EnableFollow(Node3D target, bool snapImmediately = false)
    {
        DisableFirstPerson();
        _followTarget = target;
        FollowMode = true;

        Vector3 offset = GlobalPosition - target.GlobalPosition;
        if (offset.LengthSquared() > 0.0001f)
        {
            _followOrbitRadius = offset.Length();
            float horizontalDistance = new Vector2(offset.X, offset.Z).Length();
            _followOrbitPitch = Mathf.Clamp(Mathf.Atan2(offset.Y, horizontalDistance), 0.05f, Mathf.Pi / 2f - 0.05f);
            _followOrbitYaw = Mathf.Atan2(offset.X, offset.Z);
        }
        else
        {
            _followOrbitRadius = Mathf.Sqrt(FollowHeight * FollowHeight + FollowDistance * FollowDistance);
            _followOrbitPitch = Mathf.Atan2(FollowHeight, FollowDistance);
            _followOrbitYaw = 0f;
        }

        if (snapImmediately)
        {
            SnapFollowToTarget();
        }

        if (_mouseLook)
        {
            _mouseLook = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public void DisableFollow()
    {
        _followTarget = null;
        FollowMode = false;

        if (_mouseLook)
        {
            _mouseLook = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public void EnableFirstPerson(PlayerCharacter3D target, bool snapImmediately = false)
    {
        DisableFollow();
        _firstPersonTarget = target;
        FirstPersonMode = true;

        if (target.CurrentMode != PlayerCharacter3D.Mode.Manual)
        {
            _yaw = target.GlobalRotation.Y;
            _pitch = 0f;
        }

        if (snapImmediately)
        {
            SnapFirstPersonToTarget();
        }

        Input.MouseMode = Input.MouseModeEnum.Captured;
        _mouseLook = false;
    }

    public void DisableFirstPerson()
    {
        _firstPersonTarget = null;
        FirstPersonMode = false;

        if (Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }

        _mouseLook = false;
    }

    public void SetFieldOfView(float fieldOfView)
    {
        Fov = Mathf.Clamp(fieldOfView, 55f, 100f);
    }

    public void SetExternalShakeOffset(Vector3 offset)
    {
        _externalShakeOffset = offset;
    }

    private void UpdateFollowCamera(double delta)
    {
        if (_followTarget is null)
        {
            return;
        }

        if (_followTarget is PlayerCharacter3D player && !player.IsMoving)
        {
            SnapFollowToTarget();
            return;
        }

        Vector3 targetPos = _followTarget.GlobalPosition;
        Vector3 orbitOffset = GetFollowOrbitOffset();

        float lerpFactor = 1f - Mathf.Exp(-FollowSmoothing * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(targetPos + orbitOffset, lerpFactor);
        LookAt(targetPos + new Vector3(0f, 0.3f, 0f), Vector3.Up);

        Vector3 euler = Basis.GetEuler();
        _pitch = euler.X;
        _yaw = euler.Y;
    }

    private void UpdateFirstPersonCamera(double delta)
    {
        if (_firstPersonTarget is null)
        {
            return;
        }

        if (_firstPersonTarget.CurrentMode != PlayerCharacter3D.Mode.Manual)
        {
            _yaw = _firstPersonTarget.GlobalRotation.Y;
            _pitch = 0f;
        }

        Basis desiredBasis = Basis.FromEuler(new Vector3(_pitch, _yaw, 0f));
        Vector3 forward = -desiredBasis.Z;
        Vector3 targetPosition = _firstPersonTarget.GetEyeWorldPosition() + forward * FirstPersonForwardOffset;

        float lerpFactor = 1f - Mathf.Exp(-FirstPersonSmoothing * (float)delta);
        GlobalPosition = GlobalPosition.Lerp(targetPosition, lerpFactor);
        Basis = desiredBasis;
    }

    private void HandleFollowInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                _mouseLook = mouseButton.Pressed;
                Input.MouseMode = mouseButton.Pressed ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
                return;
            }

            if (mouseButton.Pressed && (mouseButton.ButtonIndex == MouseButton.WheelUp || mouseButton.ButtonIndex == MouseButton.WheelDown))
            {
                float step = ZoomStep;
                if (Input.IsPhysicalKeyPressed(Key.Shift))
                {
                    step *= ZoomSprintMultiplier;
                }

                _followOrbitRadius = Mathf.Clamp(
                    _followOrbitRadius + (mouseButton.ButtonIndex == MouseButton.WheelUp ? -step : step),
                    1f,
                    200f);
            }

            return;
        }

        if (@event is InputEventMouseMotion motion && _mouseLook)
        {
            _followOrbitYaw -= motion.Relative.X * MouseSensitivity;
            _followOrbitPitch = Mathf.Clamp(_followOrbitPitch + motion.Relative.Y * MouseSensitivity, 0.05f, Mathf.Pi / 2f - 0.05f);
        }
    }

    private void HandleFirstPersonInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -1.2f, 1.2f);
            return;
        }

        if (@event is InputEventMouseButton mouseButton
            && mouseButton.Pressed
            && mouseButton.ButtonIndex == MouseButton.Left
            && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    private void SnapFollowToTarget()
    {
        if (_followTarget is null)
        {
            return;
        }

        Vector3 targetPos = _followTarget.GlobalPosition;
        GlobalPosition = targetPos + GetFollowOrbitOffset();
        LookAt(targetPos + new Vector3(0f, 0.3f, 0f), Vector3.Up);

        Vector3 euler = Basis.GetEuler();
        _pitch = euler.X;
        _yaw = euler.Y;
    }

    private void SnapFirstPersonToTarget()
    {
        if (_firstPersonTarget is null)
        {
            return;
        }

        if (_firstPersonTarget.CurrentMode != PlayerCharacter3D.Mode.Manual)
        {
            _yaw = _firstPersonTarget.GlobalRotation.Y;
            _pitch = 0f;
        }

        Basis desiredBasis = Basis.FromEuler(new Vector3(_pitch, _yaw, 0f));
        Vector3 forward = -desiredBasis.Z;
        GlobalPosition = _firstPersonTarget.GetEyeWorldPosition() + forward * FirstPersonForwardOffset;
        Basis = desiredBasis;
    }

    private Vector3 GetFollowOrbitOffset()
    {
        float cosPitch = Mathf.Cos(_followOrbitPitch);
        float sinPitch = Mathf.Sin(_followOrbitPitch);
        return new Vector3(
            Mathf.Sin(_followOrbitYaw) * cosPitch,
            sinPitch,
            Mathf.Cos(_followOrbitYaw) * cosPitch) * _followOrbitRadius;
    }

    private void RemoveAppliedShakeOffset()
    {
        if (_appliedShakeOffset == Vector3.Zero)
        {
            return;
        }

        GlobalPosition -= _appliedShakeOffset;
        _appliedShakeOffset = Vector3.Zero;
    }

    private void ApplyExternalShakeOffset()
    {
        if (_externalShakeOffset == Vector3.Zero)
        {
            return;
        }

        GlobalPosition += _externalShakeOffset;
        _appliedShakeOffset = _externalShakeOffset;
    }

    private Vector2 GetMoveInput()
    {
        Vector2 input = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W)) input.Y += 1f;
        if (Input.IsPhysicalKeyPressed(Key.S)) input.Y -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.A)) input.X -= 1f;
        if (Input.IsPhysicalKeyPressed(Key.D)) input.X += 1f;
        return input;
    }

    private Vector3 GetGroundMovementDirection(Vector2 input)
    {
        if (input == Vector2.Zero)
        {
            return Vector3.Zero;
        }

        if (TryGetScreenGroundAxes(out Vector3 groundRight, out Vector3 groundDown))
        {
            Vector3 projectedDirection = (groundRight * input.X - groundDown * input.Y).Normalized();
            if (projectedDirection != Vector3.Zero)
            {
                return projectedDirection;
            }
        }

        Vector3 fallbackForward = FlattenToGround(-GlobalBasis.Z);
        Vector3 fallbackRight = FlattenToGround(GlobalBasis.X);
        return (fallbackRight * input.X + fallbackForward * input.Y).Normalized();
    }

    private Vector3 GetFreeMoveDirection(Vector2 input)
    {
        if (input == Vector2.Zero)
        {
            return Vector3.Zero;
        }

        Vector3 strafeRight = FlattenToGround(GlobalBasis.X);
        Vector3 forward = -GlobalBasis.Z;
        return (strafeRight * input.X + forward * input.Y).Normalized();
    }

    private bool TryGetScreenGroundAxes(out Vector3 groundRight, out Vector3 groundDown)
    {
        groundRight = Vector3.Zero;
        groundDown = Vector3.Zero;

        Viewport? viewport = GetViewport();
        if (viewport is null)
        {
            return false;
        }

        Vector2 viewportSize = viewport.GetVisibleRect().Size;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f)
        {
            return false;
        }

        Vector2 center = viewportSize / 2f;
        float sampleX = viewportSize.X * 0.2f;
        float sampleY = viewportSize.Y * 0.2f;

        if (!TryProjectToGround(center, out Vector3 centerPoint)
            || !TryProjectToGround(center + new Vector2(sampleX, 0f), out Vector3 rightPoint)
            || !TryProjectToGround(center + new Vector2(0f, sampleY), out Vector3 downPoint))
        {
            return false;
        }

        groundRight = FlattenToGround(rightPoint - centerPoint);
        groundDown = FlattenToGround(downPoint - centerPoint);
        return groundRight != Vector3.Zero && groundDown != Vector3.Zero;
    }

    private bool TryProjectToGround(Vector2 screenPosition, out Vector3 worldPoint)
    {
        Vector3 rayOrigin = ProjectRayOrigin(screenPosition);
        Vector3 rayDirection = ProjectRayNormal(screenPosition);

        if (Mathf.Abs(rayDirection.Y) < 0.0001f)
        {
            worldPoint = Vector3.Zero;
            return false;
        }

        float distance = -rayOrigin.Y / rayDirection.Y;
        if (distance <= 0f)
        {
            worldPoint = Vector3.Zero;
            return false;
        }

        worldPoint = rayOrigin + rayDirection * distance;
        return true;
    }

    private static Vector3 FlattenToGround(Vector3 vector)
    {
        Vector3 flattened = new(vector.X, 0f, vector.Z);
        return flattened.LengthSquared() > 0.0001f ? flattened.Normalized() : Vector3.Zero;
    }

    private static Direction WorldVectorToDirection(Vector3 direction)
    {
        if (Mathf.Abs(direction.X) >= Mathf.Abs(direction.Z))
        {
            return direction.X >= 0f ? Direction.East : Direction.West;
        }

        return direction.Z >= 0f ? Direction.South : Direction.North;
    }
}