using Godot;
using System;
using System.Collections.Generic;

public partial class BaseEnemy3D : CharacterBody3D
{
    [Export] public int MaxHealth = 50;
    [Export] public int AttackDamage = 10;
    [Export] public float MoveSpeed = 3.0f; // 3D units
    [Export] public float Gravity = 25.0f;
    [Export] public int ScoreValue = 100;

    [Export] public float PatrolDistance = 5.0f;
    [Export] public float DetectRange = 8.0f;
    [Export] public float AttackRange = 1.5f;
    [Export] public float AttackCooldown = 1.5f;

    protected int Health;
    public bool IsDead = false;
    protected bool IsHurt = false;
    protected bool CanAttackPlayer = true;

    protected Vector3 StartPosition;
    protected int PatrolDirection = 1;

    protected AnimatedSprite3D AnimSprite;
    protected Area3D DetectArea;
    protected Area3D AttackArea;
    protected Timer AttackCooldownTimer;
    protected Timer HurtTimer;

    protected Node3D TargetPlayer;

    public enum EnemyState { Patrol, Chase, Attack, Hurt, Dead }
    protected EnemyState CurrentState = EnemyState.Patrol;

    public override void _Ready()
    {
        Health = MaxHealth;
        StartPosition = GlobalPosition;
        AddToGroup("enemies");

        AnimSprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
        if (AnimSprite.SpriteFrames == null) CreatePlaceholderSprites();

        AttackCooldownTimer = new Timer();
        AttackCooldownTimer.WaitTime = AttackCooldown;
        AttackCooldownTimer.OneShot = true;
        AttackCooldownTimer.Timeout += () => { CanAttackPlayer = true; };
        AddChild(AttackCooldownTimer);

        HurtTimer = new Timer();
        HurtTimer.WaitTime = 0.3f;
        HurtTimer.OneShot = true;
        HurtTimer.Timeout += () => { IsHurt = false; CurrentState = EnemyState.Patrol; };
        AddChild(HurtTimer);

        if (HasNode("DetectArea"))
        {
            DetectArea = GetNode<Area3D>("DetectArea");
            DetectArea.BodyEntered += OnDetectAreaBodyEntered;
            DetectArea.BodyExited += OnDetectAreaBodyExited;
        }

        if (HasNode("HitArea"))
        {
            AttackArea = GetNode<Area3D>("HitArea");
            AttackArea.BodyEntered += OnHitAreaBodyEntered;
        }

        AnimSprite.AnimationFinished += OnAnimationFinished;
    }

    protected virtual void CreatePlaceholderSprites() { }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;

        Vector3 velocity = Velocity;
        velocity.Z = 0; // Lock to 2D plane

        if (!IsOnFloor()) velocity.Y -= Gravity * (float)delta;

        switch (CurrentState)
        {
            case EnemyState.Patrol:
                velocity.X = PatrolDirection * MoveSpeed;
                if (IsOnWall()) PatrolDirection *= -1;
                
                float distFromStart = GlobalPosition.X - StartPosition.X;
                if (Mathf.Abs(distFromStart) >= PatrolDistance)
                {
                    PatrolDirection = distFromStart > 0 ? -1 : 1;
                }

                SetFacingDirection(PatrolDirection < 0);
                AnimSprite.Play("walk");
                break;

            case EnemyState.Chase:
                if (IsInstanceValid(TargetPlayer))
                {
                    float dir = TargetPlayer.GlobalPosition.X > GlobalPosition.X ? 1 : -1;
                    float dist = Mathf.Abs(TargetPlayer.GlobalPosition.X - GlobalPosition.X);

                    if (dist <= AttackRange)
                    {
                        velocity.X = 0;
                        if (CanAttackPlayer) CurrentState = EnemyState.Attack;
                    }
                    else
                    {
                        velocity.X = dir * MoveSpeed * 1.2f;
                        SetFacingDirection(dir < 0);
                        AnimSprite.Play("walk");
                    }
                }
                else CurrentState = EnemyState.Patrol;
                break;

            case EnemyState.Attack:
                velocity.X = 0;
                AnimSprite.Play("attack");
                break;
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    public virtual void TakeDamage(int damage)
    {
        if (IsDead) return;
        Health -= damage;
        IsHurt = true;
        CurrentState = EnemyState.Hurt;
        HurtTimer.Start();
        AnimSprite.Modulate = Colors.Red;
        var tw = CreateTween();
        tw.TweenProperty(AnimSprite, "modulate", Colors.White, 0.3f);
        if (Health <= 0) Die();
    }

    protected virtual void Die()
    {
        IsDead = true;
        CurrentState = EnemyState.Dead;
        AnimSprite.Play("die");
        if (GameManager.Instance != null) GameManager.Instance.AddScore(ScoreValue);
        GetNode<CollisionShape3D>("CollisionShape3D").SetDeferred("disabled", true);
        var timer = GetTree().CreateTimer(1.0f);
        timer.Timeout += QueueFree;
    }

    private void OnDetectAreaBodyEntered(Node body)
    {
        if (body.IsInGroup("player"))
        {
            TargetPlayer = body as Node3D;
            CurrentState = EnemyState.Chase;
        }
    }

    private void OnDetectAreaBodyExited(Node body)
    {
        if (body == TargetPlayer)
        {
            TargetPlayer = null;
            CurrentState = EnemyState.Patrol;
        }
    }

    private void OnHitAreaBodyEntered(Node body)
    {
        if (IsDead) return;
        if (body.IsInGroup("player") && body.HasMethod("TakeDamage"))
        {
            body.Call("TakeDamage", AttackDamage);
        }
    }

    protected virtual void OnAnimationFinished()
    {
        if (AnimSprite.Animation == "attack")
        {
            CurrentState = TargetPlayer != null ? EnemyState.Chase : EnemyState.Patrol;
            CanAttackPlayer = false;
            AttackCooldownTimer.Start();
        }
    }

    protected void SetFacingDirection(bool faceLeft)
    {
        AnimSprite.FlipH = faceLeft;
        if (AttackArea != null)
        {
            var pos = AttackArea.Position;
            pos.X = (faceLeft ? -1 : 1) * Mathf.Abs(pos.X);
            AttackArea.Position = pos;
        }
    }
}
