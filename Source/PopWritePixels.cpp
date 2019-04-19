#include "PopWritePixels.h"
#include <sstream>
#include <algorithm>
#include <functional>
#include <SoyUnity.h>

#if defined(ENABLE_OPENGL)
#include <SoyOpengl.h>
#include <SoyOpenglContext.h>
#endif

#if defined(ENABLE_DIRECTX)
#include <SoyDirectx.h>
#endif

#if defined(ENABLE_DIRECTX9)
#include <SoyDirectx9.h>
#endif


class TPendingBytes
{
public:
	uint8_t*	mBytes = 0;
	size_t		mBytesSize = 0;
	size_t		mRowsWritten = 0;
};

class TCache
{
public:
	bool			Used() const;
	void			Release();
	bool			HasFinished() const;
	size_t			GetRowsWritten() const;
	void			WritePixels();

public:
	size_t			mWriteRowsPerFrame = 256;
	bool			mEnableMips = true;		//	for new texture
	bool			mCreatingNewTexture = false;
	std::shared_ptr<Directx::TTexture>	mAllocatedTexture;
	void*			mTexturePtr = nullptr;
	SoyPixelsMeta	mTextureMeta;
	std::shared_ptr<TPendingBytes>	mPendingBytes;
};


namespace PopWritePixels
{
	//	gr: could be big as it's just sitting in memory, but made small so we
	//	can ensure client is releasing in case in future we NEED releasing
#define MAX_CACHES	200
	TCache		gCaches[MAX_CACHES];

	TCache&		AllocCache(int& CacheIndex);
	TCache&		GetCache(int CacheIndex);
	void		ReleaseCache(uint32_t CacheIndex);
}



template<typename RETURN,typename FUNC>
RETURN SafeCall(FUNC Function,const char* FunctionName,RETURN ErrorReturn)
{
	Soy::TScopeTimerPrint Timer(FunctionName, 1);
	try
	{
		return Function();
	}
	catch(std::exception& e)
	{
		std::Debug << FunctionName << " exception: " << e.what() << std::endl;
		return ErrorReturn;
	}
	catch(...)
	{
		std::Debug << FunctionName << " unknown exception." << std::endl;
		return ErrorReturn;
	}
}


