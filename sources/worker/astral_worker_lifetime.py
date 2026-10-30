"""Tie only this collector worker to its game parent; no process enumeration or global kills."""
import ctypes,os,threading
from ctypes import wintypes

class ParentLifetime:
 def __init__(self,parent_pid,grace_seconds=3):
  if type(parent_pid) is not int or parent_pid<=0 or parent_pid==os.getpid():raise ValueError('Invalid game process ID')
  self.parent_pid=parent_pid;self.grace_seconds=grace_seconds;self.stopping=threading.Event();self.finished=threading.Event();self.thread=None
 def start(self):
  if os.name!='nt':raise OSError('Game parent monitoring requires Windows')
  kernel=ctypes.WinDLL('kernel32',use_last_error=True)
  kernel.OpenProcess.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD];kernel.OpenProcess.restype=wintypes.HANDLE
  kernel.WaitForSingleObject.argtypes=[wintypes.HANDLE,wintypes.DWORD];kernel.WaitForSingleObject.restype=wintypes.DWORD
  kernel.CloseHandle.argtypes=[wintypes.HANDLE];kernel.CloseHandle.restype=wintypes.BOOL
  handle=kernel.OpenProcess(0x00100000,False,self.parent_pid) # SYNCHRONIZE: wait only, no mutation rights.
  if not handle:
   error=ctypes.get_last_error()
   if error==87:self.stopping.set();return self # The game already exited.
   raise OSError(error,'Cannot monitor the game process')
  self.thread=threading.Thread(target=self._wait,args=(kernel,handle),daemon=True,name='game-parent-watch');self.thread.start();return self
 def _wait(self,kernel,handle):
  try:
   kernel.WaitForSingleObject(handle,0xffffffff)
   if self.finished.is_set():return
   self.stopping.set()
   # Allow the normal loop to close SQLite/logs. A download or parser may be blocked.
   # SQLite transactions and atomic .part replacements preserve committed data on the bounded fallback exit.
   if not self.finished.wait(self.grace_seconds):os._exit(0)
  finally:kernel.CloseHandle(handle)
 def close(self):self.finished.set()
