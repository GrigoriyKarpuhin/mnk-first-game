using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Автоматические настройки импорта для пиксель-арта в Resources/Sprites:
/// Point-фильтр, без компрессии и мипмапов, PPU = ширина текстуры, чтобы
/// ширина спрайта была ровно 1 юнит (существующий код масштабирует от этого).
/// </summary>
public class PixelArtSpriteImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("Resources/Sprites/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = ReadPngWidth(path);
    }

    /// <summary>Ширина PNG из заголовка (байты 16-19, big-endian).</summary>
    private static int ReadPngWidth(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[24];
            if (stream.Read(header, 0, 24) < 24) return 32;
            return (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
        }
        catch
        {
            return 32;
        }
    }
}
