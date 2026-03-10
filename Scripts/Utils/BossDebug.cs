using Godot;
using System;

public partial class BossDebug : Node
{
    public override void _Ready()
    {
        GD.Print("[BossDebug] Starting frame dump...");
        var frames = SpriteHelper.CreateFinalBossSpriteFrames();
        if (frames == null) return;

        foreach (string anim in frames.GetAnimationNames())
        {
            int count = frames.GetFrameCount(anim);
            for (int i = 0; i < count; i++)
            {
                var texture = frames.GetFrameTexture(anim, i) as ImageTexture;
                if (texture != null)
                {
                    var img = texture.GetImage();
                    string path = $"user://debug_boss_{anim}_{i}.png";
                    img.SavePng(path);
                    GD.Print($"[BossDebug] Saved {path}");
                }
            }
        }
    }
}
