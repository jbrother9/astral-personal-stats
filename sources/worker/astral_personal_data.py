"""Personal-only replay analysis and SQLite index. Does not store other players' profiles."""
import json,pathlib,struct,time
import astral_replay as replay
import astral_card_stats as cards
import astral_chip_stats as chips
import astral_combat_dice as dice
import astral_paths as paths
import astral_game_catalog as game_catalog
VERSION=10
ROOT=pathlib.Path(__file__).resolve().parent
DEFAULTS={'player_id':0,'cache_days':7,'cache_enabled':True}
def settings():
 try:return {**DEFAULTS,**json.loads(paths.settings_file().read_text(encoding='utf8'))}
 except (OSError,ValueError):return dict(DEFAULTS)
def save_settings(value):
 p=paths.settings_file();p.parent.mkdir(parents=True,exist_ok=True);tmp=p.with_suffix('.json.part');tmp.write_text(json.dumps(value,indent=2),encoding='utf8');tmp.replace(p)
def init(db):
 db.executescript('''
 CREATE TABLE IF NOT EXISTS personal_matches(
 replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,finished_at INTEGER,started_at INTEGER,
 hero_id INTEGER,map_id INTEGER,map_type INTEGER,rank INTEGER,duration INTEGER,
 turns INTEGER,deaths INTEGER,npc_last_hits INTEGER,effect_cards INTEGER,battle_cards INTEGER,skills INTEGER,
 analysis_version INTEGER NOT NULL,PRIMARY KEY(replay_id,owner_id));
 CREATE TABLE IF NOT EXISTS personal_match_context(replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,difficulty INTEGER,map_difficulty_id INTEGER,PRIMARY KEY(replay_id,owner_id));
 CREATE TABLE IF NOT EXISTS personal_events(
 replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,frame INTEGER NOT NULL,ordinal INTEGER NOT NULL,
 round INTEGER,own_turn INTEGER,kind TEXT NOT NULL,detail TEXT NOT NULL,
 PRIMARY KEY(replay_id,owner_id,frame,ordinal));
 CREATE TABLE IF NOT EXISTS personal_game_totals(replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,metrics TEXT NOT NULL,PRIMARY KEY(replay_id,owner_id));
 CREATE TABLE IF NOT EXISTS personal_cache(replay_id TEXT PRIMARY KEY,managed INTEGER NOT NULL DEFAULT 0,pinned INTEGER NOT NULL DEFAULT 0,acquired_at REAL NOT NULL);
 CREATE TABLE IF NOT EXISTS personal_match_insights(replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,version INTEGER NOT NULL,summary TEXT NOT NULL,PRIMARY KEY(replay_id,owner_id));
 CREATE TABLE IF NOT EXISTS personal_appearances(replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,participants TEXT NOT NULL,PRIMARY KEY(replay_id,owner_id));
 CREATE TABLE IF NOT EXISTS personal_card_stats(replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,version INTEGER NOT NULL,summary TEXT NOT NULL,PRIMARY KEY(replay_id,owner_id));
 CREATE INDEX IF NOT EXISTS personal_matches_owner_mode ON personal_matches(owner_id,map_type);
 CREATE INDEX IF NOT EXISTS personal_events_owner_kind ON personal_events(owner_id,kind);
 ''');db.commit()
def signed32(x):return x-2**32 if x>=2**31 else x

