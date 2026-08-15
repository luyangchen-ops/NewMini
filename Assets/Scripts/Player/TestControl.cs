using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class TestControl : MonoBehaviour
{
    public static float EnemyTimeScale { get; private set; } = 1f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float boundaryPadding = 0.7f;

    [Header("Dash")]
    [SerializeField] private Camera worldCamera;
    [Tooltip("Maximum length of the aiming arrow.")]
    [SerializeField, Min(0.01f)] private float maximumDragDistance = 3f;
    [Header("Space Dodge")]
    [SerializeField, Min(0f)] private float dodgeDistance = 2.5f;
    [SerializeField, Min(0.01f)] private float dodgeDuration = 0.25f;
    [Tooltip("A perfect dodge triggers when a projectile is closer than this percentage of the dodge distance.")]
    [SerializeField, Range(0f, 1f)] private float perfectDodgeDistanceRatio = 0.3f;
    [SerializeField, Min(0f)] private float dashCooldown = 1f;

    [Header("Bullet Time Attack")]
    [SerializeField, Min(0f)] private float attackDashDistance = 5f;
    [SerializeField, Min(0.01f)] private float attackDashDuration = 0.18f;
    [SerializeField, Range(0.01f, 1f)] private float bulletTimeEnemyScale = 0f;

    [Header("Enemy Hit")]
    [Tooltip("Matches Enemy and Unity-style duplicate names such as Enemy (1).")]
    [SerializeField] private string enemyNamePrefix = "Enemy";

    [Header("Arrow (authored in the scene)")]
    [Tooltip("Root object containing the complete arrow hierarchy.")]
    [SerializeField] private GameObject arrowRoot;
    [Tooltip("A two-point LineRenderer used as the arrow shaft.")]
    [SerializeField] private LineRenderer arrowLine;
    [Tooltip("Arrow head whose local +X axis points forwards.")]
    [SerializeField] private Transform arrowHead;

    private Rigidbody2D body;
    private Vector2 moveInput;
    private Vector2 dragDirection;
    private float dragDistance;
    private bool isKillChainTargeting;

    private bool isDashing;
    private bool dashCanKill;
    private Vector2 dashStart;
    private Vector2 dashTarget;
    private float dashElapsed;
    private float activeDashDuration;
    private float dashReadyTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        SetArrowVisible(false);
    }

    private void Update()
    {
        ReadMovementInput();
        HandleDashInput();
        HandlePointerInput();
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            CheckPerfectDodgeDistance();
            if (!isDashing)
            {
                return;
            }

            UpdateDash();
            return;
        }

        Vector2 nextPosition = body.position;
        if (!isKillChainTargeting && moveInput.sqrMagnitude > 0f)
        {
            nextPosition += moveInput * (moveSpeed * Time.fixedDeltaTime);
        }

        Vector2 clampedPosition = ClampToBoundary(nextPosition);
        if ((clampedPosition - body.position).sqrMagnitude > 0.000001f
            || nextPosition != body.position)
        {
            body.MovePosition(clampedPosition);
        }
    }

    private void ReadMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        float horizontal = (keyboard.dKey.isPressed ? 1f : 0f)
                         - (keyboard.aKey.isPressed ? 1f : 0f);
        float vertical = (keyboard.wKey.isPressed ? 1f : 0f)
                       - (keyboard.sKey.isPressed ? 1f : 0f);

        moveInput = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    private void HandleDashInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null
            || isKillChainTargeting
            || !keyboard.spaceKey.wasPressedThisFrame
            || !CanDash())
        {
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null || worldCamera == null)
        {
            return;
        }

        Vector2 dodgeVector = ScreenToWorld(pointer.position.ReadValue()) - body.position;
        if (dodgeVector.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        StartDash(dodgeVector.normalized, dodgeDistance, dodgeDuration, false);
    }

    private void HandlePointerInput()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null || worldCamera == null)
        {
            return;
        }

        if (isKillChainTargeting)
        {
            HandleKillChainPointerInput(pointer);
        }
    }

    private void HandleKillChainPointerInput(Pointer pointer)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            ExitKillChainTargeting();
            return;
        }

        Vector2 pointerWorldPosition = ScreenToWorld(pointer.position.ReadValue());
        UpdateKillChainAim(pointerWorldPosition);

        if (pointer.press.wasPressedThisFrame)
        {
            StartKillChainDash(pointerWorldPosition);
        }
    }

    private void UpdateKillChainAim(Vector2 pointerWorldPosition)
    {
        Vector2 aimVector = pointerWorldPosition - body.position;
        float aimDistance = aimVector.magnitude;

        dragDirection = aimDistance > Mathf.Epsilon
            ? aimVector / aimDistance
            : Vector2.zero;
        dragDistance = Mathf.Min(aimDistance, maximumDragDistance);

        UpdateArrow();
    }

    private void StartKillChainDash(Vector2 pointerWorldPosition)
    {
        Vector2 dashVector = pointerWorldPosition - body.position;
        if (dashVector.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Vector2 direction = dashVector.normalized;
        ExitKillChainTargeting();
        StartDash(direction, attackDashDistance, attackDashDuration, true);
    }

    private void StartDash(Vector2 direction, float distance, float duration, bool canKill)
    {
        dashStart = body.position;
        dashTarget = ClampToBoundary(dashStart + direction * distance);
        dashElapsed = 0f;
        activeDashDuration = Mathf.Max(0.01f, duration);
        dashCanKill = canKill;
        SetDashState(true);

        if (!canKill)
        {
            dashReadyTime = Time.time + dashCooldown;
        }
    }

    private void UpdateDash()
    {
        dashElapsed += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(dashElapsed / activeDashDuration);
        float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);

        body.MovePosition(Vector2.LerpUnclamped(dashStart, dashTarget, easedProgress));

        if (progress >= 1f)
        {
            SetDashState(false);
        }
    }

    private void SetDashState(bool dashing)
    {
        isDashing = dashing;
        if (!dashing)
        {
            dashCanKill = false;
        }
    }

    private void EnterKillChainTargeting()
    {
        isKillChainTargeting = true;
        RefreshEnemyTimeScale();
        SetArrowVisible(true);

        Pointer pointer = Pointer.current;
        if (pointer != null && worldCamera != null)
        {
            UpdateKillChainAim(ScreenToWorld(pointer.position.ReadValue()));
        }
    }

    private void ExitKillChainTargeting()
    {
        isKillChainTargeting = false;
        RefreshEnemyTimeScale();
        SetArrowVisible(false);
    }

    private void RefreshEnemyTimeScale()
    {
        EnemyTimeScale = isKillChainTargeting
            ? bulletTimeEnemyScale
            : 1f;
    }

    private bool CanDash()
    {
        return !isDashing && Time.time >= dashReadyTime;
    }

    public bool IsDodging => isDashing && !dashCanKill;

    public bool TryTriggerPerfectDodge(Vector2 projectilePosition)
    {
        if (!IsDodging)
        {
            return false;
        }

        float perfectDodgeDistance = dodgeDistance * perfectDodgeDistanceRatio;
        if (((Vector2)body.position - projectilePosition).sqrMagnitude
            >= perfectDodgeDistance * perfectDodgeDistance)
        {
            return false;
        }

        SetDashState(false);
        body.linearVelocity = Vector2.zero;
        EnterKillChainTargeting();
        return true;
    }

    private void CheckPerfectDodgeDistance()
    {
        if (!IsDodging)
        {
            return;
        }

        float perfectDodgeDistance = dodgeDistance * perfectDodgeDistanceRatio;
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(body.position, perfectDodgeDistance);
        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            EnemyProjectile projectile = nearbyCollider.GetComponentInParent<EnemyProjectile>();
            if (projectile != null && TryTriggerPerfectDodge(projectile.transform.position))
            {
                projectile.IgnorePlayerCollisions(this);
                return;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHitEnemy(collision.transform);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHitEnemy(other.transform);
    }

    private void TryHitEnemy(Transform hitTransform)
    {
        if (!isDashing || !dashCanKill)
        {
            return;
        }

        Transform enemy = FindEnemyInParents(hitTransform);
        if (enemy == null)
        {
            return;
        }

        Destroy(enemy.gameObject);

        // Stop on impact and immediately enter bullet-time targeting for the next dash.
        SetDashState(false);
        dashReadyTime = 0f;
        body.linearVelocity = Vector2.zero;
        EnterKillChainTargeting();
    }

    private Transform FindEnemyInParents(Transform candidate)
    {
        while (candidate != null)
        {
            if (IsEnemyName(candidate.name))
            {
                return candidate;
            }

            candidate = candidate.parent;
        }

        return null;
    }

    private bool IsEnemyName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(enemyNamePrefix))
        {
            return false;
        }

        return objectName == enemyNamePrefix
            || (objectName.StartsWith(enemyNamePrefix + " (") && objectName.EndsWith(")"));
    }

    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        Ray ray = worldCamera.ScreenPointToRay(screenPosition);
        Plane playerPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));

        return playerPlane.Raycast(ray, out float distance)
            ? (Vector2)ray.GetPoint(distance)
            : body.position;
    }

    private Vector2 ClampToBoundary(Vector2 position)
    {
        return ExtraCameraBounds.Clamp(worldCamera, position, boundaryPadding, transform.position.z);
    }

    private void UpdateArrow()
    {
        Vector2 start = body.position;
        Vector2 end = start + dragDirection * dragDistance;

        if (arrowLine != null)
        {
            arrowLine.positionCount = 2;
            arrowLine.useWorldSpace = true;
            arrowLine.SetPosition(0, start);
            arrowLine.SetPosition(1, end);
        }

        if (arrowHead != null)
        {
            arrowHead.position = end;
            float angle = Mathf.Atan2(dragDirection.y, dragDirection.x) * Mathf.Rad2Deg;
            arrowHead.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void SetArrowVisible(bool visible)
    {
        if (arrowRoot != null)
        {
            arrowRoot.SetActive(visible);
            return;
        }

        if (arrowLine != null)
        {
            arrowLine.enabled = visible;
        }

        if (arrowHead != null)
        {
            arrowHead.gameObject.SetActive(visible);
        }
    }

    private void OnDisable()
    {
        isKillChainTargeting = false;
        RefreshEnemyTimeScale();
        SetDashState(false);
        dashReadyTime = 0f;
        moveInput = Vector2.zero;
        SetArrowVisible(false);
    }

    private void OnValidate()
    {
        maximumDragDistance = Mathf.Max(0.01f, maximumDragDistance);
        dodgeDistance = Mathf.Max(0f, dodgeDistance);
        dodgeDuration = Mathf.Max(0.01f, dodgeDuration);
        perfectDodgeDistanceRatio = Mathf.Clamp01(perfectDodgeDistanceRatio);
        attackDashDistance = Mathf.Max(0f, attackDashDistance);
        attackDashDuration = Mathf.Max(0.01f, attackDashDuration);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEnemyTimeScale()
    {
        EnemyTimeScale = 1f;
    }
}
