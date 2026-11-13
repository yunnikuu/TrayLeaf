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
int WINAPI wWinMain(HINSTANCE,HINSTANCE,PWSTR,int){
 try{
  int count=0;LPWSTR*argv=CommandLineToArgvW(GetCommandLineW(),&count);std::vector<std::wstring>targets;
  if(!argv)return 2;
  bool valid=count>=3&&count<=10&&Same(argv[1],L"--close");
  if(valid)for(int i=2;i<count;i++){auto path=Full(argv[i]);if(!ValidTarget(path)){valid=false;break;}targets.push_back(path);}
  LocalFree(argv);if(!valid)return 2;
  return (int)Stop(Find(targets,0),0);
 }catch(...){return 1;}
}
#endif
