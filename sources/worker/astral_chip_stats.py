"""Confirmed relic choices, excluding rerolls. Shop selection indices are not relic IDs."""
import collections,json,pathlib,struct
import astral_replay as replay
import astral_game_catalog as game_catalog
from astral_card_stats import fixed32_values
VERSION=5
ROOT=pathlib.Path(__file__).resolve().parent

def shared_grant_frames(data):
 # Only classify complete-roster batches without an outstanding choice action.
 # Normal selections of coincidentally identical chips must keep their candidates.
 players=set();groups=collections.defaultdict(list);pending=set();seen=set();pos=frame=0;turn=None
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;f=replay.protobuf(data[pos:pos+n]);pos+=n;frame+=1
  if op in (1003,1113):
   room=replay.protobuf(replay.first(f,1 if op==1003 else 2,b''));current=replay.first(room,14)
   if 0<current<1000000:turn=current
   for encoded in room.get(9,[]):
    pid=replay.first(replay.protobuf(encoded),1)
    if pid>0:players.add(pid)
  elif op==1015:
   current=replay.first(f,1);turn=current if 0<current<1000000 else None
  elif op==1002:
   for encoded in f.get(2,[]):
    action=replay.protobuf(encoded);pid=replay.first(action,2);sn=replay.first(action,4)
    if replay.first(action,1)!=5211 or (sn and (pid,sn) in seen):continue
    if sn:seen.add((pid,sn))
    pending.add(pid)
  elif op==5212 and not replay.first(f,3):
   pid=replay.first(f,1);chip=replay.first(f,2)
   if chip>0:
    groups[(turn,chip)].append((pid,frame,pid in pending));pending.discard(pid)
 if len(players)<2:return set()
 return {frame for (turn,chip),batch in groups.items() if turn is not None and len(batch)==len(players) and {pid for pid,_,_ in batch}==players and not any(waiting for _,_,waiting in batch) for _,frame,_ in batch}

def analyze(data,player,shared_frames=None):
 if shared_frames is None:shared_frames=shared_grant_frames(data)
 counts=collections.Counter();pos=0;rerolls=0;roundno=None;frame=0;selected_at={};sessions=[];pending=None;seen={}
 bracelet_owned=False;feather_uses=0;last_feather_frame=0;recent=collections.deque(maxlen=4)
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n
  frame+=1
  recent.append((op,raw))
  if op in (1003,1113):
   fields=replay.protobuf(raw);room=replay.protobuf(replay.first(fields,1 if op==1003 else 2,b''));current=replay.first(room,14)
   if 0<current<1000000:roundno=current
   for encoded in room.get(9,[]):
    person=replay.protobuf(encoded)
    if replay.first(person,1)!=player or 10 not in person:continue
    hero=replay.protobuf(replay.first(person,10,b''))
    # Definition IDs verified in the runtime cache and replay source fields;
    # these are effect rules, not a name/rarity reference table.
    owned=any(replay.first(replay.protobuf(v),1)==50067 and replay.first(replay.protobuf(v),2)>0 for v in hero.get(52,[]))
    if not owned or not bracelet_owned:feather_uses=0;last_feather_frame=0
    bracelet_owned=owned
   continue
  if op==1015:
   current=replay.first(replay.protobuf(raw),1);roundno=current if 0<current<1000000 else None
   continue
  if op==5056:
   f=replay.protobuf(raw)
   if replay.first(f,1)==player:
    last_feather_frame=0
    if bracelet_owned and replay.first(f,2)==21020:
     feather_uses+=1
     if feather_uses%3==0:last_feather_frame=frame
   continue
  if op==1002:
   f=replay.protobuf(raw)
   for encoded in f.get(2,[]):
    action=replay.protobuf(encoded)
    if replay.first(action,1)!=5211 or replay.first(action,2)!=player:continue
    request=replay.protobuf(replay.first(action,3,b''));sn=replay.first(action,4)
    offers=fixed32_values(request.get(2,[]));context=(replay.first(f,1),replay.first(request,5),replay.first(request,6))
    identity=(tuple(offers),context)
    if sn and seen.get(sn)==identity:continue # Includes repeats after a completed selection.
    conflict=bool(sn and sn in seen)
    if sn:seen[sn]=identity
    if pending is not None and pending['context'] is None:pending['context']=context
    if pending is None or pending['context']!=context:
     pending={'context':context,'stages':[],'ambiguous':pending is not None}
    pending['ambiguous']|=conflict or not sn
    stage={'frame':frame,'action_sn':sn,'offers':offers}
    if pending['stages'] and pending['stages'][-1].get('awaiting'):
     pending['stages'][-1]=stage
    else:
     # Refreshes normally arrive as a new action, without an IsReroll S2C response.
     if pending['stages'] and sn and not pending['ambiguous']:rerolls+=1
     pending['stages'].append(stage)
   continue
  if op!=5212:continue
  f=replay.protobuf(raw)
  if replay.first(f,1)!=player:continue
  if replay.first(f,3):
   rerolls+=1
   if pending is None:pending={'context':None,'stages':[{'offers':[]}],'ambiguous':True}
   pending['stages'].append({'frame':frame,'offers':[],'awaiting':True})
   continue
  relic=replay.first(f,2)
  if relic>0:
   counts[relic]+=1
   selected_at.setdefault(str(relic),{'round':roundno,'frame':frame})
   stages=pending['stages'] if pending else []
   confirmed=bool(stages and relic in stages[-1]['offers'] and not pending['ambiguous'])
   # Observed reward chain: third feather use, hand update, attribute update,
   # then a direct grant. Never replace an actual candidate selection.
   chain=list(recent);reward_chain=[op for op,_ in chain]==[5056,1109,1040,5212] and replay.first(replay.protobuf(chain[1][1]),2)==player and replay.first(replay.protobuf(chain[2][1]),1)==player
   reward_source=50067 if bracelet_owned and not stages and frame not in shared_frames and last_feather_frame>0 and frame-last_feather_frame==3 and reward_chain else 0
   sessions.append({'selection_id':frame,'round':roundno,'chip_id':relic,'stages':stages,'connection_confirmed':confirmed,'acquisition_only':bool(reward_source) or (frame in shared_frames and not stages),'reward_source_relic_id':reward_source})
   last_feather_frame=0
   if relic==50067 and not bracelet_owned:bracelet_owned=True;feather_uses=0
   pending=None # A later grant must never inherit a completed choice's candidates.
 return {'version':VERSION,'choices':dict(counts),'selected_at':selected_at,'rerolls':rerolls,'sessions':sessions,'basis':'SelectRelicS2C, positive RelicId, IsReroll=false; candidates from PredictActionS2C SelectRelicC2S actions'}

