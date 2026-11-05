using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace AstralPersonalMod {
 internal readonly struct CaptureRect {
  internal readonly int X,Y,Width,Height;
  internal CaptureRect(int x,int y,int w,int h){X=x;Y=y;Width=w;Height=h;}
 }
 internal static class ScreenshotClipboard {
  [StructLayout(LayoutKind.Sequential)] struct Rect {internal int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)] struct Point {internal int X,Y;}
  [StructLayout(LayoutKind.Sequential)] struct BitmapInfo {
   internal uint Size;internal int Width,Height;internal ushort Planes,BitCount;internal uint Compression,ImageSize;internal int XPels,YPels;internal uint ColorsUsed,ColorsImportant;
  }
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h,out Rect rect);
  [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h,ref Point point);
  [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
  [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h,IntPtr dc);
  [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
  [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
  [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr target,int x,int y,int w,int h,IntPtr source,int sx,int sy,uint operation);
  [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr owner);
  [DllImport("user32.dll")] static extern bool CloseClipboard();
  [DllImport("user32.dll")] static extern bool EmptyClipboard();
  [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint format,IntPtr memory);
  [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint flags,UIntPtr bytes);
  [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr memory);
  [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr memory);
  [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr memory);
  internal static CaptureRect Crop(int cw,int ch,float rw,float rh,float x,float y,float w,float h){
   if(cw<=0||ch<=0||rw<=0||rh<=0||w<=0||h<=0||!float.IsFinite(x+y+w+h+rw+rh))throw new ArgumentException("Invalid screenshot bounds");
   int left=(int)Math.Max(0,Math.Min(cw,Math.Floor(x*cw/rw))),top=(int)Math.Max(0,Math.Min(ch,Math.Floor(y*ch/rh)));
   int right=(int)Math.Max(left,Math.Min(cw,Math.Ceiling((x+w)*cw/rw))),bottom=(int)Math.Max(top,Math.Min(ch,Math.Ceiling((y+h)*ch/rh)));
   if(right<=left||bottom<=top)throw new ArgumentException("Screenshot outside game client");
   return new CaptureRect(left,top,right-left,bottom-top);
  }
  internal static int Stride(int width)=>checked((checked(width*3)+3)&~3);
  internal static byte[] DibHeader(int width,int height){
   if(width<=0||height<=0)throw new ArgumentException("Invalid image size");
   var bytes=new byte[40];Array.Copy(BitConverter.GetBytes(40),0,bytes,0,4);Array.Copy(BitConverter.GetBytes(width),0,bytes,4,4);Array.Copy(BitConverter.GetBytes(height),0,bytes,8,4);bytes[12]=1;bytes[14]=24;Array.Copy(BitConverter.GetBytes(checked(Stride(width)*height)),0,bytes,20,4);return bytes;
  }
  static IntPtr ActiveGame(){
   var window=GetForegroundWindow();GetWindowThreadProcessId(window,out uint pid);
   if(window==IntPtr.Zero||pid!=(uint)Process.GetCurrentProcess().Id)throw new InvalidOperationException("Game window is not foreground");return window;
  }
  internal static byte[] Capture(float rw,float rh,float x,float y,float w,float h){
   var window=ActiveGame();if(!GetClientRect(window,out var client))throw new InvalidOperationException("Game client unavailable");
   var crop=Crop(client.Right-client.Left,client.Bottom-client.Top,rw,rh,x,y,w,h);var point=new Point{X=crop.X,Y=crop.Y};if(!ClientToScreen(window,ref point))throw new InvalidOperationException("Screen coordinates unavailable");
   int size=checked(Stride(crop.Width)*crop.Height);if(size>128*1024*1024)throw new InvalidOperationException("Screenshot too large");
   IntPtr screen=IntPtr.Zero,memory=IntPtr.Zero,bitmap=IntPtr.Zero,previous=IntPtr.Zero;
   try{
    screen=GetDC(IntPtr.Zero);if(screen==IntPtr.Zero)throw new InvalidOperationException("Screen DC unavailable");memory=CreateCompatibleDC(screen);if(memory==IntPtr.Zero)throw new InvalidOperationException("Capture DC unavailable");
    var info=new BitmapInfo{Size=40,Width=crop.Width,Height=crop.Height,Planes=1,BitCount=24,ImageSize=(uint)size};bitmap=CreateDIBSection(screen,ref info,0,out var bits,IntPtr.Zero,0);if(bitmap==IntPtr.Zero||bits==IntPtr.Zero)throw new InvalidOperationException("Capture bitmap unavailable");
    previous=SelectObject(memory,bitmap);if(previous==IntPtr.Zero||previous==new IntPtr(-1))throw new InvalidOperationException("Capture bitmap selection failed");
    if(!BitBlt(memory,0,0,crop.Width,crop.Height,screen,point.X,point.Y,0x40CC0020))throw new InvalidOperationException("Screen capture failed");
    var data=new byte[checked(40+size)];Array.Copy(DibHeader(crop.Width,crop.Height),data,40);Marshal.Copy(bits,data,40,size);bool hasColor=false;for(int i=40;i<data.Length;i++)if(data[i]!=0){hasColor=true;break;}if(!hasColor)throw new InvalidOperationException("Game capture was blank");return data;
   }finally{if(memory!=IntPtr.Zero&&previous!=IntPtr.Zero&&previous!=new IntPtr(-1))SelectObject(memory,previous);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(memory!=IntPtr.Zero)DeleteDC(memory);if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen);}
  }
  internal static bool TryCopy(byte[] dib){
   if(dib==null||dib.Length<=40)throw new ArgumentException("Empty screenshot");return TryCopyData(dib,8);
  }
  internal static bool TryCopyText(string text){
   if(string.IsNullOrEmpty(text)||text.Length>128||text.IndexOf('\0')>=0)throw new ArgumentException("Invalid clipboard text");
   return TryCopyData(System.Text.Encoding.Unicode.GetBytes(text+"\0"),13);
  }
  static bool TryCopyData(byte[] data,uint format){
   var owner=ActiveGame();var handle=GlobalAlloc(2,(UIntPtr)(uint)data.Length);if(handle==IntPtr.Zero)throw new OutOfMemoryException();bool opened=false;
   try{
    var pointer=GlobalLock(handle);if(pointer==IntPtr.Zero)throw new InvalidOperationException("Clipboard memory unavailable");try{Marshal.Copy(data,0,pointer,data.Length);}finally{GlobalUnlock(handle);}
    if(!OpenClipboard(owner))return false;opened=true;if(!EmptyClipboard())throw new InvalidOperationException("Clipboard unavailable");
    if(SetClipboardData(format,handle)==IntPtr.Zero)throw new InvalidOperationException("Clipboard data rejected");handle=IntPtr.Zero;return true;
   }finally{if(opened)CloseClipboard();if(handle!=IntPtr.Zero)GlobalFree(handle);}
  }
 }
}
