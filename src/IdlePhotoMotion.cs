using System;
using System.Collections.Generic;
using System.Windows;

namespace PhotoCat
{
    // Landmarks are measured on the embedded 953 x 1347 original-photo cutouts.
    // No missing body parts are synthesized; each look moves only visible regions.
    internal sealed class IdlePhotoMotion
    {
        internal sealed class Eye
        {
            internal readonly Point Center;
            internal readonly double HalfLength, HalfOpening, Cos, Sin;
            internal Eye(double x, double y, double length, double opening, double degrees)
            {
                Center = new Point(x, y); HalfLength = length; HalfOpening = opening;
                Cos = Math.Cos(degrees * Math.PI / 180); Sin = Math.Sin(degrees * Math.PI / 180);
            }
            internal Vector Offset(Point p, double blink)
            {
                double x = p.X - Center.X, y = p.Y - Center.Y;
                double u = x * Cos + y * Sin, v = -x * Sin + y * Cos;
                double weight = Falloff(u, HalfLength, HalfLength + 40)
                    * Falloff(v, HalfOpening * 1.2, HalfOpening + 42);
                double arch = 2 * Math.Max(0, 1 - u * u / (HalfLength * HalfLength));
                double distance = (HalfOpening * 0.12 + arch - v * 0.86) * weight * blink;
                return new Vector(-Sin * distance, Cos * distance);
            }
        }
        internal sealed class Part
        {
            internal readonly Point Root, Tip;
            internal readonly double Width, Length;
            private readonly Vector axis;
            internal Part(double x, double y, double tx, double ty, double width)
            {
                Root = new Point(x, y); Tip = new Point(tx, ty); Width = width;
                axis = Tip - Root; Length = axis.Length; axis /= Length;
            }
            private double Weight(Point p)
            {
                Vector delta = p - Root;
                double along = Vector.Multiply(delta, axis) / Length;
                double across = delta.X * -axis.Y + delta.Y * axis.X;
                return Smooth(along) * Falloff(across, Width * 0.5, Width)
                    * (1 - Smooth((along - 1.2) / 0.45));
            }
            internal Vector Twitch(Point p, double amount)
            {
                double angle = 0.055 * amount * Weight(p);
                if (angle == 0) return new Vector();
                Vector delta = p - Root;
                double c = Math.Cos(angle), s = Math.Sin(angle);
                return new Vector(delta.X * (c - 1) - delta.Y * s, delta.X * s + delta.Y * (c - 1));
            }
            internal Vector Sway(Point p, double amount)
            {
                double distance = 12 * amount * Weight(p);
                return new Vector(-axis.Y * distance, axis.X * distance);
            }
        }

        internal Eye[] Eyes = new Eye[0];
        internal Part LeftEar, RightEar, Tail;
        internal Point Chest;
        internal double ChestWidth, ChestHeight;
        internal Point[] Anchors;

