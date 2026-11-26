using Silence.Core.Run;
using Silence.View;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Silence.Meta
{
    /// <summary>
    /// Plays the game by itself through the real screens, for play-testing and soak runs: the stand-in bots
    /// make the choices, the map and battle screens carry them out exactly as clicks would.
    /// Toggle with F10 (Shift+F10 also speeds time up), or call <see cref="Toggle"/> from a script.
    /// </summary>
    public sealed class AutoPilot : MonoBehaviour
    {
        public static AutoPilot Instance { get; private set; }
        public bool Running;
        public int RunsFinished, Wins;
        /// <summary>Stop at the run-over screen instead of starting another run.</summary>
        public bool StopAtRunOver = true;
        float nextStep;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("AutoPilot");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AutoPilot>();
        }

        public static void Toggle(bool on, float timeScale = 1f)
        {
            if (Instance == null) Create();
            Instance.Running = on;
            Time.timeScale = on ? timeScale : 1f;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame) Toggle(!Running, kb.shiftKey.isPressed ? 6f : 1f);
            if (!Running || Time.unscaledTime < nextStep) return;

            var battle = FindAnyObjectByType<BattleScreen>();
            if (battle != null && battle.Battle != null)
            {
                if (battle.IsOver)
                {
                    nextStep = Time.unscaledTime + 0.6f;
                    battle.Continue();
                }
                else if (battle.IsIdle)
                {
                    nextStep = Time.unscaledTime + 0.15f;
                    battle.BotTurn();
                }
                return;
            }

            var map = FindAnyObjectByType<MapScreen>();
            if (map == null || map.Run == null) return;
            var run = map.Run;
            nextStep = Time.unscaledTime + 0.5f;
            switch (run.Phase)
            {
                case RunPhase.Battle:
                    map.Engage();
                    break;
                case RunPhase.Over:
                    RunsFinished++;
                    if (run.Result == RunResult.Victory) Wins++;
                    Debug.Log($"[AutoPilot] run {run.Seed} ended: {run.Result}, score {RunRules.Score(run)}");
                    if (StopAtRunOver) Toggle(false);
                    else
                    {
                        long seed = run.Seed + 1;
                        Game.AbandonRun();
                        Game.NewRun(seed);
                    }
                    break;
                default:
                    RunBot.Step(run, Game.Content);
                    Game.Save();
                    map.Refresh();
                    break;
            }
        }
    }
}
