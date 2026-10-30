"""Confirmed relic choices, excluding rerolls. Shop selection indices are not relic IDs."""
import collections,json,pathlib,struct
import astral_replay as replay
import astral_game_catalog as game_catalog
VERSION=2
ROOT=pathlib.Path(__file__).resolve().parent

def analyze(data,player):
 counts=collections.Counter();pos=0;rerolls=0;roundno=None;frame=0;selected_at={}
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n
  frame+=1
  if op in (1003,1113):
   fields=replay.protobuf(raw);room=replay.protobuf(replay.first(fields,1 if op==1003 else 2,b''));current=replay.first(room,14)
   if 0<current<1000000:roundno=current
   continue
  if op==1015:
   current=replay.first(replay.protobuf(raw),1);roundno=current if 0<current<1000000 else None
   continue
  if op!=5212:continue
  f=replay.protobuf(raw)
  if replay.first(f,1)!=player:continue
  if replay.first(f,3):rerolls+=1;continue
  relic=replay.first(f,2)
  if relic>0:
   counts[relic]+=1
   selected_at.setdefault(str(relic),{'round':roundno,'frame':frame})
 return {'version':VERSION,'choices':dict(counts),'selected_at':selected_at,'rerolls':rerolls,'basis':'SelectRelicS2C, positive RelicId, IsReroll=false'}

def panel(values,total,key,title,db=None,match=False):
 resolve_name=game_catalog.resolver('chips',db)
 metadata=game_catalog.metadata('chips',db)
 counts=collections.Counter();rerolls=0;selected_at={}
 for v in values:
  counts.update({int(k):int(n) for k,n in v.get('choices',{}).items()});rerolls+=v.get('rerolls',0)
  if match:
   for rid,timing in v.get('selected_at',{}).items():selected_at.setdefault(int(rid),timing)
 analyzed=len(values)
 ordered=sorted(counts.items(),key=(lambda item:(selected_at.get(item[0],{}).get('frame',2**63),item[0])) if match else (lambda item:(-item[1],item[0])))
 def turn_label(rid):
  turn=selected_at.get(rid,{}).get('round')
  return str(turn)+'턴' if type(turn) is int and turn>0 else '턴 미확인'
 return {'key':key,'title':title,'analyzed':analyzed,'total':total,'selections':sum(counts.values()),'rerolls':rerolls,'items':[{'selected_turn':selected_at.get(rid,{}).get('round') if match else None,'turn_label':turn_label(rid) if match else '', 'chip_id':rid,'rarity':metadata.get(rid,{}).get('rarity',0),'name':resolve_name(rid),'count':n,'average':f'{n/analyzed:.2f}'.rstrip('0').rstrip('.') if analyzed else '-'} for rid,n in ordered]}

def aggregate(db,owner,mode=None,map_id=None,hero_id=None,rid=None):
 import astral_personal_data as personal
 where='m.owner_id=?';params=[owner]
 for col,value in (('map_type',mode),('map_id',map_id),('hero_id',hero_id),('replay_id',rid)):
  if value is not None:where+=' AND m.'+col+'=?';params.append(value)
 rows=db.execute('SELECT i.version,i.summary FROM personal_matches m LEFT JOIN personal_match_insights i ON i.replay_id=m.replay_id AND i.owner_id=m.owner_id WHERE '+where,params).fetchall();values=[]
 for version,raw in rows:
  if version is None or version<personal.INSIGHTS_VERSION:continue
  p=next((p for p in json.loads(raw)['participants'] if p['player_id']==owner),None)
  if p and p.get('chips',{}).get('version',0)>=1:values.append(p['chips'])
 return panel(values,len(rows),'overview','선택한 칩 · 현재 필터 기준',db=db,match=rid is not None)
