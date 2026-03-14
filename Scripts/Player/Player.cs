using Godot;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
    // Movement - Core
    [Export] public float Speed = 220.0f;
    [Export] public float Acceleration = 1200.0f;      // Tốc độ tăng tốc
    [Export] public float Deceleration = 1000.0f;      // Tốc độ giảm tốc
    [Export] public float AirAcceleration = 600.0f;    // Tăng tốc khi trên không
    [Export] public float AirDeceleration = 400.0f;    // Giảm tốc khi trên không

    // Jump - Cải thiện cảm giác nhảy
    [Export] public float JumpVelocity = -380.0f;
    [Export] public float Gravity = 900.0f;
    [Export] public float FallGravityMultiplier = 1.5f;   // Rơi nhanh hơn khi đi xuống
    [Export] public float JumpCutMultiplier = 0.5f;       // Giảm vận tốc khi thả phím nhảy sớm
    [Export] public float CoyoteTime = 0.15f;             // Tăng nhẹ lên cho dễ nhảy
    [Export] public float JumpBufferTime = 0.15f;         // Tăng buffer để nhận phím sớm hơn

    // Movement state
    private float _coyoteTimer = 0f;
    private float _jumpBufferTimer = 0f;
    private bool _wasOnFloor = false;
    private bool _isJumping = false;
    private bool _hasDoubleJumped = false;                // Thêm: Check nhảy đúp
    private bool _isHoldJumping = false;                  // Flag cho variable jump height
    private float _facingDirection = 1f;                  // 1 = phải, -1 = trái

    // Cutscene / Auto Walk
    private bool _inCutscene = false;
    private float _cutsceneDirection = 0f;
    private bool _tutorialLockActive = false;
    private string _tutorialExpectedAction = "";

    // Combat
    //[Export] public int AttackDamage = 30;
    [Export] public int AttackDamage = 30;
    [Export] public float AttackCooldown = 0.3f;
    [Export] public float ComboResetTime = 0.6f;  // Time before combo resets to attack1
    private bool _canAttack = true;
    private bool _isAttacking = false;
    private int _comboIndex = 0;  // 0-3 for 4 attack types
    private float _comboTimer = 0;  // Time since last attack
    private bool _comboActive = false;

    // Health
    private int _health;
    private bool _isDead = false;
    private bool _isHurt = false;
    private bool _deathSignalSent = false;
    // Invulnerability (used briefly after respawning)
    private bool _isInvulnerable = false;

    // Components
    private AnimatedSprite2D _animatedSprite;
    private Area2D _attackArea;
    private CollisionShape2D _attackCollision;
    private Timer _attackCooldownTimer;
    private Timer _hurtTimer;
    private Control _shadow;
    private RayCast2D _rayCastGround;

    // Audio
    private AudioStreamPlayer _sfxPlayer;
    private AudioStreamPlayer _sfxStepPlayer;
    private float _stepTimer = 0f;

    // Signals
    [Signal] public delegate void HealthChangedEventHandler(int newHealth, int maxHealth);
    [Signal] public delegate void PlayerDiedEventHandler();

    public override void _Ready()
    {
        _health = GameManager.Instance.PlayerHealth;
        AddToGroup("player");

        // Get nodes
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _animatedSprite.TextureFilter = TextureFilterEnum.Nearest;
        _attackArea = GetNode<Area2D>("AttackArea");
        _attackCollision = _attackArea.GetNode<CollisionShape2D>("CollisionShape2D");
        _attackCollision.Disabled = true;
        _shadow = GetNode<Control>("Shadow");
        _rayCastGround = GetNode<RayCast2D>("RayCastGround");

        // Xóa cache và tạo mới sprites
        SpriteHelper.ClearCache();
        CreatePlaceholderSprites();

        // Create attack cooldown timer
        _attackCooldownTimer = new Timer();
        _attackCooldownTimer.WaitTime = AttackCooldown;
        _attackCooldownTimer.OneShot = true;
        _attackCooldownTimer.Timeout += OnAttackCooldownTimeout;
        AddChild(_attackCooldownTimer);

        // Create hurt timer
        _hurtTimer = new Timer();
        _hurtTimer.WaitTime = 0.5f;
        _hurtTimer.OneShot = true;
        _hurtTimer.Timeout += OnHurtTimeout;
        AddChild(_hurtTimer);

        // Khởi tạo kênh Phát Ám Thanh Đặc Trưng (Gắn vào Player)
        _sfxPlayer = new AudioStreamPlayer();
        _sfxPlayer.VolumeDb = -5f;
        AddChild(_sfxPlayer);

        // Pre-load skill assets to prevent gameplay lag
        PreparePortraitTexture();
        PrepareAxeTexture();

        _sfxStepPlayer = new AudioStreamPlayer();
        _sfxStepPlayer.VolumeDb = -12f;
        AddChild(_sfxStepPlayer);

        // Connect attack area signal
        _attackArea.BodyEntered += OnAttackAreaBodyEntered;

        // Connect animation signal
        _animatedSprite.AnimationFinished += OnAnimationFinished;

        EmitSignal(SignalName.HealthChanged, _health, GameManager.Instance.MaxPlayerHealth);
    }

    private void CreatePlaceholderSprites()
    {
        _animatedSprite.SpriteFrames = SpriteHelper.CreatePlayerSpriteFrames();
        _animatedSprite.Play("idle");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDead) return;

        float dt = (float)delta;
        Vector2 velocity = Velocity;
        bool onFloor = IsOnFloor();

        // === COYOTE TIME - Cho phép nhảy muộn sau khi rời nền ===
        if (onFloor)
        {
            _coyoteTimer = CoyoteTime;
            _isJumping = false;
            _hasDoubleJumped = false; // Reset kĩ năng nhảy đúp
        }
        else
        {
            _coyoteTimer -= dt;
        }

        // === JUMP BUFFER - Ghi nhớ input nhảy ===
        if (Input.IsActionJustPressed("jump") && !_inCutscene && IsActionAllowedByTutorial("jump"))
        {
            _jumpBufferTimer = JumpBufferTime;
        }
        else
        {
            _jumpBufferTimer -= dt;
        }

        // === GRAVITY - Áp dụng trọng lực với fall multiplier ===
        if (!onFloor)
        {
            float gravityThisFrame = Gravity;

            // Rơi nhanh hơn khi đang đi xuống hoặc khi thả phím nhảy sớm
            if (velocity.Y > 0)
            {
                gravityThisFrame *= FallGravityMultiplier;
            }
            else if (velocity.Y < 0 && Input.IsActionJustReleased("jump"))
            {
                // Variable jump height - thả phím sớm sẽ nhảy thấp hơn (Cắt vận tốc ngay lập tức)
                velocity.Y *= JumpCutMultiplier;
            }

            velocity.Y += gravityThisFrame * dt;

            // Giới hạn tốc độ rơi tối đa
            velocity.Y = Mathf.Min(velocity.Y, 800f);
        }

        // === JUMP - Xử lý nhảy với coyote time và jump buffer ===
        if (_jumpBufferTimer > 0 && !_inCutscene)
        {
            // THÊM: Nếu đang bị đau (Hurt) mà nhấn nhảy thì cho phép "hồi phục" nhanh (Recovery Jump)
            if (_isHurt) _isHurt = false;

            // Ưu tiên 1: Nhảy từ dưới đất (hoặc Coyote Time)
            // THAY ĐỔI: Cho phép nhảy kể cả khi đang tấn công để không bị "khựng" (Jump cancels ground friction)
            bool canGroundJump = (_coyoteTimer > 0 || onFloor);

            if (canGroundJump)
            {
                velocity.Y = JumpVelocity;
                _jumpBufferTimer = 0;
                _coyoteTimer = 0;
                _isHoldJumping = true; // Flag để xử lý biến thiên độ cao nhảy
                _isJumping = true;
                _hasDoubleJumped = false;

                _sfxPlayer.Stream = SFX.GetJumpSound();
                _sfxPlayer.Play();

                // Play animation nhảy nếu không phải đang múa Skill cực mạnh
                if (!_isSpinning)
                {
                    _animatedSprite.Stop();
                    _animatedSprite.Play("jump");
                }
            }
            // Ưu tiên 2: Nhảy đúp (Double Jump) khi đang ở trên không
            else if (!_hasDoubleJumped && !onFloor)
            {
                velocity.Y = JumpVelocity * 0.95f;
                _jumpBufferTimer = 0;
                _hasDoubleJumped = true;
                _isJumping = true;

                CreateDoubleJumpVFX();

                _sfxPlayer.Stream = SFX.GetDoubleJumpSound();
                _sfxPlayer.Play();

                if (!_isSpinning)
                {
                    _animatedSprite.Stop();
                    _animatedSprite.Play("jump");
                }
            }
        }

        // === HORIZONTAL MOVEMENT - Di chuyển ngang với acceleration ===
        float direction = _inCutscene ? _cutsceneDirection : Input.GetAxis("move_left", "move_right");
        if (!IsActionAllowedByTutorial("move")) direction = 0f;

        float currentAccel;
        float currentDecel;

        // Khác biệt acceleration trên không và trên mặt đất
        if (onFloor)
        {
            currentAccel = Acceleration;
            currentDecel = Deceleration;
        }
        else
        {
            currentAccel = AirAcceleration;
            currentDecel = AirDeceleration;
        }

        if (Mathf.Abs(direction) > 0.1f)
        {
            float targetSpeed = direction * Speed;

            // Nếu đang chém trên đất thì chạy chậm lại một chút để cảm giác có trọng lực/lực cản
            if (_isAttacking && onFloor) targetSpeed *= 0.8f;

            velocity.X = Mathf.MoveToward(velocity.X, targetSpeed, currentAccel * dt);

            // Cập nhật hướng mặt và sprite
            _facingDirection = direction > 0 ? 1f : -1f;
            _animatedSprite.FlipH = direction < 0;

            // Flip attack area theo hướng
            var attackPos = _attackArea.Position;
            attackPos.X = _facingDirection * Math.Abs(attackPos.X);
            _attackArea.Position = attackPos;
        }
        else
        {
            // Dừng lại - áp dụng deceleration
            velocity.X = Mathf.MoveToward(velocity.X, 0, currentDecel * dt);
        }

        Velocity = velocity;
        MoveAndSlide();

        // Khóa giới hạn Map bên trái: Chống chạy lố trượt xuống hố khỏi bản đồ (Map border X >= 0)
        if (GlobalPosition.X < 0)
        {
            GlobalPosition = new Vector2(0, GlobalPosition.Y);
        }

        // Ghi nhớ trạng thái floor cho frame tiếp theo
        _wasOnFloor = onFloor;

        // Combo timer - reset combo if too much time passes
        if (_comboActive)
        {
            _comboTimer += dt;
            if (_comboTimer >= ComboResetTime)
            {
                _comboIndex = 0;
                _comboActive = false;
            }
        }

        // Attack on click / key press
        if (Input.IsActionJustPressed("attack") && _canAttack && !_isHurt && !_inCutscene && IsActionAllowedByTutorial("attack"))
        {
            Attack();
        }

        // Update shadow scaling and position
        UpdateShadow(dt);

        // Update animation (Truyền delta vào để quản lí tiếng động bước chân)
        UpdateAnimation(direction, dt);

        // Handle skills
        HandleSkills(dt);
    }

    private void UpdateShadow(float dt)
    {
        if (_shadow == null || _rayCastGround == null) return;

        if (_rayCastGround.IsColliding())
        {
            Vector2 collisionPoint = _rayCastGround.GetCollisionPoint();
            float distance = collisionPoint.Y - GlobalPosition.Y;

            // Keep shadow on the ground
            _shadow.GlobalPosition = new Vector2(GlobalPosition.X - 30, collisionPoint.Y - 10);

            // Scale shadow based on distance (smaller when higher)
            // Original scale is 1.0 at distance 0. Shrink to 0.4 at distance 300.
            float scaleFactor = Mathf.Clamp(1.0f - (distance / 400.0f), 0.3f, 1.0f);
            _shadow.Scale = new Vector2(scaleFactor, scaleFactor);
            
            // Adjust shadow transparency
            _shadow.Modulate = new Color(1, 1, 1, scaleFactor * 0.8f);
            _shadow.Visible = true;
        }
        else
        {
            _shadow.Visible = false;
        }
    }

    private void UpdateAnimation(float direction, float dt)
    {
        if (_isDead) return;

        // 1. Ưu tiên bị thương
        if (_isHurt)
        {
            PlayAnimationIfNotPlaying("hurt");
            return;
        }

        // 2. Ưu tiên Tấn công (Attack / Spinning) - Cho phép "Nhảy chém" hiện animation
        if (_isAttacking || _isSpinning) return;

        if (!IsOnFloor())
        {
            // Nếu đang ở trên không thì LUÔN HIỆN animation nhảy/rơi (Trừ khi đang chém đã handle ở trên)
            if (_isSpinning) return;

            if (Velocity.Y < 0)
            {
                PlayAnimationIfNotPlaying("jump");
            }
            else
            {
                if (_animatedSprite.SpriteFrames != null && _animatedSprite.SpriteFrames.HasAnimation("fall"))
                {
                    PlayAnimationIfNotPlaying("fall");
                }
                else
                {
                    PlayAnimationIfNotPlaying("jump");
                }
            }
            return;
        }

        // Dưới đất: Nếu đang chém thì ưu tiên chém (Đã handle ở trên, dòng này để an toàn)
        if (_isAttacking || _isSpinning) return;
        else if (Math.Abs(Velocity.X) > 10f)  // Dùng velocity thực tế thay vì input
        {
            PlayAnimationIfNotPlaying("run");

            // Tính nhịp bước chân Lụp Cụp (Noise Sound Generator)
            _stepTimer -= dt;
            if (_stepTimer <= 0f)
            {
                _stepTimer = 0.35f; // Chân chạy mỗi nửa giây
                _sfxStepPlayer.Stream = SFX.GetStepSound();
                _sfxStepPlayer.PitchScale = (float)GD.RandRange(0.8, 1.2); // Sỏi sạn bước chân to nhỏ khác biệt
                _sfxStepPlayer.Play();
            }
        }
        else
        {
            PlayAnimationIfNotPlaying("idle");
        }
    }

    /// <summary>
    /// Chỉ play animation nếu nó khác animation hiện tại - tránh reset animation
    /// </summary>
    private void PlayAnimationIfNotPlaying(string animName)
    {
        if (_animatedSprite.Animation != animName)
        {
            _animatedSprite.Play(animName);
            GD.Print($"Playing animation: {animName}, frames: {_animatedSprite.SpriteFrames?.GetFrameCount(animName)}");
        }
    }

    private void Attack()
    {
        _isAttacking = true;
        _canAttack = false;

        // Play the current combo attack
        string attackAnim = $"attack{_comboIndex + 1}";

        // Check if the animation exists, fallback to "attack" if not
        if (_animatedSprite.SpriteFrames != null && _animatedSprite.SpriteFrames.HasAnimation(attackAnim))
        {
            _animatedSprite.Play(attackAnim);
        }
        else
        {
            _animatedSprite.Play("attack");
        }

        // Phân biệt âm thanh nhát chém theo Tần số (Nhát 1, 2 gió vút / Nhát 3 Siêu cực mạnh)
        _sfxPlayer.Stream = SFX.GetAttackSound(_comboIndex + 1);

        // Nhát 3 xoay cường công -> âm thanh vỡ rách giòn mạnh -> Screen Shake cho ngầu
        if (_comboIndex == 2)
        {
            _sfxPlayer.VolumeDb = 2f;
        }
        else
        {
            _sfxPlayer.VolumeDb = -4f;
        }
        _sfxPlayer.Play();

        _attackCollision.Disabled = false;

        // Xử lý lỗi đánh hụt cự ly gần: Buộc Area2D quét lại các mục tiêu đang đứng sát bên trong
        // Đợi 0.05s để Physics Engine của Godot 4 kịp cập nhật danh sách va chạm sau khi bật _attackCollision
        var checkHitTimer = GetTree().CreateTimer(0.05);
        checkHitTimer.Timeout += () =>
        {
            if (!IsInstanceValid(this) || IsQueuedForDeletion() || !IsInstanceValid(_attackArea)) return;
            var bodies = _attackArea.GetOverlappingBodies();
            foreach (var body in bodies)
            {
                OnAttackAreaBodyEntered(body);
            }
        };

        // Attack lasts 0.3 seconds (since each attack is 1 frame, AnimationFinished fires too fast)
        var attackDurationTimer = GetTree().CreateTimer(0.3);
        attackDurationTimer.Timeout += () =>
        {
            // Guard: Player có thể đã bị QueueFree() trước khi timer kết thúc
            if (!IsInstanceValid(this) || IsQueuedForDeletion()) return;
            _isAttacking = false;
            _attackCollision.Disabled = true;
        };

        // Rút gọn giới hạn Combo lại thành 3 HIT liên hoàn (Từ 4 về 3)
        _comboIndex = (_comboIndex + 1) % 3;
        _comboTimer = 0;
        _comboActive = true;

        _attackCooldownTimer.Start();
    }

    private void OnAnimationFinished()
    {
        if (_animatedSprite.Animation == "die")
        {
            CompleteDeathSequence();
        }
    }

    private void CompleteDeathSequence()
    {
        if (_deathSignalSent) return;
        _deathSignalSent = true;

        // Always restore global speed before handing control to respawn/game-over flow.
        Engine.TimeScale = 1.0f;
        EmitSignal(SignalName.PlayerDied);
    }

    private void OnAttackCooldownTimeout()
    {
        _canAttack = true;
    }

    private void OnHurtTimeout()
    {
        _isHurt = false;
    }

    private void OnAttackAreaBodyEntered(Node2D body)
    {
        if (body.IsInGroup("enemies"))
        {
            if (body.HasMethod("TakeDamage"))
            {
                body.Call("TakeDamage", AttackDamage);
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsTutorialRunning) return;
        // ignore incoming damage while temporarily invulnerable (such as right after a respawn)
        if (_isDead || _isHurt || _isInvulnerable) return;

        _health -= damage;
        GameManager.Instance.PlayerHealth = _health;
        EmitSignal(SignalName.HealthChanged, _health, GameManager.Instance.MaxPlayerHealth);

        if (_health <= 0)
        {
            Die();
            return;
        }

        // --- CƠ CHẾ SUPER ARMOR (KHÁNG HIỆU ỨNG KHI ĐANG TUNG CHIÊU) ---
        // Khi đang xuất Skill hoặc combo, vẫn nhận sát thương nháy đỏ nhưng KHÔNG bị Ngắt Chiêu hay hất văng
        if (_isAttacking || _isSpinning)
        {
            _animatedSprite.Modulate = new Color(1, 0.3f, 0.3f);
            var tweenArmor = CreateTween();
            tweenArmor.TweenProperty(_animatedSprite, "modulate", Colors.White, 0.4f);
            return; // Thoát ngay, bảo vệ vòng đời chiêu thức đang múa
        }

        // Bị dính đòn bình thường (Bị ngắt)
        _isHurt = true;
        _comboIndex = 0;      // Reset combo khi bị đánh trúng
        _comboActive = false;
        _hurtTimer.Start();

        _animatedSprite.Play("hurt");
        // Red flash effect
        _animatedSprite.Modulate = new Color(1, 0.3f, 0.3f);
        var tween = CreateTween();
        tween.TweenProperty(_animatedSprite, "modulate", Colors.White, 0.4f);
        // Knockback (Hất tung)
        Velocity = new Vector2(_animatedSprite.FlipH ? 200 : -200, -150);
    }

    public void ApplyKnockback(Vector2 force)
    {
        if (_isDead) return;
        Velocity = force;
        MoveAndSlide();
        GD.Print($"[Player] Applied knockback force: {force}");
    }

    public void Heal(int amount)
    {
        _health = Math.Min(_health + amount, GameManager.Instance.MaxPlayerHealth);
        GameManager.Instance.PlayerHealth = _health;
        GD.Print($"Healing player by {amount}, new health: {_health}");
        EmitSignal(SignalName.HealthChanged, _health, GameManager.Instance.MaxPlayerHealth);
    }

    private void Die()
    {
        _isDead = true;
        _deathSignalSent = false;

        // --- Hiệu ứng Slow Motion Chuyên Nghiệp ---
        // 1. Dừng hình nhẹ (Hitstop) để cảm nhận cú đánh chí mạng
        Engine.TimeScale = 0.05f;

        // 2. Sau 0.1s thì bắt đầu diễn hoạt chậm (Cinematic Slowmo)
        var timer = GetTree().CreateTimer(0.1, true, false, true); // true cho process_always (quan trọng khi timescale thấp)
        timer.Timeout += () =>
        {
            if (!IsInstanceValid(this)) return;
            Engine.TimeScale = 0.4f; // Chậm lại còn 40% tốc độ thực
            _animatedSprite.Stop();
            _animatedSprite.SpeedScale = 0.8f; // Làm cho animation chính nó cũng chậm hơn một chút nữa
            _animatedSprite.Play("die");
        };

        // Fallback: nếu animation "die" không phát signal finished (asset lỗi/loop),
        // vẫn buộc chuyển sang luồng hồi sinh để game không bị kẹt slowmotion.
        var fallback = GetTree().CreateTimer(1.6, true, false, true);
        fallback.Timeout += () =>
        {
            if (!IsInstanceValid(this) || !_isDead) return;
            CompleteDeathSequence();
        };

        // Disable collision immediately so player can't interact while dying
        GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
    }

    public void FastReset()
    {
        _isDead = false;
        _isHurt = false;
        _deathSignalSent = false;
        _health = GameManager.Instance.MaxPlayerHealth;
        GameManager.Instance.PlayerHealth = _health;

        // Reset physics
        Velocity = Vector2.Zero;
        Engine.TimeScale = 1.0f;
        _animatedSprite.SpeedScale = 1.0f;
        _animatedSprite.Modulate = Colors.White;

        // Bật lại collision
        GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", false);

        PlayAnimationIfNotPlaying("idle");
        EmitSignal(SignalName.HealthChanged, _health, GameManager.Instance.MaxPlayerHealth);

        // Brief invulnerability after respawn so the player isn't killed instantly by nearby hazards
        StartInvulnerability(1.0f);
    }

    /// <summary>
    /// Begin a short period where incoming damage is ignored.
    /// Also flashes the player sprite to give visual feedback.
    /// </summary>
    /// <param name="duration">How long the invulnerability should last, in seconds.</param>
    public void StartInvulnerability(float duration = 1f)
    {
        _isInvulnerable = true;

        // flash effect (semi-transparent) for the duration
        var tw = CreateTween();
        tw.SetParallel();
        tw.TweenProperty(_animatedSprite, "modulate", new Color(1,1,1,0.5f), duration * 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.Chain().TweenProperty(_animatedSprite, "modulate", Colors.White, duration * 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        var timer = GetTree().CreateTimer(duration);
        timer.Timeout += () =>
        {
            _isInvulnerable = false;
            _animatedSprite.Modulate = Colors.White;
        };
    }

    public void WalkIntoCave(float direction = 1f)
    {
        _inCutscene = true;
        _cutsceneDirection = direction;
        _isAttacking = false;

        // Tạo hiệu ứng Player từ từ bị bóng tối nuốt chửng khi đi sâu vào Hang (Mờ trong 1s)
        var tw = CreateTween();
        tw.TweenProperty(this, "modulate:a", 0f, 1.0f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    public void SetTutorialExpectedAction(string expectedAction)
    {
        _tutorialLockActive = true;
        _tutorialExpectedAction = expectedAction;
    }

    public void ClearTutorialLock()
    {
        _tutorialLockActive = false;
        _tutorialExpectedAction = "";
    }

    private bool IsActionAllowedByTutorial(string action)
    {
        if (!_tutorialLockActive) return true;

        return _tutorialExpectedAction switch
        {
            "move_jump" => action == "move" || action == "jump",
            "attack" => action == "move" || action == "jump" || action == "attack",
            "skill" => true,
            _ => true,
        };
    }

    private void CreateDoubleJumpVFX()
    {
        // Thêm vụ nổ năng lượng xanh ở chân player (cần bắn vào Root parent để ko bay theo nhân vật)
        var vfx = new DoubleJumpVFX();
        vfx.GlobalPosition = GlobalPosition + new Vector2(0, 18);
        GetParent().AddChild(vfx);
    }
}

/// <summary>
/// Hiệu ứng VFX dạng sóng xung kích nổ khí quyển đẹp mắt khi Nhảy đúp (Dùng Draw() Vector tự code)
/// </summary>
public partial class DoubleJumpVFX : Node2D
{
    private float _radius = 0f;
    private float _alpha = 1f;

    public override void _Ready()
    {
        var tw = CreateTween();
        tw.SetParallel();
        // Sóng mở rộng nhanh, tàn biến mờ ảo vào không gian trong 0.35s
        tw.TweenProperty(this, "_radius", 55f, 0.35f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(this, "_alpha", 0f, 0.35f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        tw.Chain().TweenCallback(Callable.From(() => QueueFree()));
    }

    public override void _Process(double delta)
    {
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Màu lam trắng của dòng khí chân
        Color color = new Color(0.6f, 0.95f, 1f, _alpha);

        // Cung vòng tròn tỏa ra (Shockwave)
        DrawArc(Vector2.Zero, _radius, 0, Mathf.Pi * 2, 24, color, 3f * _alpha, true);

        // Tia năng lượng gió cày văng tung toé (5 đường góc rách)
        for (int i = 0; i < 5; i++)
        {
            Vector2 dir = Vector2.Up.Rotated(i * Mathf.Pi / 2.5f + (_radius * 0.02f));
            DrawLine(dir * (_radius * 0.4f), dir * (_radius * 1.5f), color, 4f * _alpha);
        }
    }
}
