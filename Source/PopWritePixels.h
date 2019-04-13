#pragma once

#include "PopUnity.h"
#include <functional>


//	alloc a cache/job to write to an existing texture
__export int		AllocCacheTexture2D(void* TexturePtr, int Width, int Height,Unity::Texture2DPixelFormat::Type PixelFormat);

//	alloc a new texture	
__export int		AllocCacheTexture(int Width, int Height,Unity::Texture2DPixelFormat::Type PixelFormat);

//	cleanup
__export void		ReleaseCache(int Cache);

//	set which pixels to write on next update
__export bool		QueueWritePixels(int Cache,uint8_t* ByteData, int ByteDataSize);

//	has the job been done?
__export bool		HasCacheWrittenBytes(int Cache);

//	if we allocated a texture, this is it (also returns the original texture if we provided one)
__export void*		GetCacheTexture(int Cache);

//	get the "run a job on render thread"
__export UnityRenderingEvent GetWritePixelsToCacheFunc();


