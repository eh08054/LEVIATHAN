using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Index = Sector.Ops.Index;

namespace Sector.UI
{
    // The district map: one block per zone, colored by how satisfied its people are; locked zones are grey.
    public class ZoneMap : VisualElement
    {
        // Each zone's block on a 100 x 100 grid (a rough top-down sketch of the painting).
        // The painting's district outlines, stretched over the city area (x 0..1, y 0.24..0.86) onto a 100 x 100 grid.
        public static Vector2[][] GridShapes => Shapes;

        static readonly Vector2[][] Shapes = OpsScreen.ZoneOutlines
            .Select(shape => shape.Select(v => new Vector2(v.x * 100f, (v.y - 0.24f) / 0.62f * 100f)).ToArray()).ToArray();

        readonly Func<int, Color> colorOf;
        readonly Func<int, bool> isOpen;
        int selected = -1;

        public event Action<int> ZoneClicked;

        public ZoneMap(Func<int, Color> colorOf, Func<int, bool> isOpen)
        {
            this.colorOf = colorOf;
            this.isOpen = isOpen;
            AddToClassList("zone-map");
            generateVisualContent += Draw;
            RegisterCallback<ClickEvent>(e =>
            {
                var r = contentRect;
                var p = new Vector2(e.localPosition.x / r.width * 100f, e.localPosition.y / r.height * 100f);
                for (int z = 0; z < Shapes.Length; z++)
                    if (Inside(Shapes[z], p) && isOpen(z))
                        ZoneClicked?.Invoke(z);
            });
        }