/*
int ReadPixelFromTexture(void* TexturePtr,SoyPixelsImpl& Pixels,SoyPixelsMeta TextureMeta)
{
#if defined(ENABLE_OPENGL)
	auto OpenglContext = Unity::GetOpenglContextPtr();
#endif

#if defined(ENABLE_DIRECTX)
	auto DirectxContext = Unity::GetDirectxContextPtr();
#endif

#if defined(ENABLE_DIRECTX9)
	auto Directx9Context = Unity::GetDirectx9ContextPtr();
#endif

#if defined(ENABLE_OPENGL)
	if ( OpenglContext )
	{
		//	gr: in this plugin we're not calling the context iteration all the time, so lets do it ourselves for ::init() to create GLGenBuffers
		OpenglContext->Iteration();
		
		//	assuming type atm... maybe we can extract it via opengl?
		GLenum Type = GL_TEXTURE_2D;
		Opengl::TTexture Texture( TexturePtr, TextureMeta, Type );
		Texture.Read( Pixels, SoyPixelsFormat::Invalid, false );
		return 0;
	}
#endif
	
#if defined(ENABLE_DIRECTX)
	if ( DirectxContext )
	{
		if ( PopReadPixels::DirectxTexturePool == nullptr )
			PopReadPixels::DirectxTexturePool.reset( new TPool<Directx::TTexture>() );

		Directx::TTexture Texture( static_cast<ID3D11Texture2D*>(TexturePtr) );
		Texture.Read( Pixels, *DirectxContext, *PopReadPixels::DirectxTexturePool );
		return 0;
	}
#endif

#if defined(ENABLE_DIRECTX9)
	if ( Directx9Context )
	{
		if ( PopReadPixels::Directx9TexturePool == nullptr )
			PopReadPixels::Directx9TexturePool.reset( new TPool<Directx9::TTexture>() );

		Directx9::TTexture Texture( static_cast<IDirect3DTexture9*>(TexturePtr) );
		Texture.Read( Pixels, *Directx9Context, *PopReadPixels::Directx9TexturePool );
		return 0;
	}
#endif

	throw Soy::AssertException("Missing graphics device");
}



__export int ReadPixelFromTexture2D(void* TexturePtr,uint8_t* PixelData,int PixelDataSize,int* WidthHeightChannels,Unity::Texture2DPixelFormat::Type PixelFormat)
{
	try
	{
		SoyPixelsMeta Meta( WidthHeightChannels[0], WidthHeightChannels[1], Unity::GetPixelFormat( PixelFormat ) );
		SoyPixelsRemote Pixels( PixelData, PixelDataSize, Meta ); 
		ReadPixelFromTexture( TexturePtr, Pixels, Meta );
		return 0;
	}
	catch(const std::exception& e)
	{
		std::Debug << "Exception in " << __func__ << "; " << e.what() << std::endl;
		return -1;
	}
	catch(...)
	{
		std::Debug << "Unknown exception in " << __func__ << std::endl;
		return -1;
	}
}

__export int ReadPixelFromRenderTexture(void* TexturePtr,uint8_t* PixelData,int PixelDataSize,int* WidthHeightChannels,Unity::RenderTexturePixelFormat::Type PixelFormat)
{
	try
	{
		SoyPixelsMeta Meta( WidthHeightChannels[0], WidthHeightChannels[1], Unity::GetPixelFormat( PixelFormat ) );
		SoyPixelsRemote Pixels( PixelData, PixelDataSize, Meta ); 
		ReadPixelFromTexture( TexturePtr, Pixels, Meta );
		return 0;
	}
	catch(const std::exception& e)
	{
		std::Debug << "Exception in " << __func__ << "; " << e.what() << std::endl;
		return -1;
	}
	catch(...)
	{
		std::Debug << "Unknown Exception in " << __func__ << std::endl;
		return -1;
	}
}



*/


TCache& PopWritePixels::AllocCache(int& CacheIndex)
{
	for ( int i=0;	i<MAX_CACHES;	i++ )
	{
		auto& Cache = gCaches[i];
		if ( Cache.Used() )
			continue;
		
		CacheIndex = i;
		return Cache;
	}
	
	throw Soy::AssertException("No free caches");
}

TCache& PopWritePixels::GetCache(int CacheIndex)
{
	if ( CacheIndex < 0 || CacheIndex >= MAX_CACHES )
	{
		throw Soy::AssertException("Invalid Cache Index");
	}
	
	auto& Cache = gCaches[CacheIndex];
	if ( !Cache.Used() )
	{
		throw Soy::AssertException("Cache not allocated");
	}
	
	return Cache;
}


void PopWritePixels::ReleaseCache(uint32_t CacheIndex)
{
	if ( CacheIndex >= MAX_CACHES )
	{
		throw Soy::AssertException("Invalid Cache Index");
	}
	
	gCaches[CacheIndex].Release();
}

int AllocCacheRenderTexture(void* TexturePtr,SoyPixelsMeta Meta,bool EnableMips)
{
	int CacheIndex = -1;
	auto& Cache = PopWritePixels::AllocCache(CacheIndex);
	Cache.mTexturePtr = TexturePtr;
	Cache.mTextureMeta = Meta;
	if ( !TexturePtr )
		Cache.mCreatingNewTexture = true;
	Cache.mEnableMips = EnableMips;

	return CacheIndex;
}


__export int AllocCacheTexture2D(void* TexturePtr,int Width,int Height,Unity::Texture2DPixelFormat::Type PixelFormat)
{
	auto Function = [&]()
	{
		SoyPixelsMeta Meta( Width, Height, Unity::GetPixelFormat( PixelFormat ) );
		return AllocCacheRenderTexture( TexturePtr, Meta, false );
	};
	return SafeCall( Function, __func__, -1 );
}

