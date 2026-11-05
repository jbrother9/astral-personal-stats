"""Validated generated game catalogue. No manually maintained ID/name references."""
import json,pathlib,time,sqlite3
import astral_paths as paths
ROOT=pathlib.Path(__file__).resolve().parent
CATEGORIES=('cards','characters','monsters','maps','chips','modes')
OPTIONAL_CATEGORIES=('difficulties','buffs','skills','lands','events','buildings')
_cache={};_reported=set()

def data_root(db=None):
 if db is not None:
  try:
   for _,name,file in db.execute('PRAGMA database_list'):
    if name=='main' and file:return pathlib.Path(file).resolve().parent
  except sqlite3.Error:pass
 return paths.data_dir()

def load(db=None):
 path=data_root(db)/'plugin-game-catalog.json'
 try:
  stat=path.stat()
  if stat.st_size>4*1024*1024:return None
  signature=(stat.st_mtime_ns,stat.st_size)
  if _cache.get(path,(None,None))[0]==signature:return _cache[path][1]
  value=json.loads(path.read_text(encoding='utf-8-sig'))
  if value.get('schema')!=1 or value.get('source')!='live_game_configuration' or value.get('complete') is not True:return None
  catalogs=value.get('catalogs');result={}
  if not isinstance(catalogs,dict):return None
  for category in CATEGORIES+OPTIONAL_CATEGORIES:
   rows=catalogs.get(category,[]) if category in OPTIONAL_CATEGORIES else catalogs.get(category)
   if not isinstance(rows,list) or len(rows)>20000:return None
   entries={}
   for row in rows:
    id=row.get('id');key=row.get('name_key');name=row.get('name');rarity=row.get('rarity',0);portrait=row.get('portrait_group',0)
    if type(id) is not int or id<0 or id in entries or type(key) is not int or not isinstance(name,str) or len(name)>2048 or type(rarity) is not int or type(portrait) is not int:return None
    entries[id]={'id':id,'name_key':key,'name':name.strip(),'rarity':rarity,'portrait_group':portrait}
   result[category]=entries
  _cache[path]=(signature,result);return result
 except (OSError,ValueError,TypeError,AttributeError):return None

def legacy_cards(db=None):
 # Compatibility with the previous generated runtime cache, never a manual reference table.
 try:
  path=data_root(db)/'plugin-card-names.json'
  if path.stat().st_size>4*1024*1024:return {}
  value=json.loads(path.read_text(encoding='utf-8-sig'))
  if value.get('schema')!=1 or value.get('source')!='live_game_configuration' or value.get('complete') is not True:return {}
  result={}
  for row in value.get('cards',[]):
   id=row.get('card_id');key=row.get('name_key');name=row.get('name')
   if type(id) is not int or id<=0 or id in result or type(key) is not int or not isinstance(name,str) or len(name)>2048:return {}
   result[id]={'id':id,'name_key':key,'name':name.strip(),'rarity':0,'portrait_group':0}
  return result
 except (OSError,ValueError,TypeError,AttributeError):return {}

def metadata(category,db=None):
 value=load(db)
 return value.get(category,{}) if value is not None else legacy_cards(db) if category=='cards' else {}

def report_missing(category,id,db=None):
 if type(id) is not int or id<0:return
 root=data_root(db);marker=(str(root),category,id)
 if marker in _reported:return
 _reported.add(marker)
 try:
  root.mkdir(parents=True,exist_ok=True)
  with (root/'catalog-name-misses.jsonl').open('a',encoding='utf8') as file:file.write(json.dumps({'category':category,'id':id,'observed_at':int(time.time())})+'\n')
 except OSError:pass

class NameMap(dict):
 def __init__(self,values,category,db):super().__init__(values);self.category=category;self.db=db
 def get(self,id,default=None):
  if id in self:return super().get(id)
  report_missing(self.category,id,self.db);return default

def names(category,db=None):
 return NameMap({id:row['name'] for id,row in metadata(category,db).items() if row['name_key']>0 and row['name']},category,db)

def hero_names(db=None):
 value=dict(names('monsters',db));value.update(names('characters',db));return NameMap(value,'heroes',db)

def resolver(category,db=None):
 values=names(category,db)
 def resolve(id):return values.get(id,'이름 미확인 #'+str(id))
 return resolve


_layout_cache={}
def land_layouts(db=None):
 path=data_root(db)/'plugin-land-layouts.json'
 try:
  stat=path.stat()
  if stat.st_size>4*1024*1024:return []
  signature=(stat.st_mtime_ns,stat.st_size)
  if _layout_cache.get(path,(None,None))[0]==signature:return _layout_cache[path][1]
  value=json.loads(path.read_text(encoding='utf-8-sig'))
  if value.get('schema')!=1 or value.get('source')!='live_scene_land_groups' or value.get('complete') is not True:return []
  layouts=value.get('layouts')
  if not isinstance(layouts,list) or len(layouts)>128:return []
  identities=set()
  for layout in layouts:
   identity=(layout.get('map_id'),layout.get('map_data_id'))
   if any(type(v) is not int or v<0 for v in identity) or identity in identities:return []
   identities.add(identity);nodes=layout.get('base_nodes');groups=layout.get('groups')
   if not isinstance(nodes,list) or not 0<len(nodes)<=2000 or not isinstance(groups,list) or len(groups)>256:return []
   ids=set()
   for node in nodes:
    id=node.get('node_id');kind=node.get('land_type')
    if type(id) is not int or id<0 or id in ids or type(kind) is not int or kind<0:return []
    ids.add(id)
   seen_groups=set()
   for group in groups:
    id=group.get('group_id');members=group.get('nodes');statuses=group.get('statuses')
    if type(id) is not int or id<=0 or id in seen_groups or not isinstance(members,list) or len(members)>2000 or not isinstance(statuses,list) or not 0<len(statuses)<=256:return []
    seen_groups.add(id)
    if any(type(node) is not int or node not in ids for node in members) or len(set(members))!=len(members):return []
    indices=set()
    for status in statuses:
     index=status.get('index');kind=status.get('land_type')
     if type(index) is not int or index<0 or index in indices or type(kind) is not int or kind<0 or type(status.get('active')) is not bool:return []
     indices.add(index)
  _layout_cache[path]=(signature,layouts);return layouts
 except (OSError,ValueError,TypeError,AttributeError):return []
