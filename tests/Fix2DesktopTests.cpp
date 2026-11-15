#include <windows.h>
#include <vector>
#include <algorithm>
#include <cstdlib>
#include <iostream>
#include "../installer/DesktopPlacement.h"
int main(){int fail=0;auto check=[&](bool x,const char* n){std::cout<<(x?"PASS ":"FAIL ")<<n<<"\n";if(!x)fail++;};
check(DesktopPlacementPolicy(true,true,false)==DesktopPlacementMode::Restore,"existing-shortcut-restored");check(DesktopPlacementPolicy(true,false,false)==DesktopPlacementMode::Keep,"existing-without-position-never-moves-to-tail");check(DesktopPlacementPolicy(false,false,false)==DesktopPlacementMode::Tail,"new-shortcut-moves-to-tail");check(DesktopPlacementPolicy(true,true,true)==DesktopPlacementMode::Keep,"autoarrange-existing-kept");check(DesktopPlacementPolicy(false,false,true)==DesktopPlacementMode::Keep,"autoarrange-new-delegated-to-explorer");std::vector<POINT> items={{0,0},{0,80},{0,160},{80,0}};POINT next={},spacing={80,80};check(DesktopTailPosition(items,spacing,800,600,next)&&next.x==80&&next.y==80,"new-shortcut-after-current-tail");std::vector<int> events;
auto redraw=[&](bool on){events.push_back(on?4:1);return true;};
check(DesktopPublishAtomically(redraw,[&](){events.push_back(2);return true;},[&](){events.push_back(3);return true;})&&events==std::vector<int>({1,2,3,4}),"publish-position-before-first-repaint");
events.clear();check(!DesktopPublishAtomically(redraw,[&](){events.push_back(2);return false;},[&](){events.push_back(3);return true;})&&events==std::vector<int>({1,2,4}),"save-failure-always-restores-redraw");
events.clear();check(DesktopPublishAtomically([](bool){return false;},[&](){events.push_back(2);return true;},[&](){return true;})&&events==std::vector<int>({2}),"shortcut-saved-without-redraw-guard");
events.clear();try{DesktopPublishAtomically(redraw,[&]()->bool{throw 1;},[](){return true;});}catch(...){}check(events==std::vector<int>({1,4}),"exceptions-restore-desktop-redraw");
events.clear();check(DesktopPublishAtomically(redraw,[](){return true;},[](){return false;})&&events==std::vector<int>({1,4}),"position-failure-restores-desktop-redraw");
std::cout<<"RESULT passed="<<(11-fail)<<" failed="<<fail<<"\n";return fail?1:0;}