__export int AllocCacheTexture(int Width,int Height,Unity::Texture2DPixelFormat::Type PixelFormat,bool EnableMips)
{
	auto Function = [&]()
	{
		SoyPixelsMeta Meta( Width, Height, Unity::GetPixelFormat( PixelFormat ) );
		return AllocCacheRenderTexture( nullptr, Meta, EnableMips );
	};
	return SafeCall( Function, __func__, -1 );
}


__export void ReleaseCache(int Cache)
{
	auto Function = [&]()
	{
		PopWritePixels::ReleaseCache( Cache );
		return 0;
	};
	SafeCall( Function, __func__, 0 );
}


__api(void) WritePixelsToCache(int CacheIndex)
{
	auto Function = [&]()
	{
		std::Debug << "WritePixelsWithCache(" << CacheIndex << ")" << std::endl;
		auto& Cache = PopWritePixels::GetCache(CacheIndex);
		
		//	write any pending pixels
		Cache.WritePixels();
		return 0;
	};
	SafeCall( Function, __func__, 0 );
}
/*
__export int AllocCacheRenderTexture(void* TexturePtr,int Width,int Height,Unity::RenderTexturePixelFormat::Type PixelFormat)
{
	try
	{
		auto CacheIndex32 = AllocCacheRenderTexture( TexturePtr, Width, Height, PixelFormat );
		if ( CacheIndex32 < 0 )
			throw Soy::AssertException("Failed to alloc");
		return CacheIndex32;
	}
	catch(const std::exception& e)
	{
		std::stringstream Error;
		Error << "Exception in EnumStrings; " << e.what();
		PopUnity::DebugLog( Error.str() );
		return -1;
	}
	catch(...)
	{
		std::stringstream Error;
		Error << "Unknown exception in EnumStrings";
		PopUnity::DebugLog( Error.str() );
		return -1;
	}
}


__export int AllocCacheTexture2D(void* TexturePtr,uint8_t* PixelData,uint8_t* PixelRevision,uint8_t* CacheIndex,int PixelDataSize,int Width,int Height,int Channels,Unity::Texture2DPixelFormat::Type PixelFormat)
{
	try
	{
		auto CacheIndex32 = AllocCacheTexture2D( TexturePtr, PixelData, PixelRevision, PixelDataSize, Width, Height, Channels, PixelFormat );
		if ( CacheIndex32 < 0 )
			throw Soy::AssertException("Failed to alloc");
		uint8_t CacheIndex8 = CacheIndex32;
		*CacheIndex = CacheIndex8;
		return ReadPixelsFromCache;
	}
	catch(const std::exception& e)
	{
		std::stringstream Error;
		Error << "Exception in EnumStrings; " << e.what();
		PopUnity::DebugLog( Error.str() );
		return nullptr;
	}
	catch(...)
	{
		std::stringstream Error;
		Error << "Unknown exception in EnumStrings";
		PopUnity::DebugLog( Error.str() );
		return nullptr;
	}
}
*/

__export UnityRenderingEvent GetWritePixelsToCacheFunc()
{
	return WritePixelsToCache;
}

__export bool QueueWritePixels(int CacheIndex,uint8_t* ByteData, int ByteDataSize)
{
	auto Function = [&]()
	{
		std::Debug << "WritePixels(" << CacheIndex << ")" << std::endl;
		auto& Cache = PopWritePixels::GetCache(CacheIndex);

		Cache.mPendingBytes.reset(new TPendingBytes());
		Cache.mPendingBytes->mBytes = ByteData;
		Cache.mPendingBytes->mBytesSize = ByteDataSize;
		return true;
	};
	return SafeCall( Function, __func__, false );
}

__export int GetRowsWritten(int CacheIndex)
{
	auto Function = [&]()
	{
		std::Debug << "WritePixels(" << CacheIndex << ")" << std::endl;
		auto& Cache = PopWritePixels::GetCache(CacheIndex);

		return Cache.GetRowsWritten();
	};
	return SafeCall( Function, __func__, -1 );
}

