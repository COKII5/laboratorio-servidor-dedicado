using System.IO;
using System.Linq;
using Lab.Common;
using Lab.Part1;
using Lab.Part2;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lab.EditorTools
{
    /// <summary>
    /// Menú "Laboratorio": configura el proyecto, genera las escenas y compila cada parte.
    /// También se puede llamar sin abrir el editor:
    /// Unity.exe -batchmode -quit -projectPath unity-client -executeMethod Lab.EditorTools.LabSetup.SetupProject
    /// </summary>
    public static class LabSetup
    {
        const string Part1ScenePath = "Assets/Scenes/Parte1_Estado.unity";
        const string Part2ScenePath = "Assets/Scenes/Parte2_Eventos.unity";
        const string Part1BuildPath = "Builds/Parte1/ServidorDedicado.exe";
        const string Part2BuildPath = "Builds/Parte2/ServidorDedicado.exe";

        const int CheckpointCount = 6;
        const float TrackRadiusX = 11f;
        const float TrackRadiusZ = 6f;
        const float CarSpeed = 7f;
        static readonly Vector3 CarScale = new Vector3(1f, 0.6f, 1f);

        [MenuItem("Laboratorio/Configurar proyecto y regenerar escena Parte 1")]
        public static void SetupProject()
        {
            if (!ConfirmOverwrite(Part1ScenePath)) return;
            ConfigurePlayerSettings();
            CreatePart1Scene();
        }

        [MenuItem("Laboratorio/Regenerar escena Parte 2")]
        public static void SetupPart2()
        {
            if (!ConfirmOverwrite(Part2ScenePath)) return;
            CreatePart2Scene();
        }

        [MenuItem("Laboratorio/Compilar Parte 1 para Windows")]
        public static void BuildPart1() => Build(Part1ScenePath, Part1BuildPath);

        [MenuItem("Laboratorio/Compilar Parte 2 para Windows")]
        public static void BuildPart2() => Build(Part2ScenePath, Part2BuildPath);

        static bool ConfirmOverwrite(string scenePath) =>
            Application.isBatchMode || !File.Exists(scenePath) ||
            EditorUtility.DisplayDialog("Regenerar escena", $"Se reemplazará {scenePath} y se perderán los cambios hechos a mano.", "Reemplazar", "Cancelar");

        // Cada parte se compila por separado para que el ejecutable arranque directo en su escena.
        static void Build(string scenePath, string buildPath)
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            bool succeeded = report.summary.result == BuildResult.Succeeded;
            if (succeeded)
                Debug.Log($"[Laboratorio] Compilación lista en {Path.GetFullPath(buildPath)}");
            else
                Debug.LogError($"[Laboratorio] La compilación falló: {report.summary.result}");

            if (Application.isBatchMode && !succeeded) EditorApplication.Exit(1);
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Laboratorio";
            PlayerSettings.productName = "ServidorDedicado";
            // El servicio de referencia usa HTTP sin cifrar, que Unity bloquea por defecto.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            // La instancia que no tiene el foco debe seguir sondeando.
            PlayerSettings.runInBackground = true;
            // Ventanas pequeñas para poner las dos instancias lado a lado.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 540;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
        }

        static void CreatePart1Scene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Vista amplia: los jugadores empiezan cerca de x = 0 y la ráfaga del experimento B llega a x = 20.
            PlaceCamera(new Vector3(9f, 10f, -12f), new Vector3(9f, 0f, 1f));
            CreatePrimitive(PrimitiveType.Plane, "Suelo", new Vector3(9f, 0f, 0f), new Vector3(3.2f, 1f, 1.4f));

            var local = CreatePrimitive(PrimitiveType.Cube, "JugadorLocal", new Vector3(-2f, 0.5f, 0f), Vector3.one);
            local.AddComponent<LocalPlayerMover>();
            var remote = CreatePrimitive(PrimitiveType.Cube, "JugadorRemoto", new Vector3(2f, 0.5f, 0f), Vector3.one);

            var network = new GameObject("Red");
            var client = network.AddComponent<PositionSyncClient>();
            network.AddComponent<Part1Controls>();  // RequireComponent agrega los dos experimentos
            SetReferences(client, ("localPlayer", local.transform), ("remotePlayer", remote.transform));

            SaveScene(scene, Part1ScenePath);
        }

        static void CreatePart2Scene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            PlaceCamera(new Vector3(0f, 18f, -15f), new Vector3(0f, 0f, -1f));
            CreatePrimitive(PrimitiveType.Plane, "Suelo", Vector3.zero, new Vector3(3f, 1f, 2f));
            Track track = CreateTrack();

            // Los dos autos empiezan en la meta, uno a cada lado.
            var local = CreatePrimitive(PrimitiveType.Cube, "AutoLocal", new Vector3(-1.2f, 0.3f, -6f), CarScale);
            SetFloat(local.AddComponent<LocalPlayerMover>(), "speed", CarSpeed);
            var race = local.AddComponent<RaceController>();

            var remote = CreatePrimitive(PrimitiveType.Cube, "AutoRemoto", new Vector3(1.2f, 0.3f, -6f), CarScale);
            var remoteRacer = remote.AddComponent<RemoteRacerView>();

            var network = new GameObject("Red");
            var client = network.AddComponent<EventSyncClient>();
            var controls = network.AddComponent<Part2Controls>();  // RequireComponent agrega el experimento C

            SetReferences(race, ("client", client), ("track", track));
            SetReferences(remoteRacer, ("client", client), ("track", track));
            SetReferences(controls, ("race", race), ("remoteRacer", remoteRacer));

            SaveScene(scene, Part2ScenePath);
        }

        // Checkpoints sobre una elipse, en sentido antihorario desde la meta (abajo al centro).
        static Track CreateTrack()
        {
            var trackRoot = new GameObject("Pista");
            var track = trackRoot.AddComponent<Track>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var serializedTrack = new SerializedObject(track);
            SerializedProperty checkpointsProperty = serializedTrack.FindProperty("checkpoints");
            checkpointsProperty.arraySize = CheckpointCount;

            for (int i = 0; i < CheckpointCount; i++)
            {
                float angle = (-90f + i * 360f / CheckpointCount) * Mathf.Deg2Rad;
                var position = new Vector3(TrackRadiusX * Mathf.Cos(angle), 0.03f, TrackRadiusZ * Mathf.Sin(angle));
                bool isFinishLine = i == 0;

                var checkpoint = CreatePrimitive(PrimitiveType.Cylinder, isFinishLine ? "Checkpoint0_Meta" : $"Checkpoint{i}", position, new Vector3(3f, 0.03f, 3f));
                checkpoint.transform.SetParent(trackRoot.transform);
                checkpointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = checkpoint.transform;

                CreateLabel(trackRoot.transform, $"Etiqueta{i}", isFinishLine ? "META" : i.ToString(), position + new Vector3(0f, 1.2f, 0f), font);
            }

            serializedTrack.ApplyModifiedPropertiesWithoutUndo();
            return track;
        }

        static void CreateLabel(Transform parent, string name, string caption, Vector3 position, Font font)
        {
            var label = new GameObject(name);
            label.transform.SetParent(parent);
            label.transform.SetPositionAndRotation(position, Quaternion.Euler(50f, 0f, 0f));

            var text = label.AddComponent<TextMesh>();
            text.text = caption;
            text.font = font;
            text.fontSize = 48;
            text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        static void PlaceCamera(Vector3 position, Vector3 lookAt)
        {
            Transform camera = Camera.main.transform;
            camera.position = position;
            camera.LookAt(lookAt);
        }

        static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position, Vector3 scale)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.position = position;
            primitive.transform.localScale = scale;
            return primitive;
        }

        // Los campos [SerializeField] son privados; desde el editor se asignan con SerializedObject.
        static void SetReferences(Object target, params (string field, Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach ((string field, Object value) in references)
                serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SaveScene(Scene scene, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { Part1ScenePath, Part2ScenePath }
                .Where(File.Exists)
                .Select(scenePath => new EditorBuildSettingsScene(scenePath, true))
                .ToArray();
            Debug.Log($"[Laboratorio] Escena generada en {path}");
        }
    }
}
