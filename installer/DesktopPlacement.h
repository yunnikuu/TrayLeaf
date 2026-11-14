#pragma once
#include <windows.h>
#include <vector>
#include <algorithm>
inline bool DesktopTailPosition(const std::vector<POINT>& items,POINT spacing,LONG width,LONG height,POINT& next){
 if(spacing.x<=0||spacing.y<=0||width<spacing.x||height<spacing.y)return false;
 LONG firstY=spacing.y/2,firstX=spacing.x/2;
 if(items.empty()){next={firstX,firstY};return true;}
 POINT last=items.front();for(const auto& p:items){firstY=(std::min)(firstY,p.y);if(p.x>last.x||(p.x==last.x&&p.y>last.y))last=p;}
 next=last;next.y+=spacing.y;if(next.y+spacing.y/2>height){next.x+=spacing.x;next.y=firstY;}
 if(next.x+spacing.x/2>width||next.x<0||next.y<0)return false;
 for(const auto& p:items)if(abs(p.x-next.x)<spacing.x/2&&abs(p.y-next.y)<spacing.y/2)return false;return true;
}

// Existing shortcuts never enter the tail-placement branch, even if coordinate capture failed.
enum class DesktopPlacementMode { Keep, Restore, Tail };
static DesktopPlacementMode DesktopPlacementPolicy(bool existed,bool hasPosition,bool autoArrange){return autoArrange?DesktopPlacementMode::Keep:existed?(hasPosition?DesktopPlacementMode::Restore:DesktopPlacementMode::Keep):DesktopPlacementMode::Tail;}
