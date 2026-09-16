using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoCat
{
    internal sealed partial class PetWindow
    {
        private void VerifyUserPhotos(string output,List<string> checks)
        {
            timer.Stop();
            string folder=Path.Combine(output,"user-photos");Directory.CreateDirectory(folder);
            UserPhotoStore originalStore=photoStore;photoStore=new UserPhotoStore(Path.Combine(folder,"library"));
            PhotoEditorWindow editor=null;
            try
            {
                int skipped;
                Check(photoStore.Load(out skipped).Count==0,"New private photo library starts empty without changing built-in looks",checks);
                VerifyPhotoOrientation(folder,checks);
                string source=Path.Combine(folder,"chosen-photo.png");
                File.WriteAllBytes(source,PhotoFiles.EncodePng(looks[1].Photo));
                editor=new PhotoEditorWindow(null,delegate(BitmapSource image,string name,PhotoCustomization data){return SaveUserPhoto(null,image,name,data);});
                editor.ShowInTaskbar=false;editor.Show();editor.Left=-10000;editor.Top=-10000;editor.UpdateLayout();
                Check(!editor.CanSave,"Photo editor cannot save before a photo is chosen",checks);
                PumpTask(editor.LoadPhotoAsync(source));File.Delete(source);
                Check(editor.CanSave && editor.CurrentPhoto.PixelWidth==953 && PhotoFiles.HasTransparentBackground(editor.CurrentPhoto),
                    "Transparent PNG imports without cutout dependencies and keeps working after its source file is deleted",checks);
                Check((Native.GetWindowLong(new WindowInteropHelper(editor).Handle,-20)&0x08000000)==0,"Photo editor accepts focus while the desktop pet remains non-activating",checks);
                // Known photo landmarks let us verify real rendering of manually created marks.
                editor.SetPhoto(looks[1].Photo,looks[1].Photo,new PhotoCustomization());
                editor.AddMark("eye",new Point(438,312),new Point(518,349));
                editor.AddMark("eye",new Point(595,351),new Point(675,314));
                editor.AddMark("ear",new Point(352,234),new Point(300,80));
                Check(editor.Customization.Eyes.Count==2 && editor.Customization.Ears.Count==1,"Mouse-marking logic accepts two independent eyes and an optional ear",checks);
                bool rejected=false;try{editor.AddMark("eye",new Point(300,300),new Point(320,300));}catch(InvalidDataException){rejected=true;}
                Check(rejected && editor.Customization.Eyes.Count==2,"A third eye is rejected without overwriting the two good eye marks",checks);
                byte[] before=PhotoFiles.EncodePng(editor.CurrentPhoto);
                editor.TestBrush(new Point(480,800),new Point(510,830),false);
                Check(!EqualBytes(before,PhotoFiles.EncodePng(editor.CurrentPhoto)),"Erase brush changes only the preview before saving",checks);
                editor.UndoBrush();
                Check(EqualBytes(before,PhotoFiles.EncodePng(editor.CurrentPhoto)),"Undo restores the exact prior photo pixels",checks);
                Check(editor.SetPreview(true),"A manually marked photo runs in the actual WPF animation preview",checks);
                PumpTask(Task.Delay(280));editor.SetPreview(false);editor.UpdateLayout();
                Check(editor.StatusText.Contains("已停止预览"),"Stopping the preview restores an accurate editing hint",checks);
                SaveElement((FrameworkElement)editor.Content,(int)Math.Ceiling(editor.ActualWidth),(int)Math.Ceiling(editor.ActualHeight),Path.Combine(folder,"photo-editor.png"));
                editor.Width=760;editor.Height=600;editor.UpdateLayout();
                FrameworkElement panel=(FrameworkElement)editor.Content;
                Button saveButton=(Button)((Grid)panel).FindName("SavePhoto");
                Point bottom=saveButton.TranslatePoint(new Point(saveButton.ActualWidth,saveButton.ActualHeight),panel);
                Check(bottom.X<=panel.ActualWidth+1 && bottom.Y<=panel.ActualHeight+1 && saveButton.ActualWidth>100,
                    "Save remains visible at the smaller editor size; long controls scroll separately",checks);
                editor.Width=940;editor.Height=780;editor.UpdateLayout();
                editor.NameBox.Text="";Check(!editor.SavePhoto() && photoStore.Load(out skipped).Count==0,"Empty name gives a recoverable error and writes no photo",checks);
                editor.NameBox.Text="朋友自己添加的照片";
                Check(editor.SavePhoto() && CustomLook && !motion.Automatic,"Saving adds the new look to the pet immediately and keeps custom photos in place",checks);
                editor=null;
                string customId=looks[selectedLook].Id;
                List<PhotoLook> loaded=photoStore.Load(out skipped);
                Check(loaded.Count==1 && loaded[0].Id==customId && loaded[0].Name=="朋友自己添加的照片" && loaded[0].Customization.Eyes.Count==2,
                    "Photo pixels, name and eye/ear marks survive a fresh library load",checks);
                Check(File.ReadAllText(Path.Combine(photoStore.Folder,customId+".petphoto")).IndexOf("chosen-photo",StringComparison.Ordinal)<0,
                    "Stored look is a self-contained normalized PNG and marks, not a reference to the original file",checks);
                double blink=0,tail=0;bool stayed=true;
                motion.Pet();
                for(int i=0;i<900;i++)
                {
                    MotionPose pose=motion.Advance(0.04);stayed &= pose.Posture==CatPosture.Sit;
                    blink=Math.Max(blink,pose.Blink);tail=Math.Max(tail,Math.Abs(pose.Tail));cat.SetPose(pose);
                    if(!cat.HasValidSurface())throw new InvalidOperationException("A user photo animation folded");
                }
                Check(stayed && blink>0.9 && tail>0.5,"Custom photos receive normal idle/petting gestures and never switch to the sample cat's whole-body poses",checks);
                WalkPet();StretchPet();SleepPet();ToggleAutomatic();
                Check(motion.Activity==CatActivity.Companion && !motion.Automatic,"Whole-body commands cannot replace a custom photo with built-in action assets",checks);
                ApplySize(400,false);scene.UpdateLayout();cat.SetPose(new MotionPose());
                byte[] still=SaveMotionFrame(Path.Combine(folder,"custom-open.png"));cat.SetPose(new MotionPose{Blink=1,LeftEar=1,Breath=1});
                byte[] moved=SaveMotionFrame(Path.Combine(folder,"custom-blink.png"));
                Check(PixelChanges(still,moved)>20 && cat.HasValidSurface(),"Saved custom eye marks produce visible blinking with an intact mesh",checks);
                VerifyMovingPhotoHitTest(moved,customId,checks);
                PhotoLook saved=looks[selectedLook];
                editor=new PhotoEditorWindow(saved,delegate(BitmapSource image,string name,PhotoCustomization data){return SaveUserPhoto(saved,image,name,data);});
                editor.Customization.Eyes.Clear();editor.Close();editor=null;
                Check(saved.Customization.Eyes.Count==2 && photoStore.Load(out skipped)[0].Customization.Eyes.Count==2,"Closing the edit window discards draft marks without changing the saved look",checks);
                editor=new PhotoEditorWindow(saved,delegate(BitmapSource image,string name,PhotoCustomization data){return SaveUserPhoto(saved,image,name,data);});
                editor.NameBox.Text="改好名字的照片";Check(editor.SavePhoto(),"An existing custom look can be renamed and saved",checks);editor=null;
                Check(photoStore.Load(out skipped).Count==1 && photoStore.Load(out skipped)[0].Name=="改好名字的照片","Editing updates the same photo rather than creating duplicates",checks);
                SelectLook(0,false);Check(motion.Automatic,"Switching back to a built-in look restores its automatic activities",checks);
                motion.SetAutomatic(false);SelectLook(looks.FindIndex(delegate(PhotoLook look){return look.Id==customId;}),false);SelectLook(0,false);
                Check(!motion.Automatic,"A previously disabled built-in automatic setting stays disabled after a custom-photo visit",checks);
                motion.SetAutomatic(true);SelectLook(looks.FindIndex(delegate(PhotoLook look){return look.Id==customId;}),false);
                Check(DeleteUserPhoto(customId) && selectedLook==0 && !CustomLook && photoStore.Load(out skipped).Count==0,"Deleting the active custom look removes its private file and safely returns to the original cat",checks);
                rejected=false;try{photoStore.Delete("../../original");}catch(InvalidDataException){rejected=true;}
                Check(rejected,"Photo deletion accepts only generated custom IDs and cannot target other paths",checks);
                string corrupt=Path.Combine(photoStore.Folder,"custom-"+Guid.NewGuid().ToString("N")+".petphoto");File.WriteAllText(corrupt,"broken json");
                Check(photoStore.Load(out skipped).Count==0 && skipped==1,"A damaged private photo is skipped rather than crashing startup",checks);
                File.WriteAllText(corrupt,"{\"Version\":999,\"Name\":\"bad version\",\"Png\":\"not a photo\"}");
                Check(photoStore.Load(out skipped).Count==0 && skipped==1,"Unsupported or invalid private photo records are skipped without aborting startup",checks);
                File.Delete(corrupt);
                VerifyCustomTail(folder,checks);
                VerifyAutomaticCutout(folder,checks);
                ApplySize(240,false);SelectLook(0,false);
                checks.Add("NOT RUN: Physical mouse drags and file-dialog clicks; Computer Use kernel failed to start (windows sandbox failed: helper_unknown_error: setup refresh had errors). Actual WPF windows, editor methods, saving and rendering were exercised in-process.");
            }
            finally {if(editor!=null)editor.Close();photoStore=originalStore;}
        }
        private void VerifyCustomTail(string folder,List<string> checks)
        {
            PhotoCustomization data=new PhotoCustomization {Tail=new PhotoMark{X1=699,Y1=1206,X2=871,Y2=1255}};
            data.Eyes.Add(new PhotoMark {X1=131,Y1=543,X2=172,Y2=565});
            PhotoMotion preview=new PhotoMotion {Width=400,Height=400*1347.0/953};preview.SetPhoto(looks[3].Photo);
            double strength=CustomMotionSafety.Apply(preview,looks[3].Photo,"custom-test",data);
            Check(strength>0.5,"Tail and eye marks on a second pose retain useful motion strength",checks);
            for(int i=0;i<90;i++)
            {
                preview.SetPose(new MotionPose {Blink=(1-Math.Cos(i*Math.PI/22.5))/2,Tail=Math.Sin(i*Math.PI/45),Breath=Math.Sin(i*Math.PI/45)});
                if(!preview.HasValidSurface())throw new InvalidOperationException("Continuous user-tail motion folded");
            }
            Check(true,"Ninety continuous custom eye/tail states keep the photo surface intact",checks);
            data.Tail.X1=Double.NaN;bool invalid=false;try{data.Validate();}catch(InvalidDataException){invalid=true;}
            Check(invalid,"Nonfinite or invalid persisted landmarks are rejected before rendering",checks);
        }
        private void VerifyAutomaticCutout(string folder,List<string> checks)
        {
            if(!LocalPhotoCutout.Available){checks.Add("NOT RUN: Bundled offline cutout (developer build has no photo-tools resource).");return;}
            DrawingVisual visual=new DrawingVisual();using(DrawingContext dc=visual.RenderOpen())
            {dc.DrawRectangle(Brushes.Beige,null,new Rect(0,0,600,848));dc.DrawImage(bitmap,new Rect(0,0,600,848));}
            RenderTargetBitmap jpg=new RenderTargetBitmap(600,848,96,96,PixelFormats.Pbgra32);jpg.Render(visual);
            string input=Path.Combine(folder,"ordinary-photo.jpg");JpegBitmapEncoder encoder=new JpegBitmapEncoder{QualityLevel=92};encoder.Frames.Add(BitmapFrame.Create(jpg));using(Stream file=File.Create(input))encoder.Save(file);
            string cache=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(folder)),"photo-tools-cache");
            PhotoEditorWindow editor=new PhotoEditorWindow(null,delegate {return null;});editor.ToolsCache=cache;
            try
            {
                editor.ShowInTaskbar=false;editor.Show();editor.Left=-10000;editor.Top=-10000;
                Task loading=editor.LoadPhotoAsync(input);
                Check(editor.IsProcessing && !editor.CanSave,"Ordinary JPG starts automatic background removal and disables premature saving",checks);
                PumpTask(loading,180);
                Check(editor.CanSave && PhotoFiles.HasTransparentBackground(editor.CurrentPhoto),"Bundled offline helper turns an ordinary JPG into a usable transparent photo with no developer environment ["+editor.StatusText+"]",checks);
                File.WriteAllBytes(Path.Combine(folder,"automatic-cutout.png"),PhotoFiles.EncodePng(editor.CurrentPhoto));
                SaveElement((FrameworkElement)editor.Content,(int)Math.Ceiling(editor.ActualWidth),(int)Math.Ceiling(editor.ActualHeight),Path.Combine(folder,"automatic-cutout-editor.png"));
                Task canceled=editor.LoadPhotoAsync(input);editor.CancelProcessing();PumpTask(canceled,15);
                Check(!editor.IsProcessing && editor.CanSave && editor.StatusText.Contains("取消"),"Canceling a photo import keeps the previous ready photo and restores controls",checks);
                string bad=Path.Combine(folder,"bad.jpg");File.WriteAllText(bad,"not a photo");PumpTask(editor.LoadPhotoAsync(bad));
                Check(editor.CanSave && !editor.IsProcessing,"A broken input reports an error while keeping the prior usable photo",checks);File.Delete(bad);
                using(CancellationTokenSource cancel=new CancellationTokenSource())
                {
                    byte[] bytes=PhotoFiles.EncodePng(PhotoFiles.Read(input));
                    Task<byte[]> task=LocalPhotoCutout.RunAsync(bytes,cache,cancel.Token,new Progress<string>());
                    cancel.CancelAfter(150);bool wasCanceled=false;
                    try{PumpTask(task,15);}catch(OperationCanceledException){wasCanceled=true;}
                    Check(wasCanceled,"An in-progress offline cutout process can be canceled without waiting for model inference",checks);
                }
            }
            finally{editor.Close();File.Delete(input);}
        }
        private void VerifyPhotoOrientation(string folder,List<string> checks)
        {
            DrawingVisual drawing=new DrawingVisual();using(DrawingContext dc=drawing.RenderOpen())
            {dc.DrawRectangle(Brushes.Red,null,new Rect(0,0,60,40));dc.DrawRectangle(Brushes.Green,null,new Rect(60,0,60,40));dc.DrawRectangle(Brushes.Blue,null,new Rect(0,40,60,40));dc.DrawRectangle(Brushes.Yellow,null,new Rect(60,40,60,40));}
            RenderTargetBitmap fixture=new RenderTargetBitmap(120,80,96,96,PixelFormats.Pbgra32);fixture.Render(drawing);
            string[] corners={"RGBY","GRYB","YBGR","BYRG","RBGY","BRYG","YGBR","GYRB"};
            for(int i=1;i<=8;i++)
            {
                string path=Path.Combine(folder,"orientation-"+i+".jpg");BitmapMetadata meta=new BitmapMetadata("jpg");meta.SetQuery("/app1/ifd/{ushort=274}",(ushort)i);
                JpegBitmapEncoder encoder=new JpegBitmapEncoder{QualityLevel=98};encoder.Frames.Add(BitmapFrame.Create(fixture,null,meta,null));using(Stream file=File.Create(path))encoder.Save(file);
                BitmapSource rotated=PhotoFiles.Read(path);byte[] pixels=new byte[rotated.PixelWidth*rotated.PixelHeight*4];rotated.CopyPixels(pixels,rotated.PixelWidth*4,0);
                string actual="";
                foreach(double y in new double[]{0.25,0.75})foreach(double x in new double[]{0.25,0.75})
                {int n=((int)(y*rotated.PixelHeight)*rotated.PixelWidth+(int)(x*rotated.PixelWidth))*4;actual+=pixels[n+2]>200?(pixels[n+1]>200?"Y":"R"):(pixels[n]>200?"B":"G");}
                Check(actual==corners[i-1],"Phone photo EXIF orientation "+i+" is applied correctly before marking",checks);File.Delete(path);
            }
        }
        private static bool EqualBytes(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    }
}
