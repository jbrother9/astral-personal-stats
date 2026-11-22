using System;
using IObject=Il2CppSystem.Object;

namespace AstralPersonalMod {
 internal static class StatItemImages {
  internal static bool Card(IObject parent,int id,float x,float y,float width,float height){
   return Add(parent,RuntimeGameCatalog.CardImage(id),x,y,width,height);
  }
  internal static bool Chip(IObject parent,int id,float x,float y,float width,float height){
   return Add(parent,RuntimeGameCatalog.ChipImage(id),x,y,width,height);
  }
  static bool Add(IObject parent,string image,float x,float y,float width,float height){
   if(string.IsNullOrEmpty(image))return false;
   try{
    var loader=NativeUI.New("UI.TextureLoader");
    NativeUI.Call(loader,"SetXY",x,y);NativeUI.Call(loader,"SetSize",width,height);
    NativeUI.Set(loader,"touchable",false);NativeUI.Set(loader,"fill","Scale");
    NativeUI.Set(loader,"align","Center");NativeUI.Set(loader,"verticalAlign","Middle");
    NativeUI.Set(loader,"showErrorSign",false);
    NativeUI.Call(parent,"AddChild",loader);NativeUI.Set(loader,"url",image);
    return true;
   }catch(Exception error){RuntimeDiagnostics.Error("stats.item_thumbnail",error);return false;}
  }
 }
}
