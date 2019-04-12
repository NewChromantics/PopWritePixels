#pragma once

#include "PopUnity.h"
#include <functional>

/*
__export int					WritePixelsToTexture2D(void* TexturePtr,uint8_t* PixelData,int PixelDataSize,int Width,int Height,int Channels,Unity::RenderTexturePixelFormat::Type PixelFormat);



__export int					ReadPixelFromRenderTexture(void* TexturePtr,uint8_t* PixelData,int PixelDataSize,int* WidthHeightChannels,Unity::RenderTexturePixelFormat::Type PixelFormat);
__export int					ReadPixelFromTexture2D(void* TexturePtr,uint8_t* PixelData,int PixelDataSize,int* WidthHeightChannels,Unity::Texture2DPixelFormat::Type PixelFormat);

__export UnityRenderingEvent	GetReadPixelsFromCacheFunc();
__export int					AllocCacheRenderTexture(void* TexturePtr,int Width,int Height,Unity::boolean ReadAsFloat,Unity::RenderTexturePixelFormat::Type PixelFormat);
__export int					AllocCacheTexture2D(void* TexturePtr,int Width,int Height,Unity::boolean ReadAsFloat,Unity::Texture2DPixelFormat::Type PixelFormat);
__export void					ReleaseCache(int Cache);
__api(void)						ReadPixelsFromCache(int Cache);

__export int					ReadPixelBytesFromCache(int Cache,uint8_t* ByteData,int ByteDataSize);
__export int					ReadPixelFloatsFromCache(int Cache,float* FloatData,int FloatDataSize);
*/
__export int AllocCacheTexture2D(void* TexturePtr, int Width, int Height,Unity::Texture2DPixelFormat::Type PixelFormat);
__export void ReleaseCache(int Cache);
__export UnityRenderingEvent GetWritePixelsToCacheFunc();
__export bool QueueWritePixels(int Cache,uint8_t* ByteData, int ByteDataSize);
__export bool HasCacheWrittenBytes(int Cache);

