#include <windows.h>
#include <vector>
#include <algorithm>
#include <cstdlib>
#include <iostream>
#include "../installer/DesktopPlacement.h"
int main(){int fail=0;auto check=[&](bool x,const char* n){std::cout<<(x?"PASS ":"FAIL ")<<n<<"\n";if(!x)fail++;};
check(DesktopPlacementPolicy(true,true,false)==DesktopPlacementMode::Restore,"existing-shortcut-restored");check(DesktopPlacementPolicy(true,false,false)==DesktopPlacementMode::Keep,"existing-without-position-never-moves-to-tail");check(DesktopPlacementPolicy(false,false,false)==DesktopPlacementMode::Tail,"new-shortcut-moves-to-tail");check(DesktopPlacementPolicy(true,true,true)==DesktopPlacementMode::Keep,"autoarrange-existing-kept");check(DesktopPlacementPolicy(false,false,true)==DesktopPlacementMode::Keep,"autoarrange-new-delegated-to-explorer");std::vector<POINT> items={{0,0},{0,80},{0,160},{80,0}};POINT next={},spacing={80,80};check(DesktopTailPosition(items,spacing,800,600,next)&&next.x==80&&next.y==80,"new-shortcut-after-current-tail");std::cout<<"RESULT passed="<<(6-fail)<<" failed="<<fail<<"\n";return fail?1:0;}
