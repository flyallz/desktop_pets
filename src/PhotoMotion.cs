using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace PhotoCat
{
    // One continuous textured surface keeps the original fur and transparent silhouette.
    // The landmarks below belong to assets/cat.png, a 953 x 1347 photograph cutout.
    internal enum CatPosture { Sit, Stretch, Rest, Walk }
    internal enum CatActivity { Companion, Stretching, Sleeping, Waking, Walking, Waving, Grooming, Looking }

    internal struct MotionPose
    {
        internal double Blink, LeftEar, RightEar, Tail, Breath, Effort, ClosedEyes;
        internal CatPosture Posture;
        internal int WalkFrame;
        internal SpriteAction Sprite;
        internal int SpriteFrame;
        internal bool FaceLeft;
    }

    internal sealed partial class PhotoMotion : FrameworkElement
    {
        private readonly Viewport3D viewport = new Viewport3D();
        private readonly MeshGeometry3D mesh = new MeshGeometry3D();
        private readonly List<Point> rest = new List<Point>();
        private BitmapSource source;
        private GeometryModel3D model;
        private DrawingGroup closedEyes;
        private readonly Dictionary<CatPosture, Material> materials = new Dictionary<CatPosture, Material>();
        private readonly Dictionary<CatPosture, byte[]> alphaPixels = new Dictionary<CatPosture, byte[]>();
        private Material[] walkingMaterials;
        private byte[][] walkingAlpha;
        private MotionPose pose;
        private IdlePhotoMotion idleMotion;
        private double idleHeadTop = 56;
        internal double HeadTop { get { if (spriteMode) return sprites.HeadTop(pose); return pose.Posture == CatPosture.Sit ? idleHeadTop : pose.Posture == CatPosture.Walk ? 812 : pose.Posture == CatPosture.Stretch ? 877 : 959; } }
        internal MotionPose Pose { get { return pose; } }
        internal int VertexCount { get { return rest.Count; } }

        internal PhotoMotion()
        {
            AddVisualChild(viewport);
            AddLogicalChild(viewport);
            viewport.IsHitTestVisible = false;
            viewport.ClipToBounds = false;
        }

        internal void SetPhoto(BitmapSource bitmap)
        {
            source = bitmap;
            SaveAlpha(CatPosture.Sit, bitmap);
            double width = bitmap.PixelWidth, height = bitmap.PixelHeight;
            viewport.Camera = new OrthographicCamera(new Point3D(width / 2, -height / 2, 2000),
                new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), width);
            BuildSurface(null);
            ImageBrush photo = new ImageBrush(bitmap) { ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, 1, 1), TileMode = TileMode.None };
            photo.Freeze();
            DiffuseMaterial material = new DiffuseMaterial(photo);
            material.Freeze();
            viewport.Children.Add(new ModelVisual3D { Content = new AmbientLight(Colors.White) });
            materials[CatPosture.Sit] = material;
            model = new GeometryModel3D(mesh, material) { BackMaterial = material };
            viewport.Children.Add(new ModelVisual3D { Content = model });
            SetPose(new MotionPose());
        }

        private void BuildSurface(IdlePhotoMotion profile)
        {
            rest.Clear();
            double width = source.PixelWidth, height = source.PixelHeight;
            List<double> xs = profile == null ? Grid(width, 28, 410, 584, 4) : profile.Grid(width, true);
            List<double> ys = profile == null ? Grid(height, 28, 192, 268, 3) : profile.Grid(height, false);
            PointCollection uv = new PointCollection();
            Point3DCollection positions = new Point3DCollection();
            Int32Collection triangles = new Int32Collection();
            foreach (double y in ys)
                foreach (double x in xs)
                {
                    rest.Add(new Point(x, y));
                    positions.Add(new Point3D(x, -y, 0));
                    uv.Add(new Point(x / width, y / height));
                }
            for (int row = 0; row < ys.Count - 1; row++)
                for (int col = 0; col < xs.Count - 1; col++)
                {
                    int a = row * xs.Count + col, b = a + xs.Count;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            uv.Freeze(); triangles.Freeze();
            mesh.TextureCoordinates = uv;
            mesh.TriangleIndices = triangles;
            mesh.Positions = positions;
        }

        internal void SetIdlePhoto(BitmapSource bitmap, string lookId, IdlePhotoMotion customMotion = null)
        {
            SaveAlpha(CatPosture.Sit, bitmap);
            DiffuseMaterial material = new DiffuseMaterial(new ImageBrush(bitmap));
            material.Freeze();
            materials[CatPosture.Sit] = material;
            idleMotion = customMotion ?? IdlePhotoMotion.ForLook(lookId);
            BuildSurface(idleMotion);
            idleHeadTop = 56;
            if (idleMotion != null)
            {
                byte[] alpha = alphaPixels[CatPosture.Sit];
                for (int i = 0; i < alpha.Length; i++)
                    if (alpha[i] >= 128) { idleHeadTop = i / source.PixelWidth; break; }
            }
            SetPose(pose);
        }

        private void SaveAlpha(CatPosture posture, BitmapSource bitmap)
        {
            if (bitmap.PixelWidth != source.PixelWidth || bitmap.PixelHeight != source.PixelHeight)
                throw new InvalidOperationException("Posture photos must share the same canvas");
            byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            byte[] alpha = new byte[bitmap.PixelWidth * bitmap.PixelHeight];
            for (int i = 0; i < alpha.Length; i++) alpha[i] = pixels[i * 4 + 3];
            alphaPixels[posture] = alpha;
        }

        internal void AddPostures(BitmapSource stretch, BitmapSource restPhoto, BitmapSource sleeping)
        {
            SaveAlpha(CatPosture.Stretch, stretch);
            SaveAlpha(CatPosture.Rest, restPhoto);
            DiffuseMaterial stretching = new DiffuseMaterial(new ImageBrush(stretch));
            stretching.Freeze();
            materials[CatPosture.Stretch] = stretching;
            Rect canvas = new Rect(0, 0, source.PixelWidth, source.PixelHeight);
            DrawingGroup resting = new DrawingGroup();
            resting.Children.Add(new ImageDrawing(restPhoto, canvas));
            GeometryGroup eyes = new GeometryGroup();
            eyes.Children.Add(new EllipseGeometry(new Point(621, 1105), 47, 37));
            eyes.Children.Add(new EllipseGeometry(new Point(724, 1117), 47, 37));
            eyes.Freeze();
            // Only the registered eyelid patches dissolve. The body and its outline stay unchanged.
            closedEyes = new DrawingGroup { ClipGeometry = eyes, Opacity = 0 };
            closedEyes.Children.Add(new ImageDrawing(sleeping, canvas));
            resting.Children.Add(closedEyes);
            materials[CatPosture.Rest] = new DiffuseMaterial(new DrawingBrush(resting));
        }

        internal void AddWalking(BitmapSource[] frames)
        {
            if (frames.Length != 8) throw new InvalidOperationException("Walking needs eight registered frames");
            walkingMaterials = new Material[8];
            walkingAlpha = new byte[8][];
            for (int i = 0; i < frames.Length; i++)
            {
                SaveAlpha(CatPosture.Walk, frames[i]);
                walkingAlpha[i] = alphaPixels[CatPosture.Walk];
                DiffuseMaterial material = new DiffuseMaterial(new ImageBrush(frames[i]));
                material.Freeze();
                walkingMaterials[i] = material;
            }
        }

        internal bool IsPhotoPixel(Point local)
        {
            Point original = SourcePoint(local);
            if (spriteMode) return sprites.Hit(original, pose);
            int x = (int)Math.Floor(original.X), y = (int)Math.Floor(original.Y);
            byte[] alpha = pose.Posture == CatPosture.Walk ? walkingAlpha[pose.WalkFrame] : alphaPixels[pose.Posture];
            return x >= 0 && y >= 0 && x < source.PixelWidth && y < source.PixelHeight
                && alpha[y * source.PixelWidth + x] >= 30;
        }

        private static List<double> Grid(double end, int step, int detailStart, int detailEnd, int detailStep)
        {
            SortedSet<double> values = new SortedSet<double>();
            for (int value = 0; value < end; value += step) values.Add(value);
            for (int value = detailStart; value <= detailEnd; value += detailStep) values.Add(value);
            values.Add(end);
            return new List<double>(values);
        }

        internal void SetPose(MotionPose value)
        {
            if (spriteMode)
            {
                bool changed = sprites.ActionFor(pose) != sprites.ActionFor(value) || sprites.FrameFor(pose) != sprites.FrameFor(value);
                pose = value;
                if (changed) InvalidateVisual();
                return;
            }
            Material material = value.Posture == CatPosture.Walk ? walkingMaterials[value.WalkFrame] : materials[value.Posture];
            if (model.Material != material)
            {
                model.Material = material;
                model.BackMaterial = material;
            }
            if (closedEyes != null) closedEyes.Opacity = value.ClosedEyes;
            pose = value;
            Point3DCollection positions = new Point3DCollection(rest.Count);
            foreach (Point point in rest)
            {
                Point moved = value.Posture == CatPosture.Sit && idleMotion != null ? idleMotion.Map(point, value) : Map(point, value);
                positions.Add(new Point3D(moved.X, -moved.Y, 0));
            }
            positions.Freeze();
            mesh.Positions = positions;
        }

        private static double Smooth(double value)
        {
            double t = Math.Max(0, Math.Min(1, value));
            return t * t * (3 - 2 * t);
        }

        private static double Falloff(double value, double inner, double outer)
        {
            return 1 - Smooth((Math.Abs(value) - inner) / (outer - inner));
        }

        internal static Point Map(Point point, MotionPose value)
        {
            if (value.Posture == CatPosture.Walk)
                return value.FaceLeft ? new Point(953 - point.X, point.Y) : point;
            if (value.Posture != CatPosture.Sit) return MapPosture(point, value);
            double x = point.X, y = point.Y;
            double dx = 0, dy = 0;
            // Chest expansion stays above the paws; neither the head nor the whole photo bobs.
            double chest = Falloff(x - 440, 60, 340) * Falloff(y - 560, 30, 210);
            dx += (x - 440) * 0.012 * chest * value.Breath;
            dy -= 1.5 * chest * value.Breath;
            // Only the isolated end of this curled tail moves. Its root and both paws stay still.
            double tail = Smooth((y - 1020) / 235) * Falloff(x - 300, 150, 280)
                * (1 - Smooth((y - 1300) / 47));
            dx += 24 * tail * value.Tail;
            dy += 9 * tail * value.Tail;
            Ear(x, y, 397, 168, value.LeftEar, ref dx, ref dy);
            Ear(x, y, 605, 174, -value.RightEar, ref dx, ref dy);
            // Closing the eye compresses its iris into the lid line and draws adjacent fur over it.
            dy += Eye(x, y, 444, 230, 0.43, value.Blink);
            dy += Eye(x, y, 550, 233, -0.34, value.Blink);
            return new Point(x + dx, y + dy);
        }

        private static Point MapPosture(Point point, MotionPose value)
        {
            double x = point.X, y = point.Y, dx = 0, dy = 0;
            if (value.Posture == CatPosture.Stretch)
            {
                double back = Falloff(x - 350, 50, 240) * Falloff(y - 790, 20, 175);
                double shoulders = Falloff(x - 630, 45, 190) * Falloff(y - 1045, 20, 165);
                dx = -10 * back * value.Effort + 4 * shoulders * value.Effort;
                dy = -16 * back * value.Effort + 5 * shoulders * value.Effort - 2 * shoulders * value.Breath;
            }
            else
            {
                double body = Falloff(x - 405, 80, 365) * Falloff(y - 1055, 30, 195);
                dy = -3.5 * body * value.Breath;
                dx = (x - 405) * 0.004 * body * value.Breath;
            }
            return new Point(x + dx, y + dy);
        }

        private static void Ear(double x, double y, double rootX, double rootY,
            double amount, ref double dx, ref double dy)
        {
            if (amount == 0 || y >= rootY || Math.Abs(x - rootX) >= 105) return;
            double weight = Smooth((rootY - y) / 100) * Smooth(y / 45)
                * Falloff(x - rootX, 35, 105);
            double angle = 0.065 * amount * weight;
            double px = x - rootX, py = y - rootY;
            dx += px * (Math.Cos(angle) - 1) - py * Math.Sin(angle);
            dy += px * Math.Sin(angle) + py * (Math.Cos(angle) - 1);
        }

        private static double Eye(double x, double y, double cx, double cy, double slope, double blink)
        {
            if (blink == 0 || Math.Abs(x - cx) >= 47) return 0;
            double horizontal = Falloff(x - cx, 28, 47);
            double offset = y - (cy + slope * (x - cx));
            if (Math.Abs(offset) >= 57) return 0;
            double vertical = Falloff(offset, 20, 57);
            double arc = 3 * Math.Max(0, 1 - Math.Pow((x - cx) / 30, 2));
            double lidShift = 5 + arc - slope * 0.45 * (x - cx);
            return (lidShift - offset * 0.91) * horizontal * vertical * blink;
        }

        // Invert the actual triangle under the cursor, including a moving transparent tail edge.
        internal Point SourcePoint(Point local)
        {
            if (source == null || ActualWidth <= 0 || ActualHeight <= 0) return new Point(-1, -1);
            if (local.X < 0 || local.Y < 0 || local.X >= ActualWidth || local.Y >= ActualHeight)
                return new Point(-1, -1);
            Point target = new Point(local.X * source.PixelWidth / ActualWidth,
                local.Y * source.PixelHeight / ActualHeight);
            if (spriteMode) return target;
            Int32Collection indices = mesh.TriangleIndices;
            Point3DCollection positions = mesh.Positions;
            for (int i = 0; i < indices.Count; i += 3)
            {
                int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                Point a = Flat(positions[ia]), b = Flat(positions[ib]), c = Flat(positions[ic]);
                if (target.X < Math.Min(a.X, Math.Min(b.X, c.X)) || target.X > Math.Max(a.X, Math.Max(b.X, c.X))
                    || target.Y < Math.Min(a.Y, Math.Min(b.Y, c.Y)) || target.Y > Math.Max(a.Y, Math.Max(b.Y, c.Y))) continue;
                double determinant = Cross(b - a, c - a);
                double u = Cross(target - a, c - a) / determinant;
                double v = Cross(b - a, target - a) / determinant;
                if (u < -1e-8 || v < -1e-8 || u + v > 1 + 1e-8) continue;
                return rest[ia] + (rest[ib] - rest[ia]) * u + (rest[ic] - rest[ia]) * v;
            }
            return new Point(-1, -1);
        }

        private static Point Flat(Point3D point) { return new Point(point.X, -point.Y); }
        private static double Cross(Vector a, Vector b) { return a.X * b.Y - a.Y * b.X; }

        internal bool HasValidSurface()
        {
            if (spriteMode) return sprites != null;
            Int32Collection indices = mesh.TriangleIndices;
            for (int i = 0; i < indices.Count; i += 3)
            {
                Point a = Flat(mesh.Positions[indices[i]]), b = Flat(mesh.Positions[indices[i + 1]]),
                    c = Flat(mesh.Positions[indices[i + 2]]);
                // Source triangles face clockwise in screen coordinates; no folding or collapsed faces.
                double facing = pose.Posture == CatPosture.Walk && pose.FaceLeft ? -1 : 1;
                if (Cross(b - a, c - a) * facing >= -0.0001) return false;
            }
            return true;
        }

        protected override int VisualChildrenCount { get { return 1; } }
        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException("index");
            return viewport;
        }
        protected override Size MeasureOverride(Size availableSize) { viewport.Measure(availableSize); return viewport.DesiredSize; }
        protected override Size ArrangeOverride(Size finalSize) { viewport.Arrange(new Rect(finalSize)); return finalSize; }
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            if (spriteMode) RenderSprite(dc);
        }
    }

    // Motion time advances only while visible, unpaused and free of drag/menu interaction.
    internal sealed class PetMotion
    {
        internal const double WalkCycleSeconds = 1.12;
        internal const double WalkStridePixels = 110;
        private const double WalkRampSeconds = 0.56;
        private readonly Random random;
        private readonly Queue<CatActivity> routine = new Queue<CatActivity>();
        private double time, nextBlink, nextEar, nextTail, nextActivity = 6, walkDuration;
        private double walkingTime, turnPause, resumeTime;
        internal double WalkDistance { get; private set; }
        internal bool IsTurning { get { return Activity == CatActivity.Walking && turnPause > 0; } }
        private double blinkAt = -10, earAt = -10, tailAt = -10, petAt = -10, activityAt, wakingEyes = 1, settlingEyes;
        private bool leftEar, manualSleep;
        private int wakingSpriteFrame = 5;
        private CatActivity lastAutomatic;
        internal CatActivity Activity { get; private set; }
        internal bool Automatic { get; private set; }
        internal bool SpriteActions { get; private set; }
        internal bool IsSleeping { get { return Activity == CatActivity.Sleeping; } }
        internal PetMotion(int seed)
        {
            random = new Random(seed);
            Automatic = true;
            routine.Enqueue(CatActivity.Walking);
            routine.Enqueue(CatActivity.Stretching);
            routine.Enqueue(CatActivity.Sleeping);
            nextBlink = 1.8; nextEar = 4; nextTail = 1;
        }
        internal void SetSpriteActions(bool enabled)
        {
            if (SpriteActions == enabled) return;
            SpriteActions = enabled;
            routine.Clear();
            routine.Enqueue(CatActivity.Walking);
            if (enabled)
            {
                routine.Enqueue(CatActivity.Waving);
                routine.Enqueue(CatActivity.Grooming);
                routine.Enqueue(CatActivity.Looking);
            }
            routine.Enqueue(CatActivity.Stretching);
            routine.Enqueue(CatActivity.Sleeping);
        }
        internal void Gesture(SpriteAction action)
        {
            if (!SpriteActions) return;
            if (action == SpriteAction.Wave) Activity = CatActivity.Waving;
            else if (action == SpriteAction.Groom) Activity = CatActivity.Grooming;
            else if (action == SpriteAction.Look) Activity = CatActivity.Looking;
            else return;
            activityAt = time;
            WalkDistance = 0;
            turnPause = 0;
        }
        internal void SetAutomatic(bool enabled)
        {
            Automatic = enabled;
            if (!enabled) StopWalking();
            nextActivity = time + (enabled ? 3 : 8);
        }
        internal void Pet()
        {
            if (IsSleeping) { Wake(); return; }
            StopWalking();
            if (time - petAt >= 1.5) petAt = time;
            nextActivity = time + 8;
        }
        internal void Walk()
        {
            if (Activity == CatActivity.Walking) return;
            Activity = CatActivity.Walking;
            activityAt = time;
            walkDuration = random.Next(4, 7) * WalkCycleSeconds + WalkRampSeconds;
            walkingTime = 0; turnPause = 0; resumeTime = WalkRampSeconds;
        }
        internal void PauseForTurn()
        {
            if (Activity != CatActivity.Walking) return;
            turnPause = 0.32;
            resumeTime = 0;
        }
        // Integral of the speed curve: the same progress drives both gait and desktop travel.
        private double WalkingProgress(double elapsed)
        {
            double t = Math.Max(0, Math.Min(walkDuration, elapsed));
            if (t < WalkRampSeconds) return RampDistance(t);
            if (t > walkDuration - WalkRampSeconds)
                return walkDuration - WalkRampSeconds - RampDistance(walkDuration - t);
            return t - WalkRampSeconds / 2;
        }
        private static double RampDistance(double seconds)
        {
            double t = seconds / WalkRampSeconds;
            return WalkRampSeconds * (t * t * t - t * t * t * t / 2);
        }
        internal void StopWalking()
        {
            if (Activity == CatActivity.Walking) FinishActivity();
        }
        internal void ReturnToCompanion()
        {
            FinishActivity();
            WalkDistance = 0; turnPause = 0;
        }
        internal void AfterDrag()
        {
            StopWalking();
            nextActivity = time + 8;
        }
        private void FinishActivity()
        {
            Activity = CatActivity.Companion;
            nextActivity = time + 7 + random.NextDouble() * 6;
        }
        private void ChooseActivity()
        {
            if (routine.Count == 0)
            {
                CatActivity[] choices = SpriteActions
                    ? new CatActivity[] { CatActivity.Walking, CatActivity.Waving, CatActivity.Grooming, CatActivity.Looking, CatActivity.Stretching, CatActivity.Sleeping }
                    : new CatActivity[] { CatActivity.Walking, CatActivity.Stretching, CatActivity.Sleeping };
                for (int i = choices.Length - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    CatActivity old = choices[i]; choices[i] = choices[j]; choices[j] = old;
                }
                if (choices[0] == lastAutomatic)
                {
                    CatActivity old = choices[0]; choices[0] = choices[1]; choices[1] = old;
                }
                foreach (CatActivity choice in choices) routine.Enqueue(choice);
            }
            lastAutomatic = routine.Dequeue();
            if (lastAutomatic == CatActivity.Walking) Walk();
            else if (lastAutomatic == CatActivity.Stretching) Stretch();
            else if (lastAutomatic == CatActivity.Waving) Gesture(SpriteAction.Wave);
            else if (lastAutomatic == CatActivity.Grooming) Gesture(SpriteAction.Groom);
            else if (lastAutomatic == CatActivity.Looking) Gesture(SpriteAction.Look);
            else Sleep(false);
        }
        internal void Stretch()
        {
            if (Activity == CatActivity.Stretching) return;
            Activity = CatActivity.Stretching;
            activityAt = time;
        }
        internal void Sleep(bool untilWoken)
        {
            if (IsSleeping) return;
            settlingEyes = Activity == CatActivity.Waking ? wakingEyes * (1 - Smooth((time - activityAt) / 1.1)) : 0;
            Activity = CatActivity.Sleeping;
            activityAt = time;
            manualSleep = untilWoken;
        }
        internal void Wake()
        {
            if (!IsSleeping) return;
            wakingSpriteFrame = Math.Min(5, (int)((time - activityAt) / 1.5 * 6));
            wakingEyes = settlingEyes + (1 - settlingEyes) * Smooth((time - activityAt) / 0.9);
            Activity = CatActivity.Waking;
            activityAt = time;
        }
        internal MotionPose Advance(double seconds)
        {
            double delta = Math.Max(0, Math.Min(seconds, 0.1));
            time += delta;
            WalkDistance = 0;
            if (Activity == CatActivity.Walking)
            {
                if (IsTurning) turnPause = Math.Max(0, turnPause - delta);
                else
                {
                    double before = WalkingProgress(walkingTime);
                    resumeTime = Math.Min(WalkRampSeconds, resumeTime + delta);
                    walkingTime = Math.Min(walkDuration, walkingTime + delta * Smooth(resumeTime / WalkRampSeconds));
                    WalkDistance = (WalkingProgress(walkingTime) - before) * WalkStridePixels / WalkCycleSeconds;
                    if (walkingTime >= walkDuration) FinishActivity();
                }
            }
            double elapsed = time - activityAt;
            if (Activity == CatActivity.Stretching && elapsed >= 3.2) FinishActivity();
            if ((Activity == CatActivity.Waving && elapsed >= 2.4)
                || (Activity == CatActivity.Grooming && elapsed >= 4)
                || (Activity == CatActivity.Looking && elapsed >= 3)) FinishActivity();
            if (Activity == CatActivity.Sleeping && !manualSleep && elapsed >= 32) Wake();
            if (Activity == CatActivity.Waking && time - activityAt >= 1.4) Stretch();
            if (Activity == CatActivity.Companion && Automatic && time >= nextActivity) ChooseActivity();
            elapsed = time - activityAt;
            if (Activity == CatActivity.Walking)
                return new MotionPose { Posture = CatPosture.Walk,
                    WalkFrame = (int)(WalkingProgress(walkingTime) / WalkCycleSeconds * 8) % 8, Sprite = SpriteAction.WalkRight };
            if (Activity == CatActivity.Stretching)
                return new MotionPose { Posture = CatPosture.Stretch, Sprite = SpriteAction.Stretch, SpriteFrame = Math.Min(4, (int)(elapsed / 3.2 * 5)), Effort = Math.Sin(elapsed / 3.2 * Math.PI),
                    Breath = Math.Sin(time * Math.PI * 2 / 4.6) };
            if (Activity == CatActivity.Sleeping || Activity == CatActivity.Waking)
                return new MotionPose { Posture = CatPosture.Rest,
                    Sprite = Activity == CatActivity.Waking || elapsed < 1.5 ? SpriteAction.LieDown : SpriteAction.Sleep,
                    SpriteFrame = Activity == CatActivity.Waking ? Math.Max(0, wakingSpriteFrame - (int)(elapsed / 1.4 * (wakingSpriteFrame + 1)))
                        : elapsed < 1.5 ? Math.Min(5, (int)(elapsed / 1.5 * 6)) : (int)((elapsed - 1.5) * 2) % 6,
                    ClosedEyes = Activity == CatActivity.Sleeping ? settlingEyes + (1 - settlingEyes) * Smooth(elapsed / 0.9) : wakingEyes * (1 - Smooth(elapsed / 1.1)),
                    Breath = Math.Sin(time * Math.PI * 2 / 6.4) };
            if (Activity == CatActivity.Waving)
                return new MotionPose { Sprite = SpriteAction.Wave, SpriteFrame = Math.Min(3, (int)(elapsed / 2.4 * 4)) };
            if (Activity == CatActivity.Grooming)
                return new MotionPose { Sprite = SpriteAction.Groom, SpriteFrame = Math.Min(7, (int)(elapsed / 4 * 8)) };
            if (Activity == CatActivity.Looking)
                return new MotionPose { Sprite = SpriteAction.Look, SpriteFrame = Math.Min(5, (int)(elapsed / 3 * 6)) };
            if (time >= nextBlink) { blinkAt = time; nextBlink = time + 3.5 + random.NextDouble() * 4; }
            if (time >= nextEar) { earAt = time; leftEar = random.Next(2) == 0; nextEar = time + 8 + random.NextDouble() * 9; }
            if (time >= nextTail) { tailAt = time; nextTail = time + 8 + random.NextDouble() * 7; }
            double twitch = Pulse(time - earAt, 0.12, 0.04, 0.35);
            double tailTime = time - tailAt;
            double tail = tailTime >= 0 && tailTime < 3.8
                ? Math.Sin(tailTime / 3.8 * Math.PI) * Math.Sin(tailTime / 3.8 * Math.PI * 2) : 0;
            double blinkElapsed = time - blinkAt;
            double petElapsed = time - petAt;
            int idleFrame = petElapsed >= 0 && petElapsed < 1.5 ? Math.Min(5, (int)(petElapsed / 1.5 * 6))
                : blinkElapsed >= 0 && blinkElapsed < 0.9 ? Math.Min(5, (int)(blinkElapsed / 0.9 * 6)) : 0;
            return new MotionPose {
                Sprite = SpriteAction.Idle, SpriteFrame = idleFrame,
                Blink = Math.Max(Pulse(time - blinkAt, 0.10, 0.04, 0.18),
                    0.94 * Pulse(time - petAt, 0.42, 0.36, 0.72)),
                LeftEar = leftEar ? twitch : 0, RightEar = leftEar ? 0 : twitch,
                Tail = tail, Breath = Math.Sin(time * Math.PI * 2 / 4.6)
            };
        }
        private static double Smooth(double value)
        {
            double t = Math.Max(0, Math.Min(1, value));
            return t * t * (3 - 2 * t);
        }
        private static double Pulse(double elapsed, double close, double hold, double open)
        {
            if (elapsed < 0 || elapsed >= close + hold + open) return 0;
            double t = elapsed < close ? elapsed / close : elapsed < close + hold ? 1
                : 1 - (elapsed - close - hold) / open;
            return Smooth(t);
        }
    }
}
