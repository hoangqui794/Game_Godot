using Godot;
using System;
using System.Collections.Generic;

public partial class Player3D : CharacterBody3D
{
    // Movement - Core
    [Export] public float Speed = 10.0f;
    [Export] public float Acceleration = 60.0f;
    [Export] public float Deceleration = 50.0f;
    [Export] public float AirAcceleration = 30.0f;
    [Export] public float AirDeceleration = 20.0f;
    [Export] public float ZSpeed = 5.0f; // Tốc độ di chuyển trục Z (W/S)
    [Export] public float ZMin = -4.0f;
    [Export] public float ZMax = 4.0f;

    // Jump
    [Export] public float JumpVelocity = 12.0f;
    [Export] public float Gravity = 25.0f;
    [Export] public float FallGravityMultiplier = 1.5f;
    [Export] public float JumpCutMultiplier = 0.5f;
    [Export] public float CoyoteTime = 0.15f;
    [Export] public float JumpBufferTime = 0.15f;

    // Movement state
    private float _coyoteTimer = 0f;
    private float _jumpBufferTimer = 0f;
    private bool _wasOnFloor = false;
    private bool _isJumping = false;
    private bool _hasDoubleJumped = false;
    private bool _isHoldJumping = false;
    private float _facingDirection = 1f;

    // Combat
    [Export] public int AttackDamage = 100;
    [Export] public float AttackCooldown = 0.3f;
    [Export] public float ComboResetTime = 0.6f;
    private bool _canAttack = true;
    private bool _isAttacking = false;
    private int _comboIndex = 0;
    private float _comboTimer = 0;
    private bool _comboActive = false;

    // Health
    private int _health;
    private bool _isDead = false;
    private bool _isHurt = false;
    private bool _deathSignalSent = false;
    private bool _isInvulnerable = false;

    // Components
    private AnimatedSprite3D _animatedSprite;
    private Area3D _attackArea;
    private CollisionShape3D _attackCollision;
    private Timer _attackCooldownTimer;
    private Timer _hurtTimer;

    // Audio
    private AudioStreamPlayer3D _sfxPlayer;
    private AudioStreamPlayer3D _sfxStepPlayer;
    private float _stepTimer = 0f;

    // Signals
    [Signal] public delegate void HealthChangedEventHandler(int newHealth, int maxHealth);
    [Signal] public delegate void PlayerDiedEventHandler();

    // Skill state (from PlayerSkills.cs logic if applicable)
    private bool _isSpinning = false;

    public override void _Ready()
    {
        // Use GameManager if it exists, otherwise default
        if (GameManager.Instance != null)
            _health = GameManager.Instance.PlayerHealth;
        else
            _health = 100;

        AddToGroup("player");

        // Get nodes
        _animatedSprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
        _attackArea = GetNode<Area3D>("AttackArea");
        _attackCollision = _attackArea.GetNode<CollisionShape3D>("CollisionShape3D");
        _attackCollision.Disabled = true;

        // Sprite setup - shared SpriteFrames usually work
        // Using SpriteHelper if it's compatible
        try
        {
            _animatedSprite.SpriteFrames = SpriteHelper.CreatePlayerSpriteFrames();
            _animatedSprite.Play("idle");
        }
        catch (Exception e)
        {
            GD.PrintErr("Failed to initialize SpriteFrames: " + e.Message);
        }

        // Timers
        _attackCooldownTimer = new Timer();
        _attackCooldownTimer.WaitTime = AttackCooldown;
        _attackCooldownTimer.OneShot = true;
        _attackCooldownTimer.Timeout += OnAttackCooldownTimeout;
        AddChild(_attackCooldownTimer);

        _hurtTimer = new Timer();
        _hurtTimer.WaitTime = 0.5f;
        _hurtTimer.OneShot = true;
        _hurtTimer.Timeout += OnHurtTimeout;
        AddChild(_hurtTimer);

        // Audio 3D
        _sfxPlayer = new AudioStreamPlayer3D();
        _sfxPlayer.VolumeDb = -5f;
        AddChild(_sfxPlayer);

        _sfxStepPlayer = new AudioStreamPlayer3D();
        _sfxStepPlayer.VolumeDb = -12f;
        AddChild(_sfxStepPlayer);

        // Connect signals
        _attackArea.BodyEntered += OnAttackAreaBodyEntered;
        _animatedSprite.AnimationFinished += OnAnimationFinished;

        if (GameManager.Instance != null)
            EmitSignal(SignalName.HealthChanged, _health, GameManager.Instance.MaxPlayerHealth);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead) return;

