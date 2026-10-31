"""Combat die faces as sent to the game's dice UI; dodge is separate from defense."""
import json,struct,collections
import astral_replay as replay
VERSION=1

def analyze(data,player):
 counts={k:collections.Counter() for k in ('attack','defense','dodge')};unknown=0;pos=0
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n
  if op not in (5038,5040):continue
  f=replay.protobuf(raw)
  if replay.first(f,1)!=player:continue
  face=replay.first(f,2);kind='attack' if op==5038 else 'dodge' if replay.first(f,3) else 'defense'
  if not 1<=face<=6:unknown+=1;continue
  counts[kind][face]+=1
 return {'version':VERSION,**{kind:dict(v) for kind,v in counts.items()},'unknown':unknown}

def panel(values,total):
 result={'analyzed':len(values),'total':total,'unknown':sum(v.get('unknown',0) for v in values)}
 for kind in ('attack','defense','dodge'):
  counts=collections.Counter()
  for value in values:counts.update({int(k):int(n) for k,n in value.get(kind,{}).items()})
  n=sum(counts.values());result[kind]={'count':n,'average':f'{sum(k*v for k,v in counts.items())/n:.2f}' if n else '-','faces':[counts.get(i,0) for i in range(1,7)]}
 return result

def aggregate(db,owner,mode=None,map_id=None,hero_id=None,rid=None,difficulty=None):
 import astral_personal_data as personal
 where='m.owner_id=?';params=[owner]
 for col,value in (('map_type',mode),('map_id',map_id),('hero_id',hero_id),('replay_id',rid)):
  if value is not None:where+=' AND m.'+col+'=?';params.append(value)
 extra,args=personal.difficulty_clause('m',difficulty);where+=extra;params+=args
 rows=db.execute('SELECT i.version,i.summary FROM personal_matches m LEFT JOIN personal_match_insights i ON i.replay_id=m.replay_id AND i.owner_id=m.owner_id WHERE '+where,params).fetchall();values=[]
 for version,raw in rows:
  if version is None or version<personal.INSIGHTS_VERSION:continue
  person=next((p for p in json.loads(raw)['participants'] if p['player_id']==owner),None)
  if person and person.get('dice',{}).get('version',0)>=VERSION:values.append(person['dice'])
 return panel(values,len(rows))
