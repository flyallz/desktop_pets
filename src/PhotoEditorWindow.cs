using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace PhotoCat
{
    internal sealed class PhotoEditorWindow : Window
    {
        private readonly Grid page;
        private readonly Canvas canvas;
        private readonly Canvas marks=new Canvas { Width=953,Height=1347,IsHitTestVisible=false };
        private readonly Image still=new Image { Width=953,Height=1347,IsHitTestVisible=false };
        private PhotoMotion animated;
        private PhotoEditPixels pixels;
        private PhotoCustomization customization=new PhotoCustomization();
        private readonly Func<BitmapSource,string,PhotoCustomization,string> save;
        private readonly DispatcherTimer timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(40) };
        private CancellationTokenSource processing;
        private bool closed,playing,drawing,suppress;
        private string tool="eye";
        private Point start,last;
        private double elapsed, zoom=1;
        internal TextBox NameBox { get { return Find<TextBox>("LookName"); } }
        internal string StatusText { get { return Find<TextBlock>("Status").Text; } }
        internal bool IsProcessing { get { return processing!=null; } }
        internal bool CanSave { get { return Find<Button>("SavePhoto").IsEnabled; } }
        internal PhotoCustomization Customization { get { return customization; } }
        internal BitmapSource CurrentPhoto { get { return pixels==null?null:pixels.Snapshot(); } }
        internal string ToolsCache=LocalPhotoCutout.CacheRoot;

        internal PhotoEditorWindow(PhotoLook existing,Func<BitmapSource,string,PhotoCustomization,string> onSave)
        {
            save=onSave;
            ChatUi.WindowStyle(this,existing==null?"猫咪桌宠 · 添加自己的照片":"猫咪桌宠 · 调整照片造型",940,780);
            Width=Math.Min(940,SystemParameters.WorkArea.Width-40);Height=Math.Min(780,SystemParameters.WorkArea.Height-40);
            MinWidth=740;MinHeight=560;
            using(Stream input=Assembly.GetExecutingAssembly().GetManifestResourceStream("PhotoCat.PhotoEditor.xaml")) page=(Grid)XamlReader.Load(input);
            Content=page; canvas=Find<Canvas>("DrawingCanvas");
            Find<ScrollViewer>("PhotoScroll").SizeChanged+=delegate { ResizePreview(); };
            Find<Button>("ZoomIn").Click+=delegate { zoom=Math.Min(4,zoom+0.5);ResizePreview(); };
            Find<Button>("ZoomOut").Click+=delegate { zoom=Math.Max(1,zoom-0.5);ResizePreview(); };
            Find<Button>("ZoomFit").Click+=delegate { zoom=1;ResizePreview(); };
            canvas.Children.Add(still);canvas.Children.Add(marks);
            Find<Button>("ChoosePhoto").Click+=async delegate
            {
                OpenFileDialog choose=new OpenFileDialog { Title="选一张猫咪照片",Filter="猫咪照片 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",CheckFileExists=true };
                if(choose.ShowDialog(this)==true) await LoadPhotoAsync(choose.FileName);
            };
            Find<Button>("StopProcessing").Click+=delegate { CancelProcessing(); };
            Find<Button>("CancelEdit").Click+=delegate { Close(); };
            Find<Button>("SavePhoto").Click+=delegate { SavePhoto(); };
            Find<Button>("TogglePreview").Click+=delegate { SetPreview(!playing); };
            Find<Button>("ClearMarks").Click+=delegate
            {
                SetPreview(false);
                if(tool=="eye")customization.Eyes.Clear();else if(tool=="tail")customization.Tail=null;else if(tool=="ear")customization.Ears.Clear();
                DrawMarks();Status("已清除这类标记，可以重新画。");
            };
            Find<Button>("UndoBrush").Click+=delegate { UndoBrush(); };
            Find<RadioButton>("EyeTool").Checked+=delegate { SetTool("eye"); };
            Find<RadioButton>("TailTool").Checked+=delegate { SetTool("tail"); };
            Find<RadioButton>("EarTool").Checked+=delegate { SetTool("ear"); };
            Find<RadioButton>("EraseTool").Checked+=delegate { SetTool("erase"); };
            Find<RadioButton>("RestoreTool").Checked+=delegate { SetTool("restore"); };
            Find<Slider>("EyeHeight").ValueChanged+=delegate { if(!suppress){SetPreview(false);customization.EyeOpening=Find<Slider>("EyeHeight").Value;DrawMarks();} };
            Find<Slider>("TailSize").ValueChanged+=delegate { if(!suppress){SetPreview(false);customization.TailWidth=Find<Slider>("TailSize").Value;DrawMarks();} };
            canvas.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e)
            {
                if(pixels==null || processing!=null)return;
                SetPreview(false);drawing=true;start=last=Clamp(e.GetPosition(canvas));canvas.CaptureMouse();
                if(tool=="erase" || tool=="restore") { pixels.BeginStroke();pixels.Paint(start,start,Find<Slider>("BrushSize").Value/2,tool=="restore"); }
                e.Handled=true;
            };
            canvas.MouseMove+=delegate(object sender,MouseEventArgs e)
            {
                if(!drawing)return;Point point=Clamp(e.GetPosition(canvas));
                if(tool=="erase" || tool=="restore")pixels.Paint(last,point,Find<Slider>("BrushSize").Value/2,tool=="restore");
                else {DrawMarks();DrawLine(start,point,Brushes.DarkOrange,6);}
                last=point;
            };
            canvas.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e)
            {
                if(!drawing)return;drawing=false;canvas.ReleaseMouseCapture();
                if(tool!="erase" && tool!="restore")
                {
                    try {AddMark(tool,start,Clamp(e.GetPosition(canvas)));}
                    catch(InvalidDataException ex){Status(ex.Message,true);DrawMarks();}
                }
                e.Handled=true;
            };
            canvas.LostMouseCapture+=delegate {drawing=false;};
            timer.Tick+=delegate
            {
                if(!playing || animated==null)return;elapsed+=0.04;
                double cycle=elapsed%6;
                double blink=cycle<1.1?Math.Pow(Math.Sin(Math.PI*cycle/1.1),2):0;
                animated.SetPose(new MotionPose { Blink=blink,Breath=Math.Sin(elapsed*Math.PI/3),Tail=Math.Sin(elapsed*Math.PI/2.8),
                    LeftEar=cycle>2.3&&cycle<2.9?Math.Sin((cycle-2.3)/0.6*Math.PI):0,
                    RightEar=cycle>3.4&&cycle<4?Math.Sin((cycle-3.4)/0.6*Math.PI):0 });
            };
            Closed+=delegate {closed=true;timer.Stop();CancelProcessing();};
            if(existing!=null)
            {
                NameBox.Text=existing.Name;
                PhotoCustomization copy=new JavaScriptSerializer().Deserialize<PhotoCustomization>(new JavaScriptSerializer().Serialize(existing.Customization??new PhotoCustomization()));
                SetPhoto(existing.Photo,existing.Photo,copy);
                Status("可以改名字、调整动作或擦掉残留背景。保存后生效。");
            }
        }
        private void ResizePreview()
        {
            ScrollViewer scroll=Find<ScrollViewer>("PhotoScroll");
            double w=Math.Max(150,Math.Min(scroll.ActualWidth-20,(scroll.ActualHeight-20)*953/1347));
            Find<Viewbox>("PhotoBox").Width=w*zoom;Find<Viewbox>("PhotoBox").Height=w*zoom*1347/953;
        }
        private T Find<T>(string name) where T:class {return (T)page.FindName(name);}
        private static Point Clamp(Point p) {return new Point(Math.Max(0,Math.Min(952,p.X)),Math.Max(0,Math.Min(1346,p.Y)));}
        private void Status(string message,bool error=false)
        {Find<TextBlock>("Status").Text=message;Find<TextBlock>("Status").Foreground=error?Brushes.Firebrick:new SolidColorBrush(Color.FromRgb(70,88,79));}
        internal async Task LoadPhotoAsync(string path)
        {
            if(processing!=null)return;
            SetPreview(false);CancellationTokenSource request=new CancellationTokenSource();processing=request;SetBusy(true);
            try
            {
                BitmapSource original=await Task.Run(delegate {return PhotoFiles.Read(path);});
                request.Token.ThrowIfCancellationRequested();BitmapSource cutout=original;
                if(!PhotoFiles.HasTransparentBackground(original))
                {
                    var progress=new Progress<string>(delegate(string message){if(!closed)Find<TextBlock>("BusyText").Text=message;});
                    byte[] result=await LocalPhotoCutout.RunAsync(PhotoFiles.EncodePng(original),ToolsCache,request.Token,progress);
                    request.Token.ThrowIfCancellationRequested();
                    using(MemoryStream stream=new MemoryStream(result)) cutout=PhotoFiles.Bgra(BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad));
                    if(cutout.PixelWidth!=original.PixelWidth || cutout.PixelHeight!=original.PixelHeight)throw new InvalidDataException("处理后的照片尺寸异常，请换一张照片重试。");
                }
                Int32Rect b=PhotoFiles.Bounds(cutout);
                int padding=Math.Max(12,(int)(Math.Max(b.Width,b.Height)*0.025));
                int x=Math.Max(0,b.X-padding),y=Math.Max(0,b.Y-padding);
                b=new Int32Rect(x,y,Math.Min(original.PixelWidth,b.X+b.Width+padding)-x,Math.Min(original.PixelHeight,b.Y+b.Height+padding)-y);
                if(!closed)
                {
                    SetPhoto(PhotoFiles.Frame(cutout,b),PhotoFiles.Frame(original,b),new PhotoCustomization());
                    Status("照片准备好了。给眼睛、尾巴画线，再点“看看它动起来”。");
                }
            }
            catch(OperationCanceledException){if(!closed)Status("已取消这次处理，原来的造型没有改动。");}
            catch(Exception ex)
            {
                if(!(ex is InvalidDataException || ex is IOException || ex is ArgumentException || ex is FormatException || ex is NotSupportedException || ex is InvalidOperationException
                    || ex is System.Runtime.InteropServices.COMException || ex is System.ComponentModel.Win32Exception))throw;
                if(!closed)Status(ex is InvalidDataException?ex.Message:"没能处理这张照片。请检查照片是否能打开，或换一张 JPG / PNG 再试。",true);
            }
            finally{processing=null;request.Dispose();if(!closed)SetBusy(false);}
        }
        internal void CancelProcessing(){if(processing!=null)processing.Cancel();}
        private void SetBusy(bool value)
        {
            Find<Border>("BusyOverlay").Visibility=value?Visibility.Visible:Visibility.Collapsed;
            Find<TextBlock>("BusyText").Text="正在读取照片……";
            Find<Button>("ChoosePhoto").IsEnabled=!value;
            Find<StackPanel>("EditingControls").IsEnabled=!value&&pixels!=null;
            Find<Button>("SavePhoto").IsEnabled=!value&&pixels!=null;
            NameBox.IsEnabled=!value;
        }
        internal void SetPhoto(BitmapSource photo,BitmapSource restore,PhotoCustomization data)
        {
            SetPreview(false);pixels=new PhotoEditPixels(photo,restore);customization=data;still.Source=pixels.View;
            if(animated!=null)canvas.Children.Remove(animated);
            animated=new PhotoMotion {Width=953,Height=1347,IsHitTestVisible=false,Visibility=Visibility.Collapsed};animated.SetPhoto(photo);
            canvas.Children.Insert(1,animated);
            Find<TextBlock>("EmptyHint").Visibility=Visibility.Collapsed;
            Find<StackPanel>("EditingControls").IsEnabled=true;Find<Button>("SavePhoto").IsEnabled=true;
            suppress=true;Find<Slider>("EyeHeight").Value=data.EyeOpening;Find<Slider>("TailSize").Value=data.TailWidth;suppress=false;
            zoom=1;ResizePreview();DrawMarks();
        }
        internal void SetTool(string value)
        {
            SetPreview(false);tool=value;canvas.Cursor=Cursors.Cross;
            string hint=value=="eye"?"沿一只眼睛，从一侧眼角拖到另一侧。两只眼睛分别画；闭着眼可跳过。":value=="tail"?"从尾巴根部拖到尾尖。没露出尾巴就跳过，不用硬画。":value=="ear"?"从耳朵底部拖到耳尖。每只耳朵画一次，也可以跳过。":value=="erase"?"按住拖动，擦掉背景残留。擦多了可点“撤销”或用“恢复”。":"按住拖动，恢复当前照片中被擦掉的部分。";
            Find<TextBlock>("ToolHint").Text=hint;
            Find<StackPanel>("EyeRange").Visibility=value=="eye"?Visibility.Visible:Visibility.Collapsed;
            Find<StackPanel>("TailRange").Visibility=value=="tail"?Visibility.Visible:Visibility.Collapsed;
            Find<StackPanel>("BrushRange").Visibility=value=="erase"||value=="restore"?Visibility.Visible:Visibility.Collapsed;
            Find<Button>("ClearMarks").IsEnabled=value=="eye"||value=="tail"||value=="ear";
        }
        internal void AddMark(string kind,Point from,Point to)
        {
            PhotoMark mark=new PhotoMark {X1=from.X,Y1=from.Y,X2=to.X,Y2=to.Y};
            if(kind=="eye") {mark.Validate(18,210);if(customization.Eyes.Count==2)throw new InvalidDataException("已经标好两只眼睛。需要重画时，先点“清除这类标记”。");customization.Eyes.Add(mark);}
            else if(kind=="ear"){mark.Validate(24,260);if(customization.Ears.Count==2)throw new InvalidDataException("已经标好两只耳朵。需要重画时，先清除耳朵标记。");customization.Ears.Add(mark);}
            else {mark.Validate(40,850);customization.Tail=mark;}
            DrawMarks();Status("标记好了，可以预览；椭圆要覆盖眼睛，粗线要贴合尾巴。");
        }
        internal void UndoBrush(){SetPreview(false);if(pixels!=null)pixels.Undo();}
        internal void TestBrush(Point from,Point to,bool restore){pixels.BeginStroke();pixels.Paint(from,to,32,restore);}
        private void DrawLine(Point from,Point to,Brush color,double width)
        {marks.Children.Add(new Line{X1=from.X,Y1=from.Y,X2=to.X,Y2=to.Y,Stroke=color,StrokeThickness=width,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});}
        private void DrawMarks()
        {
            marks.Children.Clear();if(pixels==null)return;
            foreach(PhotoMark m in customization.Eyes)
            {
                Ellipse ellipse=new Ellipse {Width=m.Length,Height=m.Length*customization.EyeOpening,Stroke=Brushes.Teal,StrokeThickness=5,
                    Fill=new SolidColorBrush(Color.FromArgb(30,0,128,128)),RenderTransformOrigin=new Point(0.5,0.5),RenderTransform=new RotateTransform(Math.Atan2(m.Y2-m.Y1,m.X2-m.X1)*180/Math.PI)};
                Canvas.SetLeft(ellipse,(m.X1+m.X2-m.Length)/2);Canvas.SetTop(ellipse,(m.Y1+m.Y2-m.Length*customization.EyeOpening)/2);marks.Children.Add(ellipse);
            }
            foreach(PhotoMark m in customization.Ears)DrawLine(m.Start,m.End,Brushes.SteelBlue,10);
            if(customization.Tail!=null)
            {
                PhotoMark m=customization.Tail;DrawLine(m.Start,m.End,new SolidColorBrush(Color.FromArgb(50,206,117,49)),Math.Max(20,Math.Min(160,m.Length*customization.TailWidth))*2);
                DrawLine(m.Start,m.End,Brushes.Chocolate,6);
            }
            Find<TextBlock>("MarkCount").Text="眼睛 "+customization.Eyes.Count+" / 2 · 尾巴 "+(customization.Tail==null?0:1)+" / 1 · 耳朵 "+customization.Ears.Count+" / 2";
        }
        internal bool SetPreview(bool value)
        {
            bool wasPlaying=playing;timer.Stop();playing=false;
            if(animated!=null)animated.Visibility=Visibility.Collapsed;still.Visibility=Visibility.Visible;marks.Visibility=Visibility.Visible;
            Find<Button>("TogglePreview").Content="▶ 看看它动起来";
            if(!value || pixels==null){if(wasPlaying)Status("已停止预览，可以继续调整标记。");return true;}
            try
            {
                double strength=CustomMotionSafety.Apply(animated,pixels.Snapshot(),"custom-preview",customization);
                animated.Visibility=Visibility.Visible;still.Visibility=Visibility.Collapsed;marks.Visibility=Visibility.Collapsed;
                elapsed=0;playing=true;timer.Start();Find<Button>("TogglePreview").Content="停止预览，继续标记";
                Status(strength<0.8?"标记范围有重叠，已自动调轻动作。可以缩小标记范围后再预览。":"正在预览。觉得不合适，停止预览后就能重新标记。");return true;
            }
            catch(InvalidDataException ex){Status(ex.Message,true);return false;}
        }
        internal bool SavePhoto()
        {
            if(pixels==null || processing!=null)return false;
            try
            {
                string name=UserPhotoStore.ValidName(NameBox.Text);
                BitmapSource photo=pixels.Snapshot();PhotoFiles.Bounds(photo);
                CustomMotionSafety.Apply(animated,photo,"custom-preview",customization);
                string error=save(photo,name,customization);
                if(error!=null){Status(error,true);return false;}
                Close();return true;
            }
            catch(InvalidDataException ex){Status(ex.Message,true);return false;}
        }
    }
}