__export void SetWriteRowsPerFrame(int CacheIndex,int WriteRowsPerFrame)
{
	auto Function = [&]()
	{
		auto& Cache = PopWritePixels::GetCache(CacheIndex);
		
		if ( WriteRowsPerFrame < 1 )
			WriteRowsPerFrame = 1;
		Cache.mWriteRowsPerFrame = WriteRowsPerFrame;
		return 0;
	};
	SafeCall( Function, __func__, -1 );
}


__export void* GetCacheTexture(int CacheIndex)
{
	auto Function = [&]()
	{
		std::Debug << "GetCacheTexture(" << CacheIndex << ")" << std::endl;
		auto& Cache = PopWritePixels::GetCache(CacheIndex);
		if ( Cache.mTexturePtr )
			return Cache.mTexturePtr;

		if ( Cache.mAllocatedTexture )
		{
			//	unity needs a shader resource view
			auto& ResourceView = Cache.mAllocatedTexture->GetResourceView();
			return static_cast<void*>(&ResourceView);
			//return static_cast<void*>(Cache.mAllocatedTexture.get());
		}

		return static_cast<void*>(nullptr);
	};
	return SafeCall<void*>( Function, __func__, nullptr );
}


bool TCache::Used() const		
{
	if ( mTexturePtr )
		return true;
	if ( mAllocatedTexture )
		return true;
	if ( mCreatingNewTexture )
		return true;

	return false;
}

void TCache::Release() 
{
	mTexturePtr = nullptr;	
	mCreatingNewTexture = false; 
	mAllocatedTexture.reset();

	//	verify logic
	if ( Used() )
		throw Soy::AssertException("Post Release cache is still marked as used");
}

size_t TCache::GetRowsWritten() const
{
	//	waiting for data
	if ( !mPendingBytes )
		return 0;

	auto& Pending = *mPendingBytes;
	return Pending.mRowsWritten;
}

bool TCache::HasFinished() const
{
	auto RowsWritten = GetRowsWritten();
	if ( RowsWritten < mTextureMeta.GetHeight() )
		return false;

	return true;
}

void TCache::WritePixels()
{
	if ( !mPendingBytes )
		throw Soy::AssertException("No queued texture bytes");

	auto& Pending = *mPendingBytes;
	SoyPixelsRemote Pixels(Pending.mBytes, Pending.mBytesSize, mTextureMeta);


#if defined(ENABLE_DIRECTX)
	auto DirectxContext = Unity::GetDirectxContextPtr();

	if ( DirectxContext )
	{
		//	create  a new texture if there isn't one
		if ( !mTexturePtr && !mAllocatedTexture )
		{
			//auto TextureMode = Directx::TTextureMode::WriteOnly;
			static auto TextureMode = Directx::TTextureMode::RenderTarget;
			//auto TextureMode = Directx::TTextureMode::GpuOnly;
			mAllocatedTexture.reset(new Directx::TTexture(mTextureMeta, *DirectxContext, TextureMode, mEnableMips ));
		}

		auto RowFirst = mPendingBytes->mRowsWritten;
		auto RowLast = std::min<size_t>(RowFirst + mWriteRowsPerFrame, mTextureMeta.GetHeight() );
		auto RowCount = RowLast - RowFirst;

		if ( mAllocatedTexture )
		{
			mAllocatedTexture->Write(Pixels, *DirectxContext, RowFirst, RowCount );
			
			//	need a texture resource view for unity
			auto& Device = DirectxContext->LockGetDevice();
			auto& Resource = mAllocatedTexture->GetResourceView(Device);

			auto& Context = DirectxContext->LockGetContext();
			Context.GenerateMips(&Resource);

			DirectxContext->Unlock();
			DirectxContext->Unlock();
		}
		else
		{
			Directx::TTexture Texture(static_cast<ID3D11Texture2D*>(mTexturePtr));
			Texture.Write(Pixels, *DirectxContext, RowFirst, RowCount);
		}
				
		Pending.mRowsWritten = RowLast;
		return ;
	}
#endif

	throw Soy::AssertException("No device context");
}

