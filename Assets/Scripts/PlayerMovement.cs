using UnityEngine;
using UnityEngine.InputSystem;

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

        // Dash buffer execution
        if (dashBufferTimer > 0 && canDash && !isDashing)
        {
            TryDash();
            dashBufferTimer = 0;
        }

        if (moveInput != Vector2.zero)
        {
            lastMoveDir = moveInput.normalized;
            facingDir = Mathf.Sign(moveInput.x);
        }

        // Visual flip
        if (sr != null)
        {
            if (isDashing)
            {
                if (!isBackDashing)
                {
                    if (dashDir.x != 0)
                        sr.flipX = dashDir.x > 0;
                }
            }
            else
            {
                if (moveInput.x != 0)
                    sr.flipX = moveInput.x > 0;
            }
        }

        // Animation
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
            rb.linearVelocity = dashVelocity;
        }
        else
        {
            Move();
        }
    }

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

        dir = dir.normalized;

        dashDir = dir;
        dashVelocity = dir * dashForce;

        rb.gravityScale = 0f;
        rb.linearVelocity = dashVelocity;

        if (Mathf.Abs(dir.y) > 0.6f && Mathf.Abs(dir.x) < 0.6f)
            dashType = 1;
        else if (Mathf.Abs(dir.x) > 0.6f && Mathf.Abs(dir.y) < 0.6f)
            dashType = 0;
        else
            dashType = 2;

        if (animator != null)
        {
            animator.SetInteger("DashType", dashType);
            animator.SetTrigger("Dash");
        }
    }

    void TryBackDash()
    {
        if (!canDash || isDashing) return;

        isDashing = true;
        isBackDashing = true;
        canDash = false;
        dashTimer = backDashTime;

        float dir = -facingDir;

        dashDir = new Vector2(dir, 0f);
        dashVelocity = new Vector2(dir * backDashForce, 0f);

        rb.gravityScale = 0f;
        rb.linearVelocity = dashVelocity;

        if (animator != null)
        {
            animator.SetTrigger("BackDash");
        }
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

            // Prevent floaty feeling
            if (!IsGrounded() && rb.linearVelocity.y > -2f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -2f);
            }
        }
    }

    void HandleWallSlide()
    {
        bool right = Physics2D.Raycast(transform.position, Vector2.right, 0.6f, wallLayer);
        bool left = Physics2D.Raycast(transform.position, Vector2.left, 0.6f, wallLayer);

        isTouchingWall = right || left;
        wallDir = right ? 1 : left ? -1 : 0;

        if (isTouchingWall && !IsGrounded() && rb.linearVelocity.y < 0)
        {
            isWallSliding = true;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideSpeed);
        }
        else
        {
            isWallSliding = false;
        }
    }

    void UpdateCameraFeel()
    {
        if (cam == null) return;

        Vector3 target = transform.position;
        cam.position = Vector3.Lerp(cam.position, target, cameraLerp * Time.deltaTime);
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
}