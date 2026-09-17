using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PhotoCat
{
    internal sealed partial class PetWindow
    {
        private BitmapSource SaveSpriteFrame(string path)
        {
            // Render the root, then crop: rendering a positioned child directly also
            // applies its parent offset, which can truncate the captured paws or tail.
            scene.UpdateLayout();
            RenderTargetBitmap target = new RenderTargetBitmap((int)Math.Ceiling(Width),
                (int)Math.Ceiling(Height), 96, 96, PixelFormats.Pbgra32);
            target.Render(scene);
            Point origin = cat.TransformToAncestor(scene).Transform(new Point());
            BitmapSource frame = new CroppedBitmap(target, new Int32Rect((int)Math.Round(origin.X),
                (int)Math.Round(origin.Y), (int)Math.Ceiling(cat.Width), (int)Math.Ceiling(cat.Height)));
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frame));
            using (Stream file = File.Create(path)) encoder.Save(file);
            return frame;
        }
        private void VerifySpriteActions(string output, List<string> checks)
        {
            string folder = Path.Combine(output, "sprites");
            Directory.CreateDirectory(folder);
            int index = looks.FindIndex(delegate(PhotoLook look) { return look.Id == "animated"; });
            Check(index >= 0 && new PetPreferences().LookId == "animated", "A fresh install selects the new animated look", checks);
            SelectLook(index, false);
            motion.SetAutomatic(false);
            animate = false;
            ApplySize(320, false); scene.UpdateLayout();
            bubble.Visibility = Visibility.Collapsed;
            Check(cat.SpriteMode && motion.SpriteActions, "Selecting the animated look enables the sprite renderer and expanded routine", checks);
            double width = Width, height = Height;
            for (int row = 0; row < SpriteAtlas.Counts.Length; row++)
            {
                for (int frame = 0; frame < SpriteAtlas.Counts[row]; frame++)
                {
                    MotionPose pose = new MotionPose { Sprite = (SpriteAction)row, SpriteFrame = frame,
                        Posture = row == 1 || row == 2 ? CatPosture.Walk : row == 4 ? CatPosture.Stretch
                            : row >= 7 ? CatPosture.Rest : CatPosture.Sit,
                        WalkFrame = frame, FaceLeft = row == 2 };
                    cat.SetPose(pose); scene.UpdateLayout();
                    string file = Path.Combine(folder, ((SpriteAction)row).ToString() + "-" + frame + ".png");
                    BitmapSource painted = SaveSpriteFrame(file);
                    BitmapSource bgra = new FormatConvertedBitmap(painted, PixelFormats.Bgra32, null, 0);
                    byte[] rendered = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
                    bgra.CopyPixels(rendered, bgra.PixelWidth * 4, 0);
                    int solid = 0, clear = 0;
                    for (int p = 3; p < rendered.Length; p += 4) { if (rendered[p] > 200) solid++; if (rendered[p] == 0) clear++; }
                    if (solid < 500 || clear < rendered.Length / 8) throw new InvalidOperationException("A rendered sprite vanished or lost transparency.");
                    for (int y = 0; y < bgra.PixelHeight; y++)
                        for (int x = 0; x < bgra.PixelWidth; x++)
                            if ((x == 0 || y == 0 || x == bgra.PixelWidth - 1 || y == bgra.PixelHeight - 1)
                                && rendered[(y * bgra.PixelWidth + x) * 4 + 3] >= 32)
                                throw new InvalidOperationException("A rendered sprite was clipped by its canvas.");
                    BitmapSource sourceFrame = cat.Sprites.Frame(pose);
                    byte[] pixels = new byte[192 * 208 * 4]; sourceFrame.CopyPixels(pixels, 192 * 4, 0);
                    for (int y = 3; y < 208; y += 11)
                        for (int x = 3; x < 192; x += 11)
                        {
                            Rect rect = SpriteAtlas.CanvasRect;
                            Point local = new Point((rect.X + (x + 0.5) * rect.Width / 192) * cat.ActualWidth / 953,
                                (rect.Y + (y + 0.5) * rect.Height / 208) * cat.ActualHeight / 1347);
                            if (cat.IsPhotoPixel(local) != (pixels[(y * 192 + x) * 4 + 3] >= 30))
                                throw new InvalidOperationException("Sprite mouse hit-test differs from its visible frame.");
                        }
                    if (Width != width || Height != height) throw new InvalidOperationException("A sprite frame resized the desktop window.");
                }
                Check(true, "Every " + ((SpriteAction)row).ToString() + " frame renders unclipped with real alpha, correct mouse hits and fixed window size", checks);
            }
            for (int row = 0; row < SpriteAtlas.Counts.Length; row++)
            {
                HashSet<BitmapSource> displayed = new HashSet<BitmapSource>();
                for (int frame = 0; frame < SpriteAtlas.DisplayCount(row); frame++)
                {
                    MotionPose pose = new MotionPose { Sprite = (SpriteAction)row,
                        Posture = row == 1 || row == 2 ? CatPosture.Walk : CatPosture.Sit,
                        FaceLeft = row == 2, WalkFrame = frame / SpriteAtlas.Steps[row],
                        SpriteFrame = frame / SpriteAtlas.Steps[row],
                        SpriteTween = (frame % SpriteAtlas.Steps[row] + 0.001) / SpriteAtlas.Steps[row] };
                    BitmapSource image = cat.Sprites.Frame(pose);
                    displayed.Add(image);
                    byte[] pixels = new byte[192 * 208 * 4]; image.CopyPixels(pixels, 192 * 4, 0);
                    cat.SetPose(pose);
                    for (int y = 5; y < 208; y += 13)
                        for (int x = 5; x < 192; x += 13)
                        {
                            Rect r = SpriteAtlas.CanvasRect;
                            Point local = new Point((r.X + (x + 0.5) * r.Width / 192) * cat.ActualWidth / 953,
                                (r.Y + (y + 0.5) * r.Height / 208) * cat.ActualHeight / 1347);
                            if (cat.IsPhotoPixel(local) != (pixels[(y * 192 + x) * 4 + 3] >= 30))
                                throw new InvalidOperationException("An interpolated silhouette disagrees with mouse hit-testing.");
                        }
                }
                Check(displayed.Count == SpriteAtlas.DisplayCount(row), "All continuous " + ((SpriteAction)row)
                    + " frames are reachable with matching alpha hits (" + displayed.Count + " frames)", checks);
            }
            MotionPose left = new MotionPose { Posture = CatPosture.Walk, FaceLeft = true, WalkFrame = 3 };
            MotionPose right = new MotionPose { Posture = CatPosture.Walk, FaceLeft = false, WalkFrame = 3 };
            Check(cat.Sprites.ActionFor(left) == SpriteAction.WalkLeft && cat.Sprites.ActionFor(right) == SpriteAction.WalkRight
                && !Object.ReferenceEquals(cat.Sprites.Frame(left), cat.Sprites.Frame(right)),
                "Left and right walking select distinct authored rows, without mirroring or reversing frame order", checks);

            PetMotion behavior = new PetMotion(45);
            behavior.SetSpriteActions(true); behavior.SetAutomatic(false);
            foreach (SpriteAction action in new SpriteAction[] { SpriteAction.Wave, SpriteAction.Groom, SpriteAction.Look })
            {
                behavior.Gesture(action);
                HashSet<int> visited = new HashSet<int>();
                for (int tick = 0; tick < 60; tick++)
                {
                    MotionPose pose = behavior.Advance(0.1);
                    if (pose.Sprite == action) visited.Add(pose.SpriteFrame);
                }
                Check(visited.Count == SpriteAtlas.Counts[(int)action] && behavior.Activity == CatActivity.Companion,
                    action.ToString() + " plays every frame once and returns to the selected idle look", checks);
            }
            behavior.Sleep(true);
            HashSet<int> settle = new HashSet<int>();
            HashSet<int> asleep = new HashSet<int>();
            for (int i = 0; i < 400; i++)
            {
                MotionPose pose = behavior.Advance(0.1);
                if (pose.Sprite == SpriteAction.LieDown) settle.Add(pose.SpriteFrame);
                if (pose.Sprite == SpriteAction.Sleep) asleep.Add(pose.SpriteFrame);
            }
            Check(settle.Count == 6 && asleep.Count == 6 && behavior.IsSleeping,
                "Sleeping plays the six-frame lie-down once, then holds a sleep loop until woken", checks);
            behavior.Wake();
            bool reversed = behavior.Advance(0).SpriteFrame == 5, stretched = false;
            for (int i = 0; i < 65; i++) stretched |= behavior.Advance(0.1).Sprite == SpriteAction.Stretch;
            Check(reversed && stretched && behavior.Activity == CatActivity.Companion,
                "Waking reverses the lie-down transition, stretches and returns to idle", checks);

            behavior.Sleep(true);
            for (int i = 0; i < 8; i++) behavior.Advance(0.1);
            int interruptedFrame = behavior.Advance(0).SpriteFrame;
            behavior.Wake();
            Check(behavior.Advance(0).SpriteFrame == interruptedFrame,
                "Waking midway through lying down continues from the visible frame", checks);

            behavior = new PetMotion(45); behavior.SetSpriteActions(true);
            HashSet<CatActivity> activities = new HashSet<CatActivity>();
            for (int i = 0; i < 3600; i++) { behavior.Advance(0.1); activities.Add(behavior.Activity); }
            Check(activities.Contains(CatActivity.Walking) && activities.Contains(CatActivity.Waving)
                && activities.Contains(CatActivity.Grooming) && activities.Contains(CatActivity.Looking)
                && activities.Contains(CatActivity.Stretching) && activities.Contains(CatActivity.Sleeping),
                "Unattended animation includes walking, waving, grooming, looking, stretching and sleeping", checks);

            motion.ReturnToCompanion(); motion.SetAutomatic(false);
            SpriteGesture(SpriteAction.Groom);
            for (int i = 0; i < 12; i++) cat.SetPose(motion.Advance(0.1));
            MotionPose frozen = cat.Pose;
            animate = false; Tick();
            Check(cat.Pose.Sprite == frozen.Sprite && cat.Pose.SpriteFrame == frozen.SpriteFrame, "Pause freezes the exact new action frame", checks);
            animate = true; moving = true; Tick(); moving = false;
            Check(cat.Pose.SpriteFrame == frozen.SpriteFrame, "Dragging pauses the new frame sequence", checks);
            menuOpen = true; Tick(); menuOpen = false;
            Check(cat.Pose.SpriteFrame == frozen.SpriteFrame, "An open menu pauses the new frame sequence", checks);
            HidePet(); Reveal(); timer.Stop();
            Check(cat.Pose.SpriteFrame == frozen.SpriteFrame, "Hiding and restoring preserve the new action frame", checks);

            foreach (bool faceLeft in new bool[] { false, true })
            {
                walkingDirection = faceLeft ? -1 : 1;
                pendingWalkingDirection = 0; walkingRemainder = 0; Left = 500;
                motion.ReturnToCompanion(); WalkPet();
                double before = Left;
                AdvanceWalkingPosition(new Rect(0, 0, 1600, 1000), 40);
                MotionPose pose = motion.Advance(0); pose.FaceLeft = walkingDirection < 0; cat.SetPose(pose);
                Check((faceLeft ? Left < before : Left > before)
                    && cat.Sprites.ActionFor(cat.Pose) == (faceLeft ? SpriteAction.WalkLeft : SpriteAction.WalkRight),
                    "Desktop travel agrees with the visible " + (faceLeft ? "left" : "right") + " walking row", checks);
            }
            SelectLook(0, false);
            Check(!cat.SpriteMode && !motion.SpriteActions, "Original-photo looks retain their calibrated local motion renderer", checks);
            SelectLook(index, false);
            motion.SetAutomatic(false); motion.ReturnToCompanion();

            HashSet<int> liveFrames = new HashSet<int>();
            int liveTicks = 0;
            DispatcherFrame loop = new DispatcherFrame();
            EventHandler observe = delegate
            {
                liveTicks++;
                if (cat.Pose.Sprite == SpriteAction.Wave) liveFrames.Add(cat.Pose.SpriteFrame);
                if (liveFrames.Count == 4) loop.Continue = false;
            };
            // Advance caps long ticks at 100 ms. Wait for observed frames, not a fixed FPS,
            // after the large software-rendered regression suite has stressed the dispatcher.
            DispatcherTimer finish = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            finish.Tick += delegate { loop.Continue = false; };
            SpriteGesture(SpriteAction.Wave);
            timer.Tick += observe;
            timer.Start(); finish.Start();
            try { Dispatcher.PushFrame(loop); }
            finally { finish.Stop(); timer.Stop(); timer.Tick -= observe; animate = false; }
            Check(liveFrames.Count == 4, "The live WPF timer visibly advances the new gesture frames (ticks=" + liveTicks + ", frames=" + liveFrames.Count + ", visible=" + IsVisible + ", activity=" + motion.Activity + ")", checks);
            VerifySpriteRenderClock(folder, checks);
            motion.ReturnToCompanion(); cat.SetPose(motion.Advance(0));
            SaveElement(scene, (int)Math.Ceiling(Width), (int)Math.Ceiling(Height), Path.Combine(folder, "desktop-pet.png"));
        }
        private void VerifySpriteRenderClock(string folder, List<string> checks)
        {
            List<double> intervals = new List<double>();
            HashSet<int> frames = new HashSet<int>();
            int ticks = 0;
            TimeSpan previous = TimeSpan.MinValue;
            DispatcherFrame loop = new DispatcherFrame();
            DispatcherTimer finish = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            finish.Tick += delegate { loop.Continue = false; };
            EventHandler observe = delegate(object sender, EventArgs args)
            {
                TimeSpan time = ((RenderingEventArgs)args).RenderingTime;
                if (time == previous) return;
                if (previous != TimeSpan.MinValue) intervals.Add((time - previous).TotalMilliseconds);
                previous = time; ticks++;
                if (cat.Pose.Posture == CatPosture.Walk) frames.Add(cat.Sprites.FrameFor(cat.Pose));
            };
            double start = Left;
            verifyingRenderClock = true;
            motion.ReturnToCompanion(); motion.SetAutomatic(false);
            WalkPet();
            CompositionTarget.Rendering += observe;
            finish.Start();
            try { Dispatcher.PushFrame(loop); }
            finally { finish.Stop(); CompositionTarget.Rendering -= observe; verifyingRenderClock = false; animate = false; }
            intervals.Sort();
            double median = intervals.Count == 0 ? 0 : intervals[intervals.Count / 2];
            double p95 = intervals.Count == 0 ? 0 : intervals[(int)((intervals.Count - 1) * 0.95)];
            Check(ticks >= 35 && frames.Count >= 24, "Display clock advances continuous walking (ticks=" + ticks
                + ", distinct frames=" + frames.Count + ", median ms=" + median.ToString("F2") + ", p95 ms=" + p95.ToString("F2") + ")", checks);
            Check(Math.Abs(Left - start) > 5, "The display clock moves the real desktop window together with the gait", checks);
            motion.ReturnToCompanion(); motion.SetAutomatic(false);
            for (int i = 0; i < 96; i++)
            {
                MotionPose pose = new MotionPose { Posture = CatPosture.Walk, WalkFrame = (i / 4) % 8, SpriteTween = (i % 4) / 4.0 };
                cat.SetPose(pose);
                SaveSpriteFrame(Path.Combine(folder, "smooth-walk-" + i.ToString("D3") + ".png"));
            }
            Check(true, "Rendered three complete 32-frame walking cycles for visual inspection", checks);
        }
    }
}
