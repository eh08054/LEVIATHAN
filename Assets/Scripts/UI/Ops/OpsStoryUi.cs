using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sector.UI
{
    // The residents' story on screen: arc scenes (two portraits facing each other, lines typed out one at a time,
    // then the AI's answer), overheard talk as speech bubbles, and the citizen page's stance and story tabs.
    public partial class OpsScreen
    {
        static readonly Color[] StanceColors =
        {
            new Color32(53, 201, 232, 255),  // 신뢰
            new Color32(167, 139, 250, 255), // 의존
            new Color32(242, 182, 93, 255),  // 의심
            new Color32(241, 109, 121, 255), // 저항
        };

        // ------------------------------------------------------------------ arc scene

        void ShowScene()
        {
            var def = Story.Scene(session.PendingScene);
            if (def == null)
                return;
            var body = OpenOverlay($"관찰 기록 · {def.Title}", "ops-modal--event scene-modal", closable: false);
            var a = Residents.Get(def.A);
            var b = Residents.Get(def.B);
            Text(body, $"{a?.Name}의 이야기 · {def.Step}장", "scene-kicker");

            var stage = Add(body, "scene-stage");
            VisualElement Face(ResidentDef r, string side)
            {
                var holder = Add(stage, "scene-face scene-face--" + side);
                var portrait = new CastPortrait(r.Id, tall: true) { Expression = MoodFace(r) };
                portrait.AddToClassList("scene-portrait");
                holder.Add(portrait);
                Text(holder, $"{r.Name} · {r.Job}", "scene-name");
                Text(holder, OpsSession.StanceLabel(session.Dominant(r.Id)), "scene-stance");
                return holder;
            }
            var left = Face(a, "left");
            var talk = Add(stage, "scene-talk");
            var right = Face(b, "right");
            var transcript = new ScrollView();
            transcript.AddToClassList("scene-transcript");
            talk.Add(transcript);
            var foot = Add(body, "scene-foot");

            int at = 0;
            Label current = null;
            IVisualElementScheduledItem typing = null;
            string full = "";
            void Finish()
            {
                typing?.Pause();
                if (current != null)
                    current.text = full;
            }
            void ShowAnswers()
            {
                foot.Clear();
                if (def.Options.Count == 0)
                {
                    Text(foot, def.Note ?? "", "scene-note");
                    var row = Add(foot, "ops-row");
                    Add(row, "spacer");
                    MakeButton(row, "기록 종료", () => ChooseScene(-1), "ops-primary");
                    return;
                }
                Text(foot, "AI의 대응", "section-title");
                var options = Add(foot, "ops-options");
                for (int i = 0; i < def.Options.Count; i++)
                {
                    int pick = i;
                    var option = def.Options[i];
                    var button = new Button(() => ChooseScene(pick));
                    button.AddToClassList("ops-option");
                    button.AddToClassList("ops-option--" + option.Tag.ToString().ToLowerInvariant());
                    var head = Add(button, "ops-option-head");
                    Text(head, $"[{OpsEvents.TagName(option.Tag)}]", "ops-tag");
                    Text(head, option.Label, "ops-option-label");
                    Text(button, (option.Effect * OpsTuning.EventScale).Describe(), "ops-card-foot");
                    options.Add(button);
                }
            }
            void Next()
            {
                if (typing != null && current != null && current.text != full)
                {
                    Finish();
                    return;
                }
                if (at >= def.Lines.Count)
                    return;
                var line = def.Lines[at++];
                bool fromA = line.Who == 0;
                left.EnableInClassList("scene-face--speaking", fromA);
                right.EnableInClassList("scene-face--speaking", !fromA);
                var bubble = Add(transcript.contentContainer, "scene-line scene-line--" + (fromA ? "left" : "right"));
                Text(bubble, (fromA ? a : b)?.Name, "scene-line-name");
                current = Text(bubble, "", "scene-line-text");
                full = line.Text;
                int shown = 0;
                typing?.Pause();
                typing = current.schedule.Execute(() =>
                {
                    shown = Math.Min(full.Length, shown + 2);
                    current.text = full.Substring(0, shown);
                }).Every(24).Until(() => shown >= full.Length);
                transcript.schedule.Execute(() => transcript.scrollOffset = new Vector2(0f, float.MaxValue)).StartingIn(30);
                sfx?.Play(Cue.Select, 0.5f);
                if (at >= def.Lines.Count)
                    ShowAnswers();
            }
            talk.RegisterCallback<ClickEvent>(_ => Next());
            var nextRow = Add(foot, "ops-row");
            Text(nextRow, "대화를 누르면 다음 줄", "ops-note");
            Add(nextRow, "spacer");
            MakeButton(nextRow, "다음 ▶", Next, "ops-primary");
            Next();
        }

        void ChooseScene(int option)
        {
            var def = Story.Scene(session.PendingScene);
            var before = def == null ? null : new[] { def.A, def.B }.ToDictionary(id => id, id => session.Dominant(id));
            var result = session.ResolveScene(option);
            if (result == null)
            {
                sfx.Play(Cue.Deny);
                return;
            }
            sfx.Play(Cue.Choose);
            var body = OpenOverlay(def.Title, "ops-modal--narrow", closable: false);
            Text(body, result, "ops-para");
            foreach (var pair in before)
            {
                int now = session.Dominant(pair.Key);
                var r = Residents.Get(pair.Key);
                Text(body, now != pair.Value
                    ? $"{r.Name}: {OpsSession.StanceLabel(pair.Value)} → {OpsSession.StanceLabel(now)}"
                    : $"{r.Name}: {OpsSession.StanceLabel(now)}", "scene-result-stance" + (now != pair.Value ? " scene-result-stance--changed" : ""));
            }
            var row = Add(body, "ops-row");
            Add(row, "spacer");
            MakeButton(row, "확인", () =>
            {
                CloseOverlay();
                if (session.IsOver)
                    ShowEnd();
            }, "ops-primary");
            Refresh();
        }

        // A finished scene, read again from the citizen page.
        void ShowTranscript(SceneDef def)
        {
            if (overlayOpen)
                return;
            bool wasPaused = paused;
            paused = true;
            var body = OpenOverlay($"관찰 기록 · {def.Title}", "ops-modal--narrow", onClose: () => paused = wasPaused);
            var a = Residents.Get(def.A);
            var b = Residents.Get(def.B);
            foreach (var line in def.Lines)
            {
                bool fromA = line.Who == 0;
                var bubble = Add(body, "scene-line scene-line--" + (fromA ? "left" : "right"));
                Text(bubble, (fromA ? a : b)?.Name, "scene-line-name");
                Text(bubble, line.Text, "scene-line-text");
            }
            if (!string.IsNullOrEmpty(def.Note))
                Text(body, def.Note, "scene-note");
        }

        // ------------------------------------------------------------------ overheard talk

        void OnTalkOverheard(TalkDef talk)
        {
            var visible = sim.VisibleCast.ToList();
            bool onCamera = currentPage == Page.Watch && visible.Contains(talk.A) && visible.Contains(talk.B);
            if (onCamera)
            {
                for (int i = 0; i < talk.Lines.Count; i++)
                {
                    var line = talk.Lines[i];
                    sim.schedule.Execute(() => sim.Say(line.Who == 0 ? talk.A : talk.B, line.Text)).StartingIn(i * 2600);
                }
                return;
            }
            var a = Residents.Get(talk.A);
            var b = Residents.Get(talk.B);
            Banner(BannerKind.Info, "엿들은 대화", $"{a?.Name} · {b?.Name}", $"“{talk.Lines[0].Text}”", 3.2f);
        }

        // ------------------------------------------------------------------ citizen page: stance and story

        [NonSerialized] int citizenTab;

        void CitizenTabs(VisualElement info, ResidentDef def)
        {
            var row = Add(info, "ctabs");
            string[] names = { "프로필", "입장과 관계", "이야기" };
            for (int i = 0; i < names.Length; i++)
            {
                int at = i;
                var tab = MakeButton(row, names[i], () =>
                {
                    citizenTab = at;
                    sfx.Play(Cue.Click);
                    RenderPage();
                }, "ctab");
                tab.EnableInClassList("ctab--on", citizenTab == i);
            }
        }

        void StanceBars(VisualElement parent, string id)
        {
            for (int i = 0; i < 4; i++)
            {
                float v = session.StanceValue(id, (Stance)i);
                var row = Add(parent, "stance-row");
                Text(row, Story.StanceName((Stance)i), "stance-name");
                var bar = Add(row, "mini-bar mini-bar--wide");
                var fill = Add(bar, "mini-fill");
                fill.style.width = Length.Percent(v);
                fill.style.backgroundColor = StanceColors[i];
                Text(row, $"{v:0}", "stance-value num");
            }
        }

        void RenderCitizenStance(VisualElement content, ResidentDef def)
        {
            int dominant = session.Dominant(def.Id);
            var head = Add(content, "stance-head");
            Text(head, "지금의 입장", "section-title");
            var tag = Text(head, OpsSession.StanceLabel(dominant), "stance-tag");
            if (dominant >= 0)
                tag.style.backgroundColor = StanceColors[dominant];
            StanceBars(content, def.Id);
            Text(content, "입장은 기분, 반기거나 싫어하는 정책, 통제와 유도, 그리고 이 사람이 숨은 통제를 얼마나 눈치채는지에 따라 매주 움직인다. " +
                          "가장 높은 값이 25를 넘으면 그것이 입장이 된다.", "ops-note");

            Text(content, "관계", "section-title");
            foreach (var rel in Story.RelationsOf(def.Id))
            {
                var other = Residents.Get(rel.A == def.Id ? rel.B : rel.A);
                var row = Add(content, "relation-row");
                var face = new CastPortrait(other.Id) { Expression = MoodFace(other) };
                face.AddToClassList("relation-face");
                row.Add(face);
                var text = Add(row, "relation-text");
                var name = Add(text, "relation-name-row");
                Text(name, other.Name, "relation-name");
                Text(name, OpsSession.StanceLabel(session.Dominant(other.Id)), "relation-stance");
                Text(text, rel.Text, "relation-desc");
                var otherId = other.Id;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    featured = otherId;
                    sfx.Play(Cue.Select);
                    RenderPage();
                    RefreshResident();
                });
            }

            Text(content, "입장 변화 기록", "section-title");
            var changes = session.StanceLog.Where(c => c.Id == def.Id).Reverse().Take(12).ToList();
            if (changes.Count == 0)
                Text(content, "아직 입장이 바뀐 적이 없다.", "ops-note");
            foreach (var c in changes)
            {
                var row = Add(content, "stance-log");
                Text(row, $"{OpsSession.Start.AddDays(c.Day):yyyy.MM.dd}", "goal-log-date num");
                Text(row, $"{OpsSession.StanceLabel(c.From)} → {OpsSession.StanceLabel(c.To)}", "stance-log-change");
                Text(row, c.Reason, "stance-log-reason");
            }
        }

        void RenderCitizenStory(VisualElement content, ResidentDef def)
        {
            var arc = Story.Scenes.Where(s => s.Arc == def.Id).OrderBy(s => s.Step).ToList();
            Text(content, $"{def.Name}의 이야기 · 주제 '{def.Theme}'", "section-title");
            if (arc.Count == 0)
                Text(content, "이 사람의 이야기는 아직 기록되지 않았다.", "ops-note");
            else
            {
                var steps = Add(content, "arc-steps");
                foreach (var scene in arc)
                {
                    bool seen = session.ScenesSeen.Contains(scene.Id);
                    var step = Add(steps, "arc-step" + (seen ? " arc-step--seen" : ""));
                    Text(step, $"{scene.Step}장", "arc-step-no");
                    Text(step, seen ? scene.Title : "아직 일어나지 않았다", "arc-step-title");
                    if (seen)
                    {
                        var s = scene;
                        step.RegisterCallback<ClickEvent>(_ =>
                        {
                            sfx.Play(Cue.Select);
                            ShowTranscript(s);
                        });
                        step.tooltip = "눌러서 다시 보기";
                    }
                }
            }

            Text(content, "엿들은 대화", "section-title");
            var talks = session.Overheard.AsEnumerable().Reverse()
                .Select(o => (o.Day, talk: Story.TalkOf(o.Id)))
                .Where(x => x.talk != null && (x.talk.A == def.Id || x.talk.B == def.Id)).Take(6).ToList();
            if (talks.Count == 0)
                Text(content, "아직 이 사람의 대화를 엿듣지 못했다.", "ops-note");
            foreach (var (day, talk) in talks)
            {
                var box = Add(content, "overheard");
                var other = Residents.Get(talk.A == def.Id ? talk.B : talk.A);
                Text(box, $"{OpsSession.Start.AddDays(day):yyyy.MM.dd} · {other?.Name}와(과)", "overheard-head");
                foreach (var line in talk.Lines)
                    Text(box, $"{Residents.Get(line.Who == 0 ? talk.A : talk.B)?.Name}: {line.Text}", "overheard-line");
            }
        }
    }
}
