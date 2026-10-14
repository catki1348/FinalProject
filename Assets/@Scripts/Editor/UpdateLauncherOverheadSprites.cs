using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// AI 작업: OpenAI Codex (GPT-6), 2026-10-14.
// 발사장치의 기존 Sprite GUID/폭을 유지하여 프리팹과 씬의 연결·배치를 보존한다.
public static class UpdateLauncherOverheadSprites
{
    public const string SheetPath = "Assets/@ExternalAssets/Traps/LauncherOverhead_20261014/T_Launchers_Overhead_Final.png";
    static readonly string[] Themes = { "Stage1_Forest", "Stage2_Jungle", "Stage3_Lava", "Stage4_DemonCave", "Stage5_DemonCastle" };

    // 생성본의 4행 중 숲/정글/용암/동굴/마왕성 5개 셀만 사용한다. 추가 변형 2개는 사용하지 않는다.
    public static string Apply()
    {
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        if (!importer) throw new Exception("Launcher sheet is missing");
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        var pixels = texture.GetPixels32();
        var cells = new[] {
            new Rect(0, .75f, .5f, .25f), new Rect(0, .5f, .5f, .25f),
            new Rect(0, .25f, .5f, .25f), new Rect(.5f, .25f, .5f, .25f),
            new Rect(0, 0, 1, .25f)
        };
        var rects = new Rect[5];
        var sprites = new Sprite[5];
        // 모든 영역을 먼저 검사하여 잘못된 시트로 일부 테마만 바뀌는 것을 방지한다.
        for (int row = 0; row < 5; row++)
        {
            int bottom = Mathf.RoundToInt(cells[row].yMin * texture.height);
            int top = Mathf.RoundToInt(cells[row].yMax * texture.height);
            int left = Mathf.RoundToInt(cells[row].xMin * texture.width);
            int right = Mathf.RoundToInt(cells[row].xMax * texture.width);
            int minX = texture.width, minY = top, maxX = -1, maxY = -1;
            for (int y = bottom; y < top; y++)
            for (int x = left; x < right; x++)
            {
                Color32 color = pixels[y * texture.width + x];
                if (color.a < 16 || Mathf.Min(color.r, Mathf.Min(color.g, color.b)) > 247) continue;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            if (maxX < minX || maxY < minY) throw new Exception(Themes[row] + ": empty image row");
            if (minY <= bottom || maxY >= top - 1) throw new Exception(Themes[row] + ": image crosses row boundary");
            rects[row] = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            string path = "Assets/Sprites/Traps/Themed/T_Trap_Launcher_" + Themes[row] + ".asset";
            sprites[row] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprites[row]) throw new Exception("Existing sprite missing: " + path);
        }
        var report = new List<string>();
        for (int row = 0; row < 5; row++)
        {
            var sprite = sprites[row];
            string path = AssetDatabase.GetAssetPath(sprite);
            string guid = AssetDatabase.AssetPathToGUID(path);
            float width = sprite.bounds.size.x;
            // 새 스프라이트를 기존 자산에 복사하므로 fileID와 모든 참조가 그대로 유지된다.
            var replacement = Sprite.Create(texture, rects[row], new Vector2(.5f, .5f), rects[row].width / width, 0, SpriteMeshType.FullRect);
            replacement.name = sprite.name;
            EditorUtility.CopySerialized(replacement, sprite);
            UnityEngine.Object.DestroyImmediate(replacement);
            EditorUtility.SetDirty(sprite);
            AssetDatabase.SaveAssetIfDirty(sprite);
            if (AssetDatabase.AssetPathToGUID(path) != guid || Mathf.Abs(sprite.bounds.size.x - width) > .001f || sprite.texture != texture)
                throw new Exception(Themes[row] + ": sprite identity/width validation failed");
            report.Add(Themes[row] + ": " + rects[row] + ", width preserved=" + width + ", GUID=" + guid);
        }
        return string.Join("\n", report);
    }
}
