using UnityEngine;
using System.Collections;                   // required for Coroutines
using System.Runtime.InteropServices;       // required for DllImport
using System;                               // requred for IntPtr
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
	private static extern int AllocCacheTexture(int Width, int Height, TextureFormat PixelFormat, bool EnableMips);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern void ReleaseCache(int Cache);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern bool QueueWritePixels(int Cache, byte[] ByteData, int ByteDataSize);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern bool QueueWritePixels(int Cache, System.IntPtr ByteData, int ByteDataSize);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern int GetRowsWritten(int Cache);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr GetCacheTexture(int Cache);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr GetWritePixelsToCacheFunc();

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern void SetWriteRowsPerFrame(int Cache, int RowsPerFrame);




	public class JobCache
	{
		int? CacheIndex = null;
		IntPtr PluginFunction;
		Camera.CameraCallback IssueEventCallback = null;

		int? RowCount = null;	//	store height/row count for progress counter

		//	updating existing texture
		IntPtr TexturePtr;

		//	new texture
		int? NewWidth = null;
		int? NewHeight = null;
		TextureFormat? NewFormat = null;
		Texture2D NewTexture = null;
		bool? NewTextureMips = null;

		public JobCache(Texture2D texture)
		{
			TexturePtr = texture.GetNativeTexturePtr();
			//	gr: replace format with channels?
			CacheIndex = AllocCacheTexture2D(TexturePtr, texture.width, texture.height, texture.format);
			if (CacheIndex == -1)
				throw new System.Exception("Failed to allocate cache index");

			RowCount = texture.height;
			PluginFunction = GetWritePixelsToCacheFunc();
		}

		public JobCache(int Width, int Height, TextureFormat TextureFormat, bool GenerateMips)
		{
			//	gr: replace format with channels?
			CacheIndex = AllocCacheTexture(Width, Height, TextureFormat, GenerateMips);
			if (CacheIndex == -1)
				throw new System.Exception("Failed to allocate cache index");

			NewTextureMips = GenerateMips;
			RowCount = Height;
			NewWidth = Width;
			NewHeight = Height;
			NewFormat = TextureFormat;
			PluginFunction = GetWritePixelsToCacheFunc();
		}

		~JobCache()
		{
			Release();
		}

		public void SetWriteRowsPerFrame(int RowsPerFrame)
		{
			PopWritePixels.SetWriteRowsPerFrame(CacheIndex.Value, RowsPerFrame);
		}

		//	queue a write update
		public void QueueUpdate(Camera AfterCamera = null)
		{
			//	queue a write
			if (AfterCamera != null)
			{
				if (IssueEventCallback == null)
				{
					IssueEventCallback = (c) => {
						if (c == AfterCamera)
							GL.IssuePluginEvent(PluginFunction, CacheIndex.Value);
					};
					Camera.onPostRender += IssueEventCallback;
				}
			}
			else
			{
				GL.IssuePluginEvent(PluginFunction, CacheIndex.Value);
			}
		}

		public void QueueWrite(System.IntPtr Bytes, int Bytes_Length, bool Copy = false, Camera AfterCamera = null)
		{
			//	todo: make copy of bytes here, but it'll be slow
			//			we can pin/get GC handle for byte[] but intptr won't work that way
			if (Copy)
				throw new System.Exception("Currently not copying, assuming caller won't delete bytes over the next frame");

			if (!QueueWritePixels(CacheIndex.Value, Bytes, Bytes_Length))
				throw new System.Exception("SetCacheBytes returned error");

			QueueUpdate(AfterCamera);
		}

		public void QueueWrite(byte[] Bytes, bool Copy = false, Camera AfterCamera = null)
		{
			//	todo: make copy of bytes here, but it'll be slow
			//			we can pin/get GC handle for byte[] but intptr won't work that way
			if (Copy)
				throw new System.Exception("Currently not copying, assuming caller won't delete bytes over the next frame");

			if (!QueueWritePixels(CacheIndex.Value, Bytes, Bytes.Length))
				throw new System.Exception("SetCacheBytes returned error");

			//	queue a write
			if (AfterCamera != null)
			{
				if (IssueEventCallback == null)
				{
					IssueEventCallback = (c) => {
						if (c == AfterCamera)
							GL.IssuePluginEvent(PluginFunction, CacheIndex.Value);
					};
					Camera.onPostRender += IssueEventCallback;
				}
			}
			else
			{
				GL.IssuePluginEvent(PluginFunction, CacheIndex.Value);
			}
		}

		public float GetProgress()
		{
			var RowsWritten = GetRowsWritten(CacheIndex.Value);

			if (RowsWritten < 0)
				throw new System.Exception("Error with GetRowsWritten(): " + RowsWritten);

			return RowsWritten / (float)RowCount;
		}

		public bool HasFinished()
		{
			var RowsWritten = GetRowsWritten(CacheIndex.Value);

			if (RowsWritten < 0)
				throw new System.Exception("Error with GetRowsWritten(): " + RowsWritten);

			if (RowsWritten >= RowCount)
				return true;

			//	do another write in case it's multi-staged
			//	todo: some error/progress codes or something to say error vs more-todo
			QueueUpdate();
			return false;
		}

		public Texture GetTexture(bool LinearFilter)
		{
			//	already created
			if (NewTexture)
				return NewTexture;

			var TexturePtr = GetCacheTexture(CacheIndex.Value);
			//	catch this, as it crashes unity if we create textures with it
			if (TexturePtr == IntPtr.Zero)
				throw new System.Exception("Cache texture is null");

			//	create new texture
			NewTexture = Texture2D.CreateExternalTexture(NewWidth.Value, NewHeight.Value, NewFormat.Value, NewTextureMips.Value, LinearFilter, TexturePtr);
			return NewTexture;
		}

		public void Release()
		{
			//	release unity's texture before we destroy the texture/shader view
			if (NewTexture)
			{
				//	update to zero, will crash on dx11
				//	metal it seems from googling needs an explicit release.
				//t.UpdateExternalTexture(System.IntPtr.Zero);
				//	delete unity's texture and we're safe to free resources (texture will go black)
				Texture2D.Destroy(NewTexture);
				NewTexture = null;
			}

			//	gr: check we don't release whilst still using data
			if (IssueEventCallback != null)
			{
				Camera.onPostRender -= IssueEventCallback;
				IssueEventCallback = null;
			}

			if (CacheIndex.HasValue)
			{
				//	if we release here with a texture still using the plugin's texture, we may crash!
				ReleaseCache(CacheIndex.Value);
				CacheIndex = null;
			}
		}
	}

	public static JobCache WritePixelsAsync(Texture texture, byte[] Pixels, Camera AfterCamera = null)
	{
		/*
		if ( texture is RenderTexture )
		{
			var Job = new JobCache( texture as RenderTexture );
			Job.QueueWrite(Pixels,AfterCamera);
			return Job;
		}
		*/
		if (texture is Texture2D)
		{
			var Job = new JobCache(texture as Texture2D);
			Job.QueueWrite(Pixels, AfterCamera);
			return Job;
		}

		throw new System.Exception("Texture type not handled");
	}


	public static JobCache WritePixelsAsync(int Width, int Height, TextureFormat Format, bool GenerateMips, byte[] Pixels, Camera AfterCamera = null)
	{
		var Job = new JobCache(Width, Height, Format, GenerateMips);
		Job.QueueWrite(Pixels, AfterCamera);
		return Job;
	}

	public static JobCache WritePixelsAsync(int Width, int Height, TextureFormat Format, bool GenerateMips, System.IntPtr PixelBytes, int PixelBytesLength, Camera AfterCamera = null)
	{
		var Job = new JobCache(Width, Height, Format, GenerateMips);
		Job.QueueWrite(PixelBytes, PixelBytesLength, AfterCamera);
		return Job;
	}

}