def fixed64_target_ids(values):
 # UseEffectCardS2C uses FieldCodec.ForSFixed64, including its packed representation.
 result=[]
 for value in values:
  if type(value) is int:
   result.append(value-2**64 if value>=2**63 else value)
  elif isinstance(value,bytes):
   if len(value)%8:raise ValueError('Invalid packed sfixed64 target IDs')
   result.extend(struct.unpack('<'+'q'*(len(value)//8),value))
  else:raise ValueError('Unsupported target ID representation')
 return list(dict.fromkeys(value for value in result if value>0))


def move_skill_attributes(raw):
 # HeroSkillMoveEffectS2C.EffectData is a node -> HeroSkillMoveEffect map.
 # Each value.Effects contains full UpdateHeroAttrS2C messages, not target IDs.
 outer=replay.protobuf(raw);index=0
 for entry in outer.get(2,[]):
  pair=replay.protobuf(entry);value=replay.protobuf(replay.first(pair,2,b''))
  for body in value.get(1,[]):
   yield index,replay.first(outer,1),replay.protobuf(body)
   index+=1


def gold_transfers(data,players,include_skills=True):
 # Explicit declaration + attributable debit/credit. Amounts need not be equal.
 from collections import deque
 window=deque(maxlen=3);result=[];pos=frame=roundno=0
 def change(packet,pid,cause_type,skill=0):
  _,op,raw,_=packet
  if op!=1040:return None
  f=replay.protobuf(raw);cause=replay.protobuf(replay.first(f,2,b''))
  if replay.first(cause,1)!=cause_type or skill and replay.first(cause,3)!=skill:return None
  values=[]
  for raw_effect in f.get(4,[]):
   effect=replay.protobuf(raw_effect)
   if replay.first(effect,1)!=pid or 2 not in effect:continue
   value=replay.protobuf(replay.first(effect,2,b''))
   if replay.first(value,1)!=pid:return None
   values.append(signed32(replay.first(value,4))-signed32(replay.first(value,3)))
  return values[0] if len(values)==1 else None
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1
  if op==1015:roundno=replay.first(replay.protobuf(raw),1)
  elif op in (1003,1113):
   room=replay.protobuf(replay.first(replay.protobuf(raw),1 if op==1003 else 2,b''));roundno=replay.first(room,14,roundno)
  if op==1096 and include_skills:
   for index,actor,f in move_skill_attributes(raw):
    sender=replay.first(f,1);cause=replay.protobuf(replay.first(f,2,b''));changes=[]
    if sender!=actor or sender not in players or replay.first(cause,1)!=1 or not replay.first(cause,3):continue
    for body in f.get(4,[]):
     e=replay.protobuf(body)
     if 2 not in e:continue
     g=replay.protobuf(replay.first(e,2,b''));pid=replay.first(e,1)
     if replay.first(g,1)!=pid:changes=[];break
     delta=signed32(replay.first(g,4))-signed32(replay.first(g,3))
     if delta:changes.append((pid,delta))
    debit=[v for pid,v in changes if pid==sender and v<0];credits=[(pid,v) for pid,v in changes if pid!=sender and pid in players and v>0]
    if len(changes)!=2 or len(debit)!=1 or len(credits)!=1:continue
    recipient,credit=credits[0]
    skill=replay.first(cause,3);paid=-debit[0];sender_gain=0
    # Verified Skill 30512: gain 1 while passing an ally, then transfer 3.
    # The replay merges these into a net sender debit of 2. Do not generalize
    # this decomposition to other skills, chips, or differing amounts.
    if skill==30512 and paid==2 and credit==3:paid=3;sender_gain=1
    result.append({'sender':sender,'recipient':recipient,'amount':paid,'received':credit,'sender_gain':sender_gain,'net_debit':-debit[0],'source':'skill','source_skill_id':skill,'round':roundno,'debit_frame':frame,'credit_frame':frame,'nested_index':index})
  window.append((frame,op,raw,roundno))
  if len(window)!=3:continue
  declaration=window[0];op=declaration[1]
  if op!=5216 and (not include_skills or op!=5056):continue
  f=replay.protobuf(declaration[2]);sender=replay.first(f,1);skill=0
  if op==5216:
   recipient=replay.first(f,3);cause_type=14;source='shop'
   if f.get(2):continue
  else:
   selected=fixed64_target_ids(f.get(3,[]));skill=replay.first(f,9)
   if not replay.first(f,8) or not skill or len(selected)!=1:continue
   recipient=selected[0];cause_type=1;source='skill'
  if sender==recipient or sender not in players or recipient not in players:continue
  debit=change(window[1],sender,cause_type,skill);credit=change(window[1],recipient,cause_type,skill);credit_packet=window[1]
  if credit is None:credit=change(window[2],recipient,cause_type,skill);credit_packet=window[2]
  if debit is None or credit is None or debit>=0 or credit<=0:continue
  result.append({'sender':sender,'recipient':recipient,'amount':-debit,'received':credit,'source':source,'source_skill_id':skill,'round':declaration[3],'debit_frame':window[1][0],'credit_frame':credit_packet[0]})
 return result


def shop_transfers(data,players):
 return gold_transfers(data,players,include_skills=False)


def kill_gold_rewards(data,players):
 # Pending payouts are grouped by killer and recorded wallet amount. Multiple
 # deaths may precede all their credits. Explicitly sourced effects are kept
 # separate; a credit is consumed only once and only for the same killer/amount.
 wallets={};pending={};rewards={};pos=frame=0
 def clear_actor(actor):
  for key in list(pending):
   if key[0]==actor:pending.pop(key,None)
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1
  for key,group in list(pending.items()):
   if frame-group['deaths'][0]['frame']>64:pending.pop(key,None)
  if op in (1003,1113,1018):
   fields=replay.protobuf(raw)
   if op in (1003,1113):pending.clear()
   monsters=fields.get(1,[]) if op==1018 else replay.protobuf(replay.first(fields,1 if op==1003 else 2,b'')).get(10,[])
   for b in monsters:
    person=replay.protobuf(b);hero=replay.protobuf(replay.first(person,10,b''));wallets[replay.first(person,1)]=signed32(replay.first(hero,7))
  elif op==1115:
   f=replay.protobuf(raw);victim,killer=replay.first(f,1),replay.first(f,3)
   if victim in players:clear_actor(victim)
   elif killer in players and wallets.get(victim,0)>0:
    key=(killer,wallets[victim]);group=pending.setdefault(key,{'deaths':[],'credited':0})
    group['deaths'].append({'frame':frame,'victim_id':victim,'hero_id':replay.first(f,2)})
  # Round-start broadcasts can precede a queued payout from the last kill.
  # Actual player actions below, snapshots, and the bounded window end it.
  elif op in (5056,5036,5216):
   # A new action by the killer ends the causal window. NPC automatic skill
   # notifications and another player's action must not erase their payouts.
   actor=replay.first(replay.protobuf(raw),1)
   if actor in players:clear_actor(actor)
  elif op==1040:
   f=replay.protobuf(raw);cause=replay.protobuf(replay.first(f,2,b''));changes=[];level_players=set()
   for b in f.get(4,[]):
    effect=replay.protobuf(b);pid=replay.first(effect,1)
    if 11 in effect:level_players.add(pid)
    if 2 not in effect:continue
    gold=replay.protobuf(replay.first(effect,2,b''));after=signed32(replay.first(gold,4));before=signed32(replay.first(gold,3))
    if pid not in players:wallets[pid]=after
    if after!=before:changes.append((pid,after-before))
   # A skill/card/buff origin is independent evidence for that effect, not
   # the source-less NPC wallet payout. No individual skill IDs are required.
   if replay.first(cause,1) or replay.first(cause,3):continue
   debited={pid for pid,delta in changes if delta<0}
   for pid in level_players-debited:clear_actor(pid)
   if sum(delta>0 for _,delta in changes)>1:
    for pid,delta in changes:
     if delta>0:clear_actor(pid)
    continue
   for pid,amount in changes:
    if pid not in players or amount<=0:continue
    if pid in level_players:clear_actor(pid);continue
    key=(pid,amount);group=pending.get(key)
    if group is None:
     clear_actor(pid);continue
    deaths=group['deaths'];heroes=sorted({v['hero_id'] for v in deaths})
    reward={'killer':pid,'amount':amount,'frame':min(v['frame'] for v in deaths),'hero_id':heroes[0] if len(heroes)==1 else 0,'hero_ids':heroes,'victim_id':deaths[0]['victim_id'] if len(deaths)==1 else None,'victim_ids':[v['victim_id'] for v in deaths],'ambiguous_individual':len(deaths)>1}
    # All source-less positive changes in one update must be unambiguous at
    # the recipient level; otherwise retaining an explicit unknown is safer.
    if sum(recipient==pid for recipient,_ in changes)!=1:clear_actor(pid);continue
    rewards[frame]=reward;group['credited']+=1
    if group['credited']==len(deaths):pending.pop(key,None)
 return rewards


def purchase_gold_cards(data,players):
 # Use confirmed closing offers/sold slots, not newly acquired hand cards or inferred prices.
 sessions={};batches=[];awaiting=None;pos=frame=0
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1
  if awaiting is not None:
   batch=awaiting;awaiting=None
   if op==1040:
    f=replay.protobuf(raw);cause=replay.protobuf(replay.first(f,2,b''));cost=[]
    if replay.first(cause,1)==14:
     for b in f.get(4,[]):
      e=replay.protobuf(b)
      if replay.first(e,1)!=batch['player_id'] or 2 not in e:continue
      gold=replay.protobuf(replay.first(e,2,b''))
      if replay.first(gold,1)==batch['player_id']:cost.append(signed32(replay.first(gold,4))-signed32(replay.first(gold,3)))
     if len(cost)==1 and cost[0]<0:batch['debit_frame']=frame
  if op in (1015,1003,1113):sessions.clear()
  if op!=5216:continue
  f=replay.protobuf(raw);pid=replay.first(f,1)
  if pid not in players:continue
  try:choices=cards.fixed32_values(f.get(2,[]))
  except ValueError:sessions.pop(pid,None);continue
  session=sessions.setdefault(pid,[])
  if choices:
   batch={'player_id':pid,'indices':choices};session.append(batch);batches.append(batch);awaiting=batch
  if not replay.first(f,6):continue
  try:
   offers=cards.fixed32_values(f.get(4,[]));sold=[]
   for value in f.get(5,[]):sold.extend([value] if isinstance(value,int) else value)
   chosen=[idx for batch in session for idx in batch['indices']]
   valid=bool(chosen) and len(offers)==len(sold) and all(v in (0,1) for v in sold) and len(chosen)==len(set(chosen)) and set(chosen)=={i for i,v in enumerate(sold) if v} and all(0<=i<len(offers) and offers[i]>0 for i in chosen)
   if valid:
    for batch in session:batch['card_ids']=[offers[i] for i in batch['indices']]
  except ValueError:pass
  sessions.pop(pid,None)
 return {b['debit_frame']:{'player_id':b['player_id'],'card_ids':b['card_ids']} for b in batches if 'debit_frame' in b and 'card_ids' in b}


def buff_effect_sources(data):
 # Resolve instance IDs in the holder's container at the effect frame, never from future records.
 active={};result={};pos=frame=0
 def read_buff(raw,previous=None):
  b=replay.protobuf(raw);map_id=replay.first(b,1)
  if isinstance(replay.first(b,2),bytes):b=replay.protobuf(replay.first(b,2,b''))
  uid=replay.first(b,1) or map_id;definition=replay.first(b,2)
  if type(uid) is not int or type(definition) is not int or uid<=0:return None
  old=previous.get(uid,{}) if previous else {};source=replay.protobuf(replay.first(b,50,b'')) if 50 in b else None
  return {'instance_id':uid,'definition_id':definition or old.get('definition_id',0),'source_type':replay.first(source,1) if source is not None else old.get('source_type',0),'source_id':replay.first(source,2) if source is not None else old.get('source_id',0)}
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1
  if op in (1003,1113):
   active.clear();room=replay.protobuf(replay.first(replay.protobuf(raw),1 if op==1003 else 2,b''))
   for b in room.get(9,[])+room.get(10,[]):
    person=replay.protobuf(b);holder=replay.first(person,1);hero=replay.protobuf(replay.first(person,10,b''));container={}
    for raw_buff in hero.get(4,[]):
     buff=read_buff(raw_buff)
     if buff and buff['definition_id']>0:container[buff['instance_id']]=buff
    active[holder]=container
  if op!=1040:continue
  f=replay.protobuf(raw);cause=replay.protobuf(replay.first(f,2,b''));holder=replay.first(f,1);uid=replay.first(cause,3)
  before=active.get(holder,{}).get(uid) if replay.first(cause,1)==7 else None
  for b in f.get(4,[]):
   effect=replay.protobuf(b)
   if 6 not in effect:continue
   change=replay.protobuf(replay.first(effect,6,b''));owner=replay.first(change,1) or replay.first(effect,1);operation=replay.first(change,3);container=active.setdefault(owner,{})
   if operation==0:
    replacement={}
    for raw_buff in change.get(4,[]):
     buff=read_buff(raw_buff)
     if buff and buff['definition_id']>0:replacement[buff['instance_id']]=buff
    active[owner]=replacement
   elif operation in (1,2,3) and 2 in change:
    buff=read_buff(replay.first(change,2,b''),container)
    if not buff:continue
    if operation==2:container.pop(buff['instance_id'],None)
    elif buff['definition_id']>0:container[buff['instance_id']]=buff
  if replay.first(cause,1)==7:
   buff=before or active.get(holder,{}).get(uid)
   if buff:result[frame]=dict(buff)
 return result


def land_effect_sources(data,layouts=None):
 # Resolve node types against the exact base layout and reported group states.
 # Unknown group membership/state must not silently reuse a stale base type.
 if layouts is None:layouts=game_catalog.land_layouts()
 lands={};states={};groups=None;result={};pos=frame=0;map_id=None;uncertain=False
 def resolve(node):
  kind=lands.get(node)
  if groups is None:return kind if not uncertain else None
  if any(group not in groups for group in states):return None
  overrides=[]
  for group_id,group in groups.items():
   if node not in group['nodes'] or group_id not in states:continue
   status=next((v for v in group['statuses'] if v['index']==states[group_id]),None)
   if status is None or not status['active']:return None
   overrides.append(status['land_type'] or kind)
  return overrides[0] if overrides and len(set(overrides))==1 else kind if not overrides else None
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1
  if op in (1003,1113):
   room=replay.protobuf(replay.first(replay.protobuf(raw),1 if op==1003 else 2,b''));lands={};states={};map_id=replay.first(room,5);data_id=replay.first(room,15);uncertain=bool(room.get(61));groups=None
   for body in room.get(12,[]):
    pair=replay.protobuf(body);land=replay.protobuf(replay.first(pair,2,b''));node=replay.first(pair,1)
    if replay.first(land,1)==node:lands[node]=replay.first(land,2)
   for body in room.get(61,[]):
    pair=replay.protobuf(body);states[replay.first(pair,1)]=replay.first(pair,2)
   matches=[v for v in layouts if v['map_id']==map_id and v['map_data_id']==data_id and {n['node_id']:n['land_type'] for n in v['base_nodes']}==lands]
   if len(matches)==1:groups={v['group_id']:v for v in matches[0]['groups']}
  elif op==1084:
   f=replay.protobuf(raw)
   if replay.first(f,1)==map_id:states[replay.first(f,2)]=replay.first(f,4);uncertain=True
  elif op==1040:
   cause=replay.protobuf(replay.first(replay.protobuf(raw),2,b''))
   if replay.first(cause,1)!=9:continue
   node=replay.first(cause,3);result[frame]={'node_id':node,'land_type':resolve(node)}
 return result


def support_skill_sources(data):
 # Anchor inferred targets to an actual active-skill use, not passive ticks.
 teams={};active=None;result={};pos=frame=0
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1;f=replay.protobuf(raw)
  if active and (frame-active['frame']>32 or op in (1002,1003,1113,1015,1026,5056)):active=None
  if op in (1003,1113):
   room=replay.protobuf(replay.first(f,1 if op==1003 else 2,b''));teams={replay.first(v,1):replay.first(replay.protobuf(replay.first(v,10,b'')),61) for b in room.get(9,[]) for v in [replay.protobuf(b)]}
  if op==5056 and replay.first(f,8):
   actor=replay.first(f,1);active={'frame':frame,'actor':actor,'team':teams.get(actor,0),'teams':dict(teams)};result[frame]=[]
   candidates=fixed64_target_ids(f.get(3,[]))
  elif active and op in (1040,1096):
   candidates=[]
   attrs=[f] if op==1040 else [attr for _,_,attr in move_skill_attributes(raw)]
   for attr in attrs:
    cause=replay.protobuf(replay.first(attr,2,b''))
    if replay.first(attr,1)!=active['actor'] or replay.first(cause,1)!=1:continue
    for b in attr.get(4,[]):
     effect=replay.protobuf(b)
     if any(k!=1 for k in effect):candidates.append(replay.first(effect,1))
  else:continue
  if active and active['team']>0:
   for target in candidates:
    if target!=active['actor'] and active['teams'].get(target)==active['team'] and target not in result[active['frame']]:result[active['frame']].append(target)
 return result


def cure_healing_sources(data):
 # CureNum is Hero field 64 / HeroAttrEffect field 15. Attribute effects
 # describe its before/after values; never infer stacks from a later snapshot.
 stacks={};pending=[];result={};pos=frame=0
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1;f=replay.protobuf(raw)
  if pending:
   if op==1026 and not replay.first(f,2):
    for key,pid,count in pending:
     if replay.first(f,1)==pid:result[key]={'stacks':count,'definition_id':10004}
   # Other automatic turn-start card/buff effects may finish before the
   # notification. An actual card-use/request message still breaks the chain;
   # never label a manually played healing card as the preceding cure tick.
   cause=replay.protobuf(replay.first(f,2,b'')) if op==1040 else {}
   effects=[replay.protobuf(v) for v in f.get(4,[])] if op==1040 else []
   keep=op==1040 and replay.first(cause,1) in (2,7) and all(3 not in v and 15 not in v for v in effects)
   # Automatic hand rebuilding can use UpdateHeroAttr's Cards field as well
   # as the standalone hand notification. No manual use/input may intervene.
   hand_refresh=op==1040 and not replay.first(cause,1) and not replay.first(cause,3) and bool(effects) and all((9 in v or 24 in v) and set(v)<= {1,9,24} and replay.first(v,1)==replay.first(f,1) for v in effects)
   keep=keep or hand_refresh
   holder=replay.first(f,1) if op==1040 else replay.first(f,2) if op==1109 else None
   if not ((keep or op==1109) and all(pid==holder and frame-key[0]<=12 for key,pid,count in pending)):pending=[]
  if op in (1003,1113):
   stacks.clear();room=replay.protobuf(replay.first(f,1 if op==1003 else 2,b''))
   for raw_person in room.get(9,[])+room.get(10,[]):
    person=replay.protobuf(raw_person);hero=replay.protobuf(replay.first(person,10,b''));stacks[replay.first(person,1)]=replay.first(hero,64)
  elif op==1040:
   cause=replay.protobuf(replay.first(f,2,b''));empty_cause=not replay.first(cause,1) and not replay.first(cause,3)
   for i,raw_effect in enumerate(f.get(4,[])):
    effect=replay.protobuf(raw_effect);pid=replay.first(effect,1)
    if 15 in effect:
     change=replay.protobuf(replay.first(effect,15,b''));before=signed32(replay.first(change,3));after=signed32(replay.first(change,4))
     stacks[pid]=after if before>=0 and after>=0 and replay.first(change,1)==pid else None
    if 3 not in effect or not empty_cause:continue
    hp=replay.protobuf(replay.first(effect,3,b''));before=signed32(replay.first(hp,3));after=signed32(replay.first(hp,4));requested=signed32(replay.first(hp,2));maximum=replay.first(hp,7,None);count=stacks.get(pid)
    if type(count) is int and count>0 and requested==count and before>0 and type(maximum) is int and before<after<=maximum and after==min(before+count,maximum) and not replay.first(hp,8) and not replay.first(hp,9):pending.append(((frame,i),pid,count))
 return result


def hp_maximum_sources(data):
 # MaxHp is the post-change maximum. Recover the earlier maximum only from
 # preceding snapshots/updates, never by copying the later value backwards.
 maximums={};result={};pos=frame=0
 def changes(fields,key):
  for index,body in enumerate(fields.get(4,[])):
   effect=replay.protobuf(body)
   if 3 not in effect:continue
   pid=replay.first(effect,1);hp=replay.protobuf(replay.first(effect,3,b''));after=replay.first(hp,7,None)
   result[key+(index,)]={'hp_max_before':maximums.get(pid),'hp_max_after':after}
   if type(after) is int and after>0:maximums[pid]=after
   else:maximums.pop(pid,None)
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1
  if op in (1003,1113):
   maximums.clear();room=replay.protobuf(replay.first(replay.protobuf(raw),1 if op==1003 else 2,b''))
   for body in room.get(9,[])+room.get(10,[]):
    person=replay.protobuf(body);hero=replay.protobuf(replay.first(person,10,b''));value=replay.first(hero,23,None)
    if type(value) is int and value>0:maximums[replay.first(person,1)]=value
  elif op==1018:
   person=replay.protobuf(replay.first(replay.protobuf(raw),1,b''));hero=replay.protobuf(replay.first(person,10,b''));pid=replay.first(person,1);value=replay.first(hero,23,None)
   if type(value) is int and value>0:maximums[pid]=value
   else:maximums.pop(pid,None)
  elif op==1040:changes(replay.protobuf(raw),(frame,))
  elif op==1096:
   for index,_,fields in move_skill_attributes(raw):changes(fields,(frame,index))
 return result


def death_effect_sources(data,buff_refs,land_refs,hp_refs):
 # A source-less ReplayDie can precede the forced HP loss. Match only a
 # nearby lethal effect, or a same-message ModifyNum expiry + full HP loss.
 result={};lethal={};waiting={};pos=frame=0
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1;f=replay.protobuf(raw)
  if op in (1003,1113,1015):lethal.clear();waiting.clear()
  elif op==5056:lethal.pop(replay.first(f,1),None);waiting.pop(replay.first(f,1),None)
  elif op==1115:
   pid=replay.first(f,1);killer=replay.first(f,3);hit=lethal.pop(pid,None)
   if not killer:
    if hit and frame-hit['hp_frame']<=5 and not hit['source'] and hit['cause_type']:
     result[frame]={k:v for k,v in hit.items() if k!='source'}
    waiting[pid]=frame
  elif op==1040:
   cause=replay.protobuf(replay.first(f,2,b''));effects=[replay.protobuf(v) for v in f.get(4,[])]
   expired=set()
   for effect in effects:
    if 22 not in effect:continue
    change=replay.protobuf(replay.first(effect,22,b''));pid=replay.first(effect,1)
    if replay.first(change,1)==pid and replay.first(change,3)>0 and replay.first(change,4)==0 and signed32(replay.first(change,2))==-replay.first(change,3) and replay.first(change,5):expired.add(pid)
   for i,effect in enumerate(effects):
    if 3 not in effect:continue
    pid=replay.first(effect,1);hp=replay.protobuf(replay.first(effect,3,b''));before=signed32(replay.first(hp,3));after=signed32(replay.first(hp,4));lethal.pop(pid,None)
    if before<=0 or after>0:continue
    detail={'hp_frame':frame,'victim_id':pid,'hp_before':before,'hp_after':after,'damage_value':max(0,-signed32(replay.first(hp,2))),'cause_type':replay.first(cause,1),'cause_id':replay.first(cause,3),'damage_type':replay.first(hp,8),'buff':buff_refs.get(frame),'land':land_refs.get(frame),**hp_refs.get((frame,i),{})}
    detail['source']=replay.first(hp,9);lethal[pid]=detail
    death=waiting.pop(pid,None)
    if death and frame-death<=12 and pid in expired and not detail['cause_type'] and not detail['cause_id'] and not detail['source'] and signed32(replay.first(hp,2))<=-before:
     result[death]={**{k:v for k,v in detail.items() if k!='source'},'cause_type':7,'buff':{'definition_id':10009,'source_type':0,'source_id':0},'modifier_expired':True}
 return result


def terminal_unapplied_gold(data,summary):
 # Only the authoritative last room snapshot immediately followed by settlement
 # can contradict queued visual effects. Never subtract merely to fit a total.
 snapshots=[];changes=[];pos=frame=0;finish_frame=None
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1;f=replay.protobuf(raw)
  if op in (1003,1113):
   room=replay.protobuf(replay.first(f,1 if op==1003 else 2,b''));people={}
   for b in room.get(9,[]):
    person=replay.protobuf(b)
    if 10 not in person:continue
    hero=replay.protobuf(replay.first(person,10,b''))
    if 47 not in hero:continue
    cond=replay.protobuf(replay.first(hero,47,b''));people[replay.first(person,1)]=(signed32(replay.first(hero,7)),replay.first(cond,13),replay.first(cond,21))
   snapshots.append((frame,people));snapshots=snapshots[-2:]
  elif op==1040:
   for b in f.get(4,[]):
    effect=replay.protobuf(b)
    if 2 not in effect:continue
    gold=replay.protobuf(replay.first(effect,2,b''));pid=replay.first(effect,1);before=signed32(replay.first(gold,3));after=signed32(replay.first(gold,4))
    if before!=after:changes.append((frame,pid,before,after))
  elif op==1096:
   # Nested coin changes are not reconciled by this narrower direct-effect rule.
   for _,_,attr in move_skill_attributes(raw):
    for b in attr.get(4,[]):
     effect=replay.protobuf(b)
     if 2 in effect:changes.append((frame,replay.first(effect,1),None,None))
  elif op==1016:
   finish_frame=frame;break # Later cleanup effects do not supersede settlement.
 if len(snapshots)!=2 or finish_frame!=snapshots[-1][0]+1:return set()
 (before_frame,before_people),(after_frame,after_people)=snapshots;excluded=set()
 for pid,final in after_people.items():
  initial=before_people.get(pid)
  if initial is None or initial[2]!=final[2] or final[1]<initial[1]:continue
  official=summary.get('settlement',{}).get(str(pid),{}).get('gold_acquired')
  if official is None or official!=final[1]+final[2]:continue
  effects=[v for v in changes if before_frame<v[0]<after_frame and v[1]==pid]
  if not effects:continue
  # A confirmed prefix may contain real purchases and gains. Its wallet AND
  # acquisition counter must both equal the final snapshot. Only a unique,
  # subsequent positive-only suffix that the snapshot rejects is discarded.
  balance=initial[0];gained=0;candidates=[]
  for cut in range(len(effects)):
   if balance==final[0] and gained==final[1]-initial[1]:
    suffix_balance=balance;valid=True
    for _,_,before,after in effects[cut:]:
     if before is None or before!=suffix_balance or after<=before:valid=False;break
     suffix_balance=after
    if valid:candidates.append(cut)
   _,_,before,after=effects[cut]
   if before is None or before!=balance:break
   balance=after;gained+=max(0,after-before)
  if len(candidates)==1:excluded.update((fr,pid,before,after) for fr,pid,before,after in effects[candidates[0]:])
 return excluded


def analyze(data,summary,owner,transfers=None,kill_rewards=None,purchases=None,buff_refs=None,land_refs=None,hp_refs=None,unapplied_gold=None,death_refs=None):
 person=next((p for p in summary['participants'] if p['player_id']==owner),None)
 if person is None:raise ValueError('Own player not in replay')
 if buff_refs is None:buff_refs=buff_effect_sources(data)
 if land_refs is None:land_refs=land_effect_sources(data)
 if hp_refs is None:hp_refs=hp_maximum_sources(data)
 if purchases is None:purchases=purchase_gold_cards(data,{p['player_id'] for p in summary['participants']})
 if kill_rewards is None:kill_rewards=kill_gold_rewards(data,{p['player_id'] for p in summary['participants']})
 if transfers is None:transfers=gold_transfers(data,{p['player_id'] for p in summary['participants']})
 transfer_frames={t['debit_frame']:{'direction':'sent','counterpart':t['recipient'],'amount':t['amount'],'received':t.get('received',t['amount']),'source':t.get('source','shop')} for t in transfers if t['sender']==owner and 'nested_index' not in t}
 transfer_frames.update({t['credit_frame']:{'direction':'received','counterpart':t['sender'],'amount':t['amount'],'received':t.get('received',t['amount']),'source':t.get('source','shop')} for t in transfers if t['recipient']==owner and 'nested_index' not in t})
 nested_transfers={(t['debit_frame'],t['nested_index']):t for t in transfers if 'nested_index' in t}
 cure_refs=cure_healing_sources(data);support_refs=support_skill_sources(data)
 if unapplied_gold is None:unapplied_gold=terminal_unapplied_gold(data,summary)
 if death_refs is None:death_refs=death_effect_sources(data,buff_refs,land_refs,hp_refs)
 expiry_refs={(v['hp_frame'],v['victim_id'],v['hp_before'],v['hp_after']):v for v in death_refs.values() if v.get('modifier_expired')}
 teams={};events=[];stats=dict(turns=0,deaths=0,npc_last_hits=0,effect_cards=0,battle_cards=0,skills=0)
 pos=frame=roundno=0;actor=0;level=None;star_level=None;known_cards={};battle_id=0;opponent=0;battle_bonuses={};battle_opponents={};lethal_hits={};initial_state_seen=False;awaiting_revival=False
 def remember_cards(raw_cards):
  for raw in raw_cards:
   card=replay.protobuf(raw);uid,definition=replay.first(card,1),replay.first(card,2)
   if uid>0 and definition>0:known_cards[uid]=definition
 def add(kind,detail):
  if kind=='HP 변화':
   expiry=expiry_refs.get((frame,owner,detail.get('before'),detail.get('after')))
   if expiry:detail={**detail,'cause_type':expiry['cause_type'],'cause_id':0,'buff':expiry['buff'],'modifier_expired':True}
  if kind=='골드 변화' and (frame,owner,detail.get('before'),detail.get('after')) in unapplied_gold:detail={**detail,'not_applied':True,'not_applied_reason':'terminal_snapshot_unchanged'}
  events.append((summary['replay_id'],owner,frame,len(events),roundno,int(actor==owner),kind,json.dumps(detail,ensure_ascii=False)))
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;body=data[pos:pos+n];pos+=n;frame+=1
  if op not in (1003,1113,1015,5056,5036,1115,1040,5022,5028,5212,1109,1007,1096):continue
  f=replay.protobuf(body);pid=replay.first(f,1)
  if op in (1015,1003,1113,5056,5036):lethal_hits.clear()
  if op==1007:
   battle=replay.protobuf(replay.first(f,1,b''));battle_id=replay.first(battle,1);opponent=0
   attacker=replay.protobuf(replay.first(battle,2,b''));defender=replay.protobuf(replay.first(battle,3,b''))
   ap,dp=replay.first(attacker,1),replay.first(defender,1)
   role=attacker if ap==owner else defender if dp==owner else None
   if role is not None and ap!=dp:
    opponent=dp if ap==owner else ap
    previous=battle_opponents.get(battle_id,opponent);battle_opponents[battle_id]=opponent if previous==opponent else 0
    bonuses=[]
    for raw_bonus in role.get(23,[]):
     bonus=replay.protobuf(raw_bonus);bonuses.append((replay.first(bonus,1),signed32(replay.first(bonus,2))))
    if battle_id and bonuses:battle_bonuses[battle_id]=bonuses
   if replay.first(battle,5):opponent=0
  elif op==1015:
   roundno=replay.first(f,1);actor=replay.first(f,5)
   if actor==owner:stats['turns']+=1;add('턴 시작',{'round':roundno})
  elif op in (1003,1113):
   room=replay.protobuf(replay.first(f,1 if op==1003 else 2,b''))
   teams={replay.first(person,1):replay.first(replay.protobuf(replay.first(person,10,b'')),61) for raw_person in room.get(9,[]) for person in [replay.protobuf(raw_person)]}
   initial_state=not initial_state_seen;initial_state_seen=True
   if replay.first(room,14)>0:roundno=replay.first(room,14)
   for raw in room.get(9,[]):
    p=replay.protobuf(raw)
    if replay.first(p,1)!=owner:continue
    hero=replay.protobuf(replay.first(p,10,b''));remember_cards(hero.get(8,[]));current=replay.first(hero,33);star_level=current
    if signed32(replay.first(hero,14))>0:awaiting_revival=False
    if initial_state and replay.first(room,14)==0:add('시작 골드',{'amount':signed32(replay.first(hero,7))})
    if current!=level:
     add('성장 상태',{'previous':level,'current':current,'snapshot_round':replay.first(room,14),'note':'게임 내부 성장 값; 스냅샷에서 확인한 시점'});level=current
  elif op==1109 and replay.first(f,2)==owner:remember_cards(f.get(1,[]))
  elif op==5056 and pid==owner:
   if replay.first(f,8):stats['skills']+=1;add('스킬',{'skill_id':replay.first(f,9),'targets':fixed64_target_ids(f.get(3,[]))})
   if replay.first(f,2) and not replay.first(f,8):stats['effect_cards']+=1;add('효과 카드',{'card_id':replay.first(f,2),'definition_id':replay.first(f,2),'targets':fixed64_target_ids(f.get(3,[]))})
   allies=support_refs.get(frame,[]) if replay.first(f,8) else list(dict.fromkeys(target for target in fixed64_target_ids(f.get(3,[])) if target!=owner and teams.get(owner,0)>0 and teams.get(target)==teams[owner]))
   if allies and (replay.first(f,8) or replay.first(f,2)):add('아군 지원',{'support_kind':'skill' if replay.first(f,8) else 'card','skill_id':replay.first(f,9) if replay.first(f,8) else 0,'definition_id':replay.first(f,2) if not replay.first(f,8) else 0,'targets':allies})
  elif op==5036 and pid==owner and replay.first(f,2) and not replay.first(f,3):
   stats['battle_cards']+=1;add('전투 카드',{'card_id':replay.first(f,2),'definition_id':known_cards.get(replay.first(f,2)),'battle_id':battle_id,'opponent_id':opponent})
  elif op==1115:
   hit=lethal_hits.pop(pid,None);killer=replay.first(f,3);health={}
   if hit and frame-hit['frame']<=5 and hit['source'] in (0,killer):health={key:hit[key] for key in ('hp_before','hp_after','damage_value','hp_max_before','hp_max_after')}
   if pid==owner:awaiting_revival=True;stats['deaths']+=1;add('사망',{'killer_id':killer,**health,**death_refs.get(frame,{})})
   elif killer==owner and pid not in [p['player_id'] for p in summary['participants']]:stats['npc_last_hits']+=1;add('NPC 마지막 공격',{'target_hero_id':replay.first(f,2),**health})
  elif op==5022 and replay.first(f,3)==owner:
   values=cards.fixed32_values(f.get(1,[]));point=replay.first(f,2)
   add('주사위',{'move_point':point,'dice_values':values,'adjustment':point-sum(values) if values else None,'controlled':bool(replay.first(f,5))})
  elif op==5028 and pid==owner:add('이동',{'distance':len(cards.fixed32_values(f.get(2,[])))})
  elif op==5212 and pid==owner and replay.first(f,2):add('칩 선택 응답',{'relic_id':replay.first(f,2),'is_reroll':bool(replay.first(f,3))})
  elif op==1096:
   for index,_,attr in move_skill_attributes(body):
    cause=replay.protobuf(replay.first(attr,2,b''));transfer=nested_transfers.get((frame,index));link=None
    if transfer and owner in (transfer['sender'],transfer['recipient']):
     sent=owner==transfer['sender'];link={'direction':'sent' if sent else 'received','counterpart':transfer['recipient'] if sent else transfer['sender'],'amount':transfer['amount'],'received':transfer['received'],'source':'skill'}
    for nested_body in attr.get(4,[]):
     effect=replay.protobuf(nested_body)
     if replay.first(effect,1)!=owner or 2 not in effect:continue
     gold=replay.protobuf(replay.first(effect,2,b''))
     if replay.first(gold,1)!=owner:continue
     before=signed32(replay.first(gold,3));after=signed32(replay.first(gold,4));origin={'source':0,'cause_type':replay.first(cause,1),'cause_id':replay.first(cause,3)}
     gain=transfer.get('sender_gain',0) if transfer and link and link['direction']=='sent' else 0
     if gain and before-after==transfer['net_debit']:
      combined={'combined_change':True,'observed_before':before,'observed_after':after}
      add('골드 변화',{'before':before,'after':before+gain,'requested_change':gain,'transfer':None,**origin,**combined})
      add('골드 변화',{'before':before+gain,'after':after,'requested_change':-link['amount'],'transfer':link,**origin,**combined})
     else:add('골드 변화',{'before':before,'after':after,'requested_change':signed32(replay.first(gold,2)),'transfer':link,**origin})
    if link and link['direction']=='sent':add('골드 전달',{'amount':link['amount'],'received':link['received'],'sender_gain':transfer.get('sender_gain',0),'source':'skill','target':link['counterpart']})
  elif op==1040:
   cause=replay.protobuf(replay.first(f,2,b''));origin={'cause_type':replay.first(cause,1),'cause_id':replay.first(cause,3),'buff':buff_refs.get(frame),'land':land_refs.get(frame)}
   if origin['cause_type']==12 and battle_opponents.get(origin['cause_id']):origin['opponent_id']=battle_opponents[origin['cause_id']]
   star_up=False
   for item in f.get(4,[]):
    change=replay.protobuf(item)
    if replay.first(change,1)!=owner or 11 not in change:continue
    lv=replay.protobuf(replay.first(change,11,b''));new_level=replay.first(lv,2)
    if replay.first(lv,1)==owner and new_level>0 and (star_level is None or new_level>star_level):star_up=True
    star_level=new_level
   for effect_index,raw in enumerate(f.get(4,[])):
    effect=replay.protobuf(raw)
    target=replay.first(effect,1)
    if 3 in effect and isinstance(effect[3][0],bytes):
     hp=replay.protobuf(effect[3][0]);source=replay.first(hp,9);damage_type=replay.first(hp,8)
     hp_before=signed32(replay.first(hp,3));hp_after=signed32(replay.first(hp,4));hit_change=signed32(replay.first(hp,2))
     lethal_hits.pop(target,None)
     maximum=hp_refs.get((frame,effect_index),{'hp_max_before':None,'hp_max_after':None})
     revived=target==owner and awaiting_revival and hp_before==0 and hp_after>0 and hp_after==maximum.get('hp_max_after') and origin['cause_type']==0 and not origin['cause_id'] and not damage_type and not star_up
     if target==owner and hp_after>0:awaiting_revival=False
     if hp_before>0 and hp_after<=0:lethal_hits[target]={'frame':frame,'source':source,'hp_before':hp_before,'hp_after':hp_after,'damage_value':-hit_change if hit_change<0 else max(0,hp_before-hp_after),**maximum}
     if source==owner and target and target!=owner and 1<=damage_type<=11:
      before=signed32(replay.first(hp,3));after=signed32(replay.first(hp,4));amount=max(0,max(0,before)-max(0,after))
      requested=signed32(replay.first(hp,2));health={'hp_before':before,'hp_after':after,**maximum}
      if amount or requested<0:add('가한 피해',{'amount':amount,'damage_value':-requested if requested<0 else amount,'target':target,'damage_type':damage_type,**health,**origin})
      if damage_type==7 and requested<0:add('전투 피해',{'amount':-requested,'target':target,'damage_type':7,**health,**origin})
    if target!=owner:continue
    for field,kind in ((2,'골드 변화'),(3,'HP 변화')):
     if field not in effect or not isinstance(effect[field][0],bytes):continue
     v=replay.protobuf(effect[field][0]);delta=signed32(replay.first(v,4))-signed32(replay.first(v,3));star_change=('reward' if delta>0 else 'cost' if field==2 and delta<0 else None) if star_up else None;add(kind,{'purchase_card_ids':purchases.get(frame,{}).get('card_ids') if field==2 and purchases.get(frame,{}).get('player_id')==owner else None,'kill_reward':kill_rewards.get(frame) if field==2 and kill_rewards.get(frame,{}).get('killer')==owner else None,'transfer':transfer_frames.get(frame) if field==2 else None,'star_change':star_change,'before':signed32(replay.first(v,3)),'after':signed32(replay.first(v,4)),'requested_change':signed32(replay.first(v,2)),'source':replay.first(v,9) if field==3 else 0,'damage_type':replay.first(v,8) if field==3 else None,**origin,**(maximum if field==3 else {}),**({'revival':True} if field==3 and revived else {}),**({'cure_healing':cure_refs[(frame,effect_index)]} if field==3 and not star_up and (frame,effect_index) in cure_refs else {})})
   transfer=transfer_frames.get(frame)
   if transfer and transfer['direction']=='sent':add('골드 전달',{'amount':transfer['amount'],'received':transfer.get('received',transfer['amount']),'source':transfer.get('source','shop'),'target':transfer['counterpart']})
 # A repeated definition in one battle is ambiguous; do not assign a bonus by guessing order.
 battle_events={}
 for i,event in enumerate(events):
  if event[6]!='전투 카드':continue
  detail=json.loads(event[7]);key=(detail.get('battle_id'),detail.get('definition_id'));battle_events.setdefault(key,[]).append(i)
 for (battle,definition),indices in battle_events.items():
  values=[v for card,v in battle_bonuses.get(battle,[]) if card==definition]
  if not battle or not definition or len(indices)!=1 or len(values)!=1:continue
  i=indices[0];event=list(events[i]);detail=json.loads(event[7]);detail['card_bonus']=values[0];event[7]=json.dumps(detail,ensure_ascii=False);events[i]=tuple(event)
 settlement=summary.get('settlement',{}).get(str(owner))
 if settlement is not None:
  stats['deaths']=settlement['deaths'];add('최종 결과',settlement)
 record=(summary['replay_id'],owner,summary['finish_time'],summary.get('start_time'),person['hero_id'],summary.get('map_id'),summary.get('map_type'),person.get('rank'),max(0,summary['finish_time']-(summary.get('start_time') or summary['finish_time'])),*[stats[k] for k in ('turns','deaths','npc_last_hits','effect_cards','battle_cards','skills')],VERSION)
 return record,events

def index(db,data,summary,owner,managed=False):
 record,events=analyze(data,summary,owner,land_refs=land_effect_sources(data,game_catalog.land_layouts(db)))
 card_summary=cards.analyze(data,owner)
 with db:
  difficulty=summary.get('difficulty');map_difficulty=summary.get('map_difficulty_id')
  if type(difficulty) is not int or not 0<=difficulty<2**31:difficulty=None
  if type(map_difficulty) is not int or not 0<=map_difficulty<2**31:map_difficulty=None
  db.execute('INSERT OR REPLACE INTO personal_match_context VALUES (?,?,?,?)',(summary['replay_id'],owner,difficulty,map_difficulty))
  cards.save(db,summary['replay_id'],owner,card_summary)
  save_appearances(db,summary,owner)
  save_insights(db,data,summary,owner)
  db.execute('INSERT OR REPLACE INTO personal_matches VALUES ('+','.join('?'*16)+')',record)
  metrics=summarize_events(record,events)
  db.execute('INSERT OR REPLACE INTO personal_game_totals VALUES (?,?,?)',(summary['replay_id'],owner,json.dumps(metrics,ensure_ascii=False)))
  db.execute('INSERT OR IGNORE INTO personal_cache VALUES (?,?,0,?)',(summary['replay_id'],int(managed),time.time()))
 return len(events)

def summarize_events(record,events):
 metrics={key:record[index] for key,index in (('turns',9),('deaths',10),('npc_last_hits',11),('effect_cards',12),('battle_cards',13),('skills',14))}
 metrics.update(movement_distance=0,damage_dealt=0,chip_selection_events=0,dice_events=0,move_events=0,hp_increase=0,hp_decrease=0,gold_increase=0,gold_decrease=0,received_gold=0,ally_support=0,growth3_round=None,growth_final=None)
 for event in events:
  roundno,kind,detail=event[4],event[6],json.loads(event[7])
  if detail.get('not_applied'):continue
  key={'칩 선택 응답':'chip_selection_events','주사위':'dice_events','이동':'move_events'}.get(kind)
  if kind=='칩 선택 응답' and detail.get('is_reroll'):key=None
  if kind=='아군 지원':metrics['ally_support']+=1
  if key:metrics[key]+=1
  if kind=='이동':metrics['movement_distance']+=detail.get('distance',0)
  if kind=='가한 피해':metrics['damage_dealt']+=detail.get('amount',0)
  if kind in ('HP 변화','골드 변화'):
   delta=detail['after']-detail['before'];prefix='hp' if kind=='HP 변화' else 'gold'
   metrics[prefix+('_increase' if delta>=0 else '_decrease')]+=abs(delta)
  if kind=='골드 변화' and (detail.get('transfer') or {}).get('direction')=='received':
   metrics['received_gold']+=detail['transfer'].get('received',detail['transfer'].get('amount',0))
  if kind=='성장 상태':
   metrics['growth_final']=detail.get('current')
   reached=detail.get('snapshot_round',roundno)
   if detail.get('current')==3 and isinstance(reached,int) and reached>0:metrics['growth3_round']=min(metrics['growth3_round'] or reached,reached)
 if record[15]<4:
  metrics.pop('movement_distance',None);metrics.pop('damage_dealt',None)
 if record[15]>=5:
  settlement=next((json.loads(event[7]) for event in reversed(events) if event[6]=='최종 결과'),None)
  for key in ('damage_dealt','damage_taken','deaths','movement_distance','transferred_gold','max_battle_damage'):metrics[key]=settlement.get(key) if settlement is not None else None
  metrics['settlement_available']=settlement is not None
  if record[15]>=8:
   metrics['transferred_gold_settlement']=metrics['transferred_gold']
   metrics['transferred_gold']=sum(json.loads(e[7]).get('amount',0) for e in events if e[6]=='골드 전달')
  if record[15]>=9:
   metrics['gold_change_increase']=metrics['gold_increase']
   metrics['starting_gold']=next((json.loads(e[7])['amount'] for e in events if e[6]=='시작 골드'),None)
   metrics['gold_increase']=settlement.get('gold_acquired') if settlement is not None else None
 return metrics

def normalized_totals(value):
 value=dict(value)
 if not value.get('settlement_available'):
  for key in ('damage_dealt','damage_taken','deaths','movement_distance','transferred_gold','max_battle_damage'):value[key]=None
 return value

def game_totals(db,rid,owner):
 row=db.execute('SELECT metrics FROM personal_game_totals WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 if row:return normalized_totals(json.loads(row[0]))
 record=db.execute('SELECT * FROM personal_matches WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 if record is None:return None
 events=db.execute('SELECT * FROM personal_events WHERE replay_id=? AND owner_id=? ORDER BY frame,ordinal',(rid,owner)).fetchall()
 return normalized_totals(summarize_events(record,events))

def cleanup(db,root,days):
 if not 1<=days<=3650:raise ValueError('Invalid cache days')
 folder=(pathlib.Path(root)/'replays').resolve();cutoff=time.time()-days*86400;removed=0
 for (rid,) in db.execute('SELECT replay_id FROM personal_cache WHERE managed=1 AND pinned=0 AND acquired_at<?',(cutoff,)).fetchall():
  if not replay.valid_id(rid):continue
  p=folder/rid
  if p.is_file() and p.resolve().parent==folder:p.unlink();removed+=1
 return removed

# MapModeType names from the installed runtime. Asymmetric/LuckyStar rules remain unverified.
PVE_MODES=frozenset((4,6,9,10,12))
PVP_MODES=frozenset((1,2,3,5,8))

def difficulty_clause(alias,difficulty):
 """None means all; -2 means unknown. Difficulty filters only include PvE matches."""
 if difficulty is None:return '',[]
 if type(difficulty) is not int or (difficulty!=-2 and not 0<=difficulty<2**31):raise ValueError('Invalid difficulty filter')
 field='(SELECT context.difficulty FROM personal_match_context context WHERE context.replay_id='+alias+'.replay_id AND context.owner_id='+alias+'.owner_id)'
 clause=' AND '+alias+'.map_type IN ('+','.join(str(v) for v in sorted(PVE_MODES))+') AND '+field
 return (clause+' IS NULL',[]) if difficulty==-2 else (clause+'=?',[difficulty])

def mode_family(mode):
 return 'pve' if mode in PVE_MODES else 'pvp' if mode in PVP_MODES else 'other'

def match_outcome(mode,rank):
 if type(rank) is not int or rank<=0:return None
 family=mode_family(mode)
 if family=='pve':return 'win' if rank==1 else 'loss' if rank==2 else None
 if family=='pvp':return 'win' if rank==1 else 'loss'
 return None

def result_label(mode,rank):
 outcome=match_outcome(mode,rank)
 if outcome is None:return '미확인'
 label='승리' if outcome=='win' else '패배'
 return label if mode_family(mode)=='pve' else label+' · '+str(rank)+'위'

def rate_label(rate):return '-' if rate is None else f'{rate*100:.1f}%'

def family_summary(label,row):
 if not row['matches']:return label+' 기록 없음'
 if not row['decided']:return label+' '+str(row['matches'])+'경기 · 결과 미확인'
 value=label+' '+str(row['wins'])+'승 '+str(row['losses'])+'패 · '+rate_label(row['win_rate'])
 if row['unresolved']:value+=' (미확인 '+str(row['unresolved'])+')'
 return value

def statistics(db,owner,mode=None,map_id=None,hero_id=None,difficulty=None):
 """Personal-only aggregation. Separate PvE/PvP win rates; unknown results are excluded."""
 where='owner_id=?';params=[owner]
 if mode is not None:where+=' AND map_type=?';params.append(mode)
 if map_id is not None:where+=' AND map_id=?';params.append(map_id)
 if hero_id is not None:where+=' AND hero_id=?';params.append(hero_id)
 extra,args=difficulty_clause('personal_matches',difficulty);where+=extra;params+=args
 rows=db.execute('SELECT replay_id,hero_id,map_id,map_type,rank,duration,turns,deaths,npc_last_hits,effect_cards,battle_cards,skills,(SELECT difficulty FROM personal_match_context context WHERE context.replay_id=personal_matches.replay_id AND context.owner_id=personal_matches.owner_id) FROM personal_matches WHERE '+where,params).fetchall()
 rows=[tuple(row[:12])+(row[12] if mode_family(row[3])=='pve' else None,) for row in rows]
 growth={}
 selected={r[0] for r in rows}
 for rid,roundno,raw in db.execute("SELECT replay_id,round,detail FROM personal_events WHERE owner_id=? AND kind='성장 상태'",(owner,)):
  if rid not in selected:continue
  detail=json.loads(raw)
  if detail.get('current')==3:
   reached=detail.get('snapshot_round',roundno)
   if isinstance(reached,int) and reached>0:growth[rid]=min(growth.get(rid,reached),reached)
 for rid,raw in db.execute('SELECT replay_id,metrics FROM personal_game_totals WHERE owner_id=?',(owner,)):
  if rid not in selected:continue
  reached=json.loads(raw).get('growth3_round');growth.pop(rid,None)
  if reached is not None:growth[rid]=reached
 metrics_by_id={rid:normalized_totals(json.loads(raw)) for rid,raw in db.execute('SELECT replay_id,metrics FROM personal_game_totals WHERE owner_id=?',(owner,))}
 def movement_damage(items):
  values=[metrics_by_id.get(r[0],{}) for r in items];result={}
  for key in ('movement_distance','damage_dealt','damage_taken','deaths','transferred_gold','max_battle_damage'):
   found=[r[key] for r in values if r.get(key) is not None]
   result[key+'_analyzed']=len(found)
   result[key]=max(found) if key=='max_battle_damage' and found else sum(found)
   result['average_'+key]=sum(found)/len(found) if found else None
  result['movement_damage_analyzed']=sum(r.get('movement_distance') is not None and r.get('damage_dealt') is not None for r in values)
  return result
 def aggregate(items):
  count=len(items);ranked=[r[4] for r in items if r[4] is not None];rounds=[growth[r[0]] for r in items if r[0] in growth]
  outcomes=[match_outcome(r[3],r[4]) for r in items];wins=outcomes.count('win');losses=outcomes.count('loss');decided=wins+losses;families={mode_family(r[3]) for r in items}
  win_rate=wins/decided if decided and len(families)==1 and 'other' not in families else None
  return {'wins':wins,'losses':losses,'decided':decided,'unresolved':count-decided,'win_rate':win_rate,'matches':count,'duration':sum(r[5] or 0 for r in items),'first_places':sum(r[4]==1 for r in items),'average_rank':sum(ranked)/len(ranked) if ranked else None,'average_duration':sum(r[5] or 0 for r in items)/count if count else 0,'deaths':sum(r[7] or 0 for r in items),'average_deaths':sum(r[7] or 0 for r in items)/count if count else 0,'average_npc_last_hits':sum(r[8] or 0 for r in items)/count if count else 0,'average_effect_cards':sum(r[9] or 0 for r in items)/count if count else 0,'average_battle_cards':sum(r[10] or 0 for r in items)/count if count else 0,'average_skills':sum(r[11] or 0 for r in items)/count if count else 0,**movement_damage(items),'growth3_average_round':sum(rounds)/len(rounds) if rounds else None,'growth3_reached_matches':len(rounds)}
 def group(indices):
  groups={}
  for row in rows:groups.setdefault(tuple(row[i] for i in indices),[]).append(row)
  return [{'key':key,**aggregate(items)} for key,items in sorted(groups.items(),key=lambda pair:(-len(pair[1]),str(pair[0])))]
 return {'overall':aggregate(rows),'by_family':{kind:aggregate([r for r in rows if mode_family(r[3])==kind]) for kind in ('pve','pvp','other')},'by_difficulty':group((12,3)),'by_mode':group((3,)),'by_map':group((2,3)),'by_hero':group((1,3))}


def save_appearances(db,summary,owner):
 if owner not in [p['player_id'] for p in summary['participants']]:raise ValueError('Own player not in replay')
 participants=[{'player_id':p['player_id'],'name':p.get('name',''),'hero_id':p.get('equipped_hero_id') or p['hero_id'],'standing_painting_id':p.get('standing_painting_id',0)} for p in summary['participants']]
 db.execute('INSERT OR REPLACE INTO personal_appearances VALUES (?,?,?)',(summary['replay_id'],owner,json.dumps(participants,ensure_ascii=False)))

def appearances(db,rid,owner):
 row=db.execute('SELECT participants FROM personal_appearances WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 return json.loads(row[0]) if row else []


INSIGHTS_VERSION=46

def has_insights(db,rid,owner):
 row=db.execute('SELECT version FROM personal_match_insights WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 return bool(row and row[0]>=INSIGHTS_VERSION)

def match_insights(db,rid,owner):
 row=db.execute('SELECT summary FROM personal_match_insights WHERE replay_id=? AND owner_id=? AND version>=?',(rid,owner,7)).fetchone()
 return json.loads(row[0]) if row else None

def save_insights(db,data,summary,owner):
 if owner not in [p['player_id'] for p in summary['participants']]:raise ValueError('Own player not in replay')
 import collections
 entities={p['player_id']:{'kind':'player','hero_id':p['hero_id']} for p in summary['participants']};ambiguous=set();deaths=[];pos=0
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n
  if op in (1003,1113,1018):
   fields=replay.protobuf(raw)
   monsters=fields.get(1,[]) if op==1018 else replay.protobuf(replay.first(fields,1 if op==1003 else 2,b'')).get(10,[])
   for monster in monsters:
    p=replay.protobuf(monster);pid=replay.first(p,1);hero=replay.protobuf(replay.first(p,10,b''));hid=replay.first(hero,2)
    if not pid or not hid:continue
    value={'kind':'npc','hero_id':hid}
    if pid in entities and entities[pid]!=value:ambiguous.add(pid)
    entities[pid]=value
  elif op==1115:
   f=replay.protobuf(raw)
   if replay.first(f,1)==owner:deaths.append(replay.first(f,3))
 counter=collections.Counter();unknown=0
 for killer in deaths:
  entity=entities.get(killer)
  if not killer or killer in ambiguous or entity is None or killer==owner:unknown+=1;continue
  counter[(entity['kind'],entity['hero_id'])]+=1
 participants=[];transfers=gold_transfers(data,{p['player_id'] for p in summary['participants']});kill_rewards=kill_gold_rewards(data,{p['player_id'] for p in summary['participants']});purchases=purchase_gold_cards(data,{p['player_id'] for p in summary['participants']});buff_refs=buff_effect_sources(data);land_refs=land_effect_sources(data,game_catalog.land_layouts(db));hp_refs=hp_maximum_sources(data)
 unapplied_gold=terminal_unapplied_gold(data,summary);death_refs=death_effect_sources(data,buff_refs,land_refs,hp_refs);shared_chips=chips.shared_grant_frames(data)
 for p in summary['participants']:
  record,events=analyze(data,summary,p['player_id'],transfers,kill_rewards,purchases,buff_refs,land_refs,hp_refs,unapplied_gold,death_refs)
  timeline=[]
  for event in events:
   if event[6] in ('성장 상태','최종 결과'):continue
   detail=json.loads(event[7]);source=detail.get('killer_id',detail.get('source',0))
   # Transfer source is a route label, not a player ID. Resolve its explicit target.
   if event[6]=='골드 전달':source=detail.get('target',0)
   elif type(source) is not int or not source:source=detail.get('target',0)
   entity=entities.get(source) if source not in ambiguous else None
   timeline.append({'round':event[4] or None,'kind':event[6],'detail':detail,'entity':entity,'entity_id':source,'transfer_party':{'id':(detail.get('transfer') or {}).get('counterpart',0),'entity':entities.get((detail.get('transfer') or {}).get('counterpart'))},'targets':[{'id':target,'entity':entities.get(target) if target not in ambiguous else None} for target in detail.get('targets',[])],'opponent':{'id':detail.get('opponent_id',0),'entity':entities.get(detail.get('opponent_id')) if detail.get('opponent_id') not in ambiguous else None}})
  participants.append({'player_id':p['player_id'],'timeline':timeline,'totals':summarize_events(record,events),'cards':cards.analyze(data,p['player_id']),'chips':chips.analyze(data,p['player_id'],shared_chips),'dice':dice.analyze(data,p['player_id'])})
 value={'participants':participants,'killers':[{'kind':kind,'hero_id':hero,'count':count} for (kind,hero),count in sorted(counter.items())],'unknown_deaths':unknown,'deaths':len(deaths)}
 db.execute('INSERT OR REPLACE INTO personal_match_insights VALUES (?,?,?,?)',(summary['replay_id'],owner,INSIGHTS_VERSION,json.dumps(value,ensure_ascii=False)))

def killer_statistics(db,owner,mode=None,map_id=None,hero_id=None,difficulty=None):
 import collections
 where='m.owner_id=?';params=[owner]
 for column,value in (('map_type',mode),('map_id',map_id),('hero_id',hero_id)):
  if value is not None:where+=' AND m.'+column+'=?';params.append(value)
 extra,args=difficulty_clause('m',difficulty);where+=extra;params+=args
 rows=db.execute('SELECT i.version,i.summary FROM personal_matches m LEFT JOIN personal_match_insights i ON m.replay_id=i.replay_id AND m.owner_id=i.owner_id WHERE '+where,params).fetchall()
 counts=collections.Counter();analyzed=unknown=deaths=0
 for version,raw in rows:
  if version is None or version<7:continue
  value=json.loads(raw);analyzed+=1;unknown+=value['unknown_deaths'];deaths+=value['deaths']
  for p in value['killers']:counts[(p['kind'],p['hero_id'])]+=p['count']
 return {'analyzed':analyzed,'total':len(rows),'unknown':unknown,'deaths':deaths,'items':[{'kind':kind,'hero_id':hero,'count':count} for (kind,hero),count in sorted(counts.items(),key=lambda p:(-p[1],p[0]))[:3]]}
