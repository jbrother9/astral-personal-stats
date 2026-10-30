"""Read-only replay summaries. No SDK credentials are read or exported."""
import collections,datetime,hashlib,re,struct
from astral_protobuf import protobuf
MAX_REPLAY_BYTES=64*1024*1024
COMMANDS={1003:'RunningGameS2C',1016:'GameFinishS2C',1015:'RoundStartS2C',1040:'UpdateHeroAttrS2C',1113:'ReplaySnapshotS2C',1115:'ReplayDieS2C',5056:'UseEffectCard',5036:'BattleUseCard',5022:'ThrowDice',5028:'Move'}
def first(fields,key,default=0):return fields.get(key,[default])[0]
def text(value):return value.decode('utf8') if isinstance(value,bytes) else str(value)
def valid_id(value):return isinstance(value,str) and re.fullmatch(r'\d{10,24}',value) is not None
def kst(timestamp):
 if not timestamp:return None
 return datetime.datetime.fromtimestamp(timestamp,datetime.timezone(datetime.timedelta(hours=9))).isoformat()
def finish_info(payload,strict=True):
 f=protobuf(payload);rid=text(first(f,11,b''))
 if strict and not valid_id(rid):raise ValueError('Missing or unsupported replay ID')
 ranks={}
 for raw in f.get(14,[]):
  if not isinstance(raw,bytes):
   if strict:raise ValueError('Unsupported rank format')
   continue
  item=protobuf(raw);ranks[first(item,1)]=first(item,2)
 return {'replay_id':rid,'room_id':first(f,1),'finish_time':first(f,3),'version':text(first(f,12,b'')),'map_type':first(f,13),'ranks':ranks}
def parse_replay(data,expected_id=None):
 if not data or len(data)>MAX_REPLAY_BYTES:raise ValueError('Replay size outside limits')
 pos=0;counts=collections.Counter();players={};room_info={};finish=None;hp=gold=0
 while pos<len(data):
  if pos+6>len(data):raise ValueError('Incomplete replay header')
  op,size=struct.unpack_from('>HI',data,pos);pos+=6
  if size>len(data)-pos:raise ValueError('Incomplete replay payload')
  payload=data[pos:pos+size];pos+=size;counts[COMMANDS.get(op,str(op))]+=1
  if op in (1003,1113):
   f=protobuf(payload);raw=first(f,1 if op==1003 else 2,None)
   if raw is not None:
    room=protobuf(raw)
    room_info.update(room_id=first(room,1),map_id=first(room,5),map_type=first(room,36),start_time=first(room,30))
    for raw_player in room.get(9,[]):
     p=protobuf(raw_player);pid=first(p,1);hero=protobuf(first(p,10,b''))
     if pid:players[pid]={'player_id':pid,'name':text(first(p,2,b'')),'slot':first(p,6),'account_level':first(p,25),'hero_id':first(hero,2),'level':first(hero,33),'gold':first(hero,7),'hp':first(hero,14),'standing_painting_id':players.get(pid,{}).get('standing_painting_id') or first(hero,46),'equipped_hero_id':players.get(pid,{}).get('equipped_hero_id') if players.get(pid,{}).get('standing_painting_id') else first(hero,2)}
  elif op==1016:finish=finish_info(payload)
  elif op==1040:
   for raw in protobuf(payload).get(4,[]):
    effect=protobuf(raw);hp+=int(3 in effect);gold+=int(2 in effect)
 if not finish:raise ValueError('Replay has no settlement')
 if expected_id is not None and finish['replay_id']!=expected_id:raise ValueError('Replay ID mismatch')
 for pid,p in players.items():p['rank']=finish['ranks'].get(pid)
 return {'schema_version':1,'replay_id':finish['replay_id'],'room_id':finish['room_id'],'map_id':room_info.get('map_id'),'map_type':finish['map_type'],'start_time':room_info.get('start_time'),'start_time_kst':kst(room_info.get('start_time')),'finish_time':finish['finish_time'],'finish_time_kst':kst(finish['finish_time']),'version':finish['version'],'participants':list(players.values()),'frame_count':sum(counts.values()),'message_counts':dict(counts),'hp_effect_entries':hp,'gold_effect_entries':gold,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'statistics_note':'Message counts are not damage totals. Player attributes are from the last available room snapshot, not necessarily final settlement.'}