        float dt = (float)delta;
        Vector3 velocity = Velocity;
        bool onFloor = IsOnFloor();

        // Z-axis movement (W/S) for 2.5D depth
        float zDirection = 0f;
        if (Input.IsActionPressed("move_up")) zDirection -= 1f;
        if (Input.IsActionPressed("move_down")) zDirection += 1f;
        velocity.Z = zDirection * ZSpeed;

        // Coyote Time
        if (onFloor)
        {
            _coyoteTimer = CoyoteTime;
            _isJumping = false;
            _hasDoubleJumped = false;
        }
        else
        {
            _coyoteTimer -= dt;
        }

        // Jump Buffer
        if (Input.IsActionJustPressed("jump"))
        {
            _jumpBufferTimer = JumpBufferTime;
        }
        else
        {
            _jumpBufferTimer -= dt;
        }

        // Gravity (3D Y is UP)
        if (!onFloor)
        {
            float gravityThisFrame = Gravity;
            if (velocity.Y < 0)
            {
                gravityThisFrame *= FallGravityMultiplier;
            }
            else if (velocity.Y > 0 && Input.IsActionJustReleased("jump"))
            {
                velocity.Y *= JumpCutMultiplier;
            }
            velocity.Y -= gravityThisFrame * dt;
        }

        // Jump logic
        if (_jumpBufferTimer > 0)
        {
            if (_isHurt) _isHurt = false;

            if (_coyoteTimer > 0 || onFloor)
            {
                velocity.Y = JumpVelocity;
                _jumpBufferTimer = 0;
                _coyoteTimer = 0;
                _isHoldJumping = true;
                _isJumping = true;
                _hasDoubleJumped = false;

                PlaySFX(SFX.GetJumpSound());
                PlayAnimation("jump");
            }
            else if (!_hasDoubleJumped)
            {
                velocity.Y = JumpVelocity * 0.95f;
                _jumpBufferTimer = 0;
                _hasDoubleJumped = true;
                _isJumping = true;

                PlaySFX(SFX.GetDoubleJumpSound());
                PlayAnimation("jump");
                // CreateDoubleJumpVFX3D() - TODO
            }
        }

        // Horizontal Movement
        float direction = Input.GetAxis("move_left", "move_right");
        float targetSpeed = direction * Speed;
        float currentAccel = onFloor ? Acceleration : AirAcceleration;
        float currentDecel = onFloor ? Deceleration : AirDeceleration;

