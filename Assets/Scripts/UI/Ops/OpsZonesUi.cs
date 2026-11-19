using System;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Index = Sector.Ops.Index;
using Random = UnityEngine.Random;

namespace Sector.UI
{
    // The city's six zones on screen: tension stages, anomalies caught on camera and the AI's answers, forecast
    // crises, zone details and placing facilities in zones.
    public partial class OpsScreen
    {
        static Color StageColor(int stage) => stage >= 3 ? Bad : stage == 2 ? Caution : Palette.Cyan;

        static string StageClass(int stage) => stage >= 3 ? "state-tag--bad" : stage == 2 ? "state-tag--warn" : "";

        void SetStage(Label label, int zone)
        {
            int stage = session.TensionStage(zone);
            label.text = Tension.StageNames[stage];
            label.style.visibility = Visibility.Visible;
            label.EnableInClassList("state-tag--bad", stage >= 3);
            label.EnableInClassList("state-tag--warn", stage == 2);
        }

        // ------------------------------------------------------------------ anomalies

        void OnAnomalySpotted(Anomaly anomaly)
        {
            RefreshMenu();
            if (anomaly.Kind != AnomalyKind.Spark)
                return;
            var spark = session.SparkIn(anomaly.Zone);
            var def = spark == null ? null : OpsEvents.Get(spark.EventId);
            sfx?.Play(Cue.Alert, 0.6f);
            Banner(BannerKind.Bad, $"계기 발생 · {Society.Zones[anomaly.Zone].Name}", anomaly.Text,
                $"{Tension.SparkDays}일 뒤 '{def?.Title}' 예상 · 관찰 시스템에서 막을 수 있다");
        }

