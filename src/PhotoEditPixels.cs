using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace PhotoCat
{
    internal sealed class PhotoEditPixels
    {
        private readonly byte[] original;
        private byte[] pixels;
        private readonly List<byte[]> undo=new List<byte[]>();
        internal readonly WriteableBitmap View;
        internal PhotoEditPixels(BitmapSource photo,BitmapSource restore)
        {
            pixels=new byte[953*1347*4]; original=new byte[pixels.Length];
            PhotoFiles.Bgra(photo).CopyPixels(pixels,953*4,0); PhotoFiles.Bgra(restore).CopyPixels(original,953*4,0);
            View=new WriteableBitmap(953,1347,96,96,PixelFormats.Bgra32,null); Refresh();
        }
        internal void BeginStroke() { undo.Add((byte[])pixels.Clone()); if(undo.Count>5) undo.RemoveAt(0); }
        internal void Undo() { if(undo.Count==0)return; pixels=undo[undo.Count-1]; undo.RemoveAt(undo.Count-1); Refresh(); }
        internal void Paint(Point from,Point to,double radius,bool restore)
        {
            int count=Math.Max(1,(int)Math.Ceiling((to-from).Length/Math.Max(2,radius/3)));
            for(int i=0;i<=count;i++)
            {
                Point p=from+(to-from)*(i/(double)count);
                for(int y=Math.Max(0,(int)(p.Y-radius));y<=Math.Min(1346,(int)(p.Y+radius));y++)
                for(int x=Math.Max(0,(int)(p.X-radius));x<=Math.Min(952,(int)(p.X+radius));x++)
                {
                    double d=Math.Sqrt((x-p.X)*(x-p.X)+(y-p.Y)*(y-p.Y)); if(d>radius)continue;
                    int index=(y*953+x)*4;
                    double weight=Math.Min(1,(radius-d)/Math.Max(2,radius*0.15));
                    if(restore)
                    {
                        for(int c=0;c<3;c++)pixels[index+c]=original[index+c];
                        pixels[index+3]=(byte)(pixels[index+3]+(original[index+3]-pixels[index+3])*weight);
                    }
                    else pixels[index+3]=(byte)(pixels[index+3]*(1-weight));
                }
            }
            Refresh();
        }
        private void Refresh() { View.WritePixels(new Int32Rect(0,0,953,1347),pixels,953*4,0); }
        internal BitmapSource Snapshot()
        {
            byte[] visible=(byte[])pixels.Clone();
            for(int i=0;i<visible.Length;i+=4) if(visible[i+3]==0) { visible[i]=0;visible[i+1]=0;visible[i+2]=0; }
            BitmapSource result=BitmapSource.Create(953,1347,96,96,PixelFormats.Bgra32,null,visible,953*4); result.Freeze();return result;
        }
    }
}
