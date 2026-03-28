using UnityEngine;
using UnityEditor;
using System;

namespace NoiseGenerator
{
    public class NoiseGeneratorWindow : EditorWindow
    {
        // Image Settings
        private int textureWidth = 256;
        private int textureHeight = 256;
        private bool uncompressed = true;
        private bool generateMipMaps = false;

        // Noise Settings
        private NoiseType noiseType = NoiseType.Perlin;
        private float scale = 25f;
        private int octaves = 4;
        private float persistence = 0.5f;
        private float lacunarity = 2f;
        private int seed = 0;
        private Vector2 offset = Vector2.zero;
        
        // Voronoi Settings
        private VoronoiType voronoiType = VoronoiType.F1;
        private float voronoiScale = 1f;
        
        // FBM Settings
        private bool useFBM = true;
        
        // Output Settings
        private string outputFolder = "Assets/NoiseGenerator/Output";
        private string fileName = "NoiseTexture";

        private Texture2D previewTexture;

        [MenuItem("Tools/Noise Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<NoiseGeneratorWindow>("Noise Generator");
            window.minSize = new Vector2(400, 600);
            window.maxSize = new Vector2(600, 900);
        }

        void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Noise Generator - Mathematical Texture Creator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawImageSettings();
            EditorGUILayout.Space();
            DrawNoiseSettings();
            EditorGUILayout.Space();
            DrawAdvancedSettings();
            EditorGUILayout.Space();
            DrawOutputSettings();
            EditorGUILayout.Space();
            DrawPreview();
            EditorGUILayout.Space();
            DrawGenerateButton();
        }

        private void DrawImageSettings()
        {
            EditorGUILayout.LabelField("Image Settings", EditorStyles.boldLabel);
            
            textureWidth = EditorGUILayout.IntSlider("Width", textureWidth, 32, 2048);
            textureHeight = EditorGUILayout.IntSlider("Height", textureHeight, 32, 2048);
            uncompressed = EditorGUILayout.Toggle("Uncompressed (TrueColor)", uncompressed);
            generateMipMaps = EditorGUILayout.Toggle("Generate MipMaps", generateMipMaps);

            if (GUILayout.Button("Set Square (256)"))
                textureWidth = textureHeight = 256;
            if (GUILayout.Button("Set Square (512)"))
                textureWidth = textureHeight = 512;
            if (GUILayout.Button("Set Square (1024)"))
                textureWidth = textureHeight = 1024;
        }