        public void Select(int zone)
        {
            selected = zone;
            MarkDirtyRepaint();
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            var p = ctx.painter2D;
            Vector2 At(Vector2 v) => new Vector2(r.width * v.x / 100f, r.height * v.y / 100f);
            // faint grid
            p.strokeColor = new Color(0.47f, 0.75f, 0.96f, 0.08f);
            p.lineWidth = 1f;
            for (int i = 1; i < 10; i++)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(r.width * i / 10f, 0f));
                p.LineTo(new Vector2(r.width * i / 10f, r.height));
                p.Stroke();
                p.BeginPath();
                p.MoveTo(new Vector2(0f, r.height * i / 10f));
                p.LineTo(new Vector2(r.width, r.height * i / 10f));
                p.Stroke();
            }
            for (int z = 0; z < Shapes.Length; z++)
            {
                bool open = isOpen(z);
                var c = open ? colorOf(z) : new Color(0.45f, 0.5f, 0.58f);
                p.fillColor = new Color(c.r, c.g, c.b, open ? 0.32f : 0.12f);
                p.strokeColor = new Color(c.r, c.g, c.b, z == selected ? 1f : open ? 0.75f : 0.35f);
                p.lineWidth = z == selected ? 2.5f : 1.2f;
                p.BeginPath();
                p.MoveTo(At(Shapes[z][0]));
                for (int i = 1; i < Shapes[z].Length; i++)
                    p.LineTo(At(Shapes[z][i]));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }
        }

        public static Vector2 Center(int zone)
        {
            var s = Shapes[zone];
            return new Vector2(s.Average(v => v.x), s.Average(v => v.y));
        }
    }

    public partial class OpsScreen
    {
        // ------------------------------------------------------------------ tone and delta helpers

        static readonly Color Good = new Color32(96, 220, 170, 255);
        static readonly Color Mid = new Color32(130, 200, 245, 255);
        static readonly Color Caution = new Color32(247, 167, 72, 255);
        static readonly Color Bad = new Color32(238, 92, 92, 255);

        static string ToneClass(float value) => value < 30f ? "tone--bad" : value < 45f ? "tone--warn" : value < 60f ? "tone--mid" : "tone--good";

        static Color ToneColor(float value) => value < 30f ? Bad : value < 45f ? Caution : Palette.Cyan;

        static Label Toned(VisualElement parent, string text, float value, string classes)
        {
            var label = Text(parent, text, classes);
            label.AddToClassList(ToneClass(value));
            return label;
        }

        // "▲1.2" in green or "▼0.8" in red; nothing for tiny changes. invert: lower is better.
        static Label DeltaLabel(VisualElement parent, float change, string classes = "delta", bool invert = false, string unit = "")
        {
            var label = Text(parent, Math.Abs(change) < 0.05f ? "–" : change > 0 ? $"▲{change:0.#}{unit}" : $"▼{-change:0.#}{unit}", classes);
            bool good = invert ? change < 0 : change > 0;
            label.EnableInClassList("delta--up", Math.Abs(change) >= 0.05f && good);
            label.EnableInClassList("delta--down", Math.Abs(change) >= 0.05f && !good);
            return label;
        }

        static VisualElement MiniBar(VisualElement parent, float value, Color color, string classes = "mini-bar")
        {
            var bar = Add(parent, classes);
            var fill = Add(bar, "mini-fill");
            fill.style.width = Length.Percent(Mathf.Clamp(value, 0f, 100f));
            fill.style.backgroundColor = color;
            return bar;
        }

        // Every index goes up when things improve, except real autonomy: for the AI, fewer free choices is progress.
        static bool HigherIsBetter(Index index) => index != Index.AutonomyReal;

        static bool IsGood(Index index, float change) => HigherIsBetter(index) ? change > 0 : change < 0;

        static string Population(float people) => $"{Mathf.RoundToInt(people):N0}명";

        // A vertical list of sub-pages on the left of a page body, like the reference screens.
        readonly Dictionary<Page, int> subPage = new Dictionary<Page, int>();

        int SubNav(VisualElement body, Page page, params (IconKind icon, string label)[] items)
        {
            subPage.TryGetValue(page, out int selected);
            var nav = Add(body, "subnav glass");
            int index = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].label.StartsWith("§"))
                {
                    Text(nav, items[i].label.Substring(1), "subnav-header");
                    continue;
                }
                int at = index++;
                var button = new Button(() =>
                {
                    subPage[page] = at;
                    sfx.Play(Cue.Click);
                    RenderPage();
                });
                button.AddToClassList("subnav-item");
                button.EnableInClassList("subnav-item--on", at == selected);
                button.Add(new LineIcon(items[i].icon));
                Text(button, items[i].label, "subnav-label").pickingMode = PickingMode.Ignore;
                nav.Add(button);
            }
            return selected;
        }

        // ------------------------------------------------------------------ groups (citizens page, second tab)

        void RenderGroups(VisualElement body)
        {
            var scroll = Scroll(body, "page-scroll");
            var shares = session.GroupShares;
            var head = Add(scroll.contentContainer, "groups-head");
            var heroCard = Add(head, "hero glass");
            Text(heroCard, "관리 인구", "hero-label");
            Text(heroCard, Population(session.ManagedPopulation), "hero-value");
            Text(heroCard, $"시범 도시 {session.Zones}개 구역 전체", "hero-sub");
            var satCard = Add(head, "hero glass");
            Text(satCard, "인구 가중 만족도", "hero-label");
            Toned(satCard, $"{session.PopulationSatisfaction:0.0}", session.PopulationSatisfaction, "hero-value");
            var lowest = session.LowestGroup;
            Text(satCard, $"가장 불만이 큰 집단: {Society.Get(lowest).Name} ({session.GroupSat[(int)lowest]:0})", "hero-sub");

            var grid = Add(scroll.contentContainer, "group-grid");
            foreach (var group in Society.Groups)
            {
                int g = (int)group;
                var def = Society.Get(group);
                float sat = session.GroupSat[g];
                var card = Add(grid, "group-card glass");
                var top = Add(card, "group-top");
                Text(top, def.Name, "group-name");
                Text(top, $"{shares[g] * 100f:0}% · {Population(session.ManagedPopulation * shares[g])}", "group-share");
                var valueRow = Add(card, "group-value-row");
                Toned(valueRow, $"{sat:0}", sat, "group-value");
                float weekly = session.GroupPush(group) + (session.Indices[Index.Happiness] - sat) * OpsTuning.GroupPull;
                DeltaLabel(valueRow, weekly, "delta delta--big", unit: "/주");
                MiniBar(card, sat, ToneColor(sat));
                Text(card, def.Description, "group-desc");
                var reasons = session.Active.Keys.Select(id => (id, v: session.GroupReaction(id, group)))
                    .Where(x => Math.Abs(x.v) > 0.05f).OrderBy(x => x.v).ToList();
                var chips = Add(card, "chip-row");
                if (reasons.Count == 0)
                    Text(chips, "영향을 주는 정책 없음", "ops-note");
                foreach (var (id, v) in reasons)
                {
                    var chip = Text(chips, $"{Policies.Get(id).Name} {(v > 0 ? "▲" : "▼")}{Math.Abs(v):0.#}", "chip chip--live");
                    chip.AddToClassList(v > 0 ? "chip--like" : "chip--dislike");
                }
            }
        }

        // ------------------------------------------------------------------ observation sub-pages

        void RenderZones(VisualElement body)
        {
            var main = Scroll(body, "pg-main page-scroll").contentContainer;
            ZoneStatus(main);
            var zones = Card(main, "zone-table", IconKind.Pin, "구역별 상세");
            var header = Add(zones, "table-row table-row--head");
            Text(header, "구역", "table-cell table-cell--name");
            Text(header, "인구", "table-cell table-cell--num");
            Text(header, "만족도", "table-cell table-cell--num");
            Text(header, "긴장", "table-cell table-cell--num");
            Text(header, "단계", "table-cell table-cell--center");
            Text(header, "이상", "table-cell table-cell--num");
            for (int z = 0; z < Society.Zones.Count; z++)
            {
                var def = Society.Zones[z];
                int zone = z;
                var row = Add(zones, "table-row");
                row.EnableInClassList("table-row--on", sim.Zone == z);
                Text(row, $"{def.Name} · {def.Kind}", "table-cell table-cell--name");
                Text(row, Population(session.ZonePop[z]), "table-cell table-cell--num");
                float sat = session.ZoneSatisfaction(z);
                Toned(row, $"{sat:0.0}", sat, "table-cell table-cell--num");
                Text(row, $"{session.ZoneTension[z]:0}", "table-cell table-cell--num");
                int stage = session.TensionStage(z);
                Text(row, Tension.StageNames[stage], "table-cell table-cell--center").AddToClassList(stage >= 3 ? "tone--bad" : stage == 2 ? "tone--warn" : "tone--mid");
                Text(row, $"{session.Anomalies.Count(a => a.Zone == z)}", "table-cell table-cell--num");
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    sim.ShowZone(zone);
                    sfx.Play(Cue.Click);
                    RenderPage();
                });
            }
            RenderZoneDetail(main, sim.Zone);
            RenderWatchSide(body);
        }

        void RenderBehavior(VisualElement body)
        {
            var main = Add(body, "pg-main");

            var groups = Card(main, "group-table", IconKind.People, "집단별 만족도");
            foreach (var group in Society.Groups)
            {
                int g = (int)group;
                float sat = session.GroupSat[g];
                var row = Add(groups, "bar-row");
                Text(row, Society.Get(group).Name, "bar-row-name");
                MiniBar(row, sat, ToneColor(sat), "mini-bar mini-bar--wide");
                Toned(row, $"{sat:0}", sat, "bar-row-value");
                float weekly = session.GroupPush(group) + (session.Indices[Index.Happiness] - sat) * OpsTuning.GroupPull;
                DeltaLabel(row, weekly, "delta bar-row-delta", unit: "/주");
                var state = Text(row, "", "state-tag");
                SetStatus(state, sat);
            }
        }

        // Things that need attention now.
        void RenderWarnings(VisualElement body)
        {
            var main = Add(body, "pg-main");
            var card = Card(main, "warning-list", IconKind.Alert, "이상 징후");
            var items = new List<(int level, string title, string detail)>();
            foreach (var group in Society.Groups)
            {
                float sat = session.GroupSat[(int)group];
                if (sat < OpsTuning.GroupUnrestPoint)
                    items.Add((sat < 25f ? 2 : 1, $"{Society.Get(group).Name} 불만 누적 ({sat:0})", "집단 시위와 민원이 늘어난다. 이 집단이 반기는 정책을 고려하라."));
            }
            if (session.Indices[Index.Stability] < OpsTuning.FallStability)
                items.Add((2, $"사회 안정 기준 미달 ({session.FallStreak}/{OpsTuning.FallMonths}개월)", "3개월 이어지면 위임 단계가 내려간다."));
            float net = session.PowerPerWeek - session.UpkeepPerWeek;
            if (net < 0f)
                items.Add((net < -5f ? 2 : 1, $"에너지 적자 ({net:0.#}/주)", "바닥나면 강도가 가장 높은 정책부터 자동으로 내려간다. 발전소를 짓거나 강도를 낮춰라."));
            foreach (var id in session.Active.Keys)
            {
                float h = session.Habituation(id);
                if (h < 0.8f)
                    items.Add((1, $"'{Policies.Get(id).Name}' 효과 약화 ({h * 100f:0}%)", "주민들이 통제에 익숙해졌다. 유도 정책으로 바꾸는 것을 고려하라."));
            }
            foreach (var link in session.ActiveLinks.Where(l => l.Conflict))
                items.Add((1, $"정책 충돌: {link.Name}", $"'{Policies.Get(link.A).Name}'과(와) '{Policies.Get(link.B).Name}'이(가) 서로를 방해한다."));
            float gap = session.Indices[Index.AutonomyFelt] - session.Indices[Index.AutonomyReal];
            if (session.Indices[Index.AutonomyFelt] < 40f)
                items.Add((1, "주민이 통제를 체감하고 있다", "자율성(인지)이 낮다. 보이는 통제가 많을수록 반발이 커진다."));
            if (items.Count == 0)
                items.Add((0, "특이 사항 없음", gap > 30f ? "주민들은 스스로 선택하고 있다고 느낀다." : "구역은 평온하다."));
            foreach (var (level, title, detail) in items.OrderByDescending(i => i.level))
            {
                var row = Add(card, "warning");
                row.AddToClassList(level == 2 ? "warning--bad" : level == 1 ? "warning--warn" : "warning--ok");
                row.Add(new LineIcon(level == 0 ? IconKind.Info : IconKind.Alert, level == 2 ? Bad : level == 1 ? Caution : Good));
                var text = Add(row, "warning-text");
                Text(text, title, "warning-title");
                Text(text, detail, "warning-detail");
            }
            RenderWatchSide(body);
        }

        void RenderPrediction(VisualElement body)
        {
            var main = Add(body, "pg-main pg-main--split");
            AnalysisCard(main, AnalysisKind.Discontent, "집단마다 무엇이 만족을 끌어내리고 올리는지 정책 단위로 분해한다.", card =>
            {
                foreach (var (group, sat, causes) in session.Discontent())
                {
                    var block = Add(card, "cause-block");
                    var head = Add(block, "cause-head");
                    Text(head, Society.Get(group).Name, "cause-name");
                    Toned(head, $"{sat:0}", sat, "cause-value");
                    if (causes.Count == 0)
                        Text(block, "영향을 주는 정책 없음", "ops-note");
                    foreach (var (name, push) in causes)
                    {
                        var row = Add(block, "cause-row");
                        Text(row, name, "cause-policy");
                        DeltaLabel(row, push, "delta", unit: "/주");
                    }
                }
            });
            AnalysisCard(main, AnalysisKind.Forecast, "앞으로 30일 동안 일어날 가능성이 높은 사건을 계산한다.", card =>
            {
                foreach (var (def, chance) in session.ForecastEvents().Take(8))
                {
                    var row = Add(card, "forecast-event");
                    Art(row, EventArt(def.Kind), "forecast-event-art");
                    var text = Add(row, "forecast-event-text");
                    Text(text, def.Title, "forecast-event-title");
                    Text(text, OpsEvents.KindName(def.Kind) + (Society.EventGroups.TryGetValue(def.Id, out var g) ? $" · {Society.Get(g).Name}" : ""), "forecast-event-kind");
                    MiniBar(text, chance * 100f, chance > 0.5f ? Bad : chance > 0.2f ? Caution : Mid);
                    Text(row, $"{chance * 100f:0}%", "forecast-event-chance");
                }
            });
        }

        void AnalysisCard(VisualElement parent, AnalysisKind kind, string description, Action<VisualElement> results)
        {
            var card = Card(parent, "analysis-card", kind == AnalysisKind.Discontent ? IconKind.People : IconKind.Gauge,
                OpsSession.AnalysisName(kind));
            Text(card, description, "detail-text");
            if (session.HasAnalysis(kind))
            {
                Text(card, $"{session.Date:yyyy년 M월} 분석 결과 · 이번 달 동안 갱신된다", "pill pill--on");
                var scroll = Scroll(card, "analysis-results");
                results(scroll.contentContainer);
            }
            else
            {
                var foot = Add(card, "panel-foot");
                Text(foot, $"정보 {OpsSession.AnalysisCost(kind):0} 소모 · 보유 {session.Info:0}", "ops-note");
                var button = MakeButton(foot, "분석 실행", () =>
                {
                    if (session.Analyze(kind))
                        sfx.Play(Cue.Reveal);
                    else
                        sfx.Play(Cue.Deny);
                    Refresh();
                    RenderPage();
                }, "kbtn");
                button.SetEnabled(session.Info >= OpsSession.AnalysisCost(kind));
            }
        }

        // ------------------------------------------------------------------ report sub-pages

        void RenderReportCitizens(VisualElement body, MonthlyReport r)
        {
            var main = Add(body, "pg-main");
            var pop = Card(main, "report-pop", IconKind.People, "인구");
            var row = Add(pop, "hero-row");
            Text(row, Population(r.PopulationAfter), "hero-value");
            DeltaLabel(row, r.PopulationAfter - r.PopulationBefore, "delta delta--big", unit: "명");
            var groups = Card(main, "report-groups", IconKind.Smile, "집단별 만족도 변화");
            for (int g = 0; g < Math.Min(r.GroupsAfter.Length, r.GroupsBefore.Length); g++)
            {
                var line = Add(groups, "bar-row");
                Text(line, Society.GroupDefs[g].Name, "bar-row-name");
                MiniBar(line, r.GroupsAfter[g], ToneColor(r.GroupsAfter[g]), "mini-bar mini-bar--wide");
                Text(line, $"{r.GroupsBefore[g]:0} → {r.GroupsAfter[g]:0}", "bar-row-value bar-row-value--wide");
                DeltaLabel(line, r.GroupsAfter[g] - r.GroupsBefore[g], "delta bar-row-delta");
            }
        }

        void RenderReportPolicies(VisualElement body, MonthlyReport r)
        {
            var main = Add(body, "pg-main");
            var card = Card(main, "report-policies", IconKind.Policy, "정책 성과 (이번 달 누적 효과)");
            if (r.Effects.Count == 0)
            {
                Text(card, "이번 달 시행된 정책이 없다.", "ops-note");
                return;
            }
            var header = Add(card, "table-row table-row--head");
            Text(header, "정책", "table-cell table-cell--name");
            foreach (var index in IndexNames.All)
                Text(header, IndexNames.Short(index), "table-cell table-cell--num");
            foreach (var pair in r.Effects.OrderByDescending(p => p.Value.Happiness + p.Value.Stability + p.Value.Cohesion))
            {
                bool link = pair.Key.StartsWith("link:");
                var name = link ? "[연계] " + pair.Key.Substring(5) : Policies.Get(pair.Key)?.Name ?? pair.Key;
                var line = Add(card, "table-row");
                Text(line, name, "table-cell table-cell--name");
                foreach (var index in IndexNames.All)
                {
                    float v = pair.Value[index];
                    var cell = Text(line, Math.Abs(v) < 0.05f ? "–" : $"{(v > 0 ? "+" : "")}{v:0.#}", "table-cell table-cell--num");
                    cell.EnableInClassList("delta--up", Math.Abs(v) >= 0.05f && IsGood(index, v));
                    cell.EnableInClassList("delta--down", Math.Abs(v) >= 0.05f && !IsGood(index, v));
                }
            }
            Text(card, "초록은 운영에 이로운 변화, 빨강은 해로운 변화다. 실제 자율성 감소는 운영의 진척으로 본다.", "ops-note");
        }
    }
}
