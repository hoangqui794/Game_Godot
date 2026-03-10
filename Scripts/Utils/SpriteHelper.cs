using Godot;
using System;
using System.Collections.Generic;

public static class SpriteHelper
{
    private static SpriteFrames _cachedPlayerFrames = null;

    public static SpriteFrames CreatePlayerSpriteFrames()
    {
        if (_cachedPlayerFrames != null) return _cachedPlayerFrames;

        var anims = new Dictionary<string, Texture2D[]>();

        // 1. Load Idle (High-Res)
        var idleImage = LoadAndCleanImage("res://Assets/Sprites/Player/Tháchanh_ đưng yen.png");
        var idleTextures = new List<Texture2D>();

        if (idleImage != null)
        {
            var blobs = FindBlobs(idleImage);
            foreach (var r in blobs)
            {
                idleTextures.Add(SmartPad(idleImage, r, 240, 240));
            }
            if (idleTextures.Count > 0) anims["idle"] = idleTextures.ToArray();
            GD.Print($"Idle: created {idleTextures.Count} high-res frames");
        }

        // 2. Run animation - load từ các file riêng lẻ 1, 2, 4 (High-Res)
        var runTextures = new List<Texture2D>();
        string[] runPaths = { "res://Assets/Sprites/Enemies/1.png", "res://Assets/Sprites/Enemies/2.png", "res://Assets/Sprites/Enemies/4.png" };

        foreach (var path in runPaths)
        {
            var frameImg = LoadAndCleanImage(path);
            if (frameImg != null)
            {
                var bounds = FindBounds(frameImg);
                if (bounds.Size.X > 5 && bounds.Size.Y > 5)
                {
                    runTextures.Add(SmartPad(frameImg, bounds, 240, 240));
                }
            }
        }

        if (runTextures.Count > 0)
        {
            anims["run"] = runTextures.ToArray();
            GD.Print($"Run: created {runTextures.Count} high-res frames from 1, 2, 4.png");
        }
        else if (idleTextures.Count > 0)
        {
            anims["run"] = idleTextures.ToArray();
        }

        // Jump animation - load from ThachSanh_Ani_Nhay.png (2 cols x 2 rows = 4 frames)
        var jumpImage = LoadAndCleanImage("res://Assets/Sprites/Player/ThachSanh_Ani_Nhay.png");
        var jumpTextures = new List<Texture2D>();

        if (jumpImage != null)
        {
            var jumpFramesRaw = SliceSpriteSheetGridRaw(jumpImage, 2, 2);
            GD.Print($"Jump: sliced {jumpFramesRaw.Count} raw frames from sprite sheet");

            foreach (var frameImg in jumpFramesRaw)
            {
                var bounds = FindBounds(frameImg);
                if (bounds.Size.X > 5 && bounds.Size.Y > 5)
                {
                    // Scale jump animation by 0.65x so it matches the normal character size
                    jumpTextures.Add(SmartPad(frameImg, bounds, 240, 240, 0.65f));
                }
            }

            if (jumpTextures.Count >= 4)
            {
                // Mặc định: 2 frame đầu nhảy lên, 2 frame sau rơi xuống
                anims["jump"] = new[] { jumpTextures[0], jumpTextures[1] };
                anims["fall"] = new[] { jumpTextures[2], jumpTextures[3] };
            }
            else if (jumpTextures.Count > 0)
            {
                anims["jump"] = jumpTextures.ToArray();
                anims["fall"] = jumpTextures.ToArray();
            }
            GD.Print($"Jump: created {jumpTextures.Count} frames from ThachSanh_Ani_Nhay.png");
        }
        else
        {
            GD.Print("Jump: failed to load ThachSanh_Ani_Nhay.png");
        }

        // 3. Load Attack (Sử dụng Grid 3 cột x 4 hàng)
        var attackImage = LoadAndCleanImage("res://Assets/Sprites/Player/ThachSanhChem.png");
        var attackTextures = new List<Texture2D>();

        if (attackImage != null)
        {
            // Cắt lưới 3 cột x 4 hàng (tổng 12 frames)
            var attackFramesRaw = SliceSpriteSheetGridRaw(attackImage, 3, 4);
            GD.Print($"Attack: sliced {attackFramesRaw.Count} raw frames from grid");

            foreach (var frameImg in attackFramesRaw)
            {
                var bounds = FindBounds(frameImg);
                if (bounds.Size.X > 5 && bounds.Size.Y > 5)
                {
                    attackTextures.Add(SmartPad(frameImg, bounds, 240, 240));
                }
                else
                {
                    // Nếu ô trống, tạo một frame rỗng (fallback) để giữ đủ 12 frames
                    attackTextures.Add(CreateColoredRect(240, 240, new Color(0, 0, 0, 0)));
                }
            }

            if (attackTextures.Count >= 12)
            {
                anims["attack1"] = new[] { attackTextures[0], attackTextures[1], attackTextures[2] };
                anims["attack2"] = new[] { attackTextures[3], attackTextures[4], attackTextures[5] };
                anims["attack3"] = new[] { attackTextures[6], attackTextures[7], attackTextures[8] };
                anims["attack4"] = new[] { attackTextures[9], attackTextures[10], attackTextures[11] };
                anims["attack"] = anims["attack1"];
            }
            else if (attackTextures.Count > 0)
            {
                anims["attack"] = attackTextures.ToArray();
            }
            GD.Print($"Attack: successfully assigned 4 combo attacks from {attackTextures.Count} processed frames.");
        }

        // 4. Fallbacks
        Texture2D defaultFrame = idleTextures.Count > 0 ? idleTextures[0] : CreateColoredRect(60, 80, new Color(0.7f, 0.45f, 0.2f));

        if (!anims.ContainsKey("idle")) anims["idle"] = new[] { defaultFrame };
        if (!anims.ContainsKey("run")) anims["run"] = new[] { defaultFrame };
        if (!anims.ContainsKey("attack1")) anims["attack1"] = new[] { defaultFrame };
        if (!anims.ContainsKey("attack2")) anims["attack2"] = new[] { defaultFrame };
        if (!anims.ContainsKey("attack3")) anims["attack3"] = new[] { defaultFrame };
        if (!anims.ContainsKey("attack4")) anims["attack4"] = new[] { defaultFrame };
        if (!anims.ContainsKey("attack")) anims["attack"] = new[] { defaultFrame };

        if (!anims.ContainsKey("jump")) anims["jump"] = new[] { defaultFrame };
        if (!anims.ContainsKey("fall")) anims["fall"] = new[] { defaultFrame };

        anims["hurt"] = new[] { defaultFrame };
        anims["die"] = new[] { defaultFrame };

        _cachedPlayerFrames = BuildSpriteFrames(anims, 12.0f);
        return _cachedPlayerFrames;
    }

