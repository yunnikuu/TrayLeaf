#define UNICODE
#define _UNICODE
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <msi.h>
#include <msiquery.h>
#include <tlhelp32.h>
#include <shellapi.h>
#include <wincrypt.h>
#include <string>
#include <vector>
#include <algorithm>
#include <taskschd.h>
#include <sddl.h>

// Kept native: server-side SfxCA unpacking can fail before CLR/custom code starts.
struct RunningImage { DWORD pid; FILETIME created; std::wstring image; };
struct Handle {
 HANDLE h;
 explicit Handle(HANDLE value=NULL):h(value){}
 ~Handle(){if(h&&h!=INVALID_HANDLE_VALUE)CloseHandle(h);}
 Handle(const Handle&)=delete;Handle& operator=(const Handle&)=delete;
};
static bool Same(const std::wstring&a,const std::wstring&b){return _wcsicmp(a.c_str(),b.c_str())==0;}
static std::wstring Property(MSIHANDLE session,const wchar_t*name){
 DWORD n=0;MsiGetPropertyW(session,name,L"",&n);std::vector<wchar_t>b(n+1);DWORD size=n+1;
 if(MsiGetPropertyW(session,name,b.data(),&size)!=ERROR_SUCCESS)return L"";return std::wstring(b.data());
}
static void Log(MSIHANDLE s,const std::wstring&text){
 if(!s)return;MSIHANDLE record=MsiCreateRecord(0);MsiRecordSetStringW(record,0,(L"TrayLeaf pre-validation: "+text).c_str());MsiProcessMessage(s,INSTALLMESSAGE_INFO,record);MsiCloseHandle(record);
}
static std::wstring Full(const std::wstring&path){
 std::vector<wchar_t>b(32768);DWORD n=GetFullPathNameW(path.c_str(),(DWORD)b.size(),b.data(),NULL);
 if(!n||n>=b.size())return L"";return std::wstring(b.data());
}
static bool ValidTarget(const std::wstring&p){
 size_t slash=p.find_last_of(L"\\/");return slash!=std::wstring::npos&&Same(p.substr(slash+1),L"TrayLeaf.exe");
}
static std::wstring ImagePath(HANDLE p){std::vector<wchar_t>b(32768);DWORD n=(DWORD)b.size();if(!QueryFullProcessImageNameW(p,0,b.data(),&n))return L"";return Full(std::wstring(b.data(),n));}
static bool Created(HANDLE p,FILETIME&time){FILETIME e,k,u;return GetProcessTimes(p,&time,&e,&k,&u)!=0;}
static std::vector<BYTE> Hash(const std::wstring&path){
 std::vector<BYTE>out;Handle file(CreateFileW(path.c_str(),GENERIC_READ,FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_SHARE_DELETE,NULL,OPEN_EXISTING,FILE_ATTRIBUTE_NORMAL,NULL));
 if(file.h==INVALID_HANDLE_VALUE)return out;
 HCRYPTPROV provider=0;HCRYPTHASH hash=0;
 if(!CryptAcquireContextW(&provider,NULL,NULL,PROV_RSA_AES,CRYPT_VERIFYCONTEXT))return out;
 if(CryptCreateHash(provider,CALG_SHA_256,0,0,&hash)){
  BYTE buffer[16384];DWORD count=0;bool ok=true;
  while(ReadFile(file.h,buffer,sizeof(buffer),&count,NULL)&&count){if(!CryptHashData(hash,buffer,count,0)){ok=false;break;}}
  if(ok){DWORD n=32;out.resize(n);if(!CryptGetHashParam(hash,HP_HASHVAL,out.data(),&n,0))out.clear();}
  CryptDestroyHash(hash);
 }
 CryptReleaseContext(provider,0);return out;
}
static bool Matches(const std::wstring&image,const std::vector<std::wstring>&targets){
 for(const auto&t:targets)if(Same(image,t))return true;
 // Claim a renamed legacy MSI image only on the same target volume with equal bytes.
 size_t slash=image.find_last_of(L"\\"),dot=image.find_last_of(L".");
 if(slash==std::wstring::npos||dot==std::wstring::npos||!Same(image.substr(dot),L".rbf"))return false;
 if(image.size()<3||image[1]!=L':'||!Same(image.substr(0,slash),image.substr(0,3)+L"Config.Msi"))return false;
 auto actual=Hash(image);if(actual.empty())return false;
 for(const auto&t:targets)if(t.size()>2&&Same(t.substr(0,3),image.substr(0,3))){auto expected=Hash(t);if(!expected.empty()&&actual==expected)return true;}
 return false;
}
static std::vector<RunningImage> Find(const std::vector<std::wstring>&targets,MSIHANDLE session){
 std::vector<RunningImage>result;Handle snapshot(CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS,0));
 if(snapshot.h==INVALID_HANDLE_VALUE)throw GetLastError();
 PROCESSENTRY32W entry={};entry.dwSize=sizeof(entry);
 if(Process32FirstW(snapshot.h,&entry))do{
  if(!Same(entry.szExeFile,L"TrayLeaf.exe"))continue;
  Handle p(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,FALSE,entry.th32ProcessID));
  if(!p.h){if(GetLastError()!=ERROR_INVALID_PARAMETER)throw GetLastError();continue;}
  auto image=ImagePath(p.h);if(image.empty())throw GetLastError();
  if(Matches(image,targets)){
   RunningImage item={entry.th32ProcessID,{},image};if(!Created(p.h,item.created))throw GetLastError();result.push_back(item);
   Log(session,L"Matched installed TrayLeaf PID "+std::to_wstring(item.pid));
  }
 }while(Process32NextW(snapshot.h,&entry));return result;
}
// 5 asks for elevation. The handle binds path, creation time, termination and wait.
static DWORD Stop(const std::vector<RunningImage>&images,MSIHANDLE session){
 DWORD result=0;
 for(const auto&id:images){
  Handle p(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION|PROCESS_TERMINATE|SYNCHRONIZE,FALSE,id.pid));
  if(!p.h){DWORD e=GetLastError();if(e!=ERROR_INVALID_PARAMETER)result=e;continue;}
  if(WaitForSingleObject(p.h,0)==WAIT_OBJECT_0)continue;
  FILETIME created={};if(!Created(p.h,created)||CompareFileTime(&created,&id.created)!=0||!Same(ImagePath(p.h),id.image)){result=ERROR_INVALID_DATA;continue;}
  if(!TerminateProcess(p.h,0)){result=GetLastError();continue;}
  if(WaitForSingleObject(p.h,2500)!=WAIT_OBJECT_0){result=WAIT_TIMEOUT;continue;}
  Log(session,L"Closed confirmed TrayLeaf PID "+std::to_wstring(id.pid));
 }return result;
}
static bool Authorized(MSIHANDLE session,size_t count){
 if(Property(session,L"TRAYLEAF_CLOSE_RUNNING")==L"1")return true;
 if(_wtoi(Property(session,L"UILevel").c_str())<4)return false;
 MSIHANDLE record=MsiCreateRecord(0);
 MsiRecordSetStringW(record,0,L"TrayLeaf.exe 仍在运行\n\n是否强制关闭并继续安装？未保存的数据可能丢失");
 // Route through MSI's UI channel, never a service-desktop MessageBox. X/Esc => Cancel.
 int answer=MsiProcessMessage(session,(INSTALLMESSAGE)(INSTALLMESSAGE_USER|MB_OKCANCEL|MB_ICONWARNING|MB_DEFBUTTON2),record);
 MsiCloseHandle(record);return answer==IDOK;
}
static DWORD ElevatedStop(MSIHANDLE session,const std::vector<std::wstring>&targets){
 std::wstring temp=Property(session,L"TempFolder");if(temp.empty())return ERROR_PATH_NOT_FOUND;
 wchar_t name[MAX_PATH];if(!GetTempFileNameW(temp.c_str(),L"TLC",0,name))return GetLastError();DeleteFileW(name);
 std::wstring dir=name;if(!CreateDirectoryW(dir.c_str(),NULL))return GetLastError();
 std::wstring helper=dir+L"\\TrayLeafInstallerClose.exe";DWORD result=ERROR_INSTALL_FAILURE;
 MSIHANDLE db=MsiGetActiveDatabase(session),view=0,record=0;
 if(MsiDatabaseOpenViewW(db,L"SELECT `Data` FROM `Binary` WHERE `Name`='ShutdownHelperBinary'",&view)==ERROR_SUCCESS&&MsiViewExecute(view,0)==ERROR_SUCCESS&&MsiViewFetch(view,&record)==ERROR_SUCCESS){
  bool written=false;
  {Handle file(CreateFileW(helper.c_str(),GENERIC_WRITE,0,NULL,CREATE_NEW,FILE_ATTRIBUTE_NORMAL,NULL));
   if(file.h!=INVALID_HANDLE_VALUE){char bytes[16384];DWORD n=sizeof(bytes),put=0;written=true;
    for(;;){n=sizeof(bytes);UINT e=MsiRecordReadStream(record,1,bytes,&n);if(e!=ERROR_SUCCESS){written=false;break;}if(!n)break;if(!WriteFile(file.h,bytes,n,&put,NULL)||put!=n){written=false;break;}}
   }}
  if(written){
   std::wstring args=L"--close";for(const auto&t:targets)args+=L" \""+t+L"\"";
   SHELLEXECUTEINFOW info={};info.cbSize=sizeof(info);info.fMask=SEE_MASK_NOCLOSEPROCESS|SEE_MASK_FLAG_NO_UI;info.lpVerb=L"runas";info.lpFile=helper.c_str();info.lpParameters=args.c_str();info.nShow=SW_HIDE;
   Log(session,L"Requesting elevation only after force-close consent");
   if(ShellExecuteExW(&info)){Handle process(info.hProcess);result=WaitForSingleObject(process.h,10000)==WAIT_OBJECT_0?0:WAIT_TIMEOUT;if(!result&&!GetExitCodeProcess(process.h,&result))result=GetLastError();}
   else result=GetLastError();
  }
 }
 if(record)MsiCloseHandle(record);if(view){MsiViewClose(view);MsiCloseHandle(view);}if(db)MsiCloseHandle(db);
 DeleteFileW(helper.c_str());RemoveDirectoryW(dir.c_str());return result;
}
#ifndef TRAYLEAF_HELPER
extern "C" __declspec(dllexport) UINT __stdcall CloseTrayLeafForInstall(MSIHANDLE session){
 try{
  std::vector<std::wstring>targets;
  for(const wchar_t*property:{L"INSTALLFOLDER",L"TRAYLEAF_PREVIOUS_FOLDER"}){
   auto folder=Property(session,property);if(folder.empty())continue;
   auto exe=Full(folder+((folder.back()==L'\\')?L"":L"\\")+L"TrayLeaf.exe");
   if(!ValidTarget(exe))return ERROR_INSTALL_FAILURE;if(std::find(targets.begin(),targets.end(),exe)==targets.end())targets.push_back(exe);
  }
  auto images=Find(targets,session);if(images.empty())return ERROR_SUCCESS;
  if(!Authorized(session,images.size())){Log(session,L"Cancelled without terminating processes");return ERROR_INSTALL_USEREXIT;}
  Log(session,L"Explicit force-close consent received");
  DWORD result=Stop(images,session);if(result==ERROR_ACCESS_DENIED)result=ElevatedStop(session,targets);
  if(result||!Find(targets,session).empty()){Log(session,L"Close failed; installation cancelled before validation, code "+std::to_wstring(result));return ERROR_INSTALL_USEREXIT;}
  return ERROR_SUCCESS;
 }catch(DWORD error){Log(session,L"Identity inspection failed; refusing installation, code "+std::to_wstring(error));return ERROR_INSTALL_FAILURE;}
 catch(...){Log(session,L"Unexpected exception; refusing installation");return ERROR_INSTALL_FAILURE;}
}
#else
// Native deferred cleanup: no WPF startup, no nested UAC, and a bounded wait.
static DWORD WINAPI CleanupStartupTask(void* context){
 const std::wstring name=L"TrayLeaf-"+*static_cast<std::wstring*>(context);
 HRESULT init=CoInitializeEx(NULL,COINIT_MULTITHREADED);ITaskService* service=NULL;ITaskFolder* folder=NULL;HRESULT hr=E_FAIL;
 if(SUCCEEDED(init)){hr=CoCreateInstance(CLSID_TaskScheduler,NULL,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&service));
  if(SUCCEEDED(hr)){VARIANT empty;VariantInit(&empty);hr=service->Connect(empty,empty,empty,empty);}
  if(SUCCEEDED(hr)){BSTR root=SysAllocString(L"\\");hr=service->GetFolder(root,&folder);SysFreeString(root);}
  if(SUCCEEDED(hr)){BSTR task=SysAllocString(name.c_str());hr=folder->DeleteTask(task,0);SysFreeString(task);if(hr==HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND))hr=S_OK;}
  if(folder)folder->Release();if(service)service->Release();CoUninitialize();}
 return SUCCEEDED(hr)?0:(DWORD)hr;
}
static int WaitStartupCleanup(LPTHREAD_START_ROUTINE action,void* context){
 Handle worker(CreateThread(NULL,0,action,context,0,NULL));if(!worker.h)return (int)GetLastError();
 if(WaitForSingleObject(worker.h,2500)!=WAIT_OBJECT_0)return WAIT_TIMEOUT;DWORD code=1;GetExitCodeThread(worker.h,&code);return (int)code;
}
#ifdef TRAYLEAF_CLEANUP_TEST
static DWORD WINAPI SimulatedStall(void*){Sleep(60000);return 0;}
static DWORD WINAPI SimulatedSuccess(void*){return 0;}
#endif
static int RemoveStartupWithDeadline(const std::wstring& owner){
 PSID sid=NULL;if(!ConvertStringSidToSidW(owner.c_str(),&sid))return ERROR_INVALID_SID;bool valid=IsValidSid(sid)!=FALSE;LocalFree(sid);if(!valid)return ERROR_INVALID_SID;
 // Static lifetime remains valid even if the COM worker stalls until ExitProcess.
 static std::wstring copy;copy=owner;return WaitStartupCleanup(CleanupStartupTask,&copy);
}
int WINAPI wWinMain(HINSTANCE,HINSTANCE,PWSTR,int){
 try{
  int count=0;LPWSTR*argv=CommandLineToArgvW(GetCommandLineW(),&count);std::vector<std::wstring>targets;
  if(!argv)return 2;
#ifdef TRAYLEAF_CLEANUP_TEST
  if(count==2&&Same(argv[1],L"--fixture-stall"))ExitProcess(WaitStartupCleanup(SimulatedStall,NULL));
  if(count==2&&Same(argv[1],L"--fixture-success"))ExitProcess(WaitStartupCleanup(SimulatedSuccess,NULL));
#endif
  if(count==3&&Same(argv[1],L"--remove-startup")){std::wstring owner=argv[2];LocalFree(argv);int result=RemoveStartupWithDeadline(owner);ExitProcess((UINT)result);}
  bool valid=count>=3&&count<=10&&Same(argv[1],L"--close");
  if(valid)for(int i=2;i<count;i++){auto path=Full(argv[i]);if(!ValidTarget(path)){valid=false;break;}targets.push_back(path);}
  LocalFree(argv);if(!valid)return 2;
  return (int)Stop(Find(targets,0),0);
 }catch(...){return 1;}
}
#endif

