#include "PopUnity.h"
#include <exception>
#include <stdexcept>
#include <vector>
#include <sstream>
#include <algorithm>


int Unity::GetPluginEventId()
{
	return 0xdefdef;
}

bool Unity::IsDebugPluginEventEnabled()
{
	return false;
}
