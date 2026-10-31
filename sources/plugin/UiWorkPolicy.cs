using System;
using System.Collections.Generic;
namespace AstralPersonalMod {
 // Incremental, visible-only search. The stack retains at most one cursor per tree depth.
 internal sealed class ProfileTreeSearch<T> where T:class {
  readonly Func<T,bool> visible,match;readonly Func<T,int> count;readonly Func<T,int,T> child;readonly Func<T,T> parent;readonly Func<T,T,bool> same;
  struct Cursor {internal T Node;internal int Depth,Next,Checked;internal bool Entered;}
  readonly Stack<Cursor> stack=new Stack<Cursor>();
  T activeRoot;int rootCount,tick;
  internal bool Pending=>stack.Count>0;
  internal bool UsedCache{get;private set;}
  internal ProfileTreeSearch(Func<T,bool> visible,Func<T,bool> match,Func<T,int> count,Func<T,int,T> child,Func<T,T> parent,Func<T,T,bool> same){this.visible=visible;this.match=match;this.count=count;this.child=child;this.parent=parent;this.same=same;}
  internal void Clear(){stack.Clear();activeRoot=null;rootCount=0;}
  bool ValidCache(T root,T cached,ref int visits){
   if(cached==null)return false;
   for(int depth=0;cached!=null&&depth<32;depth++,cached=parent(cached)){visits++;if(!visible(cached))return false;if(same(cached,root))return true;}
   return false;
  }
  internal T Find(T root,T cached,out int visits,int budget=64){
   visits=0;UsedCache=false;tick++;
   if(root==null){Clear();return null;}visits++;if(!visible(root)){Clear();return null;}
   if(match(root)){Clear();return root;}int n=count(root);
   // Native windows are normally direct GRoot children. Prefer the topmost matching window.
   for(int i=n-1;i>=Math.Max(0,n-64);i--){var item=child(root,i);visits++;if(item!=null&&visible(item)&&match(item)){UsedCache=cached!=null&&same(item,cached);Clear();return item;}}
   if(cached!=null&&ValidCache(root,cached,ref visits)&&match(cached)){UsedCache=true;Clear();return cached;}
   bool invalidAncestor=false;
   foreach(var ancestor in stack)if(ancestor.Entered&&!visible(ancestor.Node)){invalidAncestor=true;break;}
   if(invalidAncestor)Clear();
   if(activeRoot==null||!same(activeRoot,root)||rootCount!=n||!Pending){Clear();activeRoot=root;rootCount=n;stack.Push(new Cursor{Node=root,Depth=0,Next=n-1,Entered=true,Checked=tick});}
   int work=0;
   while(stack.Count>0&&work<Math.Max(1,budget)){
    var cursor=stack.Pop();if(cursor.Node==null)continue;
    if(!cursor.Entered){
     work++;visits++;if(!visible(cursor.Node))continue;
     if(match(cursor.Node)){var found=cursor.Node;Clear();return found;}
     if(cursor.Depth>=4)continue;cursor.Next=count(cursor.Node)-1;cursor.Entered=true;cursor.Checked=tick;
    }else if(cursor.Checked!=tick){cursor.Checked=tick;if(!visible(cursor.Node))continue;cursor.Next=Math.Min(cursor.Next,count(cursor.Node)-1);}
    if(cursor.Next<0)continue;
    var next=child(cursor.Node,cursor.Next--);stack.Push(cursor);stack.Push(new Cursor{Node=next,Depth=cursor.Depth+1});
   }
   return null;
  }
 }
 internal sealed class UiPlacementCache {
  bool valid;float width,height,scale,x,y;
  internal void Invalidate(){valid=false;}
  internal bool Changed(float width,float height,ModalBounds bounds){
   bool changed=!valid||Math.Abs(this.width-width)>.01f||Math.Abs(this.height-height)>.01f||Math.Abs(scale-bounds.Scale)>.0001f||Math.Abs(x-bounds.X)>.01f||Math.Abs(y-bounds.Y)>.01f;
   if(changed){valid=true;this.width=width;this.height=height;scale=bounds.Scale;x=bounds.X;y=bounds.Y;}return changed;
  }
 }
}
