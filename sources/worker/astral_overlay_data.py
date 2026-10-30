"""Small account-scoped presentation snapshot for the FairyGUI in-game window."""
import datetime,json,pathlib,sqlite3,time
import astral_personal_data as personal
import astral_card_names as card_names
import astral_game_catalog as game_catalog
ROOT=pathlib.Path(__file__).resolve().parent

def references(db=None):
 return game_catalog.hero_names(db),game_catalog.names('maps',db),game_catalog.names('modes',db)

def number(v):return '-' if v is None else f'{v:.2f}'.rstrip('0').rstrip('.')
def duration(v):return f'{int(v or 0)//60}분 {int(v or 0)%60}초'
def metric_rows(totals):
 turns=totals.get('turns',0);lines=[]
 for key,label in [('turns','플레이어 턴'),('deaths','사망'),('npc_last_hits','NPC 막타'),('effect_cards','효과 카드 사용'),('battle_cards','전투 카드 사용'),('skills','스킬 사용'),('chip_selection_events','칩 선택 횟수'),('dice_events','이동 주사위 굴림 횟수'),('movement_distance','총 이동 거리 (칸)'),('damage_dealt','총 피해량 (가한 피해)'),('damage_taken','총 받은 피해량'),('transferred_gold','전달 골드'),('max_battle_damage','최대 전투 피해'),('gold_increase','골드 증가량'),('gold_decrease','골드 감소량'),('hp_increase','HP 증가량'),('hp_decrease','HP 감소량')]:
  value=totals.get(key);
  if value is None:lines.append([label,'-','-']);continue
  lines.append([label,str(value),'-' if key in ('turns','max_battle_damage') or not turns else number(value/turns)])
 return lines


def aggregate_metrics(r):
 rows=[]
 for key,label in [('damage_dealt','총 피해량 (가한 피해)'),('damage_taken','총 받은 피해량'),('deaths','사망'),('movement_distance','총 이동 거리 (칸)'),('transferred_gold','전달 골드'),('max_battle_damage','최대 전투 피해')]:
  measured=r[key+'_analyzed'];value=r[key]
  amount=number(value) if measured else '-'
  if measured and measured<r['matches']:amount+=' ('+str(measured)+'/'+str(r['matches'])+'경기)'
  rows.append([label,amount,number(r['average_'+key])])
 return rows

def card_ranking(*args,**kwargs):
 value=personal.cards.aggregate(*args,**kwargs)
 value.pop('used',None);value.pop('acquired',None)
 return value

def card_panel(value,key,title,db=None):
 resolve_name=card_names.resolver(db)
 panel={k:value.get(k,0) for k in ('unresolved_use','unresolved_purchase','assisted_purchase')};panel.update(key=key,title=title,analyzed=1,total=1)
 for kind in ('battle_used','effect_used','bought'):
  panel[kind]=[{'card_id':int(id),'name':resolve_name(int(id)),'count':count,'average':str(count)} for id,count in sorted(value.get(kind,{}).items(),key=lambda x:(-x[1],int(x[0])))]
 return panel