    /// <summary>
    /// Tìm các vùng sprite riêng biệt trong ảnh
    /// </summary>
    private static List<Rect2I> FindBlobs(Image img)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        bool[,] visited = new bool[w, h];
        var blobs = new List<Rect2I>();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!visited[x, y] && img.GetPixel(x, y).A > 0.1f)
                {
                    int minX = x, minY = y, maxX = x, maxY = y;
                    var q = new Queue<Vector2I>();
                    q.Enqueue(new Vector2I(x, y));
                    visited[x, y] = true;
                    int pixelCount = 0;

                    while (q.Count > 0)
                    {
                        var curr = q.Dequeue();
                        pixelCount++;

                        if (curr.X < minX) minX = curr.X;
                        if (curr.X > maxX) maxX = curr.X;
                        if (curr.Y < minY) minY = curr.Y;
                        if (curr.Y > maxY) maxY = curr.Y;

                        for (int dx = -2; dx <= 2; dx++)
                        {
                            for (int dy = -2; dy <= 2; dy++)
                            {
                                int nx = curr.X + dx;
                                int ny = curr.Y + dy;
                                if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                                {
                                    if (!visited[nx, ny] && img.GetPixel(nx, ny).A > 0.1f)
                                    {
                                        visited[nx, ny] = true;
                                        q.Enqueue(new Vector2I(nx, ny));
                                    }
                                }
                            }
                        }
                    }

                    if (pixelCount > 50)
                    {
                        blobs.Add(new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1));
                    }
                }
            }
        }

        // Merge intersecting blobs
        for (int i = 0; i < blobs.Count; i++)
        {
            for (int j = i + 1; j < blobs.Count; j++)
            {
                if (blobs[i].Intersects(blobs[j].Grow(1)))
                {
                    blobs[i] = blobs[i].Merge(blobs[j]);
                    blobs.RemoveAt(j);
                    j--;
                }
            }
        }

        // Sort by row then column
        blobs.Sort((a, b) =>
        {
            int rowA = (a.Position.Y + a.Size.Y / 2) / 150;
            int rowB = (b.Position.Y + b.Size.Y / 2) / 150;
            if (rowA != rowB) return rowA.CompareTo(rowB);
            return a.Position.X.CompareTo(b.Position.X);
        });

        return blobs;
    }

    /// <summary>
    /// Cắt và căn chỉnh sprite vào frame cố định, cho phép đổi tỷ lệ với customScale
    /// </summary>
    public static ImageTexture SmartPad(Image source, Rect2I rect, int outW, int outH, float customScale = 1.0f, bool flipX = false)
    {
        int bw = rect.Size.X;
        int bh = rect.Size.Y;

        var crop = Image.CreateEmpty(bw, bh, false, Image.Format.Rgba8);
        for (int x = 0; x < bw; x++)
        {
            for (int y = 0; y < bh; y++)
            {
                int sx = rect.Position.X + x;
                int sy = rect.Position.Y + y;
                if (sx >= 0 && sy >= 0 && sx < source.GetWidth() && sy < source.GetHeight())
                {
                    crop.SetPixel(x, y, source.GetPixel(sx, sy));
                }
            }
        }

        if (flipX) crop.FlipX();

        int targetMaxHeight = outH - 20;
        int targetMaxWidth = outW - 20;

        float scaleToFit = 1.0f;
        if (bw > targetMaxWidth || bh > targetMaxHeight)
        {
            float scaleX = (float)targetMaxWidth / bw;
            float scaleY = (float)targetMaxHeight / bh;
            scaleToFit = Math.Min(scaleX, scaleY);
        }

        // Áp dụng scale bằng customScale kết hợp với scaleToFit
        float finalScale = scaleToFit * customScale;

        if (Math.Abs(finalScale - 1.0f) > 0.01f)
        {
            int newW = Math.Max(1, (int)(bw * finalScale));
            int newH = Math.Max(1, (int)(bh * finalScale));

            var interp = (bw > 600 || bh > 600) ? Image.Interpolation.Bilinear : Image.Interpolation.Nearest;
            crop.Resize(newW, newH, interp);
            bw = newW;
            bh = newH;
        }

        var frame = Image.CreateEmpty(outW, outH, false, Image.Format.Rgba8);
        int destX = (outW - bw) / 2;
        int destY = outH - bh - 2;

        for (int x = 0; x < bw; x++)
        {
            for (int y = 0; y < bh; y++)
            {
                if (destX + x >= 0 && destX + x < outW && destY + y >= 0 && destY + y < outH)
                {
                    frame.SetPixel(destX + x, destY + y, crop.GetPixel(x, y));
                }
            }
        }
        return ImageTexture.CreateFromImage(frame);
    }

    /// <summary>
    /// Heuristic to detect if a character image is facing RIGHT.
    /// Uses Red-Eye detection as primary (since the snake has red eyes) 
    /// and weighted Center of Mass as fallback.
    /// </summary>
    public static bool IsFacingRight(Image img)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        
        // Focus on the top 45% of the sprite where the head/eyes are
        int analysisHeight = (int)(h * 0.45f);
        int centerX = w / 2;
        
        long redPixelsXSum = 0;
        int redPixelsCount = 0;
        
        double weightedXDist = 0;
        long totalPixels = 0;
        
        for (int y = 0; y < analysisHeight; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color p = img.GetPixel(x, y);
                if (p.A > 0.1f)
                {
                    // 1. Red-Eye Detection: look for eyes (high Red, low Green/Blue)
                    // The snake eyes are quite distinctively red, but might be dark.
                    if (p.R > 0.35f && p.R > p.G * 1.6f && p.R > p.B * 1.6f)
                    {
                        redPixelsXSum += x;
                        redPixelsCount++;
                    }

                    // 2. Fallback Weighted Mass
                    weightedXDist += (x - centerX);
                    totalPixels++;
                }
            }
        }
        
        // If we found eyes, they are the absolute ground truth
        if (redPixelsCount > 0)
        {
            float avgEyeX = (float)redPixelsXSum / redPixelsCount;
            // For a snake, eyes are near the front. If eye is on the right of center, it faces Right.
            return avgEyeX > centerX;
        }

        if (totalPixels == 0) return false;
        
        // Fallback to mass distribution
        return weightedXDist > 0;
    }

    /// <summary>
    /// Process a list of images and ensure they all face LEFT based on majority vote.
    /// </summary>
    public static List<Texture2D> CreateNormalizedAnimation(List<Image> frames, int outW, int outH, float customScale = 1.0f, string animName = "unknown")
    {
        int rightCount = 0;
        var boundsList = new List<Rect2I>();
        int validCount = 0;
        
        // 1. Analyze all frames
        foreach (var img in frames)
        {
            var bounds = FindBounds(img);
            boundsList.Add(bounds);
            
            if (bounds.Size.X > 5 && bounds.Size.Y > 5)
            {
                var crop = Image.CreateEmpty(bounds.Size.X, bounds.Size.Y, false, Image.Format.Rgba8);
                crop.BlitRect(img, bounds, Vector2I.Zero);
                if (IsFacingRight(crop)) rightCount++;
                validCount++;
            }
        }
        
        // 2. Determine flip decision independently for this set
        bool needsFlip = (validCount > 0 && (float)rightCount / validCount > 0.4f);
        
        GD.Print($"[SpriteHelper] Normalizing Animation '{animName}': RightCount={rightCount}/{validCount}, NeedsFlip={needsFlip}");
        
        // 3. Create textures with consistent flipping
        var textures = new List<Texture2D>();
        for (int i = 0; i < frames.Count; i++)
        {
            if (boundsList[i].Size.X > 5 && boundsList[i].Size.Y > 5)
            {
                textures.Add(SmartPad(frames[i], boundsList[i], outW, outH, customScale, needsFlip));
            }
            else
            {
                textures.Add(CreateColoredRect(outW, outH, new Color(0, 0, 0, 0)));
            }
        }
        return textures;
    }

    /// <summary>
    /// Cắt sprite sheet theo grid và trả về List Image (raw, chưa convert sang texture)
    /// </summary>
    private static List<Image> SliceSpriteSheetGridRaw(Image img, int cols, int rows)
    {
        var frames = new List<Image>();

        int imgW = img.GetWidth();
        int imgH = img.GetHeight();
        int frameW = imgW / cols;
        int frameH = imgH / rows;

        GD.Print($"SliceSpriteSheetGridRaw: img={imgW}x{imgH}, grid={cols}x{rows}, frame={frameW}x{frameH}");

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int x = col * frameW;
                int y = row * frameH;

                var crop = Image.CreateEmpty(frameW, frameH, false, Image.Format.Rgba8);
                for (int px = 0; px < frameW; px++)
                {
                    for (int py = 0; py < frameH; py++)
                    {
                        int sx = x + px;
                        int sy = y + py;
                        if (sx < imgW && sy < imgH)
                        {
                            crop.SetPixel(px, py, img.GetPixel(sx, sy));
                        }
                    }
                }

                frames.Add(crop);
            }
        }

        return frames;
    }

    /// <summary>
    /// Tìm bounding box của pixels visible trong một image
    /// </summary>
    public static Rect2I FindBounds(Image img)
    {
        int w = img.GetWidth();
        int h = img.GetHeight();
        int minX = w, minY = h, maxX = 0, maxY = 0;
        bool found = false;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (img.GetPixel(x, y).A > 0.1f)
                {
                    found = true;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (!found) return new Rect2I(0, 0, 0, 0);
        return new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>
    /// Cắt sprite sheet theo grid cố định (cols x rows)
    /// </summary>
    private static List<Texture2D> SliceSpriteSheetGrid(Image img, int cols, int rows)
    {
        var textures = new List<Texture2D>();

        int imgW = img.GetWidth();
        int imgH = img.GetHeight();
        int frameW = imgW / cols;
        int frameH = imgH / rows;

        GD.Print($"SliceSpriteSheetGrid: img={imgW}x{imgH}, grid={cols}x{rows}, frame={frameW}x{frameH}");

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int x = col * frameW;
                int y = row * frameH;

                // Crop frame
                var crop = Image.CreateEmpty(frameW, frameH, false, Image.Format.Rgba8);
                for (int px = 0; px < frameW; px++)
                {
                    for (int py = 0; py < frameH; py++)
                    {
                        int sx = x + px;
                        int sy = y + py;
                        if (sx < imgW && sy < imgH)
                        {
                            crop.SetPixel(px, py, img.GetPixel(sx, sy));
                        }
                    }
                }

                textures.Add(ImageTexture.CreateFromImage(crop));
            }
        }

        return textures;
    }

    public static SpriteFrames CreateSnakeSpriteFrames()
    {
        var imgWalk = LoadAndCleanImage("res://Assets/Sprites/Enemies/snack3_move.png");
        var imgHurt = LoadAndCleanImage("res://Assets/Sprites/Enemies/snack_bidanh.png");

        if (imgWalk == null) return CreateFallbackSprites(new Color(0.2f, 0.6f, 0.15f), false);

        // Ảnh di chuyển là lưới 4x1
        var rawFramesWalk = SliceSpriteSheetGridRaw(imgWalk, 4, 1);
        // Di chuyển dùng các khung hình uốn lượn liên tiếp
        var anims = new Dictionary<string, Texture2D[]>();
        anims["walk"] = CreateNormalizedAnimation(rawFramesWalk, 350, 350, 1.0f, "snake_walk").ToArray();

        var framesHurtRaw = new List<Image>();
        if (imgHurt != null)
        {
            framesHurtRaw = SliceSpriteSheetGridRaw(imgHurt, 2, 1);
        }
        else
        {
            framesHurtRaw.Add(rawFramesWalk[0]);
            if (rawFramesWalk.Count > 3) framesHurtRaw.Add(rawFramesWalk[3]);
        }

        // Normalize independently to handle inconsistent source sheets
        var texturesHurt = CreateNormalizedAnimation(framesHurtRaw, 350, 350, 1.0f, "snake_hurt");
        
        anims["attack"] = new[] { anims["walk"][0], anims["walk"][1], anims["walk"].Length > 3 ? anims["walk"][3] : anims["walk"][0], anims["walk"][0] };
        anims["hurt"] = new[] { texturesHurt[0] };
        anims["die"] = new[] { texturesHurt.Count > 1 ? texturesHurt[1] : texturesHurt[0] };

        // Tạo animation
        return BuildSpriteFrames(anims, 8.0f);
    }

    public static SpriteFrames CreateEagleSpriteFrames()
    {
        var frames = LoadSpriteSheet2x2("res://Assets/Sprites/Enemies/eagle.png");
        if (frames == null) return CreateFallbackSprites(new Color(0.35f, 0.25f, 0.15f), false);
        return BuildSpriteFrames(new Dictionary<string, Texture2D[]> {
            {"walk", new[] { frames[0] }}, {"attack", new[] { frames[1] }},
            {"hurt", new[] { frames[2] }}, {"die", new[] { frames[3] }}
        });
    }

    public static SpriteFrames CreatePrincessSpriteFrames()
    {
        var frames = LoadSpriteSheet2x2("res://Assets/Sprites/NPCs/princess.png");
        if (frames == null) return CreateFallbackSprites(Colors.DeepPink, false);
        return BuildSpriteFrames(new Dictionary<string, Texture2D[]> {
            {"idle", new[] { frames[0], frames[2] }}, {"rescued", new[] { frames[1], frames[3] }}
        }, 4.0f);
    }

    public static SpriteFrames CreateFinalBossSpriteFrames()
    {
        // 1. Tải và lọc phông xanh
        var bossImg = LoadAndCleanImage("res://Assets/Sprites/Enemies/FinalBoss.jpg");
        if (bossImg == null) return null;

        GD.Print($"[FinalBossHelper] Analyzing FinalBoss.jpg dynamically...");

        // 2. Tìm tất cả các con quái (blobs) trên toàn bộ ảnh
        var allBlobs = FindBlobs(bossImg);
        GD.Print($"[FinalBossHelper] Found {allBlobs.Count} initial blobs.");

        // Lọc bỏ text hoặc nhiễu (Final Boss phải to, ví dụ > 100x100 hoặc pixel count lớn)
        var bossBlobs = new List<Rect2I>();
        foreach (var b in allBlobs)
        {
            if (b.Size.X > 100 && b.Size.Y > 100)
            {
                bossBlobs.Add(b);
            }
        }
        GD.Print($"[FinalBossHelper] Filtered to {bossBlobs.Count} boss sprites.");

        // 3. Gom nhóm theo hàng (Dùng Y-Bottom - Chân chạm đất - làm mốc để chính xác nhất)
        var rows = new List<List<Rect2I>>();
        bossBlobs.Sort((a, b) => (a.Position.Y + a.Size.Y).CompareTo(b.Position.Y + b.Size.Y));

        if (bossBlobs.Count > 0)
        {
            var currentRow = new List<Rect2I> { bossBlobs[0] };
            rows.Add(currentRow);
            for (int i = 1; i < bossBlobs.Count; i++)
            {
                // Nếu chân quái lệch nhau ít (ví dụ < 100px) thì coi là cùng hàng
                int currentBottom = currentRow[0].Position.Y + currentRow[0].Size.Y;
                int nextBottom = bossBlobs[i].Position.Y + bossBlobs[i].Size.Y;

                if (Math.Abs(nextBottom - currentBottom) < 100)
                {
                    currentRow.Add(bossBlobs[i]);
                }
                else
                {
                    currentRow.Sort((a, b) => a.Position.X.CompareTo(b.Position.X));
                    currentRow = new List<Rect2I> { bossBlobs[i] };
                    rows.Add(currentRow);
                }
            }
            rows[rows.Count - 1].Sort((a, b) => a.Position.X.CompareTo(b.Position.X));
        }

        GD.Print($"[FinalBossHelper] Detected {rows.Count} rows of sprites.");

        var anims = new Dictionary<string, Texture2D[]>();
        int outSize = 600; // Final Boss cho to hẳn ra 600x600

        // Đúng theo hình minh họa: Dòng 2=attack, Dòng 3=die. Bỏ qua dòng 1.
        string[] animNames = { "idle", "SKIP", "attack", "die" };

        for (int i = 0; i < animNames.Length; i++)
        {
            if (animNames[i] == "SKIP") continue;
            
            if (i < rows.Count)
            {
                var rawRowImages = new List<Image>();
                foreach (var rect in rows[i])
                {
                    var crop = Image.CreateEmpty(rect.Size.X, rect.Size.Y, false, Image.Format.Rgba8);
                    crop.BlitRect(bossImg, rect, Vector2I.Zero);
                    rawRowImages.Add(crop);
                }
                
                // Chuẩn hóa phát hiện hướng ĐỘC LẬP cho từng hàng
                var textures = CreateNormalizedAnimation(rawRowImages, outSize, outSize, 1.0f, $"final_boss_{animNames[i]}");
                
                anims[animNames[i]] = textures.ToArray();
                GD.Print($"[FinalBossHelper] Assigned Row {i} to {animNames[i]} ({textures.Count} frames) normalized to LEFT.");
            }
        }

        // Fallbacks & Map Walk/Hurt
        if (anims.ContainsKey("idle") && !anims.ContainsKey("walk")) anims["walk"] = anims["idle"];
        if (anims.ContainsKey("die") && !anims.ContainsKey("hurt")) anims["hurt"] = new[] { anims["die"][0] };
        else if (anims.ContainsKey("attack") && !anims.ContainsKey("hurt")) anims["hurt"] = new[] { anims["attack"][0] };

        // Ensure all exist
        foreach (var name in new[] { "idle", "walk", "attack", "hurt", "die" })
            if (!anims.ContainsKey(name)) anims[name] = new[] { CreateColoredRect(outSize, outSize, new Color(1, 0, 0, 0.2f)) };

        return BuildSpriteFrames(anims, 6.0f);
    }

    /// <summary>
    /// Tìm bounds trong một vùng cụ thể (thay vì toàn bộ ảnh)
    /// </summary>
    public static Rect2I FindBoundsInRegion(Image img, Rect2I region)
    {
        int minX = region.End.X, minY = region.End.Y, maxX = region.Position.X, maxY = region.Position.Y;
        bool found = false;

        for (int y = region.Position.Y; y < region.End.Y; y++)
        {
            for (int x = region.Position.X; x < region.End.X; x++)
            {
                if (x >= 0 && x < img.GetWidth() && y >= 0 && y < img.GetHeight())
                {
                    if (img.GetPixel(x, y).A > 0.1f)
                    {
                        found = true;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }

        if (!found) return new Rect2I(0, 0, 0, 0);
        return new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public static SpriteFrames CreateBossSpriteFrames()
    {
        var bossImg = LoadAndCleanImage("res://Assets/Sprites/Enemies/BossRan.png");
        if (bossImg == null) return null;

        int cols = 6;
        int rows = 5;
        int outSize = 512;

        GD.Print($"[BossHelper] Slicing BossRan.png into {cols}x{rows} grid...");
        
        // 1. Cắt thô theo Grid
        var rawFrames = SliceSpriteSheetGridRaw(bossImg, cols, rows);
        var anims = new Dictionary<string, Texture2D[]>();
        string[] animNames = { "idle", "walk", "attack", "hurt", "die" };

        // 2. Phân bổ theo hàng và CHUẨN HÓA ĐỘC LẬP theo hàng
        for (int r = 0; r < rows; r++)
        {
            if (r >= animNames.Length) break;

            var rawRowFrames = new List<Image>();
            for (int c = 0; c < cols; c++)
            {
                int index = r * cols + c;
                if (index < rawFrames.Count)
                {
                    rawRowFrames.Add(rawFrames[index]);
                }
            }

            if (rawRowFrames.Count > 0)
            {
                // Chuẩn hóa phát hiện hướng ĐỘC LẬP để xử lý ảnh gốc lỗi hướng
                var textures = CreateNormalizedAnimation(rawRowFrames, outSize, outSize, 1.0f, $"boss_ran_{animNames[r]}");
                anims[animNames[r]] = textures.ToArray();
            }
        }

        // Fallback
        foreach (var name in animNames)
        {
            if (!anims.ContainsKey(name))
            {
                GD.PrintErr($"[BossHelper] Missing animation '{name}', using fallback.");
                anims[name] = new[] { CreateColoredRect(outSize, outSize, new Color(1, 0, 0, 0.2f)) };
            }
        }

        return BuildSpriteFrames(anims, 8.0f);
    }

    private static Texture2D[] LoadSpriteSheet2x2(string path)
    {
        var img = LoadAndCleanImage(path); if (img == null) return null;
        int w = img.GetWidth() / 2, h = img.GetHeight() / 2;
        return new[] { Crop(img, 0, 0, w, h), Crop(img, w, 0, w, h), Crop(img, 0, h, w, h), Crop(img, w, h, w, h) };
    }

    private static ImageTexture Crop(Image source, int x, int y, int w, int h)
    {
        var crop = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int px = 0; px < w; px++) for (int py = 0; py < h; py++)
            {
                int sx = x + px, sy = y + py;
                if (sx >= 0 && sy >= 0 && sx < source.GetWidth() && sy < source.GetHeight())
                    crop.SetPixel(px, py, source.GetPixel(sx, sy));
            }
        return ImageTexture.CreateFromImage(crop);
    }

    public static Image LoadAndCleanImage(string path)
    {
        var tex = GD.Load<Texture2D>(path); if (tex == null) return null;
        var img = tex.GetImage(); img.Decompress(); img.Convert(Image.Format.Rgba8);
        RemoveBackground(img); return img;
    }

    public static void RemoveBackground(Image img)
    {
        int w = img.GetWidth(), h = img.GetHeight();

        // Sample corner to detect background type
        Color bg = img.GetPixel(0, 0);
        bool isGreenScreen = bg.G > 0.4f && bg.G > bg.R && bg.G > bg.B;
        bool isMagentaScreen = bg.R > 0.4f && bg.B > 0.4f && bg.R > bg.G && bg.B > bg.G;
        bool isBlueScreen = bg.B > 0.4f && bg.B > bg.R && bg.B > bg.G;

        // Detect checkered/caro pattern (transparent grid pattern)
        // Usually alternating light gray and white or similar
        Color bg2 = (w > 1 && h > 1) ? img.GetPixel(1, 0) : bg;
        bool isCheckered = IsCheckeredBackground(bg, bg2);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Color p = img.GetPixel(x, y);
                bool remove = false;

                if (isGreenScreen)
                {
                    // Chromakey removal: Green is dominant
                    if (p.G > 0.3f && p.G > p.R * 1.1f && p.G > p.B * 1.1f) remove = true;
                    // Catch dark green shadows
                    else if (p.G > 0.15f && p.G > p.R * 1.4f) remove = true;
                }
                else if (isMagentaScreen)
                {
                    // Chromakey removal: Magenta (Red + Blue) is dominant
                    if (p.R > 0.3f && p.B > 0.3f && p.R > p.G * 1.1f && p.B > p.G * 1.1f) remove = true;
                }
                else if (isBlueScreen)
                {
                    // Chromakey removal: Blue is dominant
                    if (p.B > 0.3f && p.B > p.R * 1.1f && p.B > p.G * 1.1f) remove = true;
                }
                else if (isCheckered)
                {
                    // Remove checkered/caro background (typically gray/white pattern)
                    if (IsCheckeredPixel(p, bg, bg2)) remove = true;
                }
                else
                {
                    if (ColorsClose(p, bg, 0.1f)) remove = true;
                }

                if (remove) img.SetPixel(x, y, new Color(0, 0, 0, 0));
            }
        }
    }

    /// <summary>
    /// Detect if background is a checkered/caro pattern (transparency grid)
    /// </summary>
    private static bool IsCheckeredBackground(Color c1, Color c2)
    {
        // Checkered background typically has two similar gray tones
        // Common pattern: light gray (#CCCCCC) and white (#FFFFFF)
        // Or: gray (#808080) and light gray (#C0C0C0)

        bool c1IsGrayish = Math.Abs(c1.R - c1.G) < 0.1f && Math.Abs(c1.G - c1.B) < 0.1f;
        bool c2IsGrayish = Math.Abs(c2.R - c2.G) < 0.1f && Math.Abs(c2.G - c2.B) < 0.1f;

        // Both colors are grayish and different from each other
        if (c1IsGrayish && c2IsGrayish)
        {
            float diff = Math.Abs(c1.R - c2.R);
            if (diff > 0.05f && diff < 0.5f) return true;
        }

        // Also detect pure white/light backgrounds
        if (c1.R > 0.7f && c1.G > 0.7f && c1.B > 0.7f && c1IsGrayish)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a pixel matches the checkered background colors
    /// </summary>
    private static bool IsCheckeredPixel(Color p, Color bg1, Color bg2)
    {
        // Check if pixel is close to either checkered color
        if (ColorsClose(p, bg1, 0.15f)) return true;
        if (ColorsClose(p, bg2, 0.15f)) return true;

        // Also remove any grayish pixels that look like background
        bool isGrayish = Math.Abs(p.R - p.G) < 0.08f && Math.Abs(p.G - p.B) < 0.08f;
        if (isGrayish && p.R > 0.6f) return true;  // Light gray/white

        return false;
    }

    private static bool ColorsClose(Color a, Color b, float tolerance)
    {
        return Math.Abs(a.R - b.R) < tolerance && Math.Abs(a.G - b.G) < tolerance && Math.Abs(a.B - b.B) < tolerance;
    }

    public static SpriteFrames BuildSpriteFrames(Dictionary<string, Texture2D[]> anims, float fps = 8.0f)
    {
        var sf = new SpriteFrames(); sf.RemoveAnimation("default");
        foreach (var a in anims)
        {
            sf.AddAnimation(a.Key);
            sf.SetAnimationSpeed(a.Key, fps);
            bool shouldLoop = !a.Key.Contains("attack") && a.Key != "die";
            sf.SetAnimationLoop(a.Key, shouldLoop);
            foreach (var t in a.Value) sf.AddFrame(a.Key, t);
            GD.Print($"Animation '{a.Key}': {a.Value.Length} frames, fps={fps}, loop={shouldLoop}");
        }
        return sf;
    }

    private static SpriteFrames CreateFallbackSprites(Color c, bool p)
    {
        var tex = CreateColoredRect(40, 50, c);
        var anims = p ? new Dictionary<string, Texture2D[]> { { "idle", new[] { tex } }, { "run", new[] { tex } }, { "attack", new[] { tex } } }
                      : new Dictionary<string, Texture2D[]> { { "walk", new[] { tex } }, { "attack", new[] { tex } } };
        return BuildSpriteFrames(anims);
    }

    public static ImageTexture CreateColoredRect(int w, int h, Color c)
    {
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8); img.Fill(c);
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>
    /// Xóa cache để force reload sprites mới
    /// </summary>
    public static void ClearCache()
    {
        _cachedPlayerFrames = null;
    }

    /// <summary>
    /// Tạo animation chạy từ sprite idle bằng cách nghiêng và dịch chuyển
    /// </summary>
    private static List<Texture2D> CreateRunAnimationFromIdle(Image sourceImage, Rect2I charRect)
    {
        var frames = new List<Texture2D>();

        // Cắt nhân vật ra
        int cw = charRect.Size.X;
        int ch = charRect.Size.Y;
        var charImg = Image.CreateEmpty(cw, ch, false, Image.Format.Rgba8);

        for (int x = 0; x < cw; x++)
        {
            for (int y = 0; y < ch; y++)
            {
                int sx = charRect.Position.X + x;
                int sy = charRect.Position.Y + y;
                if (sx >= 0 && sy >= 0 && sx < sourceImage.GetWidth() && sy < sourceImage.GetHeight())
                {
                    charImg.SetPixel(x, y, sourceImage.GetPixel(sx, sy));
                }
            }
        }

        // Tạo 6 frames với các biến đổi khác nhau để giả lập chạy
        // Frame 0: nghiêng phải nhẹ + dịch xuống
        // Frame 1: thẳng + dịch lên
        // Frame 2: nghiêng trái nhẹ + dịch xuống
        // Frame 3: thẳng + dịch lên (lặp lại pattern)
        // Frame 4: nghiêng phải
        // Frame 5: nghiêng trái

        float[] rotations = { 0.05f, 0.0f, -0.05f, 0.0f, 0.03f, -0.03f };
        int[] yOffsets = { 3, -2, 3, -2, 1, 1 };
        int[] xOffsets = { 2, 0, -2, 0, 1, -1 };

        int frameSize = 240;

        for (int i = 0; i < 6; i++)
        {
            var frame = Image.CreateEmpty(frameSize, frameSize, false, Image.Format.Rgba8);

            // Tính toán vị trí đặt nhân vật (căn giữa dưới)
            int destX = (frameSize - cw) / 2 + xOffsets[i];
            int destY = frameSize - ch - 5 + yOffsets[i];

            // Copy với biến đổi nhẹ
            float rotation = rotations[i];
            float cosR = Mathf.Cos(rotation);
            float sinR = Mathf.Sin(rotation);
            int centerX = cw / 2;
            int centerY = ch;  // Rotate around feet

            for (int x = 0; x < cw; x++)
            {
                for (int y = 0; y < ch; y++)
                {
                    // Apply rotation around center bottom
                    int rx = x - centerX;
                    int ry = y - centerY;
                    int newX = (int)(rx * cosR - ry * sinR + centerX);
                    int newY = (int)(rx * sinR + ry * cosR + centerY);

                    if (newX >= 0 && newX < cw && newY >= 0 && newY < ch)
                    {
                        Color pixel = charImg.GetPixel(newX, newY);
                        if (pixel.A > 0.1f)
                        {
                            int fx = destX + x;
                            int fy = destY + y;
                            if (fx >= 0 && fx < frameSize && fy >= 0 && fy < frameSize)
                            {
                                frame.SetPixel(fx, fy, pixel);
                            }
                        }
                    }
                }
            }

            frames.Add(ImageTexture.CreateFromImage(frame));
        }

        return frames;
    }
}
