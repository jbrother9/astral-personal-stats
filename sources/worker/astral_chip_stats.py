"""Confirmed relic choices, excluding rerolls. Shop selection indices are not relic IDs."""
import collections,json,pathlib,struct
import astral_replay as replay
import astral_game_catalog as game_catalog
VERSION=1
ROOT=pathlib.Path(__file__).resolve().parent

def analyze(data,player):
 counts=collections.Counter();pos=0;rerolls=0
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n
  if op!=5212:continue
  f=replay.protobuf(raw)
  if replay.first(f,1)!=player:continue
  if replay.first(f,3):rerolls+=1;continue
  relic=replay.first(f,2)
  if relic>0:counts[relic]+=1
 return {'version':VERSION,'choices':dict(counts),'rerolls':rerolls,'basis':'SelectRelicS2C, positive RelicId, IsReroll=false'}

def panel(values,total,key,title,db=None):
 resolve_name=game_catalog.resolver('chips',db)
 metadata=game_catalog.metadata('chips',db)
 counts=collections.Counter();rerolls=0
 for v in values:
  counts.update({int(k):int(n) for k,n in v.get('choices',{}).items()});rerolls+=v.get('rerolls',0)
 analyzed=len(values)
 return {'key':key,'title':title,'analyzed':analyzed,'total':total,'selections':sum(counts.values()),'rerolls':rerolls,'items':[{'chip_id':rid,'rarity':metadata.get(rid,{}).get('rarity',0),'name':resolve_name(rid),'count':n,'average':f'{n/analyzed:.2f}'.rstrip('0').rstrip('.') if analyzed else '-'} for rid,n in sorted(counts.items(),key=lambda x:(-x[1],x[0]))]}

def aggregate(db,owner,mode=None,map_id=None,hero_id=None,rid=None):
 import astral_personal_data as personal
 where='m.owner_id=?';params=[owner]
 for col,value in (('map_type',mode),('map_id',map_id),('hero_id',hero_id),('replay_id',rid)):
  if value is not None:where+=' AND m.'+col+'=?';params.append(value)
 rows=db.execute('SELECT i.version,i.summary FROM personal_matches m LEFT JOIN personal_match_insights i ON i.replay_id=m.replay_id AND i.owner_id=m.owner_id WHERE '+where,params).fetchall();values=[]
 for version,raw in rows:
  if version is None or version<personal.INSIGHTS_VERSION:continue
  p=next((p for p in json.loads(raw)['participants'] if p['player_id']==owner),None)
  if p and p.get('chips',{}).get('version',0)>=VERSION:values.append(p['chips'])
 return panel(values,len(rows),'overview','선택한 칩 · 현재 필터 기준',db=db)
