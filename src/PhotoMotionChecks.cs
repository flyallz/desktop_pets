using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoCat
{
    internal sealed partial class PetWindow
    {
        private void VerifyPhotoLocalMotion(int index, string output, List<string> checks)
        {
            string id = looks[index].Id;
            string folder = Path.Combine(output, "motion-" + id);
            Directory.CreateDirectory(folder);
            IdlePhotoMotion profile = IdlePhotoMotion.ForLook(id);
            ApplySize(400, false); scene.UpdateLayout(); bubble.Visibility = Visibility.Collapsed;
            cat.SetPose(new MotionPose());
            byte[] neutral = SaveMotionFrame(Path.Combine(folder, "neutral.png"));
            MotionPose[] gestures = {
                new MotionPose { Blink = 0.5 }, new MotionPose { Blink = 1 },
                new MotionPose { LeftEar = 1 }, new MotionPose { RightEar = 1 },
                new MotionPose { Breath = 1 }, new MotionPose { Breath = -1 },
                new MotionPose { Tail = 1 }, new MotionPose { Tail = -1 }
            };
            string[] names = { "half-blink", "closed", "ear-left", "ear-right", "breathe-in", "breathe-out", "tail-left", "tail-right" };
            for (int g = 0; g < gestures.Length; g++)
            {
                cat.SetPose(gestures[g]);
                Check(cat.HasValidSurface(), "Photo motion has no folded mesh: " + id + "/" + names[g], checks);
                byte[] rendered = SaveMotionFrame(Path.Combine(folder, names[g] + ".png"));
                int changed = PixelChanges(neutral, rendered);
                bool visible = g < 2 ? profile.Eyes.Length > 0 : g >= 6 ? profile.Tail != null : true;
                Check(visible ? changed > 12 : changed == 0,
                    "Only visible anatomy animates: " + id + "/" + names[g] + " (changed pixels " + changed + ")", checks);
                foreach (Point anchor in profile.Anchors)
                    if ((profile.Map(anchor, gestures[g]) - anchor).Length > 0.01)
                        throw new InvalidOperationException("A photo motion moved a fixed nose/paw/crop anchor: " + id);
                if (g == 1 && profile.Eyes.Length > 0)
                {
                    foreach (IdlePhotoMotion.Eye eye in profile.Eyes)
                    {
                        Vector normal = new Vector(-eye.Sin, eye.Cos);
                        Point a = eye.Center - normal * eye.HalfOpening;
                        Point b = eye.Center + normal * eye.HalfOpening;
                        double ratio = (profile.Map(a, gestures[g]) - profile.Map(b, gestures[g])).Length / (2 * eye.HalfOpening);
                        Check(ratio < 0.15, "Blink closes along the photo's own eyelid direction: " + id, checks);
                    }
                }
            }
            checks.Add("PASS: Visible nose, feet and original crop anchors stay fixed during every local gesture: " + id);
            MotionPose together = new MotionPose { Blink = 1, LeftEar = 1, RightEar = 1, Tail = 1, Breath = 1 };
            cat.SetPose(together);
            byte[] all = SaveMotionFrame(Path.Combine(folder, "combined.png"));
            Check(cat.HasValidSurface(), "Overlapping local motions remain intact: " + id, checks);
            VerifyMovingPhotoHitTest(all, id, checks);

            Stopwatch rendering = Stopwatch.StartNew();
            for (int frame = 0; frame < 90; frame++)
            {
                double t = frame / 15.0;
                MotionPose pose = new MotionPose {
                    Breath = Math.Sin(t * Math.PI * 2 / 6),
                    Blink = DemoPulse(t, 1.2, 0.4, 0.2, 0.5),
                    LeftEar = DemoPulse(t, 3.0, 0.14, 0.04, 0.4),
                    RightEar = DemoPulse(t, 3.8, 0.14, 0.04, 0.4),
                    Tail = Math.Sin(t * Math.PI * 2 / 6) * Math.Pow(Math.Sin(t * Math.PI / 6), 2)
                };
                cat.SetPose(pose);
                if (!cat.HasValidSurface()) throw new InvalidOperationException("A continuous photo-motion frame folded: " + id);
                SaveMotionFrame(Path.Combine(folder, "frame-" + frame.ToString("D3") + ".png"));
            }
            checks.Add("PASS: Rendered 90 continuous WPF frames without tears or missing silhouettes: " + id);
            checks.Add("INFO: " + id + " mesh vertices=" + cat.VertexCount + "; offscreen render seconds=" + rendering.Elapsed.TotalSeconds.ToString("F2") + " (not screen FPS)");
            // The ordinary pet state machine must still supply these gestures, including a click blink.
            PetMotion behavior = new PetMotion(42); behavior.SetAutomatic(false); behavior.Pet();
            double blink = 0, tail = 0, ear = 0;
            for (int i = 0; i < 240; i++)
            {
                MotionPose pose = behavior.Advance(0.04);
                blink = Math.Max(blink, pose.Blink); tail = Math.Max(tail, Math.Abs(pose.Tail));
                ear = Math.Max(ear, Math.Max(pose.LeftEar, pose.RightEar));
                if (pose.Posture != CatPosture.Sit) throw new InvalidOperationException("Local photo animation started a whole-body activity with automatic activities off");
                cat.SetPose(pose);
            }
            Check(blink > 0.9 && tail > 0.5 && ear > 0.9,
                "Normal idle/click timing drives the new photo while automatic activities are off: " + id, checks);
            cat.SetPose(new MotionPose()); ApplySize(320, false); scene.UpdateLayout();
        }

        private static int PixelChanges(byte[] a, byte[] b)
        {
            int changes = 0;
            for (int i = 0; i < a.Length; i += 4)
                if (Math.Abs(a[i] - b[i]) + Math.Abs(a[i + 1] - b[i + 1]) + Math.Abs(a[i + 2] - b[i + 2])
                    + Math.Abs(a[i + 3] - b[i + 3]) > 8) changes++;
            return changes;
        }
        private static double DemoPulse(double time, double start, double close, double hold, double open)
        {
            double t = time - start;
            if (t <= 0 || t >= close + hold + open) return 0;
            double value = t < close ? t / close : t < close + hold ? 1 : 1 - (t - close - hold) / open;
            return value * value * (3 - 2 * value);
        }
        private void VerifyMovingPhotoHitTest(byte[] rendered, string id, List<string> checks)
        {
            BitmapSource photo = looks[selectedLook].Photo;
            byte[] pixels = new byte[953 * 1347 * 4]; photo.CopyPixels(pixels, 953 * 4, 0);
            int width = (int)Math.Ceiling(cat.Width), height = (int)Math.Ceiling(cat.Height);
            int checkedSolid = 0, checkedClear = 0, mismatches = 0;
            for (int y = 3; y < height - 3; y += 11)
                for (int x = 3; x < width - 3; x += 11)
                {
                    Point local = new Point(x + 0.5, y + 0.5);
                    Point source = cat.SourcePoint(local);
                    int sx = (int)Math.Floor(source.X), sy = (int)Math.Floor(source.Y);
                    if (sx < 3 || sy < 3 || sx >= 950 || sy >= 1344) continue;
                    bool solid = true, clear = true;
                    // Exclude antialiased fur boundaries from the GPU-vs-source alpha comparison.
                    for (int dy = -3; dy <= 3; dy++)
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            byte alpha = pixels[((sy + dy) * 953 + sx + dx) * 4 + 3];
                            solid &= alpha == 255; clear &= alpha == 0;
                        }
                    if (!solid && !clear) continue;
                    int shownAlpha = rendered[(y * width + x) * 4 + 3];
                    if (solid) { checkedSolid++; if (!cat.IsPhotoPixel(local) || shownAlpha < 240) mismatches++; }
                    if (clear) { checkedClear++; if (cat.IsPhotoPixel(local) || shownAlpha > 15) mismatches++; }
                }
            Check(checkedSolid > 20 && checkedClear > 20 && mismatches == 0,
                "Rendered moving pixels agree with mouse hit regions: " + id + " (mismatches=" + mismatches + ")", checks);
        }
    }
}
