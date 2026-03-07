using Godot;
using System;

/// <summary>
/// Attach this script to any StaticBody2D that has a ColorRect child named "Sprite" or "GroundSprite".
/// It will automatically create a matching collision shape based on the ColorRect size.
/// </summary>
public partial class AutoCollision : StaticBody2D
{
	public override void _Ready()
	{
		// Find the ground visual child (can be ColorRect or TextureRect)
		Control groundVisual = null;
		foreach (var child in GetChildren())
		{
			if (child is ColorRect cr)
			{
				groundVisual = cr;
				break;
			}
			if (child is TextureRect tr)
			{
				groundVisual = tr;
				break;
			}
		}

		if (groundVisual == null) return;

		// Calculate size from the offsets
		float width = groundVisual.OffsetRight - groundVisual.OffsetLeft;
		float height = groundVisual.OffsetBottom - groundVisual.OffsetTop;
		float centerX = (groundVisual.OffsetLeft + groundVisual.OffsetRight) / 2.0f;
		float centerY = (groundVisual.OffsetTop + groundVisual.OffsetBottom) / 2.0f;

		// Create or update collision shape
		var collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collisionShape != null)
		{
			var rectShape = new RectangleShape2D();
			rectShape.Size = new Vector2(width, height);
			collisionShape.Shape = rectShape;
			collisionShape.Position = new Vector2(centerX, centerY);
		}
	}
}
