# MipMapGenerator
 Use compute shader to generate mimaps 

## 



## 遇到问题

1. 使用ComputeShader采样出来的纹理，会导致颜色变暗。初步断定为 RenderTexture 和 Texture 转换的格式有误。RenderTexture 使用的是 HDR 存储格式，而 Texture2D 使用普通存储。
2. Compute Shader 的算法问题。不理解为何将整个画面一份为四，是错误的颜色，理论上不应该超出判断条件。
3. 普通采样颜色颜色显示比原来黑。需要在ComputeShader中添加 gamma 矫正才能进行转换。
4. 