        internal static IdlePhotoMotion ForLook(string id)
        {
            if (id == "original") return null;
            switch (id)
            {
                case "sweater": return new IdlePhotoMotion {
                    Eyes = new Eye[] { new Eye(478, 331, 46, 27, 25), new Eye(635, 332, 45, 27, -25) },
                    LeftEar = new Part(352, 234, 300, 80, 102), RightEar = new Part(708, 235, 772, 86, 104),
                    Chest = new Point(475, 785), ChestWidth = 245, ChestHeight = 270,
                    Anchors = new Point[] { new Point(450, 1225), new Point(641, 1070), new Point(572, 420) } };
                case "tilt": return new IdlePhotoMotion {
                    Eyes = new Eye[] { new Eye(604, 694, 47, 30, 131), new Eye(526, 866, 43, 31, 83) },
                    LeftEar = new Part(640, 994, 678, 1109, 82), RightEar = new Part(751, 714, 804, 660, 49),
                    Chest = new Point(444, 1155), ChestWidth = 305, ChestHeight = 128,
                    Anchors = new Point[] { new Point(440, 1304), new Point(454, 726) } };
                case "sofa": return new IdlePhotoMotion {
                    Eyes = new Eye[] { new Eye(151, 554, 23, 18, 26), new Eye(258, 554, 27, 17, -30) },
                    LeftEar = new Part(159, 507, 106, 419, 56), RightEar = new Part(344, 506, 390, 415, 57),
                    Tail = new Part(699, 1206, 871, 1255, 62),
                    Chest = new Point(550, 976), ChestWidth = 286, ChestHeight = 230,
                    Anchors = new Point[] { new Point(237, 1211), new Point(203, 595) } };
                case "belly": return new IdlePhotoMotion {
                    LeftEar = new Part(205, 265, 183, 224, 40), RightEar = new Part(309, 174, 298, 114, 36),
                    Tail = new Part(395, 1036, 447, 1281, 83),
                    Chest = new Point(360, 691), ChestWidth = 270, ChestHeight = 325,
                    Anchors = new Point[] { new Point(460, 320), new Point(400, 193) } };
                case "back": return new IdlePhotoMotion {
                    LeftEar = new Part(371, 232, 338, 93, 70), RightEar = new Part(502, 424, 473, 345, 53),
                    Tail = new Part(384, 1151, 281, 1270, 82),
                    Chest = new Point(482, 765), ChestWidth = 281, ChestHeight = 325,
                    Anchors = new Point[] { new Point(610, 1095), new Point(513, 261) } };
                default: return new IdlePhotoMotion { Anchors = new Point[0] };
            }
        }

        internal Point Map(Point p, MotionPose pose)
        {
            Vector shift = new Vector();
            if (ChestWidth > 0 && pose.Breath != 0)
            {
                double x = (p.X - Chest.X) / ChestWidth, y = (p.Y - Chest.Y) / ChestHeight;
                double radius = x * x + y * y;
                if (radius < 1)
                {
                    double weight = (1 - radius) * (1 - radius) * pose.Breath;
                    shift += new Vector((p.X - Chest.X) * 0.018 * weight, -4 * weight);
                }
            }
            if (pose.Blink != 0)
            {
                // Compose eye maps so their soft fur margins cannot add up to
                // a reversed surface between two nearby eyes.
                Point closing = p + shift;
                foreach (Eye eye in Eyes) closing += eye.Offset(closing, pose.Blink);
                shift = closing - p;
            }
            if (LeftEar != null && pose.LeftEar != 0) shift += LeftEar.Twitch(p, pose.LeftEar);
            if (RightEar != null && pose.RightEar != 0) shift += RightEar.Twitch(p, -pose.RightEar);
            if (Tail != null && pose.Tail != 0) shift += Tail.Sway(p, pose.Tail);
            return p + shift;
        }

        internal List<double> Grid(double end, bool horizontal)
        {
            SortedSet<double> values = new SortedSet<double>();
            for (double n = 0; n < end; n += 28) values.Add(n);
            values.Add(end);
            foreach (Eye eye in Eyes)
            {
                double length = eye.HalfLength + 40, opening = eye.HalfOpening + 42;
                double extent = horizontal ? Math.Abs(eye.Cos) * length + Math.Abs(eye.Sin) * opening
                    : Math.Abs(eye.Sin) * length + Math.Abs(eye.Cos) * opening;
                Refine(values, end, horizontal ? eye.Center.X : eye.Center.Y, extent, 4);
            }
            foreach (Part ear in new Part[] { LeftEar, RightEar })
                if (ear != null)
                {
                    double a = horizontal ? ear.Root.X : ear.Root.Y, b = horizontal ? ear.Tip.X : ear.Tip.Y;
                    Refine(values, end, (a + b) / 2, Math.Abs(a - b) / 2 + ear.Width, 12);
                }
            return new List<double>(values);
        }
        private static void Refine(SortedSet<double> values, double end, double center, double extent, int step)
        {
            for (double n = Math.Max(0, Math.Floor((center - extent) / step) * step); n <= Math.Min(end, Math.Ceiling(center + extent)); n += step)
                values.Add(n);
        }
        private static double Smooth(double value) { double t = Math.Max(0, Math.Min(1, value)); return t * t * (3 - 2 * t); }
        private static double Falloff(double value, double inner, double outer) { return 1 - Smooth((Math.Abs(value) - inner) / (outer - inner)); }
    }
}
