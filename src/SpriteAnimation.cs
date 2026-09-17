using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoCat
{
    internal enum SpriteAction { Idle, WalkRight, WalkLeft, Wave, Stretch, Groom, Look, LieDown, Sleep }

    // Decode once. Timer ticks select an existing frame and never reload a file or resize a window.
    internal sealed class SpriteAtlas
    {
        internal const int FrameWidth = 192, FrameHeight = 208;
        internal static readonly int[] Counts = { 6, 8, 8, 4, 5, 8, 6, 6, 6 };
        internal static readonly int[] Steps = { 4, 4, 4, 16, 20, 16, 16, 8, 16 };
        private static readonly string[] Names = { "idle", "walk-right", "walk-left", "wave", "stretch", "groom", "look", "lie-down", "sleep" };
        internal static int DisplayCount(int row) { return (row <= 2 || row == 8) ? Counts[row] * Steps[row] : (Counts[row] - 1) * Steps[row] + 1; }
        private readonly BitmapSource[][] frames = new BitmapSource[9][];
        private readonly byte[][][] alpha = new byte[9][][];
        private readonly int[][] top = new int[9][];
        internal static readonly Rect CanvasRect = new Rect(0, 1291 - 196 * 953.0 / 192, 953, 208 * 953.0 / 192);

        internal SpriteAtlas(BitmapSource image, Func<string, BitmapSource> load)
        {
            if (image.PixelWidth != 1536 || image.PixelHeight != 1872)
                throw new InvalidDataException("The cat sprite atlas must be 1536 x 1872.");
            for (int row = 0; row < Counts.Length; row++)
            {
                BitmapSource strip = load("sprites.smooth." + Names[row] + ".png");
                int count = DisplayCount(row);
                if (strip.PixelWidth != 8 * FrameWidth || strip.PixelHeight != ((count + 7) / 8) * FrameHeight)
                    throw new InvalidDataException("Invalid transition strip for " + Names[row]);
                frames[row] = new BitmapSource[count];
                alpha[row] = new byte[count][];
                top[row] = new int[count];
                for (int col = 0; col < count; col++)
                {
                    BitmapSource frame = new CroppedBitmap(strip, new Int32Rect((col % 8) * FrameWidth, (col / 8) * FrameHeight, FrameWidth, FrameHeight));
                    frame.Freeze();
                    frames[row][col] = frame;
                    byte[] pixels = new byte[FrameWidth * FrameHeight * 4];
                    frame.CopyPixels(pixels, FrameWidth * 4, 0);
                    byte[] mask = new byte[FrameWidth * FrameHeight];
                    int solid = 0, clear = 0, first = FrameHeight;
                    for (int i = 0; i < mask.Length; i++)
                    {
                        mask[i] = pixels[i * 4 + 3];
                        if (mask[i] >= 128) { solid++; first = Math.Min(first, i / FrameWidth); }
                        if (mask[i] == 0) clear++;
                    }
                    if (solid < 1000 || clear < 1000) throw new InvalidDataException("A cat animation frame is empty or has an opaque background.");
                    alpha[row][col] = mask;
                    top[row][col] = first;
                }
            }
        }

        internal SpriteAction ActionFor(MotionPose pose)
        {
            return pose.Posture == CatPosture.Walk
                ? (pose.FaceLeft ? SpriteAction.WalkLeft : SpriteAction.WalkRight) : pose.Sprite;
        }
        internal int FrameFor(MotionPose pose)
        {
            int row = (int)ActionFor(pose);
            int index = pose.Posture == CatPosture.Walk ? pose.WalkFrame : pose.SpriteFrame;
            return Math.Max(0, Math.Min(DisplayCount(row) - 1, index * Steps[row] + (int)(Math.Max(0, Math.Min(0.999999, pose.SpriteTween)) * Steps[row])));
        }
        internal BitmapSource Frame(MotionPose pose) { return frames[(int)ActionFor(pose)][FrameFor(pose)]; }
        internal double HeadTop(MotionPose pose) { return CanvasRect.Y + top[(int)ActionFor(pose)][FrameFor(pose)] * 953.0 / FrameWidth; }
        internal bool Hit(Point canvasPoint, MotionPose pose)
        {
            int x = (int)Math.Floor((canvasPoint.X - CanvasRect.X) * FrameWidth / CanvasRect.Width);
            int y = (int)Math.Floor((canvasPoint.Y - CanvasRect.Y) * FrameHeight / CanvasRect.Height);
            return x >= 0 && x < FrameWidth && y >= 0 && y < FrameHeight
                && alpha[(int)ActionFor(pose)][FrameFor(pose)][y * FrameWidth + x] >= 30;
        }
        internal BitmapSource IdlePhoto()
        {
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen()) dc.DrawImage(frames[0][0], CanvasRect);
            RenderTargetBitmap target = new RenderTargetBitmap(953, 1347, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual); target.Freeze();
            BitmapSource image = new FormatConvertedBitmap(target, PixelFormats.Bgra32, null, 0);
            image.Freeze(); return image;
        }
    }

    internal sealed partial class PhotoMotion
    {
        private SpriteAtlas sprites;
        private bool spriteMode;
        internal bool SpriteMode { get { return spriteMode; } }
        internal SpriteAtlas Sprites { get { return sprites; } }
        internal void LoadSpriteAtlas(BitmapSource image, Func<string, BitmapSource> load) { sprites = new SpriteAtlas(image, load); }
        internal BitmapSource SpriteIdlePhoto { get { return sprites.IdlePhoto(); } }
        internal void SetSpriteMode(bool enabled)
        {
            if (enabled && sprites == null) throw new InvalidOperationException("Cat sprite frames are missing.");
            spriteMode = enabled;
            viewport.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
            SetPose(pose);
            InvalidateVisual();
        }
        private void RenderSprite(DrawingContext dc)
        {
            Rect r = SpriteAtlas.CanvasRect;
            dc.DrawImage(sprites.Frame(pose), new Rect(r.X * ActualWidth / 953, r.Y * ActualHeight / 1347,
                r.Width * ActualWidth / 953, r.Height * ActualHeight / 1347));
        }
    }

    internal sealed partial class PetWindow
    {
        private bool verifyingRenderClock;
        private TimeSpan lastCompositionTime = TimeSpan.MinValue;
        private bool UseSpriteRenderClock { get { return cat.SpriteMode && (!testing || verifyingRenderClock); } }
        private void RenderSprites(object sender, EventArgs args)
        {
            if (!UseSpriteRenderClock) return;
            System.Windows.Media.RenderingEventArgs frame = (System.Windows.Media.RenderingEventArgs)args;
            if (frame.RenderingTime == lastCompositionTime) return;
            lastCompositionTime = frame.RenderingTime;
            Tick();
        }
        private bool SpriteLook { get { return looks.Count > 0 && looks[selectedLook].Id == "animated"; } }
        private void SpriteGesture(SpriteAction action)
        {
            if (!SpriteLook) return;
            animate = true;
            motion.Gesture(action);
            lastTick = clock.Elapsed.TotalSeconds;
            Tick();
        }
    }
}
