using UnityEngine;

public class SampleConputeShader : MonoBehaviour
{
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int Result  = Shader.PropertyToID("Result");

    //Compute Shader文件引用
    public ComputeShader computeShader;

    //线程ID
    private int _kernelCsMain;
    void Start()
    {
        //通过名字获取线程ID
        _kernelCsMain = computeShader.FindKernel("CSMain");
        
        //创建并获取贴图
        RenderTexture csMainTexture = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGBFloat);
        csMainTexture.enableRandomWrite = true;
        csMainTexture.Create();

        computeShader.SetTexture(_kernelCsMain, Result, csMainTexture);
        computeShader.Dispatch(_kernelCsMain, 32, 32, 1);
        gameObject.GetComponent<Renderer>().sharedMaterial.SetTexture(MainTex, csMainTexture);
    }
}
