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

        foreach (GameObject obj in allObjects)
        {
            MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();

            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh mesh = meshFilter.sharedMesh;

                if (mesh != null)
                {
                    MeshUtility.Optimize(mesh);
                }
            }
        }

        Debug.Log("Mesh optimization complete!");
    }

	private void OpenCompressionWindow()
	{
		TextureCompressionWindow window = EditorWindow.GetWindow<TextureCompressionWindow>("Texture Compression");
			
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

public class TextureCompressionWindow : EditorWindow
{
    int compressionQuality = 75;
    int processingSpeed = 10;

    IEnumerator jobRoutine;
    IEnumerator messageRoutine;

    float progressCount = 0f;
    float totalCount = 1f;

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

    [MenuItem("Window/LunarsTools/Texture Compression")]
    public static void OpenWindow()
    {
        TextureCompressionWindow window = GetWindow<TextureCompressionWindow>("Texture Compression");
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Texture Compression", EditorStyles.boldLabel);

        compressionQuality = EditorGUILayout.IntSlider("Compression Quality", compressionQuality, 0, 100);
        processingSpeed = EditorGUILayout.IntSlider("Processing Speed", processingSpeed, 1, 20);

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
            Rect rect = EditorGUILayout.GetControlRect();
            rect.width = rect.width * NormalizedProgress;
            GUI.Box(rect, GUIContent.none);
            EditorGUILayout.EndHorizontal();
        }
        else if (!string.IsNullOrEmpty(message))
        {
            EditorGUILayout.HelpBox(message, MessageType.None);
        }
    }

    void Update()
    {
        Repaint();
    }

    IEnumerator DisplayMessage(string message, float duration)
    {
        if (duration <= 0f || string.IsNullOrEmpty(message))
            yield break;

        messageRoutine = null;
        message = message;

        while (duration > 0f)
        {
            duration -= Time.deltaTime;
            yield return null;
        }

        message = string.Empty;
    }

    IEnumerator CrunchTextures()
    {
        message = string.Empty;

        IEnumerable<TextureImporter> textureImporters = AssetDatabase.FindAssets("t:texture")
            .Select(guid => AssetDatabase.LoadAssetAtPath<TextureImporter>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(importer => importer != null && (importer.compressionQuality != compressionQuality || !importer.crunchedCompression));

        totalCount = textureImporters.Count();
        progressCount = 0f;

        foreach (TextureImporter importer in textureImporters)
        {
            progressCount += 1f;

            importer.compressionQuality = compressionQuality;
            importer.crunchedCompression = true;
            AssetDatabase.ImportAsset(importer.assetPath);

            if (processingSpeed > 1)
                yield return new WaitForSeconds(1f / processingSpeed);
        }

        messageRoutine = DisplayMessage("Texture compression complete!", 2f);
        jobRoutine = null;
    }

    private string message = string.Empty;

    void OnEnable()
    {
        EditorApplication.update += UpdateCallback;
    }

    void OnDisable()
    {
        EditorApplication.update -= UpdateCallback;
    }

    void UpdateCallback()
    {
        if (jobRoutine != null && !jobRoutine.MoveNext())
        {
            jobRoutine = null;
        }

        if (messageRoutine != null && !messageRoutine.MoveNext())
        {
            messageRoutine = null;
            message = string.Empty;
        }
    }
}

#endif
