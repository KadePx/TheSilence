using System;
using System.IO;
using Silence.Core.Run;
using Silence.Data;
using Silence.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Silence.Meta
{
    /// <summary>
    /// The running session: the current run, its content, the save file and the moves between scenes.
    /// The run is saved as JSON after every step on the map (each node, reward, event answer and purchase)
    /// and loaded again from the main menu. A battle in progress is not saved: resuming puts the fleet back
    /// at the start of that node's fight, which is built from the same seed.
    /// </summary>
    public static class Game
    {
        public const string MenuScene = "Menu", MapScene = "Map", BattleScene = "Battle";

        public static RunState Run { get; private set; }
        public static RunContent Content => ContentLoader.Load();

        /// <summary>The editor can enter Play Mode without reloading code, so statics are cleared by hand.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Run = null;
            BattleLauncher.Clear();
            ContentLoader.Invalidate();
        }

        public static string SavePath => Path.Combine(Application.persistentDataPath, "silence_run.json");

        public static bool HasSave => File.Exists(SavePath);

        public static long RandomSeed() => Math.Abs(DateTime.UtcNow.Ticks % 1000000);

        public static void NewRun(long seed)
        {
            Run = RunRules.NewRun(seed, Content);
            Save();
            SceneManager.LoadScene(MapScene);
        }

        /// <summary>For opening the Map scene directly in the editor: use the save if there is one, else a fresh run.</summary>
        public static RunState EnsureRun()
        {
            if (Run != null) return Run;
            if (HasSave)
            {
                try
                {
                    Run = RunJson.FromJson(File.ReadAllText(SavePath));
                    return Run;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Save] could not load {SavePath}: {e.Message}");
                }
            }
            Run = RunRules.NewRun(RandomSeed(), Content);
            Save();
            return Run;
        }

        /// <summary>Load the saved run. Returns why it could not be loaded, or null.</summary>
        public static string Continue()
        {
            try
            {
                Run = RunJson.FromJson(File.ReadAllText(SavePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] could not load {SavePath}: {e.Message}");
                return "The save could not be read.";
            }
            SceneManager.LoadScene(MapScene);
            return null;
        }

        public static void Save()
        {
            if (Run == null) return;
            try
            {
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, RunJson.ToJson(Run));
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] could not write {SavePath}: {e.Message}");
            }
        }

        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] could not delete {SavePath}: {e.Message}");
            }
        }

        public static void AbandonRun()
        {
            Run = null;
            DeleteSave();
        }

        public static void ToMenu()
        {
            BattleLauncher.Clear();
            SceneManager.LoadScene(MenuScene);
        }

        /// <summary>Fight the run's pending battle in the Battle scene, then come back to the map with the result.</summary>
        public static void StartBattle()
        {
            var run = Run;
            var content = Content;
            var enc = RunRules.PendingEncounterDef(run, content);
            BattleLauncher.Create = () => RunRules.BuildBattle(run, content);
            BattleLauncher.Subtitle = enc.Tier == 3 ? "Sector boss" : enc.Tier == 5 ? "Nemesis" : enc.Tier == 2 ? "Elite" : "";
            BattleLauncher.Finished = battle =>
            {
                RunRules.FinishBattle(run, content, battle);
                Save();
                BattleLauncher.Clear();
                SceneManager.LoadScene(MapScene);
            };
            BattleLauncher.Quit = () =>
            {
                Save();
                ToMenu();
            };
            SceneManager.LoadScene(BattleScene);
        }
    }

    /// <summary>Adds the right screen object when the Boot, Menu or Map scene loads; the scenes themselves stay empty.</summary>
    static class MetaSceneBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure(scene);

        static void Ensure(Scene scene)
        {
            switch (scene.name)
            {
                case "Boot":
                    SceneManager.LoadScene(Game.MenuScene);
                    break;
                case "Menu":
                    if (UnityEngine.Object.FindAnyObjectByType<MenuScreen>() == null) new GameObject("MenuScreen").AddComponent<MenuScreen>();
                    break;
                case "Map":
                    if (UnityEngine.Object.FindAnyObjectByType<MapScreen>() == null) new GameObject("MapScreen").AddComponent<MapScreen>();
                    break;
            }
        }
    }
}
