namespace VSM.Editor.Importers
{
    using UnityEditor;

    /// <summary>Настраивает текстуры VSM и их сжатие для мобильных платформ.</summary>
    public class VSMMobileTextureImporter : AssetPostprocessor
    {
     /// <summary>Выбирает тип и цветовое пространство текстуры, ограничивает размер и включает ASTC.</summary>
     void OnPreprocessTexture()
     {
      if(!assetPath.StartsWith("Assets/VSM/")) return;
      var t=(TextureImporter)assetImporter;
      if(assetPath.Contains("/Textures/UI/")){t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.mipmapEnabled=false;t.alphaIsTransparency=true;t.textureCompression=TextureImporterCompression.Uncompressed;return;}
      bool normal=assetPath.Contains("Normal")||assetPath.Contains("normal")||assetPath.Contains("Image_2");
      bool linear=normal||assetPath.Contains("MetalSmooth")||assetPath.Contains("roughness")||assetPath.Contains("metallic")||assetPath.Contains("_Metal.")||assetPath.Contains("_Rough.");
      t.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
      t.sRGBTexture=!linear;t.mipmapEnabled=true;t.isReadable=false;t.anisoLevel=2;
      t.maxTextureSize=assetPath.Contains("Signage")?2048:1024;
      t.textureCompression=TextureImporterCompression.Compressed;
      foreach(var platform in new[]{"Android","iPhone"}) {
       var p=t.GetPlatformTextureSettings(platform);p.name=platform;p.overridden=true;p.maxTextureSize=t.maxTextureSize;p.format=TextureImporterFormat.ASTC_6x6;t.SetPlatformTextureSettings(p);
      }
     }
    }
}