        private void DrawNoiseSettings()
        {
            EditorGUILayout.LabelField("Noise Settings", EditorStyles.boldLabel);
            
            noiseType = (NoiseType)EditorGUILayout.EnumPopup("Noise Type", noiseType);
            scale = EditorGUILayout.Slider("Scale", scale, 1f, 100f);
            
            EditorGUILayout.Space();
            
            // Offset controls
            EditorGUILayout.BeginHorizontal();
            offset.x = EditorGUILayout.FloatField("Offset X", offset.x);
            offset.y = EditorGUILayout.FloatField("Offset Y", offset.y);
            if (GUILayout.Button("Random", GUILayout.Width(70)))
            {
                offset = new Vector2(UnityEngine.Random.Range(-1000f, 1000f), UnityEngine.Random.Range(-1000f, 1000f));
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAdvancedSettings()
        {
            EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);
            
            useFBM = EditorGUILayout.Toggle("Use FBM (Fractal)", useFBM);
            
            if (useFBM)
            {
                octaves = EditorGUILayout.IntSlider("Octaves", octaves, 1, 10);
                persistence = EditorGUILayout.Slider("Persistence", persistence, 0.1f, 1f);
                lacunarity = EditorGUILayout.Slider("Lacunarity", lacunarity, 1f, 4f);
            }
            
            EditorGUILayout.Space();
            
            // Voronoi specific settings
            if (noiseType == NoiseType.Voronoi)
            {
                EditorGUILayout.LabelField("Voronoi Settings", EditorStyles.boldLabel);
                voronoiType = (VoronoiType)EditorGUILayout.EnumPopup("Voronoi Type", voronoiType);
                voronoiScale = EditorGUILayout.Slider("Cell Scale", voronoiScale, 0.1f, 10f);
            }
            
            EditorGUILayout.Space();
            
            seed = EditorGUILayout.IntField("Seed (0 = Random)", seed);
            if (GUILayout.Button("Random Seed"))
                seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        private void DrawOutputSettings()
        {
            EditorGUILayout.LabelField("Output Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string selectedFolder = EditorUtility.SaveFolderPanel("Select Output Folder", "Assets", "NoiseOutput");
                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    outputFolder = "Assets" + selectedFolder.Replace(Application.dataPath, "");
                }
            }
            EditorGUILayout.EndHorizontal();
            
            fileName = EditorGUILayout.TextField("File Name", fileName);
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            
            if (previewTexture == null)
            {
                if (GUILayout.Button("Generate Preview", GUILayout.Height(200)))
                {
                    GeneratePreview();
                }
            }
            else
            {
                GUILayout.Box(previewTexture, GUILayout.Height(200), GUILayout.ExpandWidth(true));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh Preview"))
                {
                    GeneratePreview();
                }
                if (GUILayout.Button("Clear Preview"))
                {
                    if (previewTexture != null)
                    {
                        DestroyImmediate(previewTexture);
                        previewTexture = null;
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawGenerateButton()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Generate & Save", GUILayout.Width(200), GUILayout.Height(40)))
            {
                GenerateAndSave();
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void GeneratePreview()
        {
            if (previewTexture != null)
            {
                DestroyImmediate(previewTexture);
            }
            
            int previewSize = 256;
            previewTexture = GenerateNoiseTexture(previewSize, previewSize, false);
        }

        private void GenerateAndSave()
        {
            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                string[] folders = outputFolder.Split('/');
                string currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    if (!AssetDatabase.IsValidFolder(currentPath + "/" + folders[i]))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath += "/" + folders[i];
                }
            }

            Texture2D texture = GenerateNoiseTexture(textureWidth, textureHeight, true);
            
            if (texture != null)
            {
                string fullPath = outputFolder + "/" + fileName + ".png";
                
                // Save as PNG
                byte[] pngData = texture.EncodeToPNG();
                System.IO.File.WriteAllBytes(fullPath, pngData);
                
                // Import the texture
                AssetDatabase.Refresh();
                
                // Configure import settings
                string assetPath = "Assets" + fullPath.Replace(Application.dataPath, "").Replace("\\", "/");
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.mipmapEnabled = generateMipMaps;
                    importer.textureCompression = uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    AssetDatabase.ImportAsset(assetPath);
                }
                
                EditorUtility.DisplayDialog("Success", 
                    "Texture saved to:\n" + fullPath, "OK");
                
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                
                DestroyImmediate(texture);
            }
        }

        private Texture2D GenerateNoiseTexture(int width, int height, bool saveAlpha)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, generateMipMaps);
            
            int actualSeed = seed == 0 ? Environment.TickCount : seed;
            
            float[,] noiseValues = new float[width, height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float noiseValue = CalculateNoise(x, y, width, height, actualSeed);
                    noiseValue = Mathf.Clamp01(noiseValue);
                    noiseValues[x, y] = noiseValue;
                }
            }
            
            // Normalize if needed
            NormalizeNoiseValues(noiseValues, width, height);
            
            Color[] colors = new Color[width * height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = noiseValues[x, y];
                    
                    if (noiseType == NoiseType.Voronoi && voronoiType == VoronoiType.Distance)
                    {
                        // Distance mode - grayscale
                        colors[y * width + x] = new Color(value, value, value, 1f);
                    }
                    else if (noiseType == NoiseType.Voronoi && voronoiType == VoronoiType.F1MinusF2)
                    {
                        // Can show negative values in alpha
                        float normalizedValue = (value + 1f) * 0.5f;
                        colors[y * width + x] = new Color(normalizedValue, normalizedValue, normalizedValue, 1f);
                    }
                    else if (saveAlpha)
                    {
                        // Grayscale with alpha channel
                        float colorValue = noiseValues[x, y];
                        colors[y * width + x] = new Color(colorValue, colorValue, colorValue, 1f);
                    }
                    else
                    {
                        // Regular grayscale
                        float colorValue = noiseValues[x, y];
                        colors[y * width + x] = new Color(colorValue, colorValue, colorValue);
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            return texture;
        }

        private void NormalizeNoiseValues(float[,] values, int width, int height)
        {
            if (noiseType == NoiseType.Voronoi)
            {
                if (voronoiType == VoronoiType.F1MinusF2)
                {
                    // Find min and max for F1-F2
                    float min = float.MaxValue;
                    float max = float.MinValue;
                    
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            if (values[x, y] < min) min = values[x, y];
                            if (values[x, y] > max) max = values[x, y];
                        }
                    }
                    
                    float range = max - min;
                    if (range > 0)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                values[x, y] = (values[x, y] - min) / range;
                            }
                        }
                    }
                }
            }
        }

        private float CalculateNoise(float x, float y, int width, int height, int seed)
        {
            float result = 0f;
            
            switch (noiseType)
            {
                case NoiseType.Perlin:
                    result = CalculatePerlinNoise(x, y, seed);
                    break;
                case NoiseType.Simplex:
                    result = CalculateSimplexNoise(x, y, seed);
                    break;
                case NoiseType.Voronoi:
                    result = CalculateVoronoiNoise(x, y, seed);
                    break;
                case NoiseType.Value:
                    result = CalculateValueNoise(x, y, seed);
                    break;
                case NoiseType.Worley:
                    result = CalculateWorleyNoise(x, y, seed);
                    break;
                case NoiseType.White:
                    result = UnityEngine.Random.value;
                    break;
                case NoiseType.Billow:
                    result = CalculateBillowNoise(x, y, seed);
                    break;
                case NoiseType.Ridged:
                    result = CalculateRidgedNoise(x, y, seed);
                    break;
                case NoiseType.Cellular:
                    result = CalculateCellularNoise(x, y, seed);
                    break;
            }
            
            return result;
        }

        private float CalculatePerlinNoise(float x, float y, int seed)
        {
            if (!useFBM)
            {
                return CalculateSinglePerlin(x, y, seed);
            }
            
            float amplitude = 1f;
            float frequency = scale / 25f;
            float maxValue = 0f;
            float total = 0f;
            
            for (int i = 0; i < octaves; i++)
            {
                total += CalculateSinglePerlin(x * frequency + offset.x, y * frequency + offset.y, seed + i) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            
            return total / maxValue;
        }

        private float CalculateSinglePerlin(float x, float y, int seed)
        {
            // Use Unity's built-in Perlin noise
            float sampleX = (x + seed) / scale;
            float sampleY = (y + seed * 2) / scale;
            return Mathf.PerlinNoise(sampleX + offset.x / scale, sampleY + offset.y / scale);
        }

        private float CalculateSimplexNoise(float x, float y, int seed)
        {
            // Simplified simplex-like noise using multiple octaves
            if (!useFBM)
            {
                return CalculateSimplexCore(x, y, seed);
            }
            
            float amplitude = 1f;
            float frequency = scale / 25f;
            float maxValue = 0f;
            float total = 0f;
            
            for (int i = 0; i < octaves; i++)
            {
                total += CalculateSimplexCore(x * frequency + offset.x, y * frequency + offset.y, seed + i) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            
            return total / maxValue;
        }

        private float CalculateSimplexCore(float x, float y, int seed)
        {
            // Simple implementation combining multiple noise sources
            float n1 = Mathf.PerlinNoise((x + seed) / scale * 1.5f, (y + seed * 2) / scale * 1.5f);
            float n2 = Mathf.PerlinNoise((x + seed * 3) / scale * 0.5f, (y + seed * 4) / scale * 0.5f);
            return (n1 + n2) / 2f;
        }

        private float CalculateValueNoise(float x, float y, int seed)
        {
            if (!useFBM)
            {
                return CalculateSingleValueNoise(x, y, seed);
            }
            
            float amplitude = 1f;
            float frequency = scale / 25f;
            float maxValue = 0f;
            float total = 0f;
            
            for (int i = 0; i < octaves; i++)
            {
                total += CalculateSingleValueNoise(x * frequency + offset.x, y * frequency + offset.y, seed + i) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }
            
            return total / maxValue;
        }

        private float CalculateSingleValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;
            
            // Hash function
            Func<int, int, float> hash = (ix, iy) => 
            {
                int n = ix + iy * 57 + seed * 131;
                n = (n << 13) ^ n;
                return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
            };
            
            // Smooth interpolation
            float sx = xf * xf * (3f - 2f * xf);
            float sy = yf * yf * (3f - 2f * yf);
            
            float n00 = hash(xi, yi);
            float n10 = hash(xi + 1, yi);
            float n01 = hash(xi, yi + 1);
            float n11 = hash(xi + 1, yi + 1);
            
            float nx0 = Mathf.Lerp(n00, n10, sx);
            float nx1 = Mathf.Lerp(n01, n11, sx);
            
            return Mathf.Lerp(nx0, nx1, sy);
        }

        private float CalculateVoronoiNoise(float x, float y, int seed)
        {
            float scaledX = x * voronoiScale / 10f;
            float scaledY = y * voronoiScale / 10f;
            
            float cellSize = 1f;
            int cellX = Mathf.FloorToInt(scaledX / cellSize);
            int cellY = Mathf.FloorToInt(scaledY / cellSize);
            
            float minDist1 = float.MaxValue;
            float minDist2 = float.MaxValue;
            
            // Check surrounding cells
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int cx = cellX + dx;
                    int cy = cellY + dy;
                    
                    // Generate random point in cell
                    System.Random cellRng = new System.Random(seed * 10000 + cx * 100 + cy);
                    float px = cx + (float)cellRng.NextDouble();
                    float py = cy + (float)cellRng.NextDouble();
                    
                    float dist = Mathf.Sqrt((scaledX - px) * (scaledX - px) + (scaledY - py) * (scaledY - py));
                    
                    if (dist < minDist1)
                    {
                        minDist2 = minDist1;
                        minDist1 = dist;
                    }
                    else if (dist < minDist2)
                    {
                        minDist2 = dist;
                    }
                }
            }
            
            switch (voronoiType)
            {
                case VoronoiType.F1:
                    return minDist1;
                case VoronoiType.F2:
                    return minDist2;
                case VoronoiType.F1MinusF2:
                    return minDist1 - minDist2;
                case VoronoiType.Distance:
                    return minDist1;
                default:
                    return minDist1;
            }
        }

        private float CalculateWorleyNoise(float x, float y, int seed)
        {
            // Worley noise is similar to Voronoi
            return CalculateVoronoiNoise(x, y, seed);
        }

        private float CalculateBillowNoise(float x, float y, int seed)
        {
            float perlin = CalculateSinglePerlin(x, y, seed);
            return Mathf.Abs(perlin * 2f - 1f);
        }

        private float CalculateRidgedNoise(float x, float y, int seed)
        {
            float perlin = CalculateSinglePerlin(x, y, seed);
            return 1f - Mathf.Abs(perlin * 2f - 1f);
        }

        private float CalculateCellularNoise(float x, float y, int seed)
        {
            // Another variant of cellular/Voronoi noise
            return CalculateVoronoiNoise(x, y, seed);
        }
    }

    public enum NoiseType
    {
        Perlin,
        Simplex,
        Voronoi,
        Value,
        Worley,
        White,
        Billow,
        Ridged,
        Cellular
    }

    public enum VoronoiType
    {
        F1,          // Distance to nearest point
        F2,          // Distance to second nearest point
        F1MinusF2,   // F1 - F2 (creates edges)
        Distance     // Simple distance visualization
    }
}
