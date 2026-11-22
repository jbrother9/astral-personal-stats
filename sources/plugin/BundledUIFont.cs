using System;
using System.IO;
using UnityEngine;
using IObject=Il2CppSystem.Object;

namespace AstralPersonalMod {
 internal static class BundledUIFont {
  const string FontKey="AstralPersonalStats.NanumSquareRound";
  static bool attempted;
  static string loaded;
  static Font nativeFont;
  static IObject dynamicFont;

  internal static string Load(){
   if(attempted)return loaded;
   attempted=true;
   try{
    string folder=Path.GetDirectoryName(typeof(BundledUIFont).Assembly.Location);
    string path=Path.Combine(folder,"fonts","NanumSquareRoundR.ttf");
    if(!File.Exists(path))throw new FileNotFoundException("Bundled UI font is missing",path);
    nativeFont=new Font(path);
    if(!nativeFont.dynamic)throw new InvalidOperationException("Bundled UI font is not dynamic");
    foreach(char character in "플레이어 턴 사망 효과 카드 사용 전투 스킬 칩 선택 이동 주사위 총 거리 가한 피해 받은 최대 코인 송금 지원받은 획득 소모 변동 증가량 감소량 아군 상점 방문"){
     if(!char.IsWhiteSpace(character)&&!nativeFont.HasCharacter(character))
      throw new InvalidOperationException("Bundled UI font is missing character "+(int)character);
    }
    nativeFont.hideFlags=HideFlags.DontUnloadUnusedAsset;
    dynamicFont=NativeUI.New("FairyGUI.DynamicFont");
    NativeUI.Set(dynamicFont,"name",FontKey);
    NativeUI.Set(dynamicFont,"nativeFont",new IObject(nativeFont.Pointer));
    NativeUI.CallStatic("FairyGUI.FontManager","RegisterFont",dynamicFont,FontKey);
    loaded=FontKey;
    RuntimeDiagnostics.Event("bundled_ui_font_registered");
   }catch(Exception error){RuntimeDiagnostics.Error("stats.bundled_font",error);}
   return loaded;
  }
 }
}
