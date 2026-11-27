using Silence.Data;
using Silence.View;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Silence.Meta
{
    /// <summary>Main menu over the title key art: continue a saved run, start a seeded run, or play a skirmish.</summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        RectTransform root;
        TextMeshProUGUI seedText, message;
        string seed = "";
        bool seedFocused;
        Image seedWell;

        void Awake()
        {
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Skin.Ink;
            var canvas = Ui.Canvas("Menu");
            canvas.transform.SetParent(transform, false);
            root = Ui.Root(canvas);
            Ui.Backdrop(root, "title_keyart");
            Ui.Fill(root, "Shade", 0, 0, Ui.RefW, Ui.RefH, new Color(0f, 0f, 0f, 0.28f));

            var logo = Ui.Label(root, "The Silence", Skin.Logo, 168, Skin.Hex("eef1f6"), 0, 96, Ui.RefW, 190, TextAlignmentOptions.Center);
            logo.fontMaterial.EnableKeyword("UNDERLAY_ON");
            logo.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.9f));
            logo.fontMaterial.SetFloat("_UnderlayOffsetY", -0.6f);
            logo.fontMaterial.SetFloat("_UnderlaySoftness", 0.25f);
            Ui.Stamp(root, "It was broadcast to everyone at once", 24, 0, 300, Ui.RefW, 28, TextAlignmentOptions.Center, Skin.Brass);

            var panel = Ui.Plate(root, "Panel", 710, 400, 500, 590);
            float y = 34;
            var cont = Ui.Btn(panel.transform, "Continue run", 50, y, 400, 70, OnContinue, 34);
            cont.interactable = Game.HasSave;
            y += 86;
            Ui.Btn(panel.transform, "New run", 50, y, 400, 70, OnNewRun, 34);
            y += 80;

            Ui.Stamp(panel.transform, "Seed", 13, 54, y + 14, 80, 18);
            seedWell = Ui.Well(panel.transform, "SeedWell", 120, y, 200, 46);
            seedWell.raycastTarget = true;
            var relay = seedWell.gameObject.AddComponent<PointerRelay>();
            relay.Click = _ => { seedFocused = true; ShowSeed(); };
            seedText = Ui.Number(panel.transform, "", 26, 132, y + 6, 180, 34, TextAlignmentOptions.Left, Skin.Text);
            Ui.Btn(panel.transform, "Random", 330, y, 120, 46, () => { seed = Game.RandomSeed().ToString(); ShowSeed(); }, 22);
            y += 54;
            Ui.Body(panel.transform, "The same seed gives the same map, fights and rewards. Click the box to type one.", 14, 54, y, 400, 40, TextAlignmentOptions.TopLeft, Skin.Dim);
            y += 56;

            Ui.Btn(panel.transform, "Tutorial", 50, y, 400, 60, () => { PlayerPrefs.SetInt(TutorialSeenKey, 1); BattleLauncher.Clear(); BattleLauncher.Tutorial = true; SceneManager.LoadScene(Game.BattleScene); }, 30);
            y += 70;
            Ui.Btn(panel.transform, "Skirmish", 50, y, 400, 60, () => { BattleLauncher.Clear(); SceneManager.LoadScene(Game.BattleScene); }, 30);
            y += 70;
            Ui.Btn(panel.transform, "Quit", 50, y, 400, 60, Quit, 30);
            y += 72;
            message = Ui.Body(panel.transform, Game.HasSave ? "A run is in progress." : "", 15, 50, y, 400, 40, TextAlignmentOptions.Top, Skin.Dim);

            Ui.Stamp(root, "Alpha build - milestone M3", 12, 20, Ui.RefH - 30, 600, 18, TextAlignmentOptions.Left, Skin.Faint);
            seed = Game.RandomSeed().ToString();
            ShowSeed();
        }

        void OnEnable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnText;
        }

        void OnDisable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnText;
        }

        void OnText(char ch)
        {
            if (!seedFocused) return;
            if (ch == '\b') { if (seed.Length > 0) seed = seed.Substring(0, seed.Length - 1); }
            else if (char.IsDigit(ch) && seed.Length < 9) seed += ch;
            else if (ch == '\r' || ch == '\n') seedFocused = false;
            ShowSeed();
        }

        void ShowSeed()
        {
            seedText.text = seed + (seedFocused ? "<color=#c9a24a>_</color>" : "");
            seedWell.color = seedFocused ? new Color(1.3f, 1.2f, 0.9f) : Color.white;
        }

        void OnContinue()
        {
            string error = Game.Continue();
            if (error != null) message.text = $"<color=#d9573f>{error}</color>";
        }

        public const string TutorialSeenKey = "silence_tutorial_seen";
        bool confirmedOverwrite;

        void OnNewRun()
        {
            if (Game.HasSave && !confirmedOverwrite)
            {
                confirmedOverwrite = true;
                message.text = "<color=#f0a040>A run is in progress. Press New run again to abandon it and start over.</color>";
                return;
            }
            if (PlayerPrefs.GetInt(TutorialSeenKey, 0) == 0)
            {
                // First time only: point at the tutorial, but never stand in the way of a second click.
                PlayerPrefs.SetInt(TutorialSeenKey, 1);
                message.text = "<color=#e8c870>New here? The Tutorial takes five minutes. Press New run again to skip it.</color>";
                return;
            }
            if (!long.TryParse(seed, out long value) || seed.Length == 0) value = Game.RandomSeed();
            ShowFactionPicker(value);
        }

        /// <summary>Which fleet to take out: one large button per playable faction, with its flagship and what sets it apart.</summary>
        void ShowFactionPicker(long seedValue)
        {
            var shade = Ui.Fill(root, "PickerShade", 0, 0, Ui.RefW, Ui.RefH, new Color(0f, 0f, 0f, 0.8f));
            shade.raycastTarget = true;
            const float w = 1240, h = 800;
            var d = Ui.Plate(shade.transform, "Picker", (Ui.RefW - w) / 2f, (Ui.RefH - h) / 2f, w, h).rectTransform;
            Ui.Stamp(d, $"New run - seed {seedValue}", 12, 40, 26, 600, 14, TextAlignmentOptions.Left, Skin.Brass);
            Ui.Heading(d, "Choose your fleet", 32, 38, 48, 800, 36);
            Ui.Btn(d, "Back", w - 190, 30, 150, 48, () => Destroy(shade.gameObject), 16);
            float x = 40;
            foreach (string id in ContentLoader.Factions)
            {
                var content = ContentLoader.Load(id);
                var f = content.Faction;
                string faction = id;
                var b = Ui.Btn(d, "", x, 120, 570, 640, () => { Game.AbandonRun(); Game.NewRun(seedValue, faction); });
                var flag = Silence.Core.Battle.UnitLibrary.Get(f.FlagshipUnit);
                var escort = Silence.Core.Battle.UnitLibrary.Get(f.EscortUnit);
                // The flagship at twice size, on the same kind of well the battle screen puts ships in.
                var well = Ui.Well(b.transform, "Ship", 24, 24, 522, 290);
                well.raycastTarget = false;
                var sprite = ArtLibrary.Instance.Sprite(flag.Id + "_E128") ?? ArtLibrary.Instance.Sprite(flag.Id + "_E");
                if (sprite != null)
                {
                    float sw = sprite.rect.width * 2f, sh = sprite.rect.height * 2f;
                    var img = Ui.Img(well.transform, "Art", sprite, Mathf.Round((522 - sw) / 2f), Mathf.Round((290 - sh) / 2f), sw, sh);
                    img.raycastTarget = false;
                }
                Ui.Heading(b.transform, f.Name, 32, 24, 330, 522, 36);
                Ui.Stamp(b.transform, $"{flag.Name} - hull {flag.Hull}, speed {flag.MaxSpeed}   /   {escort.Name} - hull {escort.Hull}", 12, 26, 374, 520, 14, TextAlignmentOptions.Left, Skin.Brass);
                Ui.Body(b.transform, f.Blurb, 16, 26, 402, 518, 120, TextAlignmentOptions.TopLeft, Skin.Text);
                var cfg = content.Config;
                string fit = string.Join(", ", cfg.StarterModules.ConvertAll(m => content.Module(m).Name));
                Ui.Stamp(b.transform, "Fitted", 12, 26, 530, 100, 14, TextAlignmentOptions.Left, Skin.BrassDark);
                Ui.Body(b.transform, fit, 16, 26, 548, 518, 40, TextAlignmentOptions.TopLeft, Skin.Dim);
                Ui.Stamp(b.transform, $"{cfg.StartOrdinals} {f.CrewName}   /   {cfg.StartDarts} {f.CraftName}   /   {content.Rewards.Count} cards to find" + (f.HasCouncil ? "   /   Council" : ""), 12, 26, 600, 518, 14, TextAlignmentOptions.Left, Skin.Brass);
                x += 590;
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
