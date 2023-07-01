#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections;
using System.Linq;

public class Lunarstools : EditorWindow
{
    private string buildLogPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Unity/Editor/Editor.log");
    private List<BuildInfo> buildInfoList;
    private List<string> uncompressedList;
    private string totalSize;
    private Vector2 scrollPos;
    private char[] delimiterChars = { ' ', '\t' };
    private float win;
    private float w1;
    private float w2;
    private float w3;
    private bool buildLogFound;

    private class BuildInfo
    {
        public string Size;
        public string Percent;
        public string Path;
    }

    [MenuItem("Window/LunarsTools/MainMenu")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(Lunarstools));
    }

    void OnGUI()
    {
        win = (float)(position.width * 0.6);
        w1 = (float)(win * 0.15);
        w2 = (float)(win * 0.15);
        w3 = (float)(win * 0.35);

        EditorGUILayout.LabelField("Build Size Viewer", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Create a build of your world/avatar and click the button!", EditorStyles.label);

        if (GUILayout.Button("Read Build Log"))
        {
            buildLogFound = false;
            buildLogFound = GetBuildSize();
        }

        if (buildLogFound)
        {
            if (uncompressedList != null && uncompressedList.Count != 0)
            {
                EditorGUILayout.LabelField("Total Compressed Build Size: " + totalSize);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Separator();
                EditorGUILayout.EndHorizontal();

                foreach (string s in uncompressedList)
                {
                    EditorGUILayout.LabelField(s);
                }
            }

            if (buildInfoList != null && buildInfoList.Count != 0)
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Separator();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Size%", GUILayout.Width(w1));
                EditorGUILayout.LabelField("Size", GUILayout.Width(w2));
                EditorGUILayout.LabelField("Path", GUILayout.Width(w3));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Separator();
                EditorGUILayout.EndHorizontal();

                foreach (BuildInfo buildInfo in buildInfoList)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(buildInfo.Percent, GUILayout.Width(w1));
                    EditorGUILayout.LabelField(buildInfo.Size, GUILayout.Width(w2));
                    EditorGUILayout.LabelField(buildInfo.Path);
                    if (buildInfo.Path != "Resources/unity_builtin_extra")
                    {
                        if (GUILayout.Button("Go", GUILayout.Width(w1)))
                        {
                            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(buildInfo.Path, typeof(UnityEngine.Object));
                            Selection.activeObject = obj;
                            EditorGUIUtility.PingObject(obj);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Compress All Textures"))
            {
                OpenCompressionWindow();
            }
			
			if (GUILayout.Button("Optimize Meshes (Build First)"))
            {
                OptimizeMeshes();
            }
        }
		
		
    }

private void OptimizeMeshes()
{
    GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

    // Create a dictionary to store material-texture pairs
    Dictionary<Material, Texture2D> materialTextureDict = new Dictionary<Material, Texture2D>();

    foreach (GameObject obj in allObjects)
    {
        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();

        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;

            if (mesh != null)
            {
                // Perform vertex optimization
                mesh.Optimize();

                // Perform triangle optimization
                int[] triangles = mesh.triangles;
                mesh.triangles = triangles;

                // Perform submesh optimization
                int submeshCount = mesh.subMeshCount;
                for (int i = 0; i < submeshCount; i++)
                {
                    int[] submeshTriangles = mesh.GetTriangles(i);
                    mesh.SetTriangles(submeshTriangles, i);
                }

                // Perform vertex cache optimization
                mesh.UploadMeshData(true);

                // Perform readability and memory optimization
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();

                // Check if the material has already been processed
                Material[] materials = meshFilter.GetComponent<Renderer>().sharedMaterials;
                foreach (Material material in materials)
                {
                    if (!materialTextureDict.ContainsKey(material))
                    {
                        // Create a texture atlas for the material
                        Texture2D atlasTexture = CreateTextureAtlas(material, meshFilter);
                        materialTextureDict.Add(material, atlasTexture);
                    }
                }

                // Apply the texture atlas to the materials
                ApplyTextureAtlas(meshFilter, materialTextureDict);
            }
        }
    }

    Debug.Log("Mesh optimization complete!");
}

private Texture2D CreateTextureAtlas(Material material, MeshFilter meshFilter)
{
    // Get the main texture used by the material
    Texture texture = material.GetTexture("_MainTex");

    // Convert the texture to Texture2D
    Texture2D texture2D = ConvertToTexture2D(texture);

    // Calculate the size of the texture atlas based on the texture
    int atlasWidth = texture2D.width;
    int atlasHeight = texture2D.height;

    // Create a new texture atlas
    Texture2D atlasTexture = new Texture2D(atlasWidth, atlasHeight);

    // Pack the texture into the atlas
    Rect[] uvs = atlasTexture.PackTextures(new Texture2D[] { texture2D }, 0);

    // Assign the texture atlas to the material
    material.mainTexture = atlasTexture;

    // Update the UVs of the mesh to use the packed texture coordinates
    Mesh mesh = meshFilter.sharedMesh;
    Vector2[] meshUVs = new Vector2[uvs.Length * 4];
    for (int i = 0; i < uvs.Length; i++)
    {
        Rect uv = uvs[i];
        meshUVs[i * 4 + 0] = new Vector2(uv.xMin, uv.yMin);
        meshUVs[i * 4 + 1] = new Vector2(uv.xMin, uv.yMax);
        meshUVs[i * 4 + 2] = new Vector2(uv.xMax, uv.yMax);
        meshUVs[i * 4 + 3] = new Vector2(uv.xMax, uv.yMin);
    }
    mesh.uv = meshUVs;

    // Return the created atlas texture
    return atlasTexture;
}

private Texture2D ConvertToTexture2D(Texture texture)
{
    Texture2D texture2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
    RenderTexture previous = RenderTexture.active;
    RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
    Graphics.Blit(texture, temporary);
    RenderTexture.active = temporary;
    texture2D.ReadPixels(new Rect(0, 0, temporary.width, temporary.height), 0, 0);
    texture2D.Apply();
    RenderTexture.active = previous;
    RenderTexture.ReleaseTemporary(temporary);
    return texture2D;
}

private void ApplyTextureAtlas(MeshFilter meshFilter, Dictionary<Material, Texture2D> materialTextureDict)
{
    // Get the materials used by the mesh
    Material[] materials = meshFilter.GetComponent<Renderer>().sharedMaterials;

    // Assign the texture atlas to the materials
    for (int i = 0; i < materials.Length; i++)
    {
        Material material = materials[i];
        if (materialTextureDict.ContainsKey(material))
        {
            material.mainTexture = materialTextureDict[material];
        }
    }
}


	private void OpenCompressionWindow()
	{
		TextureCruncher window = EditorWindow.GetWindow<TextureCruncher>("Texture Compression");
			
	}

    private bool GetBuildSize()
    {
        //Read the text from log
        string buildLogCopyPath = buildLogPath + ".copy";
        FileUtil.ReplaceFile(buildLogPath, buildLogCopyPath);
        StreamReader reader = new StreamReader(buildLogCopyPath);

        if (reader == null)
        {
            Debug.LogWarning("Could not read build file.");
            FileUtil.DeleteFileOrDirectory(buildLogCopyPath);
            return false;
        }

        string line = reader.ReadLine();
        while (line != null)
        {
            if ((line.Contains("scene-") && line.Contains(".vrcw")) || (line.Contains("avtr") && line.Contains(".prefab.unity3d")))
            {
                buildInfoList = new List<BuildInfo>();
                uncompressedList = new List<string>();
                line = reader.ReadLine();

                while (!line.Contains("Compressed Size"))
                {
                    line = reader.ReadLine();
                }

                totalSize = line.Split(':')[1];
                line = reader.ReadLine();

                while (line != "Used Assets and files from the Resources folder, sorted by uncompressed size:")
                {
                    uncompressedList.Add(line);
                    line = reader.ReadLine();
                }

                line = reader.ReadLine();

                while (line != "-------------------------------------------------------------------------------")
                {
                    string[] splitLine = line.Split(delimiterChars);
                    BuildInfo temp = new BuildInfo();
                    temp.Size = splitLine[1] + splitLine[2];
                    temp.Percent = splitLine[4];
                    temp.Path = splitLine[5];

                    for (int i = 6; i < splitLine.Length; i++)
                    {
                        temp.Path += (" " + splitLine[i]);
                    }

                    buildInfoList.Add(temp);
                    line = reader.ReadLine();
                }
            }

            line = reader.ReadLine();
        }

        FileUtil.DeleteFileOrDirectory(buildLogCopyPath);
        reader.Close();
        return true;
    }
	
	

}

public class TextureCruncher : EditorWindow
{
    #region Variables

    int compressionQuality = 75;
    int processingSpeed = 10;
    int resolutionIndex = 0;

    IEnumerator jobRoutine;
    IEnumerator messageRoutine;

    float progressCount = 0f;
    float totalCount = 1f;

    #endregion

    #region Properties

    float NormalizedProgress
    {
        get { return progressCount / totalCount; }
    }

    float Progress
    {
        get { return progressCount / totalCount * 100f; }
    }

    string FormattedProgress
    {
        get { return Progress.ToString("0.00") + "%"; }
    }

    #endregion

    #region Script Lifecycle

    [MenuItem("Window/Lunar's Texture Cruncher")]
    static void Init()
    {
        var window = (TextureCruncher)EditorWindow.GetWindow(typeof(TextureCruncher));
        window.Show();
    }

    void Update()
    {
        Repaint();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Texture Cruncher", EditorStyles.boldLabel);

        compressionQuality = EditorGUILayout.IntSlider("Compression quality:", compressionQuality, 0, 100);
        processingSpeed = EditorGUILayout.IntSlider("Processing speed:", processingSpeed, 1, 20);

        string[] resolutionOptions = { "No Change", "256x256", "512x512", "1024x1024", "2048x2048", "4096x4096", "8192x8192" };
        int selectedResolutionIndex = Mathf.Clamp(resolutionIndex, 0, resolutionOptions.Length - 1);
        selectedResolutionIndex = EditorGUILayout.Popup("Texture Resolution:", selectedResolutionIndex, resolutionOptions);
        resolutionIndex = selectedResolutionIndex;

        string buttonLabel = jobRoutine != null ? "Cancel" : "Begin";
        if (GUILayout.Button(buttonLabel))
        {
            if (jobRoutine != null)
            {
                messageRoutine = DisplayMessage("Cancelled. " + FormattedProgress + " complete!", 4f);
                jobRoutine = null;
            }
            else
            {
                jobRoutine = CrunchTextures();
            }
        }

        if (jobRoutine != null)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PrefixLabel(FormattedProgress);

            var rect = EditorGUILayout.GetControlRect();
            rect.width = rect.width * NormalizedProgress;
            GUI.Box(rect, GUIContent.none);

            EditorGUILayout.EndHorizontal();
        }
        else if (!string.IsNullOrEmpty(_message))
        {
            EditorGUILayout.HelpBox(_message, MessageType.None);
        }
    }

    void OnEnable()
    {
        EditorApplication.update += HandleCallbackFunction;
    }

    void OnDisable()
    {
        EditorApplication.update -= HandleCallbackFunction;
        EditorUtility.ClearProgressBar();
    }

    #endregion

    void HandleCallbackFunction()
    {
        if (jobRoutine != null && !jobRoutine.MoveNext())
        {
            EditorUtility.ClearProgressBar();
            jobRoutine = null;
        }

        if (messageRoutine != null && !messageRoutine.MoveNext())
        {
            messageRoutine = null;
        }
    }

    #region Logic

    string _message = null;

    IEnumerator DisplayMessage(string message, float duration = 0f)
    {
        if (duration <= 0f || string.IsNullOrEmpty(message))
            goto Exit;

        _message = message;

        while (duration > 0)
        {
            duration -= 0.01667f;
            yield return null;
        }

    Exit:
        _message = string.Empty;
    }

    IEnumerator CrunchTextures()
    {
        DisplayMessage(string.Empty);

        var assets = AssetDatabase.FindAssets("t:texture", null).Select(o => AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(o)) as TextureImporter);
        var eligibleAssets = assets.Where(o => o != null).Where(o => o.compressionQuality != compressionQuality || !o.crunchedCompression);

        totalCount = (float)eligibleAssets.Count();
        progressCount = 0f;

        int quality = compressionQuality;
        int limiter = processingSpeed;
        foreach (var textureImporter in eligibleAssets)
        {
            progressCount += 1f;

            textureImporter.compressionQuality = quality;
            textureImporter.crunchedCompression = true;

            // Set texture resolution based on selected index
            switch (resolutionIndex)
            {
                case 0:
						// No Change
					break;
				case 1:
					textureImporter.maxTextureSize = 256;
					break;
				case 2:
					textureImporter.maxTextureSize = 512;
					break;
				case 3:
					textureImporter.maxTextureSize = 1024;
					break;
				case 4:
					textureImporter.maxTextureSize = 2048;
					break;
				case 5:
					textureImporter.maxTextureSize = 4096;
					break;
				case 6:
					textureImporter.maxTextureSize = 8192;
					break;
				default:
					textureImporter.maxTextureSize = 1024; // Default resolution
					break;
            }

            AssetDatabase.ImportAsset(textureImporter.assetPath);

            limiter -= 1;
            if (limiter <= 0)
            {
                yield return null;
                limiter = processingSpeed;
            }
        }

        messageRoutine = DisplayMessage("Crunching complete!", 6f);
        jobRoutine = null;
    }

    #endregion
}


#endif
