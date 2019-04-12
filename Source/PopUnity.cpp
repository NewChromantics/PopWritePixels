#include "PopUnity.h"
#include <exception>
#include <stdexcept>
#include <vector>
#include <sstream>
#include <algorithm>
#include "TStringBuffer.hpp"
#include <SoyUnity.h>



int Unity::GetPluginEventId()
{
	return 0xaabb22;
}

bool Unity::IsDebugPluginEventEnabled()
{
	return false;
}

