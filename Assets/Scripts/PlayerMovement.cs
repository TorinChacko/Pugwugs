using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 6f;
    public float airControl = 0.8f;
    public float jumpForce = 12f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public Vector2 groundBoxSize = new Vector2(0.6f, 0.1f);
    public LayerMask groundLayer;

    [Header("Coyote + Buffer")]
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;

    private float coyoteTimer;
    private float jumpBufferTimer;

    [Header("Dash")]
    public float dashForce = 15f;
    public float dashTime = 0.2f;

    public float dashBufferTime = 0.15f;
    private float dashBufferTimer;

    private Vector2 dashVelocity;

    [Header("BackDash")]
    public float backDashForce = 18f;
    public float backDashTime = 0.15f;

    private bool canDash = true;
    private bool isDashing;
    private bool isBackDashing;
    private float dashTimer;

    private float originalGravity;

    [Header("Wall")]
    public LayerMask wallLayer;
    public float wallSlideSpeed = 2f;
    public Vector2 wallJumpDir = new Vector2(1.2f, 1f);

    private bool isTouchingWall;
    private bool isWallSliding;
    private int wallDir;

    [Header("Camera Feel")]
    public Transform cam;
    public float cameraLerp = 8f;

    private Rigidbody2D rb;
    private InputSystem_Actions input;
    private Vector2 moveInput;

    [Header("Visuals")]
    private SpriteRenderer sr;

    [SerializeField] private Animator animator;

    private float facingDir = 1f;
    private Vector2 lastMoveDir = Vector2.right;

    private Vector2 dashDir;
    private int dashType;

    // DASH TRAIL
    public GameObject dashGhostPrefab;
    public float ghostSpawnRate = 0.05f;
    private float ghostTimer;

    // =========================
    // ATTACK SYSTEM
    // =========================
    [Header("Attack")]
    public float comboResetTime = 0.6f;

    private int comboStep = 0;
    private float comboTimer;
    private bool isAttacking;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        input = new InputSystem_Actions();

        rb.freezeRotation = true;

        sr = GetComponent<SpriteRenderer>();

        originalGravity = rb.gravityScale;

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        input.Enable();

        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        input.Player.Jump.performed += ctx => jumpBufferTimer = jumpBufferTime;

        input.Player.Dash.performed += ctx => dashBufferTimer = dashBufferTime;

        input.Player.BackDash.performed += ctx => TryBackDash();

        input.Player.Attack.performed += ctx => TryAttack();
    }

    void OnDisable()
    {
        input.Disable();
    }

    void Update()
    {
        UpdateTimers();
        HandleJump();
        HandleWallSlide();
        HandleDash();
        UpdateCameraFeel();

        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0)
            comboStep = 0;

        if (dashBufferTimer > 0 && canDash && !isDashing)
        {
            TryDash();
            dashBufferTimer = 0;
        }

        if (isDashing)
        {
            ghostTimer -= Time.deltaTime;

            if (ghostTimer <= 0f)
            {
                SpawnGhost();
                ghostTimer = ghostSpawnRate;
            }
        }

        if (moveInput != Vector2.zero)
        {
            lastMoveDir = moveInput.normalized;
            facingDir = Mathf.Sign(moveInput.x);
        }

        // Flip
        if (sr != null)
        {
            if (isDashing)
            {
                if (!isBackDashing && dashDir.x != 0)
                    sr.flipX = dashDir.x > 0;
            }
            else if (moveInput.x != 0)
            {
                sr.flipX = moveInput.x > 0;
            }
        }

        // Animator
        if (animator != null)
        {
            float yVel = isDashing ? dashDir.y * dashForce : rb.linearVelocity.y;

            animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
            animator.SetBool("isWallSliding", isWallSliding);
            animator.SetBool("isGrounded", IsGrounded());
            animator.SetFloat("yVelocity", yVel);
        }
    }

    void FixedUpdate()
    {
        if (isDashing)
        {
            rb.gravityScale = 0f; // 🔥 GRAVITY FULLY SUSPENDED HERE
            rb.linearVelocity = dashVelocity;
        }
        else
        {
            rb.gravityScale = originalGravity;
            Move();
        }
    }

    // =========================
    // ATTACK LOGIC
    // =========================
    void TryAttack()
    {
        if (isDashing) return;

        comboTimer = comboResetTime;
        isAttacking = true;

        comboStep++;
        if (comboStep > 3)
            comboStep = 1;

        animator?.SetInteger("ComboStep", comboStep);
        animator?.SetTrigger("Attack");

        Invoke(nameof(EndAttack), 0.2f);
    }

    void EndAttack()
    {
        isAttacking = false;
    }

    // =========================
    // MOVEMENT
    // =========================
    void Move()
    {
        float control = IsGrounded() ? 1f : airControl;
        rb.linearVelocity = new Vector2(moveInput.x * speed * control, rb.linearVelocity.y);
    }

    void UpdateTimers()
    {
        if (IsGrounded())
        {
            coyoteTimer = coyoteTime;
            canDash = true;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        jumpBufferTimer -= Time.deltaTime;
        dashBufferTimer -= Time.deltaTime;
    }

    void HandleJump()
    {
        if (jumpBufferTimer > 0 && coyoteTimer > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

            jumpBufferTimer = 0;
            coyoteTimer = 0;
        }
        else if (isWallSliding && jumpBufferTimer > 0)
        {
            int dir = wallDir;

            rb.linearVelocity = new Vector2(
                -dir * wallJumpDir.x,
                wallJumpDir.y
            );

            jumpBufferTimer = 0;
        }
    }

    // =========================
    // DASH
    // =========================
    void TryDash()
    {
        if (!canDash || isDashing) return;

        isDashing = true;
        isBackDashing = false;
        canDash = false;
        dashTimer = dashTime;

        Vector2 dir = lastMoveDir;

        if (dir == Vector2.zero)
            dir = new Vector2(facingDir, 0f);

        dashDir = dir.normalized;
        dashVelocity = dashDir * dashForce;

        rb.linearVelocity = dashVelocity;

        if (Mathf.Abs(dashDir.y) > 0.6f && Mathf.Abs(dashDir.x) < 0.6f)
            dashType = 1;
        else if (Mathf.Abs(dashDir.x) > 0.6f && Mathf.Abs(dashDir.y) < 0.6f)
            dashType = 0;
        else
            dashType = 2;

        animator?.SetInteger("DashType", dashType);
        animator?.SetTrigger("Dash");

        StartCoroutine(DashFreeze());
    }

    void TryBackDash()
    {
        if (!canDash || isDashing || !IsGrounded()) return;

        isDashing = true;
        isBackDashing = true;
        canDash = false;
        dashTimer = backDashTime;

        float dir = -facingDir;

        dashDir = new Vector2(dir, 0f);
        dashVelocity = dashDir * backDashForce;

        rb.linearVelocity = dashVelocity;

        animator?.SetTrigger("BackDash");

        StartCoroutine(DashFreeze());
    }

    void HandleDash()
    {
        if (!isDashing) return;

        dashTimer -= Time.deltaTime;

        if (dashTimer <= 0)
        {
            isDashing = false;
            isBackDashing = false;

            rb.gravityScale = originalGravity;

            if (!IsGrounded() && rb.linearVelocity.y > -2f)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y);
        }
    }

    // =========================
    // WALL
    // =========================
    void HandleWallSlide()
    {
        bool right = Physics2D.Raycast(transform.position, Vector2.right, 0.6f, wallLayer);
        bool left = Physics2D.Raycast(transform.position, Vector2.left, 0.6f, wallLayer);

        isTouchingWall = right || left;
        wallDir = right ? 1 : left ? -1 : 0;

        isWallSliding = isTouchingWall && !IsGrounded() && rb.linearVelocity.y < 0;
    }

    // =========================
    // CAMERA
    // =========================
    void UpdateCameraFeel()
    {
        if (cam == null) return;

        cam.position = Vector3.Lerp(cam.position, transform.position, cameraLerp * Time.deltaTime);
    }

    bool IsGrounded()
    {
        return Physics2D.OverlapBox(
            groundCheck.position,
            groundBoxSize,
            0f,
            groundLayer
        );
    }

    // =========================
    // EFFECTS
    // =========================
    IEnumerator DashFreeze()
    {
        float originalTime = Time.timeScale;

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(0f);
        Time.timeScale = originalTime;
    }

    void SpawnGhost()
    {
        GameObject ghost = Instantiate(dashGhostPrefab, transform.position, transform.rotation);

        SpriteRenderer ghostSR = ghost.GetComponent<SpriteRenderer>();
        ghostSR.sprite = sr.sprite;
        ghostSR.flipX = sr.flipX;
    }
}