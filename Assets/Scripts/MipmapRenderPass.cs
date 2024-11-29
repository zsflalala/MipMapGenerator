using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

public class MipmapRenderPass : ScriptableRenderPass
{
    private static readonly int Strategy = Shader.PropertyToID("_Strategy");
    private static readonly int Height   = Shader.PropertyToID("_Height");
    private static readonly int Width    = Shader.PropertyToID("_Width");
    private static readonly int InputTexture  = Shader.PropertyToID("_InputTexture");
    private readonly int        _outputTexture = Shader.PropertyToID("_OutputTexture");
    
    private readonly MipmapGeneratorFeature.Settings _mSettings;
    private readonly ComputeShader                   _mMipmapComputeShader;
    private RenderTexture                            _mMipmapTexture;
    private RenderTexture                            _mLastTexture = null;
    private int                                      _mComputeKernel;
    private RenderTexture[]                          _mMipmapTextures;
    private StreamWriter                             _mWriter = null;  
    private bool                                     _isMipmapGenerated = false; 

    public MipmapRenderPass(MipmapGeneratorFeature.Settings settings)
    {
        this._mSettings = settings;
        _mMipmapComputeShader = Resources.Load<ComputeShader>("MipmapComputeShader");
    }
    
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) 
    {
        if (!_isMipmapGenerated || _mLastTexture != _mMipmapTexture)
        {
            if (_mSettings.inputTexture == null)
            {
                Debug.LogError("Input Texture is missing. Please assign a texture in the Inspector.");
                return;
            }

            if (_mMipmapComputeShader == null)
            {
                Debug.LogError("Compute Shader is missing or not found in the Resources folder.");
                return;
            }
            
            GenerateMipmaps();
            _isMipmapGenerated = true;
            _mLastTexture = _mMipmapTexture;
        }
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        
    }

    private void GenerateMipmaps()
    {
        _mComputeKernel = _mMipmapComputeShader.FindKernel("CSMain");
        _mMipmapComputeShader.SetInt(Strategy, (int)_mSettings.strategy);
        
        int levels = _mSettings.mipmapLevels > 0 ? _mSettings.mipmapLevels : Mathf.FloorToInt(Mathf.Log(Mathf.Max(_mSettings.inputTexture.width, _mSettings.inputTexture.height), 2)) + 1;
        _mMipmapTextures = new RenderTexture[levels];
        Stopwatch stopwatch = new Stopwatch();
        string filePath = $"{Application.dataPath}/{_mSettings.outputDirectory}/MipmapGenerationTimesAndPixels.txt";
        string directoryPath = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directoryPath))
        {
            if (directoryPath != null) Directory.CreateDirectory(directoryPath);
        }
        if (!File.Exists(filePath))
        {
            File.Create(filePath).Dispose(); // 创建文件并立即关闭文件流
        }

        Debug.Log(_mSettings.inputTexture.format);
        Debug.Log(_mSettings.inputTexture.GetPixel(1024,1024));
        Texture2D testTexture = new Texture2D(_mSettings.inputTexture.width, _mSettings.inputTexture.height, TextureFormat.RGBA32, false);
        // Color topLeftColor = Color.red;     // 左上角区域是红色
        // Color topRightColor = Color.green;  // 右上角区域是绿色
        // Color bottomLeftColor = Color.blue; // 左下角区域是蓝色
        // Color bottomRightColor = Color.yellow; // 右下角区域是黄色
        //
        // for (int y = 0; y < 300; y++)
        // {
        //     for (int x = 0; x < 400; x++)
        //     {
        //         // 判断当前像素所在的区域
        //         Color pixelColor;
        //         if (x < 400 / 2 && y < 300 / 2)
        //         {
        //             pixelColor = topLeftColor; // 左上角
        //         }
        //         else if (x >= 400 / 2 && y < 300 / 2)
        //         {
        //             pixelColor = topRightColor; // 右上角
        //         }
        //         else if (x < 400 / 2 && y >= 300 / 2)
        //         {
        //             pixelColor = bottomLeftColor; // 左下角
        //         }
        //         else
        //         {
        //             pixelColor = bottomRightColor; // 右下角
        //         }
        //         testTexture.SetPixel(x, y, pixelColor);
        //     }
        // }
        // testTexture.Apply();
        Color[] pixels = _mSettings.inputTexture.GetPixels();
        testTexture.SetPixels(pixels);
        testTexture.Apply();
        string filePath2 = $"{Application.dataPath}/OutputMipMap/test.png";
        byte[] pngData = testTexture.EncodeToPNG();
        File.WriteAllBytes(filePath2, pngData);
        
        using (_mWriter = new StreamWriter(filePath, true)) // 'true' 表示追加到文件
        {
            for (int i = 0; i < levels; i++)
            {
                int width  = Mathf.Max(1, _mSettings.inputTexture.width  >> i);
                int height = Mathf.Max(1, _mSettings.inputTexture.height >> i);
                
                _mMipmapTextures[i] = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
                {
                    enableRandomWrite = true
                };
                _mMipmapTextures[i].Create();
                
                stopwatch.Start();
                if (i == 0) 
                {
                    Graphics.Blit(_mSettings.inputTexture, _mMipmapTextures[0]);
                }
                else
                {
                    _mMipmapComputeShader.SetTexture(_mComputeKernel, InputTexture, _mMipmapTextures[i-1]);
                    _mMipmapComputeShader.SetInt(Width, width);
                    _mMipmapComputeShader.SetInt(Height, height);
                    _mMipmapComputeShader.SetTexture(_mComputeKernel, _outputTexture, _mMipmapTextures[i]);
                    _mMipmapComputeShader.Dispatch(_mComputeKernel, Mathf.CeilToInt(width / 8.0f), Mathf.CeilToInt(height / 8.0f), 1);
                }
            
                stopwatch.Stop();
                double elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                _mWriter.WriteLine($"{i} {elapsedMilliseconds:F6} {width*height}");
                stopwatch.Reset();
                Debug.Log($"{_mSettings.inputTexture.name} level {i} generate mipmap time : {elapsedMilliseconds:F6} ms");
            }
        }
        
        if (_mSettings.saveToCPU)
        {
            // SaveMipmapsToCPU(levels);
            SaveOrigionMipmaps();
        }
    }
    
    private void SaveMipmapsToCPU(int levels)
    {
        string outputDirectory = $"{Application.dataPath}/{_mSettings.outputDirectory}";
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        for (int i = 0; i < levels; i++)
        {
            int width  = _mMipmapTextures[i].width;
            int height = _mMipmapTextures[i].height;
            
            RenderTexture.active = _mMipmapTextures[i];
            Texture2D mipTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            mipTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            mipTexture.Apply();
            RenderTexture.active = null;
            string filePath = $"{outputDirectory}/{_mSettings.inputTexture.name}_mipLevel_{i}.png";
            byte[] pngData = mipTexture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);
            Object.DestroyImmediate(mipTexture);
            Debug.Log($"Saved {_mSettings.inputTexture.name} MipLevel {i} to {filePath}");
        }
    }

    private void SaveOrigionMipmaps()
    {
        int maxMipLeve = Mathf.FloorToInt(Mathf.Log(Mathf.Max(_mSettings.inputTexture.width, _mSettings.inputTexture.height), 2)) + 1;
        RenderTexture renderTexture = new RenderTexture(_mSettings.inputTexture.width, _mSettings.inputTexture.height, 0, RenderTextureFormat.ARGB32)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = true,
            useMipMap = true,
            autoGenerateMips = true
        };
        Graphics.Blit(_mSettings.inputTexture, renderTexture);
        RenderTexture.active = renderTexture;
        Texture2D textureWithMipMap = new Texture2D(_mSettings.inputTexture.width, _mSettings.inputTexture.height, TextureFormat.RGBA32, true);
        textureWithMipMap.ReadPixels(new Rect(0, 0, _mSettings.inputTexture.width, _mSettings.inputTexture.height), 0, 0);
        textureWithMipMap.Apply();
        RenderTexture.active = null;
        renderTexture.Release();
        Object.DestroyImmediate(renderTexture);
        
        for (int i = 0; i < maxMipLeve; i++)
        {
            int mipWidth = Mathf.Max(1, _mSettings.inputTexture.width >> i);
            int mipHeight = Mathf.Max(1, _mSettings.inputTexture.height >> i);

            Texture2D mipTexture = new Texture2D(mipWidth, mipHeight, TextureFormat.RGBA32, false);
            mipTexture.SetPixels(textureWithMipMap.GetPixels(i));
            mipTexture.Apply();
            
            byte[] bytes = mipTexture.EncodeToPNG();
            string imgFilePath = $"{Application.dataPath}/UnityOrigionMipMaps/{_mSettings.inputTexture.name}_origionLevel_{i}.png";
            File.WriteAllBytes(imgFilePath, bytes);
            Debug.Log($"{_mSettings.inputTexture.name} Mipmap level {i} saved to: {imgFilePath}");
        }
    }
}