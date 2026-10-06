using System.IO;
using System.Linq;
using Lab.Part1;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lab.EditorTools
{
    /// <summary>
    /// Configura el proyecto y genera la escena de la Parte 1 (menú "Laboratorio").
    /// También funciona sin abrir el editor:
    /// Unity.exe -batchmode -quit -projectPath unity-client -executeMethod Lab.EditorTools.LabSetup.SetupProject
    /// </summary>
    public static class LabSetup
    {
        const string Part1ScenePath = "Assets/Scenes/Parte1_Estado.unity";
        const string WindowsBuildPath = "Builds/Windows/ServidorDedicado.exe";

        [MenuItem("Laboratorio/Configurar proyecto y regenerar escena Parte 1")]
        public static void SetupProject()
        {
            bool confirmed = Application.isBatchMode || !File.Exists(Part1ScenePath) ||
                EditorUtility.DisplayDialog("Regenerar escena", $"Se reemplazará {Part1ScenePath} y se perderán los cambios hechos a mano.", "Reemplazar", "Cancelar");
            if (!confirmed) return;

            ConfigurePlayerSettings();
            CreatePart1Scene();
        }

        [MenuItem("Laboratorio/Compilar para Windows")]
        public static void BuildWindows()
        {
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = WindowsBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });

            bool succeeded = report.summary.result == BuildResult.Succeeded;
            if (succeeded)
                Debug.Log($"[Laboratorio] Compilación lista en {Path.GetFullPath(WindowsBuildPath)}");
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
            // Con dos instancias abiertas, la que no tiene el foco debe seguir sondeando.
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
            Transform camera = Camera.main.transform;
            camera.position = new Vector3(9f, 10f, -12f);
            camera.LookAt(new Vector3(9f, 0f, 1f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Suelo";
            ground.transform.position = new Vector3(9f, 0f, 0f);
            ground.transform.localScale = new Vector3(3.2f, 1f, 1.4f);

            var local = GameObject.CreatePrimitive(PrimitiveType.Cube);
            local.name = "JugadorLocal";
            local.transform.position = new Vector3(-2f, 0.5f, 0f);
            local.AddComponent<LocalPlayerMover>();

            var remote = GameObject.CreatePrimitive(PrimitiveType.Cube);
            remote.name = "JugadorRemoto";
            remote.transform.position = new Vector3(2f, 0.5f, 0f);

            var network = new GameObject("Red");
            var client = network.AddComponent<PositionSyncClient>();
            network.AddComponent<Part1Controls>();  // RequireComponent agrega los dos experimentos

            var serializedClient = new SerializedObject(client);
            serializedClient.FindProperty("localPlayer").objectReferenceValue = local.transform;
            serializedClient.FindProperty("remotePlayer").objectReferenceValue = remote.transform;
            serializedClient.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(Part1ScenePath));
            EditorSceneManager.SaveScene(scene, Part1ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Part1ScenePath, true) };
            Debug.Log($"[Laboratorio] Escena generada en {Part1ScenePath}");
        }
    }
}
