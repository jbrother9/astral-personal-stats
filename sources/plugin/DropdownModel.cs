using System;
namespace AstralPersonalMod {
 public sealed class Choice {public int id{get;set;}public string label{get;set;}}
 internal sealed class DropdownModel {
  internal readonly Choice[] Options;
  internal readonly int SelectedId;
  internal const int Limit=8;
  internal int Offset{get;private set;}
  internal int Count=>Options.Length;
  internal int VisibleCount=>Math.Min(Limit,Count-Offset);
  internal DropdownModel(Choice[] options,int selected){Options=options??Array.Empty<Choice>();SelectedId=selected;int index=Array.FindIndex(Options,c=>c.id==selected);Offset=Math.Max(0,Math.Min(Math.Max(0,Count-Limit),index<Limit?0:index-Limit+1));}
  internal bool Scroll(int amount){int old=Offset;Offset=Math.Max(0,Math.Min(Math.Max(0,Count-Limit),Offset+amount));return Offset!=old;}
  internal Choice At(int index){return index>=0&&index<Count?Options[index]:null;}
 }
}
