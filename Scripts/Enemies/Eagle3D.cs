using Godot;
using System;

public partial class Eagle3D : BaseEnemy3D
{
    [Export] public float FlyHeight = 3.0f;
    [Export] public float DiveSpeed = 12.0f;
    [Export] public float FloatAmplitude = 0.5f;
    [Export] public float FloatFrequency = 2.0f;

    private float _floatTimer = 0;
    private float _baseY;
    private bool _isDiving = false;

    public override void _Ready()
    {
        MaxHealth = 150;
        AttackDamage = 20;
        MoveSpeed = 4.0f;
        ScoreValue = 300;
        PatrolDistance = 8.0f;
        DetectRange = 12.0f;
        AttackRange = 3.0f;
        AttackCooldown = 1.4f;

        base._Ready();
        _baseY = GlobalPosition.Y;
    }

    protected override void CreatePlaceholderSprites()
    {
        try {
            AnimSprite.SpriteFrames = SpriteHelper.CreateEagleSpriteFrames();
            AnimSprite.Play("walk");
        } catch (Exception e) {
            GD.PrintErr("Eagle3D: Failed to create sprites: " + e.Message);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead)
        {
            var vel = Velocity;
            vel.Y -= 15f * (float)delta;
            vel.Z = 0;
            Velocity = vel;
            MoveAndSlide();
            return;
        }

        float dt = (float)delta;
        Vector3 velocity = Velocity;
        velocity.Z = 0;
        _floatTimer += dt;

        switch (CurrentState)
        {
            case EnemyState.Patrol:
                if (IsInstanceValid(TargetPlayer))
                {
                    CurrentState = EnemyState.Chase;
                    break;
                }

                velocity.X = PatrolDirection * MoveSpeed;
                float floatY = _baseY + Mathf.Sin(_floatTimer * FloatFrequency) * FloatAmplitude;
                velocity.Y = (floatY - GlobalPosition.Y) * 5.0f;

                if (IsOnWall()) PatrolDirection *= -1;

                float distFromStart = GlobalPosition.X - StartPosition.X;
                if (Mathf.Abs(distFromStart) >= PatrolDistance)
                    PatrolDirection = distFromStart > 0 ? -1 : 1;

                SetFacingDirection(PatrolDirection < 0);
                AnimSprite.Play("walk");
                break;

            case EnemyState.Chase:
                if (IsInstanceValid(TargetPlayer))
                {
                    float chaseHeight = CanAttackPlayer ? 1.5f : 4.0f;
                    float offsetX = Mathf.Sin(_floatTimer * 3.0f) * 3.0f;

                    Vector3 hoverPos = TargetPlayer.GlobalPosition + new Vector3(offsetX, chaseHeight, 0);
                    Vector3 dirToHover = (hoverPos - GlobalPosition).Normalized();

                    velocity = dirToHover * MoveSpeed * (CanAttackPlayer ? 2.5f : 1.2f);
                    velocity.Y += Mathf.Sin(_floatTimer * FloatFrequency) * FloatAmplitude;
                    velocity.Z = 0;

                    float distX = Mathf.Abs(GlobalPosition.X - TargetPlayer.GlobalPosition.X);
                    if (distX > 0.5f)
                        SetFacingDirection((TargetPlayer.GlobalPosition.X - GlobalPosition.X) < 0);

                    if (distX <= AttackRange && CanAttackPlayer && GlobalPosition.Y > TargetPlayer.GlobalPosition.Y + 1.0f)
                    {
                        CurrentState = EnemyState.Attack;
                        _isDiving = true;
                    }
                }
                else CurrentState = EnemyState.Patrol;
                AnimSprite.Play("walk");
                break;

            case EnemyState.Attack:
                if (_isDiving && IsInstanceValid(TargetPlayer))
                {
                    Vector3 diveDir = (TargetPlayer.GlobalPosition - GlobalPosition).Normalized();
                    diveDir.Z = 0;
                    velocity = diveDir * DiveSpeed;
                    AnimSprite.Play("attack");

                    if (GlobalPosition.Y <= TargetPlayer.GlobalPosition.Y - 0.3f || IsOnFloor())
                    {
                        _isDiving = false;
                        CanAttackPlayer = false;
                        AttackCooldownTimer.Start();
                        CurrentState = EnemyState.Chase;
                    }
                }
                else CurrentState = EnemyState.Chase;
                break;

            case EnemyState.Hurt:
                velocity.X = 0;
                velocity.Y = -1.5f;
                _isDiving = false;
                AnimSprite.Play("hurt");
                break;

            case EnemyState.Dead:
                velocity.X = 0;
                velocity.Y -= 15f * dt;
                break;
        }

        Velocity = velocity;
        MoveAndSlide();
    }
}
