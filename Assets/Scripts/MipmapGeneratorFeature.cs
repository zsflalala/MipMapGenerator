using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class MipmapGeneratorFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public enum DownSampleStrategy
    {
        Min,
        Max,
        Avg
    }

    [System.Serializable]
    public class Settings
    {
        public Texture2D inputTexture;  // 用户指定纹理
        public DownSampleStrategy strategy = DownSampleStrategy.Avg; // 下采样策略
        public bool saveToCPU = true;   // 是否将Mipmap传回CPU并保存
        public int mipmapLevels = 0; // 构建的最大Mipmap级别，0表示构建到最粗糙的1x1
        public string outputDirectory = "OutputMipMap";
    }

    public Settings settings = new Settings();

    private MipmapRenderPass _renderPass;

    public override void Create()
    {
        _renderPass = new MipmapRenderPass(settings);
        _renderPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(_renderPass);
    }
}
