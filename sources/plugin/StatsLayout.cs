using System;
namespace AstralPersonalMod {
 internal readonly struct ModalBounds {
  internal readonly float Scale,X,Y,Width,Height;
  internal ModalBounds(float scale,float x,float y,float width,float height){Scale=scale;X=x;Y=y;Width=width;Height=height;}
 }
 internal static class StatsLayout {
  internal const float Width=1120,Height=760,RowHeight=39,RowStride=43,RowsTop=42;
  internal static ModalBounds Fit(float screenWidth,float screenHeight){float scale=Math.Min(screenWidth*.94f/Width,screenHeight*.94f/Height);return new ModalBounds(scale,(screenWidth-Width*scale)/2,(screenHeight-Height*scale)/2,Width*scale,Height*scale);}
  internal const float TableHeight=390;
  internal static bool Contains(float x,float y,float w,float h,float parentWidth,float parentHeight){return x>=0&&y>=0&&w>0&&h>0&&x+w<=parentWidth&&y+h<=parentHeight;}
  internal static float RowTop(int index){return RowsTop+index*RowStride;}
 }
}