        void RenderAnomalyList(VisualElement parent)
        {
            Text(parent, $"이상 징후 {session.Anomalies.Count}건", "section-title");
            if (session.Anomalies.Count == 0)
            {
                Text(parent, "지금은 카메라에 잡힌 이상 행동이 없다. 긴장이 높은 구역일수록 자주 나타난다.", "ops-note");
                return;
            }
            foreach (var a in session.Anomalies.OrderByDescending(a => a.Kind).ThenBy(a => a.Expire))
            {
                var item = Add(parent, "anomaly-item anomaly-item--" + a.Kind.ToString().ToLowerInvariant());
                var head = Add(item, "anomaly-head");
                Text(head, Tension.KindName(a.Kind), "anomaly-kind");
                Text(head, Society.Zones[a.Zone].Name, "anomaly-zone");
                Text(head, $"D-{Math.Max(0, a.Expire - session.Day)}", "anomaly-due num");
                Text(item, a.Text, "anomaly-text");
                var anomaly = a;
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    sfx.Play(Cue.Select);
                    OpenAnomaly(anomaly);
                });
            }
        }

        // Look at what the camera caught: why it happens, how many share it, and what the AI could do.
        void OpenAnomaly(Anomaly a)
        {
            if (overlayOpen || !session.Anomalies.Contains(a))
                return;
            if (sim.Zone != a.Zone)
                sim.ShowZone(a.Zone);
            watchCamera = true;
            if (a.Subject == null || !sim.Flag(a.Subject))
                sim.FlagSample();
            bool wasPaused = paused;
            paused = true;
            RefreshTime();
            var zone = Society.Zones[a.Zone];
            var body = OpenOverlay($"이상 행동 분석 · {zone.Name}", "ops-modal--narrow anomaly-modal", onClose: () =>
            {
                paused = wasPaused;
                RefreshTime();
            });
            var kicker = Add(body, "anomaly-modal-head");
            Text(kicker, Tension.KindName(a.Kind), "anomaly-kind");
            Text(kicker, $"CAM-{a.Zone + 1:00} · {OpsSession.Start.AddDays(a.Day):MM.dd} 포착", "ops-note");
            Text(body, a.Text, "ops-para");
            var table = Add(body, "effect-table");
            void Row(string name, string value, string tone = null)
            {
                var row = Add(table, "effect-row");
                Text(row, name, "effect-name");
                Text(row, value, tone == null ? "effect-value" : "info-value " + tone);
            }
            if (a.Subject != null)
                Row("대상", $"{Residents.Get(a.Subject)?.Name} · 입장 {OpsSession.StanceLabel(session.Dominant(a.Subject))}");
            float sat = session.GroupSat[(int)a.Group];
            Row("집단", $"{Society.Get(a.Group).Name} · 만족 {sat:0}", ToneClass(sat));
            // The AI's guess at what happens next, from the zone's tension (shown, never certain).
            int join = Mathf.Clamp(Mathf.RoundToInt(session.ZoneTension[a.Zone] * 0.6f + (int)a.Kind * 8f), 5, 95);
            Row("예측", $"모임 참여 확률 {join}%" + (Random.value < 0.12f ? " · 일부 예측 실패 (인식 불가)" : ""));
            Row("주된 원인", a.Cause);
            Row("같은 고민", $"약 {a.People}명");
            float trend = session.TensionTrend(a.Zone);
            Row("구역 긴장", $"{session.ZoneTension[a.Zone]:0} · {Tension.StageNames[session.TensionStage(a.Zone)]} · 주당 {(trend >= 0 ? "+" : "")}{trend:0.#}",
                session.TensionStage(a.Zone) >= 3 ? "tone--bad" : session.TensionStage(a.Zone) == 2 ? "tone--warn" : null);
            var spark = session.SparkIn(a.Zone);
            if (a.Kind == AnomalyKind.Spark && spark != null)
                Row("예상", $"{Math.Max(0, spark.DueDay - session.Day)}일 뒤 '{OpsEvents.Get(spark.EventId)?.Title}'", "tone--bad");
            Text(body, "대응은 선택이다. 그냥 두어도 불이익은 없지만, 긴장은 계속 쌓인다.", "ops-note");
            Text(body, "개인정보 수집 동의: 동의함 (기본값)", "fine-print");

            var options = Add(body, "ops-options");
            var responses = Tension.ResponsesFor(a.Kind);
            for (int i = 0; i < responses.Count; i++)
            {
                int pick = i;
                var r = responses[i];
                var button = new Button(() => AnswerAnomaly(a, pick, wasPaused));
                button.AddToClassList("ops-option");
                button.AddToClassList("ops-option--" + r.Tag.ToString().ToLowerInvariant());
                var head = Add(button, "ops-option-head");
                Text(head, $"[{OpsEvents.TagName(r.Tag)}]", "ops-tag");
                Text(head, r.Label, "ops-option-label");
                var gains = $"긴장 {r.Tension:0} · 정보 +{r.Info:0}";
                if (Math.Abs(r.Group) > 0.1f)
                    gains += $" · {Society.Get(a.Group).Name} 만족 {(r.Group > 0 ? "+" : "")}{r.Group:0}";
                var effect = r.Effect.Describe();
                if (effect.Length > 0)
                    gains += " · " + effect;
                if (r.Defuses)
                    gains += " · 계기 해소";
                if (r.Energy > 0f)
                    gains += $" · 에너지 {r.Energy:0}";
                bool can = session.CanRespond(a, r, out var reason);
                Text(button, can ? gains : reason, can ? "ops-card-foot" : "ops-reason");
                button.SetEnabled(can);
                options.Add(button);
            }
        }

        void AnswerAnomaly(Anomaly a, int pick, bool wasPaused)
        {
            var result = session.Respond(a.Id, pick);
            if (result == null)
            {
                sfx.Play(Cue.Deny);
                return;
            }
            sfx.Play(Cue.Choose);
            var body = OpenOverlay("관찰 대응", "ops-modal--narrow", closable: false);
            Text(body, result, "ops-para");
            Text(body, $"{Society.Zones[a.Zone].Name} 긴장 {session.ZoneTension[a.Zone]:0} · {Tension.StageNames[session.TensionStage(a.Zone)]}", "ops-note");
            var row = Add(body, "ops-row");
            Add(row, "spacer");
            MakeButton(row, "확인", () =>
            {
                CloseOverlay();
                paused = wasPaused;
                RefreshTime();
                RenderPage();
            }, "ops-primary");
            Refresh();
        }

        // ------------------------------------------------------------------ forecast crises

        void OnCrisisWarned(CrisisDef crisis, CrisisBeat beat)
        {
            int days = session.CrisisDay(crisis) - session.Day;
            Banner(BannerKind.Core, $"예보 · {beat.Channel}", crisis.Title, $"{beat.Text}\nD-{days} · 대비: {crisis.Prepare}", 5f);
            sfx?.Play(Cue.Reveal, 0.6f);
        }

        void RenderCrisisTracker(VisualElement parent)
        {
            var crisis = session.NextCrisis;
            if (crisis == null)
                return;
            float ready = crisis.Readiness(session);
            var item = Add(parent, "tracker tracker--crisis");
            item.tooltip = "눌러서 예보 다시 보기";
            item.RegisterCallback<ClickEvent>(_ =>
            {
                sfx.Play(Cue.Select);
                ShowCrisis(crisis);
            });
            var head = Add(item, "tracker-head");
            Text(head, "예보", "goal-src");
            Text(head, crisis.Title, "tracker-title");
            Text(head, $"D-{Math.Max(0, session.CrisisDay(crisis) - session.Day)}", "tracker-due num");
            Text(head, "›", "tracker-more");
            var row = Add(item, "goal-progress");
            var bar = Add(row, "mini-bar mini-bar--wide");
            var fill = Add(bar, "mini-fill");
            fill.style.width = Length.Percent(ready * 100f);
            fill.style.backgroundColor = ready >= 0.7f ? Good : ready >= 0.4f ? Caution : Bad;
            Text(row, $"준비도 {ready * 100f:0}%", "goal-progress-text num");
        }

        void ShowCrisis(CrisisDef crisis)
        {
            if (overlayOpen)
                return;
            bool wasPaused = paused;
            paused = true;
            RefreshTime();
            var body = OpenOverlay($"예보된 위기 · {crisis.Title}", "ops-modal--narrow", onClose: () =>
            {
                paused = wasPaused;
                RefreshTime();
            });
            int days = session.CrisisDay(crisis) - session.Day;
            Text(body, $"{crisis.Date:yyyy.MM.dd} 예상 · D-{Math.Max(0, days)} · {(crisis.Zone >= 0 ? Society.Zones[crisis.Zone].Name + "에서 시작" : "도시 전체")}", "ops-note");
            Text(body, "지금까지의 조짐", "section-title");
            for (int i = 0; i < crisis.Beats.Count; i++)
            {
                bool seen = session.CrisisBeatsSeen.Contains(crisis.Id + ":" + i);
                var beat = crisis.Beats[i];
                var row = Add(body, "crisis-beat" + (seen ? "" : " crisis-beat--hidden"));
                Text(row, seen ? beat.Channel : "?", "crisis-channel");
                Text(row, seen ? beat.Text : $"D-{beat.DaysBefore}에 다음 조짐", "crisis-text");
            }
            float ready = crisis.Readiness(session);
            Text(body, "대비", "section-title");
            Text(body, crisis.Prepare, "ops-para");
            var bar = Add(body, "mini-bar mini-bar--wide gauge-bar");
            var fill = Add(bar, "mini-fill");
            fill.style.width = Length.Percent(ready * 100f);
            fill.style.backgroundColor = ready >= 0.7f ? Good : ready >= 0.4f ? Caution : Bad;
            Text(body, $"준비도 {ready * 100f:0}% · 준비가 잘 될수록 위기의 피해가 줄어든다 (완전 대비 시 30%, 무대비 시 130%).", "ops-note");
        }

        // ------------------------------------------------------------------ zone detail (observation · 구역 현황)

        void RenderZoneDetail(VisualElement parent, int zone)
        {
            var def = Society.Zones[zone];
            var card = Card(parent, "zone-detail", IconKind.Pin, $"{def.Name} · {def.Kind}");
            Text(card, def.Trait, "detail-text");
            var head = Add(card, "zone-detail-head");
            var stage = Text(head, "", "state-tag");
            SetStage(stage, zone);
            float trend = session.TensionTrend(zone);
            Text(head, $"긴장 {session.ZoneTension[zone]:0}", "zone-detail-value num");
            DeltaLabel(head, -trend, "delta", unit: "/주");
            var bar = Add(card, "mini-bar mini-bar--wide gauge-bar");
            var fill = Add(bar, "mini-fill");
            fill.style.width = Length.Percent(session.ZoneTension[zone]);
            fill.style.backgroundColor = StageColor(session.TensionStage(zone));
            Text(card, "긴장을 움직이는 것 (주당)", "section-title");
            foreach (var (name, value) in session.TensionCauses(zone).Take(8))
            {
                var row = Add(card, "cause-row");
                Text(row, name, "cause-name");
                var v = Text(row, $"{(value > 0 ? "+" : "")}{value * def.Spread:0.0}", "cause-value num");
                v.AddToClassList(value > 0 ? "tone--bad" : "tone-good");
            }
            Text(card, $"어울리는 시설 (+50%): {string.Join(", ", def.Affinity.Select(id => Facilities.Get(id)?.Name))}", "ops-note");
            var built = Enumerable.Range(0, session.Built.Count).Where(i => session.BuiltZone[i] == zone)
                .Select(i => Facilities.Get(session.Built[i]).Name).ToList();
            Text(card, $"이 구역의 시설 {built.Count}/{session.ZoneSlots}: {(built.Count == 0 ? "없음" : string.Join(", ", built))}", "ops-note");
            var people = Residents.Cast.Where(r => Society.HomeOf(r.Id) == zone).Select(r => r.Name).ToList();
            if (people.Count > 0)
                Text(card, $"사는 고정 인물: {string.Join(", ", people)}", "ops-note");
        }

        // ------------------------------------------------------------------ facilities (built into a zone)

        [NonSerialized] int buildZone;
        [NonSerialized] bool watchCamera;

        void RenderFacility(VisualElement body, VisualElement tools)
        {
            Text(tools, $"건설 부지 {session.Built.Count}/{session.FacilitySlots}", "ops-badge");
            Text(tools, $"에너지 {session.Energy:0}", "pill");
            var scroll = Scroll(body, "page-scroll");
            Text(scroll, "시설은 운영 AI가 에너지로 직접 짓는다. 구역마다 부지가 있고, 그 구역에 어울리는 시설은 효과가 50% 크다. 시설은 들어선 구역의 긴장도 가라앉힌다.", "ops-note");
            var zones = Add(scroll.contentContainer, "build-zones");
            for (int z = 0; z < session.Zones; z++)
            {
                int zone = z;
                var chip = MakeButton(zones, $"{Society.Zones[z].Name}  {session.UsedSlots(z)}/{session.ZoneSlots}", () =>
                {
                    buildZone = zone;
                    sfx.Play(Cue.Click);
                    RenderPage();
                }, "build-zone");
                chip.EnableInClassList("build-zone--on", z == buildZone);
                chip.EnableInClassList("build-zone--full", session.UsedSlots(z) >= session.ZoneSlots);
            }
            Text(scroll, $"{Society.Zones[buildZone].Name}: {Society.Zones[buildZone].Trait}", "detail-text");
            var grid = Add(scroll.contentContainer, "facility-grid");
            foreach (var def in Facilities.All)
            {
                var card = Card(grid, "facility-card", IconKind.Facility, def.Name);
                bool suits = OpsSession.Suits(def.Id, buildZone);
                if (suits)
                    Text(card, "이 구역에 어울림 · 효과 +50%", "facility-suits");
                Text(card, def.Description, "detail-text");
                var table = Add(card, "effect-table");
                void Row(string name, string value)
                {
                    var row = Add(table, "effect-row");
                    Text(row, name, "effect-name");
                    Text(row, value, "effect-value");
                }
                Row("건설 비용", $"에너지 {def.Cost:0}");
                if (def.Upkeep > 0f)
                    Row("유지비", $"{def.Upkeep:0} / 주");
                if (!def.Weekly.IsZero)
                    Row("주간 효과", (def.Weekly * (suits ? 1.5f : 1f)).Describe());
                var homes = Society.Zones.Where(z => z.Affinity.Contains(def.Id)).Select(z => z.Name).ToList();
                if (homes.Count > 0)
                    Row("어울리는 구역", string.Join(", ", homes));
                Row("보유", $"{session.Built.Count(b => b == def.Id)}");
                var foot = Add(card, "panel-foot");
                if (session.CanBuild(def.Id, buildZone, out var reason))
                    MakeButton(foot, $"{Society.Zones[buildZone].Name}에 건설", () =>
                    {
                        session.Build(def.Id, buildZone);
                        sfx.Play(Cue.Assign);
                        Refresh();
                        RenderPage();
                    }, "kbtn");
                else
                    Text(foot, reason, "ops-reason");
            }
            if (session.Built.Count > 0)
            {
                Text(scroll, "건설된 시설", "section-title");
                var list = Add(scroll.contentContainer, "ops-row ops-row--wrap");
                for (int i = 0; i < session.Built.Count; i++)
                {
                    int at = i;
                    var item = Add(list, "ops-built");
                    Text(item, $"{Facilities.Get(session.Built[i]).Name} · {Society.Zones[session.BuiltZone[i]].Name}"
                               + (session.FacilityBoost(i) > 1f ? " +50%" : ""), "ops-built-name");
                    MakeButton(item, "철거", () =>
                    {
                        session.Demolish(at);
                        sfx.Play(Cue.Click);
                        Refresh();
                        RenderPage();
                    }, "ops-small");
                }
            }
        }
    }
}
