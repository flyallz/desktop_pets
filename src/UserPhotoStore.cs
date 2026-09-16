using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoCat
{
    internal static class PhotoFiles
    {
        internal const int CanvasWidth = 953, CanvasHeight = 1347;
        internal static BitmapSource Read(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length > 30 * 1024 * 1024) throw new InvalidDataException("照片超过 30 MB，请先缩小后再添加。");
                byte[] signature = new byte[8];
                if (stream.Read(signature, 0, 8) != 8 || !((signature[0] == 137 && signature[1] == 80 && signature[2] == 78 && signature[3] == 71)
                    || (signature[0] == 255 && signature[1] == 216)))
                    throw new InvalidDataException("请选择 JPG 或 PNG 照片。");
                stream.Position = 0;
                BitmapFrame header = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if (header.PixelWidth < 16 || header.PixelHeight < 16 || (long)header.PixelWidth * header.PixelHeight > 40000000)
                    throw new InvalidDataException("照片尺寸需至少 16 像素，且不超过 4000 万像素。");
                int orientation = 1;
                try
                {
                    BitmapMetadata metadata = header.Metadata as BitmapMetadata;
                    if (metadata != null)
                    {
                        object value = metadata.GetQuery("/app1/ifd/{ushort=274}");
                        if (value != null) orientation = Convert.ToInt32(value);
                    }
                }
                catch (NotSupportedException) { }
                int decodeWidth = Math.Max(1, (int)Math.Round(header.PixelWidth * Math.Min(1, 1600.0 / Math.Max(header.PixelWidth, header.PixelHeight))));
                stream.Position = 0;
                BitmapImage image = new BitmapImage();
                image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = decodeWidth; image.StreamSource = stream; image.EndInit();
                BitmapSource source = image;
                Matrix[] exif = { Matrix.Identity, Matrix.Identity, new Matrix(-1,0,0,1,0,0), new Matrix(-1,0,0,-1,0,0),
                    new Matrix(1,0,0,-1,0,0), new Matrix(0,1,1,0,0,0), new Matrix(0,1,-1,0,0,0),
                    new Matrix(0,-1,-1,0,0,0), new Matrix(0,-1,1,0,0,0) };
                if (orientation > 1 && orientation < exif.Length) source = new TransformedBitmap(source, new MatrixTransform(exif[orientation]));
                return Bgra(source);
            }
        }
        internal static BitmapSource Bgra(BitmapSource source)
        {
            BitmapSource result = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            result.Freeze(); return result;
        }
        internal static BitmapSource Normalize(BitmapSource image)
        {
            return Frame(image, Bounds(image));
        }
        internal static Int32Rect Bounds(BitmapSource image)
        {
            image = Bgra(image);
            int width=image.PixelWidth, height=image.PixelHeight;
            byte[] pixels=new byte[width*height*4]; image.CopyPixels(pixels,width*4,0);
            int left=width,top=height,right=-1,bottom=-1;
            for(int y=0;y<height;y++) for(int x=0;x<width;x++) if(pixels[(y*width+x)*4+3]>=16)
            {left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            if(right<left || bottom<top) throw new InvalidDataException("照片里没有可见内容，请换一张照片。");
            return new Int32Rect(left,top,right-left+1,bottom-top+1);
        }
        internal static BitmapSource Frame(BitmapSource image, Int32Rect bounds)
        {
            BitmapSource crop=new CroppedBitmap(image,bounds);
            double scale=Math.Min(857.0/crop.PixelWidth,1235.0/crop.PixelHeight);
            double dw=crop.PixelWidth*scale,dh=crop.PixelHeight*scale;
            DrawingVisual drawing=new DrawingVisual(); RenderOptions.SetBitmapScalingMode(drawing,BitmapScalingMode.HighQuality);
            using(DrawingContext dc=drawing.RenderOpen()) dc.DrawImage(crop,new Rect((CanvasWidth-dw)/2,1307-dh,dw,dh));
            RenderTargetBitmap target=new RenderTargetBitmap(CanvasWidth,CanvasHeight,96,96,PixelFormats.Pbgra32); target.Render(drawing);
            return Bgra(target);
        }
        internal static bool HasTransparentBackground(BitmapSource image)
        {
            image = Bgra(image);
            byte[] pixels = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(pixels, image.PixelWidth * 4, 0);
            int clear = 0;
            for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] < 16) clear++;
            return clear > image.PixelWidth * image.PixelHeight / 50;
        }
        internal static byte[] EncodePng(BitmapSource image)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); encoder.Save(stream);
                return stream.ToArray();
            }
        }
        internal static BitmapSource DecodePng(byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                BitmapFrame frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
                if (frame.PixelWidth != CanvasWidth || frame.PixelHeight != CanvasHeight) throw new InvalidDataException("造型尺寸不正确。");
                return Bgra(frame);
            }
        }
    }

    internal sealed class UserPhotoStore
    {
        internal readonly string Folder;
        internal static string DefaultFolder { get { return Path.Combine(Path.GetDirectoryName(PetPreferences.DefaultPath), "photos"); } }
        internal UserPhotoStore(string folder) { Folder = Path.GetFullPath(folder); }
        public sealed class Record
        {
            public int Version { get; set; }
            public string Name { get; set; }
            public string Png { get; set; }
            public PhotoCustomization Motion { get; set; }
        }
        private static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024, RecursionLimit = 16 }; }
        internal static bool IsCustom(string id)
        {
            Guid parsed;
            return id != null && id.StartsWith("custom-", StringComparison.Ordinal) && Guid.TryParseExact(id.Substring(7), "N", out parsed);
        }
        private string FilePath(string id)
        {
            if (!IsCustom(id)) throw new InvalidDataException("只能管理自己添加的照片。");
            return Path.Combine(Folder, id + ".petphoto");
        }
        internal List<PhotoLook> Load(out int skipped)
        {
            List<PhotoLook> photos = new List<PhotoLook>(); skipped = 0;
            if (!Directory.Exists(Folder)) return photos;
            foreach (string file in Directory.GetFiles(Folder, "custom-*.petphoto"))
            {
                try
                {
                    string id = Path.GetFileNameWithoutExtension(file);
                    if (!IsCustom(id) || new FileInfo(file).Length > 16 * 1024 * 1024) throw new InvalidDataException();
                    Record record = Serializer().Deserialize<Record>(File.ReadAllText(file, Encoding.UTF8));
                    if (record == null || record.Version != 1 || String.IsNullOrEmpty(record.Png)) throw new InvalidDataException();
                    PhotoCustomization customization = record.Motion ?? new PhotoCustomization(); customization.Validate();
                    photos.Add(new PhotoLook(id, ValidName(record.Name), PhotoFiles.DecodePng(Convert.FromBase64String(record.Png))) { Customization = customization });
                }
                catch (Exception ex)
                {
                    if (!(ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is FormatException
                        || ex is InvalidOperationException || ex is NotSupportedException || ex is System.Runtime.InteropServices.COMException)) throw;
                    skipped++;
                }
            }
            return photos;
        }
        internal PhotoLook Add(BitmapSource normalized, string name, PhotoCustomization customization = null)
        {
            string id = "custom-" + Guid.NewGuid().ToString("N");
            PhotoLook look = new PhotoLook(id, ValidName(name), normalized) { Customization = customization ?? new PhotoCustomization() };
            Save(look); return look;
        }
        internal void Save(PhotoLook look)
        {
            string path = FilePath(look.Id);
            if (look.Photo.PixelWidth != PhotoFiles.CanvasWidth || look.Photo.PixelHeight != PhotoFiles.CanvasHeight)
                throw new InvalidDataException("请先预览照片再保存。");
            PhotoCustomization customization = look.Customization ?? new PhotoCustomization(); customization.Validate();
            Record record = new Record { Version = 1, Motion = customization, Name = ValidName(look.Name), Png = Convert.ToBase64String(PhotoFiles.EncodePng(look.Photo)) };
            Directory.CreateDirectory(Folder);
            string temporary = Path.Combine(Folder, ".pending-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(temporary, Serializer().Serialize(record), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        internal void Delete(string id) { File.Delete(FilePath(id)); }
        internal static string ValidName(string text)
        {
            string name = (text ?? "").Trim();
            if (name.Length == 0 || name.Length > 32) throw new InvalidDataException("请给造型起一个 1～32 字的名字。");
            foreach (char c in name) if (Char.IsControl(c)) throw new InvalidDataException("名字里不能包含换行或控制字符。");
            return name;
        }
    }
}