// Save independently of Explorer. When the view is available, position before redraw.
#ifndef TRAYLEAF_HELPER
#include <shlobj.h>
#include <shobjidl.h>
#include <exdisp.h>
#include <servprov.h>
#include <shlwapi.h>
#include "DesktopPlacement.h"
#include "DesktopShortcut.h"
static UINT DesktopShortcutOperation(MSIHANDLE session,bool capture){
 HRESULT init=CoInitializeEx(NULL,COINIT_APARTMENTTHREADED);bool uninit=SUCCEEDED(init);
 std::wstring wanted,dir=Property(session,L"INSTALLFOLDER"),exe;bool saved=false,existed=false;HRESULT failure=S_OK;
 if(!dir.empty()&&dir.back()!=L'\\')dir+=L"\\";exe=dir+L"TrayLeaf.exe";
 IShellWindows* windows=NULL;IDispatch* desktop=NULL;IServiceProvider* provider=NULL;IShellBrowser* browser=NULL;IShellView* view=NULL;IFolderView2* folderView=NULL;IShellFolder* folder=NULL;IEnumIDList* items=NULL;IShellFolderView* legacy=NULL;PIDLIST_ABSOLUTE absolute=NULL;
 try{wchar_t desktopPath[MAX_PATH];if(FAILED(SHGetFolderPathW(NULL,CSIDL_DESKTOPDIRECTORY,NULL,SHGFP_TYPE_CURRENT,desktopPath)))throw E_FAIL;
 wanted=Full(std::wstring(desktopPath)+L"\\轻羽归栈.lnk");
 existed=GetFileAttributesW(wanted.c_str())!=INVALID_FILE_ATTRIBUTES;
 if(capture)MsiSetPropertyW(session,L"TRAYLEAF_DESKTOP_EXISTS",existed?L"1":L"0");
 HRESULT hr=CoCreateInstance(CLSID_ShellWindows,NULL,CLSCTX_LOCAL_SERVER,IID_PPV_ARGS(&windows));if(FAILED(hr))throw hr;
 VARIANT empty;VariantInit(&empty);long hwnd=0;if(FAILED(windows->FindWindowSW(&empty,&empty,SWC_DESKTOP,&hwnd,SWFO_NEEDDISPATCH,&desktop))||!desktop)throw E_FAIL;
 if(FAILED(desktop->QueryInterface(IID_PPV_ARGS(&provider)))||FAILED(provider->QueryService(SID_STopLevelBrowser,IID_PPV_ARGS(&browser)))||FAILED(browser->QueryActiveShellView(&view))||FAILED(view->QueryInterface(IID_PPV_ARGS(&folderView)))||FAILED(folderView->GetFolder(IID_PPV_ARGS(&folder)))||FAILED(folderView->Items(SVGIO_ALLVIEW,IID_PPV_ARGS(&items))))throw E_NOINTERFACE;
 DWORD flags=0;if(FAILED(folderView->GetCurrentFolderFlags(&flags)))throw E_FAIL;
 std::vector<POINT> points;POINT old={};bool positionValid=false;PIDLIST_RELATIVE item=NULL;
 while(items->Next(1,&item,NULL)==S_OK){STRRET name;wchar_t path[32768];POINT p;
 bool ours=SUCCEEDED(folder->GetDisplayNameOf(item,SHGDN_FORPARSING,&name))&&SUCCEEDED(StrRetToBufW(&name,item,path,32768))&&Same(Full(path),wanted);
 if(ours)positionValid=SUCCEEDED(folderView->GetItemPosition(item,&old));else if(SUCCEEDED(folderView->GetItemPosition(item,&p)))points.push_back(p);
 CoTaskMemFree(item);item=NULL;}
 if(capture){MsiSetPropertyW(session,L"TRAYLEAF_DESKTOP_EXISTS",existed?L"1":L"0");if(positionValid){MsiSetPropertyW(session,L"TRAYLEAF_DESKTOP_X",std::to_wstring(old.x).c_str());MsiSetPropertyW(session,L"TRAYLEAF_DESKTOP_Y",std::to_wstring(old.y).c_str());}throw S_FALSE;}
 POINT desired=old;bool place=positionValid;
 if(!Property(session,L"TRAYLEAF_DESKTOP_X").empty()){desired={_wtol(Property(session,L"TRAYLEAF_DESKTOP_X").c_str()),_wtol(Property(session,L"TRAYLEAF_DESKTOP_Y").c_str())};place=true;}
 else if(!existed){POINT spacing;HWND handle=NULL;RECT client={};if(SUCCEEDED(folderView->GetSpacing(&spacing))&&SUCCEEDED(view->GetWindow(&handle))&&GetClientRect(handle,&client))place=DesktopTailPosition(points,spacing,client.right,client.bottom,desired);}
 // User-selected automatic arrangement owns ordering; do not change that preference.
 if(flags&FWF_AUTOARRANGE)place=false;
 // Legacy positioning is optional; a missing Explorer interface cannot prevent creation.
 if(!existed)view->QueryInterface(IID_PPV_ARGS(&legacy));
 bool ok=DesktopPublishAtomically([&](bool enabled){return SUCCEEDED(folderView->SetRedraw(enabled?TRUE:FALSE));},[&](){
  failure=SaveDesktopShortcut(wanted.c_str(),exe.c_str(),dir.c_str());saved=SUCCEEDED(failure);return saved;
 },[&](){
  absolute=ILCreateFromPathW(wanted.c_str());if(!absolute)return false;
  if(!existed&&legacy){UINT index=0;legacy->AddObject(ILFindLastID(absolute),&index);}
  if(!place)return true;PCUITEMID_CHILD id=ILFindLastID(absolute);
  return SUCCEEDED(folderView->SelectAndPositionItems(1,&id,&desired,SVSI_POSITIONITEM));
 });
 Log(session,ok?L"Desktop shortcut saved; desktop positioning is best effort":L"Desktop shortcut save failed");
 }catch(HRESULT hr){failure=hr;if(!capture)Log(session,L"Desktop positioning unavailable, HRESULT="+std::to_wstring((long)hr)+L"; creating shortcut independently");}
 catch(...){failure=E_FAIL;if(!capture)Log(session,L"Desktop positioning unavailable; creating shortcut independently");}
 if(!capture&&!saved&&!wanted.empty()){
  failure=SaveDesktopShortcut(wanted.c_str(),exe.c_str(),dir.c_str());saved=SUCCEEDED(failure);
  Log(session,saved?L"Desktop shortcut fallback saved successfully":L"Desktop shortcut fallback save failed, HRESULT="+std::to_wstring((long)failure));
 }
 if(!capture&&saved)SHChangeNotify(existed?SHCNE_UPDATEITEM:SHCNE_CREATE,SHCNF_PATHW,wanted.c_str(),NULL);
 if(absolute)CoTaskMemFree(absolute);if(legacy)legacy->Release();if(items)items->Release();if(folder)folder->Release();if(folderView)folderView->Release();if(view)view->Release();if(browser)browser->Release();if(provider)provider->Release();if(desktop)desktop->Release();if(windows)windows->Release();if(uninit)CoUninitialize();return capture||saved?ERROR_SUCCESS:ERROR_INSTALL_FAILURE;
}
extern "C" __declspec(dllexport) UINT __stdcall CaptureDesktopShortcut(MSIHANDLE session){return DesktopShortcutOperation(session,true);}
extern "C" __declspec(dllexport) UINT __stdcall PublishDesktopShortcut(MSIHANDLE session){return DesktopShortcutOperation(session,false);}
#endif
