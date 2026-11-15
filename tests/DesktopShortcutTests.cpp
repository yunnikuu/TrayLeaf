#define UNICODE
#define _UNICODE
#include <windows.h>
#include <string>
#include <iostream>
#include "../installer/DesktopShortcut.h"
#include "../installer/DesktopPlacement.h"
bool ReadLink(const std::wstring& path,const std::wstring& exe,const std::wstring& dir){
 IShellLinkW* link=NULL;IPersistFile* file=NULL;wchar_t target[32768]={},working[32768]={},icon[32768]={};int index=-1;
 HRESULT hr=CoCreateInstance(CLSID_ShellLink,NULL,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&link));
 if(SUCCEEDED(hr))hr=link->QueryInterface(IID_PPV_ARGS(&file));if(SUCCEEDED(hr))hr=file->Load(path.c_str(),STGM_READ);
 if(SUCCEEDED(hr))hr=link->GetPath(target,32768,NULL,SLGP_RAWPATH);if(SUCCEEDED(hr))hr=link->GetWorkingDirectory(working,32768);if(SUCCEEDED(hr))hr=link->GetIconLocation(icon,32768,&index);
 bool ok=SUCCEEDED(hr)&&_wcsicmp(target,exe.c_str())==0&&_wcsicmp(working,dir.c_str())==0&&_wcsicmp(icon,exe.c_str())==0&&index==0;
 if(file)file->Release();if(link)link->Release();return ok;
}
int wmain(int argc,wchar_t**argv){if(argc!=2)return 2;HRESULT init=CoInitializeEx(NULL,COINIT_APARTMENTTHREADED);if(FAILED(init))return 3;
 std::wstring dir=argv[1];wchar_t module[32768];GetModuleFileNameW(NULL,module,32768);std::wstring exe=module;int passed=0,failed=0;
 auto check=[&](bool ok,const char* n){std::cout<<(ok?"PASS ":"FAIL ")<<n<<"\n";(ok?passed:failed)++;};
 for(int scenario=0;scenario<3;scenario++){
  std::wstring path=dir+L"\\native-"+std::to_wstring(scenario)+L".lnk";DeleteFileW(path.c_str());
  bool result=DesktopPublishAtomically([&](bool){return scenario!=0;},[&](){return SUCCEEDED(SaveDesktopShortcut(path.c_str(),exe.c_str(),dir.c_str()));},[&](){return scenario==2;});
  check(result&&ReadLink(path,exe,dir),scenario==0?"view-unavailable-real-link-target-workdir-icon":scenario==1?"position-failed-real-link-retained":"normal-real-link-target-workdir-icon");
  check(SUCCEEDED(SaveDesktopShortcut(path.c_str(),exe.c_str(),dir.c_str()))&&ReadLink(path,exe,dir),"existing-link-overwritten-and-readable");
 }
 auto invalid=dir+L"\\nonexistent-parent\\missing.lnk";
 check(FAILED(SaveDesktopShortcut(invalid.c_str(),exe.c_str(),dir.c_str()))&&GetFileAttributesW(invalid.c_str())==INVALID_FILE_ATTRIBUTES,"actual-save-failure-is-observable");
 CoUninitialize();std::cout<<"RESULT passed="<<passed<<" failed="<<failed<<"\n";return failed?1:0;
}