def panel(values,total,key,title,db=None,match=False):
 resolve_name=game_catalog.resolver('chips',db)
 metadata=game_catalog.metadata('chips',db)
 counts=collections.Counter();rerolls=0;selected_at={};sessions=[]
 for v in values:
  counts.update({int(k):int(n) for k,n in v.get('choices',{}).items()});rerolls+=v.get('rerolls',0)
  if match:
   if v.get('version',0)>=3:sessions.extend(v.get('sessions',[]))
   for rid,timing in v.get('selected_at',{}).items():selected_at.setdefault(int(rid),timing)
 analyzed=len(values)
 ordered=sorted(counts.items(),key=(lambda item:(selected_at.get(item[0],{}).get('frame',2**63),item[0])) if match else (lambda item:(-item[1],item[0])))
 def turn_label(rid):
  turn=selected_at.get(rid,{}).get('round')
  return str(turn)+'턴' if type(turn) is int and turn>0 else '턴 미확인'
 items=[{'selected_turn':selected_at.get(rid,{}).get('round') if match else None,'turn_label':turn_label(rid) if match else '', 'chip_id':rid,'rarity':metadata.get(rid,{}).get('rarity',0),'name':resolve_name(rid),'count':n,'average':f'{n/analyzed:.2f}'.rstrip('0').rstrip('.') if analyzed else '-','detail_rows':[['후보','확인 불가 · 후보 기록 재분석이 필요합니다.'],['최종 선택',resolve_name(rid)]],'detail_note':'같은 칩을 여러 번 선택한 과거 기록은 재분석 후 선택별로 표시합니다.'} for rid,n in ordered]
 if match and sessions:
  items=[]
  for session in sorted(sessions,key=lambda s:s['selection_id']):
   rid=session['chip_id'];turn=session.get('round');confirmed=session['connection_confirmed'];stages=session['stages'];acquisition_only=session.get('acquisition_only',False)
   detail_rows=[]
   for i,stage in enumerate(stages):
    text=' / '.join(resolve_name(x) for x in stage['offers']) or '후보 확인 불가'
    if not confirmed:text+=' (최종 선택 연결 미확인)'
    detail_rows.append(['최초 후보' if i==0 else '새로고침 '+str(i),text])
   if not stages:detail_rows.append(['최초 후보','확인 불가 · 연결되는 후보 기록이 없습니다.'])
   detail_rows.append(['최종 선택',resolve_name(rid)])
   label=str(turn)+'턴' if type(turn) is int and turn>0 else '턴 미확인';source=session.get('reward_source_relic_id',0)
   acquisition_label=resolve_name(source)+' 보상' if source else ''
   if acquisition_only:
    if not source:label+='에 획득'
    detail_rows=[]
   items.append({'selection_id':session['selection_id'],'acquisition_only':acquisition_only,'acquisition_label':acquisition_label,'selected_turn':turn,'turn_label':label,'chip_id':rid,'rarity':metadata.get(rid,{}).get('rarity',0),'name':resolve_name(rid),'count':1,'average':'1','connection_confirmed':confirmed,'detail_rows':detail_rows,'detail_note':('' if acquisition_only else '후보 변경 순서 기준입니다. 실제 창 열람 여부는 확인할 수 없습니다.' if confirmed else '후보와 최종 선택의 연결을 확정할 수 없습니다. 후보 누락 또는 별도 지급일 수 있습니다.')})
 return {'key':key,'title':title,'analyzed':analyzed,'total':total,'selections':sum(counts.values()),'rerolls':rerolls,'items':items}

def aggregate(db,owner,mode=None,map_id=None,hero_id=None,rid=None,difficulty=None):
 import astral_personal_data as personal
 where='m.owner_id=?';params=[owner]
 for col,value in (('map_type',mode),('map_id',map_id),('hero_id',hero_id),('replay_id',rid)):
  if value is not None:where+=' AND m.'+col+'=?';params.append(value)
 extra,args=personal.difficulty_clause('m',difficulty);where+=extra;params+=args
 rows=db.execute('SELECT i.version,i.summary FROM personal_matches m LEFT JOIN personal_match_insights i ON i.replay_id=m.replay_id AND i.owner_id=m.owner_id WHERE '+where,params).fetchall();values=[]
 for version,raw in rows:
  if version is None or version<7:continue
  p=next((p for p in json.loads(raw)['participants'] if p['player_id']==owner),None)
  if p and p.get('chips',{}).get('version',0)>=1:values.append(p['chips'])
 return panel(values,len(rows),'overview','선택한 칩 · 현재 필터 기준',db=db,match=rid is not None)
