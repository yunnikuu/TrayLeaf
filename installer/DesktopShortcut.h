#pragma once
#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
// Creating the link never requires access to the Explorer desktop view.
inline HRESULT SaveDesktopShortcut(const wchar_t* path,const wchar_t* exe,const wchar_t* directory){
 IShellLinkW* link=NULL;IPersistFile* persist=NULL;
 HRESULT hr=CoCreateInstance(CLSID_ShellLink,NULL,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&link));
 if(SUCCEEDED(hr))hr=link->SetPath(exe);
 if(SUCCEEDED(hr))hr=link->SetWorkingDirectory(directory);
 if(SUCCEEDED(hr))hr=link->SetIconLocation(exe,0);
 if(SUCCEEDED(hr))hr=link->SetDescription(L"轻羽归栈");
 if(SUCCEEDED(hr))hr=link->QueryInterface(IID_PPV_ARGS(&persist));
 if(SUCCEEDED(hr))hr=persist->Save(path,TRUE);
 if(SUCCEEDED(hr)&&GetFileAttributesW(path)==INVALID_FILE_ATTRIBUTES)hr=HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
 if(persist)persist->Release();if(link)link->Release();return hr;
}
