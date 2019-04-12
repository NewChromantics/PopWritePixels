using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreateTextureAndApplyToMaterial : MonoBehaviour {

	public int TextureWidth = 1024;
	public int TextureHeight = 1024;
	public FilterMode TextureFilterMode = FilterMode.Point;
	public TextureFormat TextureFormat = TextureFormat.RGBA32;
	public bool MipMap = false;
	public List<Color> WriteColours = new List<Color>(new Color[]{Color.red, Color.green, Color.blue});
	public Texture2D NewTexture;
	PopWritePixels.JobCache WritePixelsJob;
	byte[] PixelBytes;

	byte[] GeneratePixelBytes(int Width,int Height,int ComponentCount,Color32[] PixelColours)
	{
		UnityEngine.Profiling.Profiler.BeginSample("GeneratePixelBytes(" + Width+"x"+Height+")");

		//var PixelColours = new Color32[Width * Height];
		var PixelBytes = new byte[Width * Height * ComponentCount];
		for (int y = 0; y < TextureHeight; y++)
		{
			var Colour = WriteColours[y % WriteColours.Count];
			var Colour32 = new Color32((byte)(Colour.r * 255), (byte)(Colour.g * 255), (byte)(Colour.b * 255), (byte)(Colour.a * 255));
			for (int x = 0; x < TextureWidth; x++)
			{
				var PixelIndex = (x + (y * TextureWidth)) * 4;
				PixelBytes[PixelIndex + 0] = Colour32.r;
				PixelBytes[PixelIndex + 0] = Colour32.g;
				PixelBytes[PixelIndex + 0] = Colour32.b;
				PixelBytes[PixelIndex + 0] = Colour32.a;
				if (PixelColours!=null)
					PixelColours[x + (y * TextureWidth)] = Colour32;
			}
		}
		UnityEngine.Profiling.Profiler.EndSample();
		return PixelBytes;
	}

	IEnumerator Run()
	{
		yield return new WaitForSeconds(1);

		//	make new texture
		UnityEngine.Profiling.Profiler.BeginSample("Create Texture");
		NewTexture = new Texture2D(TextureWidth, TextureHeight, TextureFormat, MipMap);
		UnityEngine.Profiling.Profiler.EndSample();
		NewTexture.filterMode = TextureFilterMode;
		var Mat = this.GetComponent<MeshRenderer>().material;
		Mat.mainTexture = NewTexture;
		yield return null;
		
		//	generate pixels
		if (WriteColours.Count == 0)
			WriteColours.Add(Color.cyan);

		PixelBytes = GeneratePixelBytes(TextureWidth, TextureHeight, 4, null);
		yield return null;

		//	show preview
		if (NewTexture.mipmapCount == 0)
		{
			UnityEngine.Profiling.Profiler.BeginSample("LoadRawTextureData()");
			NewTexture.LoadRawTextureData(PixelBytes);
			NewTexture.Apply();
			UnityEngine.Profiling.Profiler.EndSample();
		}
		else
		{
			var PixelColours = new Color32[TextureWidth * TextureHeight];
			var PixelBytes = GeneratePixelBytes(TextureWidth, TextureHeight, 4, PixelColours);
			UnityEngine.Profiling.Profiler.BeginSample("SetPixels32()");
			NewTexture.SetPixels32(PixelColours);
			NewTexture.Apply();
			UnityEngine.Profiling.Profiler.EndSample();
		}
		yield return null;

		WritePixelsJob = PopWritePixels.WritePixelsAsync(NewTexture, PixelBytes);
		yield return null;
	}

	void OnEnable()
	{
		StartCoroutine(Run());		
	}

	void Update()
	{
		if (WritePixelsJob==null)
		{
			//	waiting...
			//throw new System.Exception("No write job");
			return;
		}

		if (WritePixelsJob.HasFinished())
		{
			Debug.Log("Finished writing pixels");
			this.enabled = false;
			//NewTexture.UpdateExternalTexture();
			WritePixelsJob.Release();
		}
	}
}
