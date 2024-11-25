using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

public class MipmapRenderPass : ScriptableRenderPass
{
    private static readonly int MipLevel = Shader.PropertyToID("_MipLevel");
    private static readonly int Strategy = Shader.PropertyToID("_Strategy");
    private static readonly int Height   = Shader.PropertyToID("_Height");
    private static readonly int Width    = Shader.PropertyToID("_Width");
    private static readonly int InputTexture  = Shader.PropertyToID("_InputTexture");
    private int                 OutputTexture = Shader.PropertyToID("_OutputTexture");
    
    private readonly MipmapGeneratorFeature.Settings m_Settings;
    private readonly ComputeShader                   m_MipmapComputeShader;
    private RenderTexture                            m_MipmapTexture;
    private RenderTexture                            m_LastTexture = null;
    private int                                      m_ComputeKernel;
    private RenderTexture[]                          m_MipmapTextures;
    private StreamWriter                             m_Writer = null;  
    private bool                                     isMipmapGenerated = false; 

    public MipmapRenderPass(MipmapGeneratorFeature.Settings settings)
    {
        this.m_Settings = settings;
        m_MipmapComputeShader = Resources.Load<ComputeShader>("MipmapComputeShader");
    }
    
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) 
    {
        if (!isMipmapGenerated || m_LastTexture != m_MipmapTexture)
        {
            if (m_Settings.inputTexture == null)
            {
                Debug.LogError("Input Texture is missing. Please assign a texture in the Inspector.");
                return;
            }

            if (m_MipmapComputeShader == null)
            {
                Debug.LogError("Compute Shader is missing or not found in the Resources folder.");
                return;
            }
            
            GenerateMipmaps();
            isMipmapGenerated = true;
            m_LastTexture = m_MipmapTexture;
        }
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        
    }
    
    public override void OnCameraCleanup(CommandBuffer cmd) 
    {
        // if (m_MipmapTextures != null)
        // {
        //     foreach (var texture in m_MipmapTextures)
        //     {
        //         if (texture != null)
        //         {
        //             texture.Release();  // 释放RenderTexture资源
        //         }
        //     }
        // }
    }

    private void GenerateMipmaps()
    {
        m_ComputeKernel = m_MipmapComputeShader.FindKernel("CSMain");
        m_MipmapComputeShader.SetInt(Strategy, (int)m_Settings.strategy);
        
        int levels = m_Settings.mipmapLevels > 0 ? m_Settings.mipmapLevels : Mathf.FloorToInt(Mathf.Log(Mathf.Max(m_Settings.inputTexture.width, m_Settings.inputTexture.height), 2)) + 1;
        m_MipmapTextures = new RenderTexture[levels];
        Stopwatch stopwatch = new Stopwatch();
        string filePath = $"{Application.dataPath}/{m_Settings.outputDirectory}/MipmapGenerationTimesAndPixels.txt";
        string directoryPath = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        if (!File.Exists(filePath))
        {
            File.Create(filePath).Dispose(); // 创建文件并立即关闭文件流
        }
        
        using (m_Writer = new StreamWriter(filePath, true)) // 'true' 表示追加到文件
        {
            for (int i = 0; i < levels; i++)
            {
                stopwatch.Start();
            
                int width  = Mathf.Max(1, m_Settings.inputTexture.width  >> i);
                int height = Mathf.Max(1, m_Settings.inputTexture.height >> i);
                
                m_MipmapTextures[i] = new RenderTexture(width, height, 0)
                {
                    dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                    enableRandomWrite = true,
                    useMipMap = true,
                    autoGenerateMips = false
                };
                m_MipmapTextures[i].Create();

                if (i == 0) 
                {
                    Graphics.Blit(m_Settings.inputTexture, m_MipmapTextures[0]);
                }
                else
                {
                    m_MipmapComputeShader.SetInt(MipLevel, i);
                    m_MipmapComputeShader.SetTexture(m_ComputeKernel, InputTexture, m_MipmapTextures[i-1]);
                    m_MipmapComputeShader.SetInt(Width, width);
                    m_MipmapComputeShader.SetInt(Height, height);
                    m_MipmapComputeShader.SetTexture(m_ComputeKernel, OutputTexture, m_MipmapTextures[i]);
                    m_MipmapComputeShader.Dispatch(m_ComputeKernel, Mathf.CeilToInt(width / 8.0f), Mathf.CeilToInt(height / 8.0f), 1);
                }
            
                stopwatch.Stop();
                double elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                Debug.Log($"{m_Settings.inputTexture.name} level {i} generate mipmap time : {elapsedMilliseconds:F6} ms");
                m_Writer.WriteLine($"{i} {elapsedMilliseconds:F6} {width*height}");
                stopwatch.Reset();
            }
        }
        
        if (m_Settings.saveToCPU)
        {
            SaveMipmapsToCPU(levels);
        }
    }
    
    private void SaveMipmapsToCPU(int levels)
    {
        string outputDirectory = $"{Application.dataPath}/{m_Settings.outputDirectory}";
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        for (int i = 0; i < levels; i++)
        {
            int width  = m_MipmapTextures[i].width;
            int height = m_MipmapTextures[i].height;
            
            RenderTexture.active = m_MipmapTextures[i];
            
            Texture2D mipTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            mipTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            mipTexture.Apply();
            
            string filePath = $"{outputDirectory}/{m_Settings.inputTexture.name}_mipLevel_{i}.png";
            byte[] pngData = mipTexture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);

            Debug.Log($"Saved {m_Settings.inputTexture.name} MipLevel {i} to {filePath}");
            Object.DestroyImmediate(mipTexture);
        }

        RenderTexture.active = null;
    }

    private void SaveOrigionMipmaps()
    {
        int maxMipLeve = Mathf.FloorToInt(Mathf.Log(Mathf.Max(m_Settings.inputTexture.width, m_Settings.inputTexture.height), 2)) + 1;
        RenderTexture renderTexture = new RenderTexture(m_Settings.inputTexture.width, m_Settings.inputTexture.height, 0)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
            enableRandomWrite = true,
            useMipMap = true,
            autoGenerateMips = true
        };
        Graphics.Blit(m_Settings.inputTexture, renderTexture);
        RenderTexture.active = renderTexture;
        Texture2D textureWithMipMap = new Texture2D(m_Settings.inputTexture.width, m_Settings.inputTexture.height, TextureFormat.RGB24, true);
        textureWithMipMap.ReadPixels(new Rect(0, 0, m_Settings.inputTexture.width, m_Settings.inputTexture.height), 0, 0);
        textureWithMipMap.Apply();
        RenderTexture.active = null;
        renderTexture.Release();
        Object.DestroyImmediate(renderTexture);
        
        for (int i = 0; i < maxMipLeve; i++)
        {
            int mipWidth = Mathf.Max(1, m_Settings.inputTexture.width >> i);
            int mipHeight = Mathf.Max(1, m_Settings.inputTexture.height >> i);

            Texture2D mipTexture = new Texture2D(mipWidth, mipHeight, TextureFormat.RGBA32, false);
            mipTexture.SetPixels(textureWithMipMap.GetPixels(i));
            mipTexture.Apply();
            
            byte[] bytes = mipTexture.EncodeToPNG();
            string imgFilePath = $"{Application.dataPath}/OutputMipMap/UnityOrigionMipMaps/{m_Settings.inputTexture.name}_origionLevel_{i}.png";
            File.WriteAllBytes(imgFilePath, bytes);
            Debug.Log($"{m_Settings.inputTexture.name} Mipmap level {i} saved to: {imgFilePath}");
        }
    }
}
