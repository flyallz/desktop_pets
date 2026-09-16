using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace PhotoCat
{
    public sealed class PhotoMark
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        internal Point Start { get { return new Point(X1, Y1); } }
        internal Point End { get { return new Point(X2, Y2); } }
        internal double Length { get { return (End - Start).Length; } }
        internal void Validate(double minimum, double maximum)
        {
            foreach (double n in new double[] { X1, Y1, X2, Y2 })
                if (Double.IsNaN(n) || Double.IsInfinity(n)) throw new InvalidDataException("照片标记有误，请重新标记。");
            if (X1 < 0 || X2 < 0 || X1 > 953 || X2 > 953 || Y1 < 0 || Y2 < 0 || Y1 > 1347 || Y2 > 1347
                || Length < minimum || Length > maximum) throw new InvalidDataException("标记范围太小或太大，请重新画一条线。");
        }
    }
    public sealed class PhotoCustomization
    {
        public List<PhotoMark> Eyes { get; set; }
        public List<PhotoMark> Ears { get; set; }
        public PhotoMark Tail { get; set; }
        public double EyeOpening { get; set; }
        public double TailWidth { get; set; }
        public PhotoCustomization() { Eyes = new List<PhotoMark>(); Ears = new List<PhotoMark>(); EyeOpening = 0.60; TailWidth = 0.24; }
        internal void Validate()
        {
            if (Eyes == null || Ears == null || Eyes.Count > 2 || Ears.Count > 2) throw new InvalidDataException("每张照片最多标记两只眼睛和两只耳朵。");
            if (!(EyeOpening >= 0.25 && EyeOpening <= 1.0) || !(TailWidth >= 0.12 && TailWidth <= 0.6)) throw new InvalidDataException("照片动作范围不正确。");
            foreach (PhotoMark m in Eyes) { if (m == null) throw new InvalidDataException(); m.Validate(18, 210); }
            foreach (PhotoMark m in Ears) { if (m == null) throw new InvalidDataException(); m.Validate(24, 260); }
            if (Tail != null) Tail.Validate(40, 850);
        }
        internal IdlePhotoMotion Build(BitmapSource photo)
        {
            Validate();
            Int32Rect bounds = PhotoFiles.Bounds(photo);
            IdlePhotoMotion result = new IdlePhotoMotion { Anchors = new Point[0], Custom = true,
                Chest = new Point(bounds.X + bounds.Width * 0.5, bounds.Y + bounds.Height * 0.64),
                ChestWidth = Math.Max(30, bounds.Width * 0.30), ChestHeight = Math.Max(30, bounds.Height * 0.23) };
            List<IdlePhotoMotion.Eye> eyes = new List<IdlePhotoMotion.Eye>();
            foreach (PhotoMark m in Eyes)
            {
                double half = m.Length / 2;
                eyes.Add(new IdlePhotoMotion.Eye((m.X1+m.X2)/2, (m.Y1+m.Y2)/2, half, half*EyeOpening,
                    Math.Atan2(m.Y2-m.Y1,m.X2-m.X1)*180/Math.PI, true));
            }
            result.Eyes = eyes.ToArray();
            for (int i=0; i<Ears.Count; i++)
            {
                PhotoMark m=Ears[i];
                var ear=new IdlePhotoMotion.Part(m.X1,m.Y1,m.X2,m.Y2,Math.Max(24,Math.Min(120,m.Length*0.60)));
                if (i==0) result.LeftEar=ear; else result.RightEar=ear;
            }
            if (Tail!=null) result.Tail=new IdlePhotoMotion.Part(Tail.X1,Tail.Y1,Tail.X2,Tail.Y2,Math.Max(20,Math.Min(160,Tail.Length*TailWidth)));
            return result;
        }
    }
    internal static class CustomMotionSafety
    {
        internal static double Apply(PhotoMotion view,BitmapSource photo,string id,PhotoCustomization customization)
        {
            IdlePhotoMotion profile=customization.Build(photo); view.SetIdlePhoto(photo,id,profile);
            foreach(double strength in new double[] { 1,0.8,0.6,0.4,0 })
            {
                profile.MotionScale=strength; bool good=true;
                foreach(double blink in new double[] { 0,0.5,1 })
                foreach(double tail in new double[] { -1,1 })
                foreach(double breath in new double[] { -1,1 })
                {
                    view.SetPose(new MotionPose { Blink=blink,LeftEar=1,RightEar=1,Tail=tail,Breath=breath });
                    if(!view.HasValidSurface())good=false;
                }
                if(good) { view.SetPose(new MotionPose());return strength; }
            }
            throw new InvalidDataException("这次标记重叠较多，请清除后重新标记。");
        }
    }

}
