using UnityEngine;
using System.Collections;					// required for Coroutines
using System.Runtime.InteropServices;		// required for DllImport
using System;								// requred for IntPtr
using System.Text;
using System.Collections.Generic;


/// <summary>
///	Low level interface
/// </summary>
public static class PopWritePixels 
{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
	private const string PluginName = "PopWritePixels";
#else
#error Unsupported platform
#endif
	
	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern int AllocCacheTexture2D(IntPtr TexturePtr, int Width, int Height, TextureFormat PixelFormat);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern void ReleaseCache(int Cache);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr GetWritePixelsWithCacheFunc();

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern bool SetWritePixelsBytes(int Cache, byte[] ByteData, int ByteDataSize);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern bool HasCacheWrittenBytes(int Cache);




	public class JobCache
	{
		int?		CacheIndex = null;
		IntPtr		TexturePtr;
		IntPtr		PluginFunction;
		Camera.CameraCallback IssueEventCallback = null;

		public JobCache(Texture2D texture)
		{
			TexturePtr = texture.GetNativeTexturePtr();
			//	gr: replace format with channels?
			CacheIndex = AllocCacheTexture2D( TexturePtr, texture.width, texture.height, texture.format );
			if ( CacheIndex == -1 )
				throw new System.Exception("Failed to allocate cache index");

			PluginFunction = GetWritePixelsWithCacheFunc();
		}

		~JobCache()
		{
			Release();
		}

		public void QueueWrite(byte[] Bytes,bool Copy=false,Camera AfterCamera=null)
		{
			//	todo: make copy of bytes here, but it'll be slow
			//			we can pin/get GC handle for byte[] but intptr won't work that way
			if (Copy)
				throw new System.Exception("Currently not copying, assuming caller won't delete bytes over the next frame");

			if (!SetWritePixelsBytes(CacheIndex.Value, Bytes, Bytes.Length))
				throw new System.Exception("SetCacheBytes returned error");

			//	queue a write
			if (AfterCamera != null) {
				if (IssueEventCallback == null) {
					IssueEventCallback = (c) => {
						if ( c == AfterCamera )
							GL.IssuePluginEvent (PluginFunction, CacheIndex.Value);
					};
					Camera.onPostRender += IssueEventCallback;
				}
			} else {
				GL.IssuePluginEvent (PluginFunction, CacheIndex.Value);
			}
		}
		
		public bool	HasFinished()
		{
			var Written = HasCacheWrittenBytes(CacheIndex.Value);
			return Written;
		}

		public void	Release()
		{
			//	gr: check we don't release whilst still using data
			if (IssueEventCallback != null)
			{
				Camera.onPostRender -= IssueEventCallback;
				IssueEventCallback = null;
			}

			if ( CacheIndex.HasValue )
				ReleaseCache( CacheIndex.Value );
		}
	}

	public static JobCache WritePixelsAsync(Texture texture,byte[] Pixels,Camera AfterCamera=null)
	{
		Debug.Log ("allocating");
		/*
		if ( texture is RenderTexture )
		{
			var Job = new JobCache( texture as RenderTexture );
			Job.QueueWrite(Pixels,AfterCamera);
			return Job;
		}
		*/
		if ( texture is Texture2D )
		{
			var Job = new JobCache( texture as Texture2D );
			Job.QueueWrite(Pixels,AfterCamera);
			return Job;
		}

		throw new System.Exception("Texture type not handled");
	}
	
	

}
