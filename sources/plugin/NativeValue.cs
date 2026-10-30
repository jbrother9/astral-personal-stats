using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using IObject=Il2CppSystem.Object;
namespace AstralPersonalMod {
 // Object.ToString dispatches value-type overrides with a boxed this-pointer in this bridge.
 // Read a verified primitive's actual unboxed payload instead of parsing that dispatch result.
 internal static class NativeValue {
  internal static long Integer(IObject value){
   if(value==null)return 0;var type=value.GetIl2CppType();string name=type.FullName;
   IntPtr data=IntPtr.Zero;
   if(name=="System.Int64"||name=="System.UInt64"||name=="System.Int32"||name=="System.UInt32"||type.IsEnum)data=IL2CPP.il2cpp_object_unbox(value.Pointer);
   if(data==IntPtr.Zero)return 0;
   if(name=="System.Int64")return Marshal.ReadInt64(data);
   if(name=="System.UInt64"){ulong n=unchecked((ulong)Marshal.ReadInt64(data));return n<=long.MaxValue?(long)n:0;}
   if(name=="System.UInt32")return unchecked((uint)Marshal.ReadInt32(data));
   return Marshal.ReadInt32(data);
  }
  internal static bool Boolean(IObject value){return value!=null&&value.GetIl2CppType().FullName=="System.Boolean"&&Marshal.ReadByte(IL2CPP.il2cpp_object_unbox(value.Pointer))!=0;}
  internal static float Float(IObject value){if(value==null)return 0;string name=value.GetIl2CppType().FullName;if(name=="System.Single")return BitConverter.Int32BitsToSingle(Marshal.ReadInt32(IL2CPP.il2cpp_object_unbox(value.Pointer)));return (float)Integer(value);}
 }
}
