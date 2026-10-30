"""Minimal protobuf wire reader used by replay analysis."""
import collections
def varint(b,i):
 v=0
 for s in range(0,70,7):
  z=b[i]; i+=1; v|=(z&127)<<s
  if z<128:return v,i
 raise ValueError('Invalid protobuf varint')
def protobuf(b):
 out=collections.defaultdict(list); i=0
 while i<len(b):
  tag,i=varint(b,i); field,wire=tag>>3,tag&7
  if not field:raise ValueError('Invalid protobuf field')
  if wire==0:value,i=varint(b,i)
  elif wire in (1,5):
   size=8 if wire==1 else 4
   if i+size>len(b):raise ValueError('Truncated protobuf fixed field')
   value=int.from_bytes(b[i:i+size],'little');i+=size
  elif wire==2:
   size,i=varint(b,i)
   if i+size>len(b):raise ValueError('Truncated protobuf bytes')
   value=b[i:i+size];i+=size
  else:raise ValueError('Unsupported protobuf wire type')
  out[field].append(value)
 return dict(out)