def make_snapshot(db,owner,mode=None,map_id=None,tab=0,page=0,hero_id=None):
 heroes,maps,modes=references(db);stats=personal.statistics(db,owner,mode,map_id,hero_id)
 options=db.execute('SELECT DISTINCT map_type,map_id,hero_id FROM personal_matches WHERE owner_id=? ORDER BY map_type,map_id',(owner,)).fetchall()
 mode_options=[{'id':-1,'label':'전체 모드'}]+[{'id':v,'label':modes.get(v,f'모드 {v}')} for v in sorted({r[0] for r in options if r[0] is not None})]
 map_options=[{'id':-1,'label':'전체 맵'}]+[{'id':v,'label':maps.get(v,f'맵 {v}')} for v in sorted({r[1] for r in options if r[1] is not None})]
 hero_options=[{'id':-1,'label':'전체 캐릭터'}]+[{'id':v,'label':heroes.get(v,f'캐릭터 {v}')} for v in sorted({r[2] for r in options if r[2] is not None})]
 aggregate=stats['overall'];count=aggregate['matches'];rows=[];columns=[];detail=[];row_cards=[]
 def aggregate_row(r,label,modecode):return [label,modes.get(modecode,f'모드 {modecode}'),str(r['matches']),str(r['wins']),str(r['losses']),personal.rate_label(r['win_rate']),duration(r['average_duration']),number(r['average_deaths']),str(r['movement_distance']) if r['movement_damage_analyzed']==r['matches'] else '분석 '+str(r['movement_damage_analyzed'])+'/'+str(r['matches']),str(r['damage_dealt']) if r['movement_damage_analyzed']==r['matches'] else '분석 '+str(r['movement_damage_analyzed'])+'/'+str(r['matches'])]
 if tab in (0,1,2):
  columns=['항목','모드','경기','승','패','승률','평균 시간','평균 사망','이동 합계','피해 합계']
  groups=stats[('by_mode','by_map','by_hero')[tab]]
  for r in groups:
   key=r['key'];label='전체' if tab==0 else (maps if tab==1 else heroes).get(key[0],str(key[0]));rows.append(aggregate_row(r,label,key[-1]))
   card=card_ranking(db,owner,mode=key[-1],map_id=key[0] if tab==1 else map_id,hero_id=key[0] if tab==2 else hero_id)
   card['basic_rows']=aggregate_metrics(r)
   card.update(key=str(tab)+':'+str(tuple(key)),title=(modes.get(key[-1],str(key[-1])) if tab==0 else label+' · '+modes.get(key[-1],str(key[-1])))+' 카드 순위');card['chips']=personal.chips.aggregate(db,owner,mode=key[-1],map_id=key[0] if tab==1 else map_id,hero_id=key[0] if tab==2 else hero_id);card['chips'].update(key=card['key'],title=card['title'].replace('카드 순위','선택한 칩'));card['dice']=personal.dice.aggregate(db,owner,mode=key[-1],map_id=key[0] if tab==1 else map_id,hero_id=key[0] if tab==2 else hero_id);row_cards.append(card)
 else:
  columns=['시각 (KST)','캐릭터','맵','모드','난이도','결과','경기 시간','사망']
  difficulties=game_catalog.names('difficulties',db)
  contexts=dict(db.execute('SELECT replay_id,difficulty FROM personal_match_context WHERE owner_id=?',(owner,)))
  where='owner_id=?';params=[owner]
  if mode is not None:where+=' AND map_type=?';params.append(mode)
  if map_id is not None:where+=' AND map_id=?';params.append(map_id)
  if hero_id is not None:where+=' AND hero_id=?';params.append(hero_id)
  for rid,finished,hero,mapcode,modecode,rank,seconds,deaths in db.execute('SELECT replay_id,finished_at,hero_id,map_id,map_type,rank,duration,deaths FROM personal_matches WHERE '+where+' ORDER BY finished_at DESC,replay_id DESC',params):
   stamp=datetime.datetime.fromtimestamp(finished,datetime.timezone(datetime.timedelta(hours=9))).strftime('%m-%d %H:%M') if finished else '-'
   rows.append([stamp,heroes.get(hero,str(hero)),maps.get(mapcode,str(mapcode)),modes.get(modecode,str(modecode)),('-' if personal.mode_family(modecode)=='pvp' else difficulties.get(contexts[rid],'난이도 '+str(contexts[rid])) if contexts.get(rid) is not None else '미확인'),personal.result_label(modecode,rank),duration(seconds),number((personal.game_totals(db,rid,owner) or {}).get('deaths'))])
   # Detail is read only for the visible page; no replay downloads on UI requests.
   detail.append(rid)
 killers=personal.killer_statistics(db,owner,mode,map_id,hero_id)
 killers['filter_label']='모드: '+('전체 모드' if mode is None else modes.get(mode,'모드 '+str(mode)))+' · 맵: '+('전체 맵' if map_id is None else maps.get(map_id,'맵 '+str(map_id)))+' · 캐릭터: '+('전체 캐릭터' if hero_id is None else heroes.get(hero_id,'캐릭터 '+str(hero_id)))
 npc_names=game_catalog.names('monsters',db)
 for item in killers['items']:
  name=(npc_names if item['kind']=='npc' else heroes).get(item['hero_id'],'이름 미확인 #'+str(item['hero_id']));item.update(hero_name=name,name=name,standing_painting_id=0)
 page_size=5 if tab!=3 and killers['items'] else 8
 total=len(rows);pages=max(1,(total+page_size-1)//page_size);page=max(0,min(page,pages-1));start=page*page_size;shown=rows[start:start+page_size]
 details=[]
 for rid in detail[start:start+page_size]:
  insight=personal.match_insights(db,rid,owner) or {};by_player={p['player_id']:p for p in insight.get('participants',[])}
  participants=[]
  for person in personal.appearances(db,rid,owner):
   pid=person['player_id'];analysis=by_player.get(pid);totals=analysis['totals'] if analysis else personal.game_totals(db,rid,owner) if pid==owner else None
   card=card_panel(analysis['cards'],rid+':'+str(pid),person.get('name','')+' · 이 경기 카드 순위',db=db) if analysis else None
   if card is not None:card['chips']=personal.chips.panel([analysis['chips']] if analysis.get('chips') else [],1,rid+':'+str(pid),person.get('name','')+' · 이 경기에서 선택한 칩',db=db,match=True)
   if card is not None:card['dice']=personal.dice.panel([analysis['dice']] if analysis.get('dice') else [],1)
   participants.append({**person,'hero_name':heroes.get(person['hero_id'],str(person['hero_id'])),'own':pid==owner,'available':totals is not None,'rows':metric_rows(totals or {}),'cards':card})
  totals=personal.game_totals(db,rid,owner) or {}
  card=card_ranking(db,owner,rid=rid);card.update(key=rid,title='이 경기 카드 순위');card['chips']=personal.chips.aggregate(db,owner,rid=rid);card['chips'].update(key=rid,title='이 경기에서 선택한 칩');card['dice']=personal.dice.aggregate(db,owner,rid=rid);details.append({'title':'경기 '+rid,'rows':metric_rows(totals),'cards':card,'participants':participants})
 overview_cards=card_ranking(db,owner,mode,map_id,hero_id);overview_cards['basic_rows']=aggregate_metrics(aggregate);overview_cards.update(key='overview',title='전체 카드 순위 · 현재 필터 기준');overview_cards['chips']=personal.chips.aggregate(db,owner,mode,map_id,hero_id);overview_cards['dice']=personal.dice.aggregate(db,owner,mode,map_id,hero_id)
 return {'page_size':page_size,'killers':killers,'card_overview':overview_cards,'row_cards':row_cards[start:start+page_size],'schema':1,'owner_id':owner,'generated_at':int(time.time()),'hero':hero_id if hero_id is not None else -1,'heroes':hero_options,'mode':mode if mode is not None else -1,'map':map_id if map_id is not None else -1,'tab':tab,'page':page,'pages':pages,'total_rows':total,'modes':mode_options,'maps':map_options,
         'summary':[str(count)+' 경기',duration(aggregate['average_duration'])+' 평균 시간']+(['승률 미분류 '+str(stats['by_family']['other']['matches'])+' 경기'] if stats['by_family']['other']['matches'] else []),'pve_summary':personal.family_summary('PvE',stats['by_family']['pve']),'pvp_summary':personal.family_summary('PvP',stats['by_family']['pvp']),
         'columns':columns,'rows':shown,'details':details,'note':'PvE: 1위 승리/2위 패배 · PvP: 1위 승리 · 미확인 결과/모드는 승률 제외'}

def publish(store,state):
 if not state or type(state.get('owner_id')) is not int or state['owner_id']<=0:return False
 try:
  file=store.root/'plugin-ui-request.json'
  if file.stat().st_size>4096:return False
  request=json.loads(file.read_text(encoding='utf-8-sig'))
  if request.get('owner_id')!=state['owner_id'] or not 0<=time.time()-request.get('updated_at',0)<=60:return False
  tab=request.get('tab',0);page=request.get('page',0);mode=request.get('mode',-1);map_id=request.get('map',-1);hero_id=request.get('hero',-1)
  if any(type(v) is not int for v in (tab,page,mode,map_id,hero_id)) or tab not in range(4) or not 0<=page<=100000:return False
  result=make_snapshot(store.db,state['owner_id'],None if mode==-1 else mode,None if map_id==-1 else map_id,tab,page,None if hero_id==-1 else hero_id)
  target=store.root/'plugin-ui-snapshot.json';temp=target.with_suffix('.part');temp.write_text(json.dumps(result,ensure_ascii=False),encoding='utf8');temp.replace(target);return True
 except (OSError,ValueError,TypeError,sqlite3.Error,OverflowError,IndexError,KeyError):return False
