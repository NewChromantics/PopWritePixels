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

	void OnEnable()
	{
		//	make new texture
		NewTexture = new Texture2D(TextureWidth, TextureHeight, TextureFormat, MipMap);
		NewTexture.filterMode = TextureFilterMode;
		var Mat = this.GetComponent<MeshRenderer>().material;
		Mat.mainTexture = NewTexture;

		//	generate pixels
		if (WriteColours.Count == 0)
			WriteColours.Add(Color.cyan);
		var PixelColours = new Color32[TextureWidth * TextureHeight];
		PixelBytes = new byte[TextureWidth*TextureHeight*4];
		for (int y = 0; y < TextureHeight; y++)
		{
			var Colour = WriteColours[y % WriteColours.Count];
			var Colour32 = new Color32((byte)(Colour.r * 255), (byte)(Colour.g * 255), (byte)(Colour.b * 255), (byte)(Colour.a * 255) );
			for (int x = 0; x < TextureWidth; x++)
			{
				var PixelIndex = (x + (y * TextureWidth)) * 4;
				PixelBytes[PixelIndex + 0] = Colour32.r;
				PixelBytes[PixelIndex + 0] = Colour32.g;
				PixelBytes[PixelIndex + 0] = Colour32.b;
				PixelBytes[PixelIndex + 0] = Colour32.a;
				PixelColours[x + (y * TextureWidth)] = Colour32;
			}
		}
		if ( NewTexture.mipmapCount == 0 )
			NewTexture.LoadRawTextureData(PixelBytes);
		else
			NewTexture.SetPixels32(PixelColours);
		NewTexture.Apply();
		WritePixelsJob = PopWritePixels.WritePixelsAsync(NewTexture, PixelBytes);
	}

	void Update()
	{
		if (WritePixelsJob==null)
		{
			throw new System.Exception("No write job");
		}

		if (WritePixelsJob.HasFinished())
		{
			Debug.Log("Finished writing pixels");
			this.enabled = false;
			WritePixelsJob.Release();
		}
	}
}
