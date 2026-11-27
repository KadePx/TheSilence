using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Silence.View
{
    /// <summary>
    /// The game's vocabulary: every rules word has one colour and one explanation, wherever it is written.
    /// <see cref="Mark"/> finds those words in a piece of text, colours them and makes them hoverable;
    /// <see cref="TipLayer"/> shows the explanation while the pointer is on one.
    /// The colours: green is hull, red is damage and danger, orange is heat, cyan is shields, brass is scrap,
    /// pink is the race (time), gold is relics, violet is hazards.
    /// </summary>
    public static class Gloss
    {
        public sealed class Term
        {
            public string Id, Colour, Title, Text;
            /// <summary>Regex alternatives, without anchors. Matched on word boundaries.</summary>
            public string Pattern;
            /// <summary>Only matched exactly as written (for words that are also ordinary English).</summary>
            public bool CaseSensitive;
        }

        /// <summary>Set by the screens so explanations can name names.</summary>
        public static string Rival = "rival", Crew = "Ordinals", Craft = "darts";

        public const string Race = "f08ab0", Hull = "8cc47a", Danger = "e0664e", Heat = "f0a040", Shield = "7fdcf2", Scrap = "c9a24a",
            CrewColour = "9fc0ff", Relic = "ffc46a", Hazard = "b48cf0", Junk = "c9785a", Approval = "d0864a", Command = "e8c870", Plain = "eef1f6";

        // Longer phrases first: the first alternative that matches wins.
        static readonly Term[] Terms =
        {
            new Term { Id = "race", Colour = Race, Title = "The race", Pattern = @"costs time: rivals gain \d+(?:\.\d+)? nodes?|rivals (?:gain|lose|fall back) \d+(?:\.\d+)? nodes?|(?:both )?rival fleets (?:lose|fall back) \d+(?:\.\d+)? nodes?|\d+(?:\.\d+)? nodes?|rival fleets?|rivals?|the race|costs time|dock time|nodes? ahead|nodes? behind",
                Text = "Every jump moves you 1 node toward the gate and each rival a little less, so 0.8 node is most of a jump won or lost. It is the track along the top of the map.\n\nRivals also gain when a battle runs long, when you buy at a shipyard, or when an event costs time.\n\nIf the {rival} gets a full node ahead it mines the lanes: every jump then costs each ship 2 hull. If it is ahead when you reach the gate, the gate is reinforced." },
            new Term { Id = "jump", Colour = Shield, Title = "Jump lane", Pattern = @"jump lanes?",
                Text = "A dotted cyan lane on the map that skips a node. You gain two nodes on the rivals for one jump of their time. Every ship pays hull for the rough passage, and you never see the node you skipped. Jump lanes are never mined." },
            new Term { Id = "mines", Colour = Danger, Title = "Mined lanes", Pattern = @"mined lanes?|lanes mined|mined|mines",
                Text = "While the {rival} leads by a full node, every ordinary jump costs each ship hull. Catch up to clear it: take a jump lane, seed false beacons at a shipyard, or stop losing time." },
            new Term { Id = "maxhull", Colour = Hull, Title = "Maximum hull", Pattern = @"max(?:imum)? hull",
                Text = "The most hull a ship can have. Raising it also adds that much hull now; lowering it is permanent." },
            new Term { Id = "junkcard", Colour = Junk, Title = "Junk", Pattern = @"Fire on Deck|Hull Breach|Jammed Mount|junk cards?|junk",
                Text = "Dead cards in your deck. Critical hits, overheating and some events add them. Fire on Deck heats the flagship while held; Hull Breach drains crew; Jammed Mount makes a weapon cost more. Strip them at a shipyard, or pay 1 command in battle to put a fire out." },
            new Term { Id = "hull", Colour = Hull, Title = "Hull", Pattern = @"hull points?|hull",
                Text = "A ship's health. At 0 the ship is destroyed and every card it provides leaves the deck. Damage carries over between battles. Mend it at a shipyard, with field repairs after a fight, or with repair cards." },
            new Term { Id = "salvage", Colour = Scrap, Title = "Salvage", Pattern = @"salvage",
                Text = "The scrap a won battle pays. Hazards and some replies multiply it." },
            new Term { Id = "scrap", Colour = Scrap, Title = "Scrap", Pattern = @"scrap",
                Text = "Currency. Won in battles and events; spent at shipyards on repairs, cards, refits and services." },
            new Term { Id = "ordinals", Colour = CrewColour, Title = "Ordinals", Pattern = @"Ordinals?",
                Text = "The Ascendancy's crew, grown to be spent. Some cards Expend them for a stronger effect. At 0, every card costs 1 more command. A few come back after each victory; shipyards decant more." },
            new Term { Id = "crew", Colour = CrewColour, Title = "Crew", Pattern = @"crew", CaseSensitive = false,
                Text = "The fleet's hands. At 0, every card costs 1 more command. Hull Breach junk drains them; a few sign on after each victory, and shipyards have more." },
            new Term { Id = "craft", Colour = Plain, Title = "Small craft", Pattern = @"gunships?|darts?|small craft|craft",
                Text = "Launched from the hangar by a card and moved by hand: click one, then a hex or an enemy. Each acts once a turn. Those lost in battle stay lost; shipyards load more." },
            new Term { Id = "approval", Colour = Approval, Title = "Council approval", Pattern = @"approval",
                Text = "0 to 6. A clean win earns 1, an elite 2. A win that costs a ship earns none; a stand-off loses 1. Spend it on Council motions between jumps (the Council button under your fleet)." },
            new Term { Id = "officer", Colour = "ebe0c6", Title = "Officers", Pattern = @"officers?",
                Text = "Named crew aboard the flagship, each a standing bonus. A critical hit on the flagship wounds one, and a wounded officer gives nothing until a shipyard sickbay or a Council motion returns them to duty." },
            new Term { Id = "relic", Colour = Relic, Title = "Relic", Pattern = @"relics?|trophy",
                Text = "Something carried for the rest of the run that changes a rule. Elites carry two and you take one; killing the commander who hunts you gives their trophy. Hover a relic under your fleet to read it." },
            new Term { Id = "hazard", Colour = Hazard, Title = "Hazard", Pattern = @"ion storm|nebula|dense field|hazards?",
                Text = "Some fights are in bad space. Ion storm: nobody's shields regenerate. Nebula: every weapon on both sides reaches 1 hex less. Dense field: twice the rock. A hazard fight pays more salvage." },
            new Term { Id = "nemesis", Colour = Danger, Title = "The commander hunting you", Pattern = @"nemesis|commander|grudge|rank III|rank II|rank I\b|breaks? off|broke off",
                Text = "One rival commander hunts each run. Below rank III their ship breaks off when it is badly hurt, after the enemy has acted, and comes back a rank higher and refitted against whatever hurt it most. Kill it in one turn from above that line, or taunt a returning commander into staying. Rank III fights to the end." },
            new Term { Id = "edict", Colour = "fff0b8", Title = "Edict of the Prime", Pattern = @"Edicts? of the Prime|Edicts?|Inquisitor Aboard|Inquisitors?",
                Text = "An order from the Prime for this sector, judged when you reach the gate. Two are read out and you take one. Obeyed, it pays what it promised. Broken, an Inquisitor comes aboard: a junk card that costs 1 command every time you draw it, until a shipyard strips it out." },
            new Term { Id = "elite", Colour = Danger, Title = "Elite", Pattern = @"elite",
                Text = "A harder fight. It offers one more card, and two relics to choose between." },
            new Term { Id = "heat", Colour = Heat, Title = "Heat", Pattern = @"heat cap|Heat|heat", CaseSensitive = true,
                Text = "Builds on the ship that plays the card and drains a little each turn. Over the heat cap at end of turn adds a Fire on Deck. Four or more over is Reactor Strain: 6 hull, straight through shields." },
            new Term { Id = "shields", Colour = Shield, Title = "Shields", Pattern = @"shields?",
                Text = "Soak damage on the arc that is hit, before armor and hull. Each arc has its own and they come back a little each turn." },
            new Term { Id = "armor", Colour = "a9b0c0", Title = "Armor", Pattern = @"armou?r",
                Text = "Takes a fixed amount off every hit that gets past the shields, on the arc that is hit." },
            new Term { Id = "command", Colour = Command, Title = "Command", Pattern = @"\d command|more command|command next turn|command on turn", CaseSensitive = true,
                Text = "What cards cost. You have 3 each turn and what you do not spend is lost." },
            new Term { Id = "expend", Colour = CrewColour, Title = "Expend", Pattern = @"Expend", CaseSensitive = true,
                Text = "An extra cost paid in crew. Some cards need it; some are stronger if you choose to pay." },
            new Term { Id = "exhaust", Colour = "c0a8e8", Title = "Exhaust", Pattern = @"Exhaust", CaseSensitive = true,
                Text = "Once played, the card is gone for the rest of this battle. It is back in the next one." },
            new Term { Id = "damage", Colour = Danger, Title = "Damage", Pattern = @"dmg|damage",
                Text = "Hits the arc of the target facing the attacker: shields first, then armor takes its bite, and the rest comes off hull. 5 or more hull in one hit is a critical." },
            new Term { Id = "crit", Colour = Danger, Title = "Critical hit", Pattern = @"critical hits?|crits?",
                Text = "5 or more hull damage in one hit. On your ships it adds a junk card or hits the engines; on the flagship it also wounds an officer." },
            new Term { Id = "arc", Colour = Plain, Title = "Arcs", Pattern = @"fore arc|aft arc|port or starboard arc|port or starboard|side arcs?|any arc|every arc|arcs?",
                Text = "Each ship has four arcs: fore (ahead), aft (behind), port and starboard (the flanks). A weapon reaches only targets inside its arc, and a hit lands on the arc of the target that faces the attacker. Turn to bring guns to bear, or to show a fresh shield." },
            new Term { Id = "range", Colour = Plain, Title = "Range", Pattern = @"range",
                Text = "Counted in hexes from the ship that fires. Rock blocks the line of fire; wreckage does not." },
            new Term { Id = "speed", Colour = Shield, Title = "Speed and drift", Pattern = @"Speed|drifts?|helm", CaseSensitive = true,
                Text = "Capital ships are not moved by hand. At the end of each turn every ship drifts straight ahead by its Speed. Change Speed one step a turn, free, with the helm on the ship's card (speeding up adds 1 heat). Cards turn the ship." },
        };

        static readonly Dictionary<string, Term> ById = new Dictionary<string, Term>();
        static Regex anyCase, exactCase;

        static void Build()
        {
            if (anyCase != null) return;
            var loose = new StringBuilder();
            var strict = new StringBuilder();
            for (int i = 0; i < Terms.Length; i++)
            {
                var t = Terms[i];
                ById[t.Id] = t;
                // Case-sensitive terms are written in their own inline-case group so the rest can ignore case.
                string group = $"(?<t{i}>{(t.CaseSensitive ? "(?-i:" + t.Pattern + ")" : t.Pattern)})";
                (strict.Length > 0 ? strict.Append('|') : strict).Append(group);
                // In capitals (stamps) everything is matched whatever its case.
                (loose.Length > 0 ? loose.Append('|') : loose).Append($"(?<t{i}>{t.Pattern})");
            }
            exactCase = new Regex(@"(?<![\w>])(?:" + strict + @")(?![\w<])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            anyCase = new Regex(@"(?<![\w>])(?:" + loose + @")(?![\w<])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        public static Term Find(string id)
        {
            Build();
            return id != null && ById.TryGetValue(id, out var t) ? t : null;
        }

        /// <summary>What a term means, with the names of this run filled in.</summary>
        public static string Explain(Term t) => t.Text.Replace("{rival}", Rival).Replace("{crew}", Crew).Replace("{craft}", Craft);

        /// <summary>The rules words that appear in a text, in the order they are met, each once.</summary>
        public static List<Term> TermsIn(string text)
        {
            Build();
            var found = new List<Term>();
            foreach (Match m in exactCase.Matches(Tag.Replace(text ?? "", "")))
                for (int i = 0; i < Terms.Length; i++)
                    if (m.Groups["t" + i].Success && !found.Contains(Terms[i])) found.Add(Terms[i]);
            return found;
        }

        /// <summary>The first sentence of a term's explanation: enough for a list of several.</summary>
        public static string Short(Term t)
        {
            string text = Explain(t);
            int stop = text.IndexOf(". ", StringComparison.Ordinal);
            if (stop >= 0 && stop < 30)
            {
                int second = text.IndexOf(". ", stop + 2, StringComparison.Ordinal);
                stop = second >= 0 ? second : text.Length - 1;
            }
            int line = text.IndexOf('\n');
            if (line >= 0 && (stop < 0 || line < stop)) stop = line - 1;
            return stop > 0 ? text.Substring(0, stop + 1) : text;
        }

        /// <summary>Every rules word in a text, each with a one-line explanation: the tooltip for a card or a relic.</summary>
        public static string Legend(string text, int max = 6)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var t in TermsIn(text))
            {
                if (n++ >= max) break;
                if (sb.Length > 0) sb.Append("\n");
                sb.Append($"<color=#{t.Colour}>{t.Title.ToUpperInvariant()}</color>  {Short(t)}");
            }
            return sb.ToString();
        }

        static readonly Regex Tag = new Regex(@"<[^>]+>", RegexOptions.CultureInvariant);

        /// <summary>
        /// Colour every rules word in the text and, with <paramref name="links"/>, make each one hoverable.
        /// Existing rich-text tags are left alone. <paramref name="capitals"/>: the text is all capitals (a stamp),
        /// so case can't tell a rules word from an ordinary one.
        /// </summary>
        public static string Mark(string text, bool links = true, bool capitals = false)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("<link=", StringComparison.Ordinal) >= 0) return text;
            Build();
            var rx = capitals ? anyCase : exactCase;
            var sb = new StringBuilder(text.Length + 64);
            int at = 0;
            foreach (Match tag in Tag.Matches(text))
            {
                sb.Append(MarkPlain(text.Substring(at, tag.Index - at), rx, links));
                sb.Append(tag.Value);
                at = tag.Index + tag.Length;
            }
            sb.Append(MarkPlain(text.Substring(at), rx, links));
            return sb.ToString();
        }

        static string MarkPlain(string plain, Regex rx, bool links)
        {
            if (plain.Length == 0) return plain;
            return rx.Replace(plain, m =>
            {
                for (int i = 0; i < Terms.Length; i++)
                {
                    if (!m.Groups["t" + i].Success) continue;
                    var t = Terms[i];
                    string coloured = $"<color=#{t.Colour}>{m.Value}</color>";
                    return links ? $"<link=\"{t.Id}\">{coloured}</link>" : coloured;
                }
                return m.Value;
            });
        }
    }

    /// <summary>Marks a text whose rules words can be hovered. Added by <see cref="Ui"/> when it glosses a label.</summary>
    public sealed class GlossText : MonoBehaviour
    {
        public static readonly List<GlossText> Active = new List<GlossText>();
        public TextMeshProUGUI Text;

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);
    }

    /// <summary>A tooltip for a thing that is not a word: an icon, a bar, a node, a button.</summary>
    public sealed class TipSource : MonoBehaviour
    {
        public string Title = "", Colour = Gloss.Command;
        public Func<string> Body;
    }

    public static class Tip
    {
        /// <summary>Give a graphic a tooltip. The graphic starts catching the pointer; clicks still reach a button beneath it.</summary>
        public static void On(UnityEngine.UI.Graphic g, string title, Func<string> body, string colour = Gloss.Command)
        {
            if (g == null) return;
            g.raycastTarget = true;
            var src = g.GetComponent<TipSource>() ?? g.gameObject.AddComponent<TipSource>();
            src.Title = title;
            src.Body = body;
            src.Colour = colour;
            TipLayer.Ensure();
        }

        public static void On(UnityEngine.UI.Graphic g, string title, string body, string colour = Gloss.Command) => On(g, title, () => body, colour);
    }

    /// <summary>
    /// Draws the one tooltip on screen, above everything. Each frame it looks for a <see cref="TipSource"/> under
    /// the pointer, then for a hoverable rules word in any glossed text that is not covered by a dialog.
    /// </summary>
    public sealed class TipLayer : MonoBehaviour
    {
        const float Width = 460f, Pad = 16f;
        static TipLayer instance;
        /// <summary>For play-test scripts: look here instead of at the mouse.</summary>
        public static Vector2? PointerOverride;

        RectTransform root, panel;
        TextMeshProUGUI title, body;
        string shown = "";
        readonly List<RaycastResult> hits = new List<RaycastResult>();

        public static void Ensure()
        {
            if (instance != null) return;
            var canvas = Ui.Canvas("Tips", 1000);
            DontDestroyOnLoad(canvas.gameObject);
            Destroy(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>()); // a tooltip never takes the pointer
            instance = canvas.gameObject.AddComponent<TipLayer>();
            instance.root = Ui.Root(canvas);
            var img = Ui.Panel(instance.root, "Tip", 0, 0, Width, 100);
            img.raycastTarget = false;
            instance.panel = img.rectTransform;
            instance.title = Ui.Label(img.transform, "", Skin.Stencil, 12, Skin.BrassLight, Pad, 12, Width - Pad * 2, 14);
            instance.body = Ui.Label(img.transform, "", Skin.Body, 16, Skin.Text, Pad, 34, Width - Pad * 2, 600);
            instance.body.lineSpacing = 10f;
            instance.panel.gameObject.SetActive(false);
        }

        void Update()
        {
            var mouse = Mouse.current;
            if ((mouse == null && PointerOverride == null) || EventSystem.current == null)
            {
                Hide();
                return;
            }
            Vector2 at = PointerOverride ?? mouse.position.ReadValue();
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = at }, hits);
            var top = hits.Count > 0 ? hits[0].gameObject.transform : null;

            // 1. A thing with its own tooltip, or a parent of the thing under the pointer that has one.
            for (var t = top; t != null; t = t.parent)
            {
                var src = t.GetComponent<TipSource>();
                if (src == null) continue;
                string text = src.Body != null ? src.Body() : "";
                if (!string.IsNullOrEmpty(text) || !string.IsNullOrEmpty(src.Title))
                {
                    Show("src:" + src.Title + text, src.Title, src.Colour, text, at);
                    return;
                }
                break;
            }

            // 2. A rules word in a text that is not underneath whatever the pointer is really on.
            var cover = Layer(top);
            foreach (var g in GlossText.Active)
            {
                if (g == null || g.Text == null || !g.Text.isActiveAndEnabled) continue;
                if (cover != null && g.Text.transform.root == cover.root && !g.Text.transform.IsChildOf(cover))
                {
                    var own = Layer(g.Text.transform);
                    if (own == null || own.parent != cover.parent || own.GetSiblingIndex() < cover.GetSiblingIndex()) continue;
                }
                int link = TMP_TextUtilities.FindIntersectingLink(g.Text, at, null);
                if (link < 0) continue;
                var term = Gloss.Find(g.Text.textInfo.linkInfo[link].GetLinkID());
                if (term == null) continue;
                Show("term:" + term.Id, term.Title, term.Colour, Gloss.Explain(term), at);
                return;
            }
            Hide();
        }

        /// <summary>The screen layer a thing belongs to: the ancestor that sits directly under a canvas root.</summary>
        static Transform Layer(Transform t)
        {
            for (; t != null && t.parent != null; t = t.parent)
                if (t.parent.name == "Root") return t;
            return null;
        }

        void Show(string key, string heading, string colour, string text, Vector2 screen)
        {
            if (key != shown)
            {
                shown = key;
                title.text = $"<color=#{colour}>{heading.ToUpperInvariant()}</color>";
                // The explanation is itself colour-coded, without links: a tooltip has no tooltips.
                body.text = Gloss.Mark(text, false);
                body.ForceMeshUpdate();
                float h = (string.IsNullOrEmpty(text) ? 30 : 34 + Mathf.Ceil(body.preferredHeight)) + 14;
                panel.sizeDelta = new Vector2(Width, h);
                panel.gameObject.SetActive(true);
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
            // Root-local has its origin at the centre, y up; the kit places from the top-left, y down.
            float x = local.x + Ui.RefW / 2f + 22, y = Ui.RefH / 2f - local.y + 26;
            if (x + Width > Ui.RefW - 8) x = local.x + Ui.RefW / 2f - Width - 18;
            if (y + panel.sizeDelta.y > Ui.RefH - 8) y = Ui.RefH - 8 - panel.sizeDelta.y;
            panel.anchoredPosition = new Vector2(Mathf.Round(Mathf.Max(8, x)), -Mathf.Round(Mathf.Max(8, y)));
        }

        void Hide()
        {
            if (shown.Length == 0) return;
            shown = "";
            panel.gameObject.SetActive(false);
        }
    }
}