        if (Mathf.Abs(direction) > 0.1f)
        {
            if (_isAttacking && onFloor) targetSpeed *= 0.8f;
            velocity.X = Mathf.MoveToward(velocity.X, targetSpeed, currentAccel * dt);

            _facingDirection = direction > 0 ? 1f : -1f;
            _animatedSprite.FlipH = direction < 0;

            // Adjust attack area for 3D
            var attackPos = _attackArea.Position;
            attackPos.X = _facingDirection * Math.Abs(attackPos.X);
            _attackArea.Position = attackPos;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0, currentDecel * dt);
        }

        Velocity = velocity;
        MoveAndSlide();

        // Map Border
        if (GlobalPosition.X < 0) GlobalPosition = new Vector3(0, GlobalPosition.Y, GlobalPosition.Z);
        // Clamp Z within bounds
        float clampedZ = Mathf.Clamp(GlobalPosition.Z, ZMin, ZMax);
        if (GlobalPosition.Z != clampedZ)
            GlobalPosition = new Vector3(GlobalPosition.X, GlobalPosition.Y, clampedZ);

        // Combo & Attack
        if (_comboActive)
        {
            _comboTimer += dt;
            if (_comboTimer >= ComboResetTime)
            {
                _comboIndex = 0;
                _comboActive = false;
            }
        }

        if (Input.IsActionJustPressed("attack") && _canAttack && !_isHurt)
        {
            Attack();
        }

        UpdateAnimation(direction, dt);
        _wasOnFloor = onFloor;
    }

    private void UpdateAnimation(float direction, float dt)
    {
        if (_isDead) return;
        if (_isHurt) { PlayAnimation("hurt"); return; }
        if (_isAttacking || _isSpinning) return;

        if (!IsOnFloor())
        {
            if (Velocity.Y > 0) PlayAnimation("jump");
            else PlayAnimation("fall");
            return;
        }

        if (Math.Abs(Velocity.X) > 0.5f)
        {
            PlayAnimation("run");
            _stepTimer -= dt;
            if (_stepTimer <= 0f)
            {
                _stepTimer = 0.35f;
                _sfxStepPlayer.Stream = SFX.GetStepSound();
                _sfxStepPlayer.PitchScale = (float)GD.RandRange(0.8, 1.2);
                _sfxStepPlayer.Play();
            }
        }
        else
        {
            PlayAnimation("idle");
        }
    }

    private void PlayAnimation(string animName)
    {
        if (_animatedSprite.Animation != animName)
        {
            if (_animatedSprite.SpriteFrames.HasAnimation(animName))
                _animatedSprite.Play(animName);
            else if (animName == "fall")
                _animatedSprite.Play("jump");
        }
    }

    private void Attack()
    {
        _isAttacking = true;
        _canAttack = false;
        string attackAnim = $"attack{_comboIndex + 1}";

        if (_animatedSprite.SpriteFrames.HasAnimation(attackAnim))
            _animatedSprite.Play(attackAnim);
        else
            _animatedSprite.Play("attack");

        _sfxPlayer.Stream = SFX.GetAttackSound(_comboIndex + 1);
        _sfxPlayer.VolumeDb = (_comboIndex == 2) ? 2f : -4f;
        _sfxPlayer.Play();

        _attackCollision.Disabled = false;

        var checkHitTimer = GetTree().CreateTimer(0.05);
        checkHitTimer.Timeout += () =>
        {
            if (!IsInstanceValid(this) || !IsInstanceValid(_attackArea)) return;
            var bodies = _attackArea.GetOverlappingBodies();
            foreach (var body in bodies) OnAttackAreaBodyEntered(body);
        };

        var attackDurationTimer = GetTree().CreateTimer(0.3);
        attackDurationTimer.Timeout += () =>
        {
            if (!IsInstanceValid(this)) return;
            _isAttacking = false;
            _attackCollision.Disabled = true;
        };

        _comboIndex = (_comboIndex + 1) % 3;
        _comboTimer = 0;
        _comboActive = true;
        _attackCooldownTimer.Start();
    }

    private void OnAttackCooldownTimeout() => _canAttack = true;
    private void OnHurtTimeout() => _isHurt = false;

    private void OnAttackAreaBodyEntered(Node body)
    {
        if (body.IsInGroup("enemies"))
        {
            if (body.HasMethod("TakeDamage"))
                body.Call("TakeDamage", AttackDamage);
        }
    }

    public void TakeDamage(int damage)
    {
        if (_isDead || _isHurt || _isInvulnerable) return;

        _health -= damage;
        if (GameManager.Instance != null) GameManager.Instance.PlayerHealth = _health;
        EmitSignal(SignalName.HealthChanged, _health, (GameManager.Instance != null) ? GameManager.Instance.MaxPlayerHealth : 100);

        if (_health <= 0) { Die(); return; }

        if (_isAttacking || _isSpinning) return;

        _isHurt = true;
        _hurtTimer.Start();
        PlayAnimation("hurt");
        Velocity = new Vector3(_facingDirection < 0 ? 5 : -5, 5, 0);
    }

    private void Die()
    {
        _isDead = true;
        Engine.TimeScale = 0.4f;
        _animatedSprite.Play("die");
        var timer = GetTree().CreateTimer(1.5, true, false, true);
        timer.Timeout += () =>
        {
            Engine.TimeScale = 1.0f;
            EmitSignal(SignalName.PlayerDied);
        };
    }

    private void OnAnimationFinished()
    {
        if (_animatedSprite.Animation == "die")
        {
            // Handled in timer above mostly
        }
    }

    private void PlaySFX(AudioStream stream)
    {
        if (stream == null) return;
        _sfxPlayer.Stream = stream;
        _sfxPlayer.Play();
    }
}
