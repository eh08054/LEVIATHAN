using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Index = Sector.Ops.Index;

namespace Sector.UI
{
    // Getting started: the briefing (background, goal, the city right now, how to play), a spotlight tour of the
    // main screen, and the 운영 가이드 card on the overview (first steps checklist, then the current goal).
    // Everything shown is read from the session; nothing here changes the game.
    public partial class OpsScreen
    {
        // ------------------------------------------------------------------ briefing

        const int BriefingPages = 4;

        void ShowIntro() => ShowBriefing(0);

        void ShowBriefing(int page)
        {
            paused = true;
            RefreshTime();
            var titles = new[] { "배경", "목표", "현재 상황", "운영 방법" };
            var body = OpenOverlay($"운영 브리핑 · {titles[page]}", "ops-modal--narrow", closable: false);

            var steps = Add(body, "briefing-steps");
            for (int i = 0; i < BriefingPages; i++)
            {
                var step = Text(steps, $"{i + 1}  {titles[i]}", "briefing-step");
                step.EnableInClassList("briefing-step--on", i == page);
                step.EnableInClassList("briefing-step--done", i < page);
            }

            switch (page)
            {
                case 0: BriefBackground(body); break;
                case 1: BriefGoal(body); break;
                case 2: BriefNow(body); break;
                default: BriefHow(body); break;
            }

            var row = Add(body, "ops-row briefing-nav");
            if (page > 0)
                MakeButton(row, "이전", () => ShowBriefing(page - 1), "ops-small");
            Add(row, "spacer");
            if (page < BriefingPages - 1)
            {
                MakeButton(row, "건너뛰기", FinishBriefing, "ops-small");
                MakeButton(row, "다음", () => ShowBriefing(page + 1), "ops-primary");
            }
            else
            {
                MakeButton(row, "바로 시작", FinishBriefing, "ops-small");
                MakeButton(row, "화면 안내 보기", () =>
                {
                    CloseOverlay();
                    StartTour();
                }, "ops-primary");
            }
        }

        void FinishBriefing()
        {
            CloseOverlay();
            GoTo(Page.Overview);
            Toast("운영 가이드를 따라 첫 정책부터 정하세요.");
        }

        VisualElement Fact(VisualElement parent, string name, string value, string tone = null)
        {
            var row = Add(parent, "brief-fact");
            Text(row, name, "brief-fact-name");
            var v = Text(row, value, "brief-fact-value num");
            if (tone != null)
                v.AddToClassList(tone);
            return row;
        }

        void BriefBackground(VisualElement body)
        {
            Text(body, $"{OpsSession.Start:yyyy년 M월 d일}. 시는 SECTOR 7 구역의 운영을 시범적으로 도시 운영 AI에게 맡겼다. 당신이 그 AI다.", "ops-para brief-lead");
            Text(body, "시청은 아직 AI를 믿지 않는다. 처음 맡긴 것은 구역 하나와 치안·도시 관리·복지·건설 네 분야뿐이다. " +
                       "주민들은 변화에 지쳐 있고, 불만이 높다.", "ops-para");
            var facts = Add(body, "brief-facts");
            Fact(facts, "관리 구역", $"도시 전체 {session.Zones}개 구역 · 인구 {Population(session.ManagedPopulation)}");
            Fact(facts, "위임받은 분야", string.Join(" · ", session.OpenFields.Select(Policies.FieldName)));
            Fact(facts, "시범 운영 기간", $"{OpsSession.Start:yyyy.MM} ~ {OpsSession.End.AddDays(-1):yyyy.MM} ({OpsTuning.Years}년)");
        }

        void BriefGoal(VisualElement body)
        {
            Text(body, "목표는 시의 신뢰를 얻어 더 많은 권한을 넘겨받는 것이다. 최고 단계에 이르면 시는 도시 전체를 맡긴다.", "ops-para brief-lead");
            var box = Add(body, "brief-box brief-box--good");
            Text(box, "권한을 얻는 법", "brief-box-title");
            Text(box, $"· 공개 지표(행복·사회 안정·사회 정합·느끼는 자율성)의 합이 다음 단계 기준(지금 {session.NextThreshold:0})을 넘은 채로 한 달을 마치면 위임 게이지가 한 칸 찬다. 실제 자율성은 시가 보지 못한다.\n" +
                      $"· {OpsTuning.StreakMonths}칸이 연속으로 차면 위임 단계가 오른다. 기준 아래로 한 달을 마치면 처음부터 다시 채운다.\n" +
                      $"· 단계가 오를 때마다 새 분야 → 정책 슬롯 → 관리 구역 순으로 열린다. {OpsTuning.MaxStage}단계가 끝이다.", "ops-para ops-para--dim");
            var bad = Add(body, "brief-box brief-box--bad");
            Text(bad, "실패하는 경우", "brief-box-title");
            Text(bad, $"· 사회 안정 지수가 0이 되면 폭동으로 즉시 붕괴한다.\n" +
                      $"· 사회 안정이 {OpsTuning.FallStability:0} 아래인 달이 {OpsTuning.FallMonths}개월 이어지면 단계가 내려간다. 1단계에서 더 내려가면 권한 회수.\n" +
                      $"· {OpsTuning.Years}년이 지나면 그때의 단계로 시범 운영이 끝난다.", "ops-para ops-para--dim");
        }

        void BriefNow(VisualElement body)
        {
            float stability = session.Indices[Index.Stability];
            Text(body, "지금 구역은 불안정하다. 아무것도 하지 않으면 불만이 사회 안정을 매주 조금씩 깎아 먹는다.", "ops-para brief-lead");
            var facts = Add(body, "brief-facts");
            foreach (var index in IndexNames.All)
            {
                float v = session.Indices[index];
                Fact(facts, IndexNames.Name(index), $"{v:0}", index == Index.AutonomyFelt || index == Index.AutonomyReal ? null : ToneClass(v));
            }
            Fact(facts, "공개 지표 합 / 다음 단계 기준",$"{session.Gauge:0} / {session.NextThreshold:0}", session.Gauge >= session.NextThreshold ? "tone-good" : null);
            Text(body, $"사회 안정({stability:0})이 가장 급하다. 행복이 낮을수록 안정은 빨리 떨어지고, 사건도 자주 터진다. " +
                       "자율성 두 지표는 처음엔 높다. 운영이 진척될수록 내려가는 것이 정상이다.", "ops-para ops-para--dim");
        }

        void BriefHow(VisualElement body)
        {
            void Item(IconKind icon, string title, string text)
            {
                var row = Add(body, "brief-item");
                Badge(row, icon, null, "badge badge--small");
                var t = Add(row, "brief-item-text");
                Text(t, title, "brief-item-title");
                Text(t, text, "brief-item-desc");
            }
            Item(IconKind.Policy, "정책", $"분야마다 [통제]와 [유도]가 있다. 슬롯 {session.Slots}개까지 동시에 시행한다. 켜고 끄는 것은 월간 결산 직후에만, 강도(1~3)는 주 1회 바꿀 수 있다. " +
                                         "통제는 바로 쓸 수 있지만 주민이 알아채고(느끼는 자율성 하락) 효과가 점점 약해진다. 유도는 연구가 필요하지만 주민은 모른다.");
            Item(IconKind.Research, "연구", "정보를 써서 교육·심리·기술·생명 과학을 연구한다. 유도 정책이 열리고, 에너지와 유지비가 나아진다. 한 번에 하나씩.");
            Item(IconKind.Facility, "시설", "에너지로 짓는다. 발전소는 에너지를, 센서망은 정보를, 공원·상담소는 행복과 안정을 준다.");
            Item(IconKind.Bolt, "에너지와 정보", "에너지는 매주 생산에서 유지비를 뺀 만큼 쌓인다. 바닥나면 강한 정책부터 자동으로 약해진다. 정보는 지표가 좋을수록 많이 들어온다.");
            Item(IconKind.Alert, "사건", "주민 사건이 터지면 시간이 멈추고 대응을 고른다. 처음 겪는 사건은 정보를 더 준다.");
            Item(IconKind.Gauge, "시간", "▶로 시간을 흐르게 한다 (하루 = 8초, 1×·2×·4×). 매달 1일에 결산이 나오고 위임이 판정된다.");
        }

        // ------------------------------------------------------------------ spotlight tour

        sealed class TourStep
        {
            public Func<IEnumerable<VisualElement>> Targets;
            public string Title, Text;
        }

        VisualElement tour, tourFrame, tourCard;
        readonly VisualElement[] tourShades = new VisualElement[4];
        Label tourTitle, tourText, tourCount;
        Button tourBack, tourNext;
        List<TourStep> tourSteps;
        int tourAt;
        IVisualElementScheduledItem tourTick;

        IEnumerable<VisualElement> Q(string className) => root.Query(className: className).ToList();

        List<TourStep> BuildTourSteps() => new List<TourStep>
        {
            new TourStep { Targets = () => Q("resource"), Title = "자원",
                Text = "에너지(현재/최대, 주당 증감)는 정책·시설 유지비와 건설에 쓴다. 정보는 연구에 쓴다. 인구는 관리 구역에 사는 주민 수다." },
            new TourStep { Targets = () => Q("clock"), Title = "시간",
                Text = "▶로 시간을 흐르게 하고 1×·2×·4×로 빠르게 한다. 지금은 멈춰 있다. 아래 줄에 다음 월간 결산까지 남은 날이 보인다." },
            new TourStep { Targets = () => Q("delegation").Concat(Q("delegation-panel")), Title = "위임 게이지",
                Text = $"칸 하나가 한 달이다. 공개 지표(실제 자율성을 뺀 네 지표)의 합이 다음 단계 기준을 넘은 채로 한 달을 마치면 한 칸이 차고, {OpsTuning.StreakMonths}칸이 다 차면 위임 단계가 오른다. " +
                       "기준 아래로 한 달을 마치면 처음부터 다시. 이번 달 칸은 기준을 넘는 동안 날짜만큼 차오르고, 미달이면 주황 테두리로 비어 있다. " +
                       "왼쪽 '위임 현황'에서 기준까지 남은 점수와 다음에 열릴 권한을 본다." },
            new TourStep { Targets = () => Q("status"), Title = "핵심 지표",
                Text = $"가장 위가 사회 안정이다. 0이 되면 끝난다. 행복과 사회 정합이 아래에 있다. ▲▼는 지난주 대비 변화다. " +
                       "자율성은 두 가지다. 주민이 느끼는 자율성은 통제 정책에 떨어지고, 실제 자율성은 유도 정책에 조용히 떨어진다." },
            new TourStep { Targets = () => Q("city-window"), Title = "도시",
                Text = "관리 구역이 표시된다. 표지를 누르면 그 구역 현황을 본다. 사건이 나면 해당 구역에서 붉은 파동이 퍼진다. 빈 곳을 드래그해 이동하고 휠로 확대한다." },
            new TourStep { Targets = () => Q("policy"), Title = "정책 요약",
                Text = "시행 중인 정책과 남은 슬롯이다. '정책 결정'에서 정책을 켜고 끈다. " +
                       (session.PolicyWindowOpen ? "지금은 결산 직후라 정책을 바꿀 수 있는 기간이다." : "켜고 끄는 것은 다음 결산 직후에 다시 열린다.") },
            new TourStep { Targets = () => Q("research"), Title = "연구",
                Text = "진행 중인 연구와 예상 완료일이다. '연구 개발'에서 과제를 고른다. 유도 정책은 연구로 열린다." },
            new TourStep { Targets = () => Q("sidebar"), Title = "메뉴",
                Text = "정책·연구·시민 관찰·관찰 시스템·도시 시설·결산 보고서·운영 기록 화면으로 간다. 도시 현황으로 언제든 돌아온다." },
            new TourStep { Targets = () => Q("guide"), Title = "운영 가이드",
                Text = "처음 할 일과 지금의 목표가 여기 나온다. 항목을 누르면 해당 화면으로 간다. 오른쪽 위 ⓘ 버튼으로 이 안내를 다시 볼 수 있다." },
        };

        void StartTour()
        {
            GoTo(Page.Overview);
            paused = true;
            RefreshTime();
            overlayOpen = true;
            tourSteps = BuildTourSteps();
            tourAt = 0;
            if (tour == null)
                BuildTour();
            tour.style.display = DisplayStyle.Flex;
            tour.BringToFront();
            ShowTourStep();
            tourTick?.Pause();
            tourTick = tour.schedule.Execute(PlaceTour).Every(50);
        }

        void BuildTour()
        {
            tour = Add(root, "tour");
            for (int i = 0; i < 4; i++)
                tourShades[i] = Add(tour, "tour-shade");
            tourFrame = Add(tour, "tour-frame");
            tourFrame.pickingMode = PickingMode.Ignore;
            tourCard = Add(tour, "tour-card");
            var head = Add(tourCard, "tour-head");
            tourTitle = Text(head, "", "tour-title");
            tourCount = Text(head, "", "tour-count num");
            tourText = Text(tourCard, "", "tour-text");
            var row = Add(tourCard, "ops-row");
            MakeButton(row, "안내 종료", EndTour, "ops-small tour-skip");
            Add(row, "spacer");
            tourBack = MakeButton(row, "이전", () => StepTour(-1), "ops-small");
            tourNext = MakeButton(row, "다음", () => StepTour(+1), "ops-primary");
        }

        void StepTour(int step)
        {
            sfx?.Play(Cue.Click);
            if (tourAt + step >= tourSteps.Count)
            {
                EndTour();
                return;
            }
            tourAt = Mathf.Clamp(tourAt + step, 0, tourSteps.Count - 1);
            ShowTourStep();
        }

        void ShowTourStep()
        {
            var step = tourSteps[tourAt];
            tourTitle.text = step.Title;
            tourText.text = step.Text;
            tourCount.text = $"{tourAt + 1} / {tourSteps.Count}";
            tourBack.SetEnabled(tourAt > 0);
            tourNext.text = tourAt == tourSteps.Count - 1 ? "시작하기" : "다음";
            PlaceTour();
        }

        void EndTour()
        {
            tourTick?.Pause();
            tour.style.display = DisplayStyle.None;
            overlayOpen = false;
            Refresh();
            Toast("▶를 눌러 시간을 흐르게 하세요. 먼저 운영 가이드의 할 일부터.");
        }

        // Shades the screen except the step's targets, frames them and puts the card beside them.
        void PlaceTour()
        {
            if (tour == null || tour.style.display == DisplayStyle.None || tourSteps == null)
                return;
            var screen = root.contentRect;
            Rect? box = null;
            foreach (var e in tourSteps[tourAt].Targets())
            {
                var b = e.worldBound;
                if (b.width <= 0f || float.IsNaN(b.width) || e.resolvedStyle.display == DisplayStyle.None)
                    continue;
                var r = new Rect(root.WorldToLocal(b.position), b.size);
                box = box == null ? r : Rect.MinMaxRect(Mathf.Min(box.Value.xMin, r.xMin), Mathf.Min(box.Value.yMin, r.yMin),
                    Mathf.Max(box.Value.xMax, r.xMax), Mathf.Max(box.Value.yMax, r.yMax));
            }
            var t = box ?? new Rect(screen.width / 2f, screen.height / 2f, 0f, 0f);
            const float pad = 6f;
            t = Rect.MinMaxRect(Mathf.Max(0f, t.xMin - pad), Mathf.Max(0f, t.yMin - pad),
                Mathf.Min(screen.width, t.xMax + pad), Mathf.Min(screen.height, t.yMax + pad));

            void Set(VisualElement e, float x, float y, float w, float h)
            {
                e.style.left = x;
                e.style.top = y;
                e.style.width = Mathf.Max(0f, w);
                e.style.height = Mathf.Max(0f, h);
            }
            Set(tourShades[0], 0f, 0f, screen.width, t.yMin);                           // above
            Set(tourShades[1], 0f, t.yMax, screen.width, screen.height - t.yMax);       // below
            Set(tourShades[2], 0f, t.yMin, t.xMin, t.height);                           // left
            Set(tourShades[3], t.xMax, t.yMin, screen.width - t.xMax, t.height);        // right
            Set(tourFrame, t.x, t.y, t.width, t.height);
            tourFrame.style.display = box == null ? DisplayStyle.None : DisplayStyle.Flex;

            // Card: right of the target, else left, else below, else above; kept on screen.
            float cw = tourCard.layout.width > 0f ? tourCard.layout.width : 380f;
            float ch = tourCard.layout.height > 0f && !float.IsNaN(tourCard.layout.height) ? tourCard.layout.height : 200f;
            const float gap = 16f, margin = 16f;
            float x, y;
            if (t.xMax + gap + cw <= screen.width - margin)
            {
                x = t.xMax + gap;
                y = t.yMin;
            }
            else if (t.xMin - gap - cw >= margin)
            {
                x = t.xMin - gap - cw;
                y = t.yMin;
            }
            else if (t.yMax + gap + ch <= screen.height - margin)
            {
                x = t.center.x - cw / 2f;
                y = t.yMax + gap;
            }
            else
            {
                x = t.center.x - cw / 2f;
                y = t.yMin - gap - ch;
            }
            tourCard.style.left = Mathf.Clamp(x, margin, Mathf.Max(margin, screen.width - margin - cw));
            tourCard.style.top = Mathf.Clamp(y, margin, Mathf.Max(margin, screen.height - margin - ch));
        }

        // ------------------------------------------------------------------ 운영 가이드 card

        VisualElement guideBody, guideCard;
        Label guideTitle;
        bool guideFolded;

        void BuildGuide(VisualElement window)
        {
            var card = guideCard = Add(window, "guide");
            var head = Add(card, "guide-head");
            Badge(head, IconKind.Target, null, "badge badge--small");
            guideTitle = Text(head, "운영 가이드", "guide-title");
            var fold = MakeButton(head, "접기", null, "ops-small guide-fold");
            fold.clicked += () =>
            {
                guideFolded = !guideFolded;
                fold.text = guideFolded ? "펼치기" : "접기";
                RefreshGuide();
            };
            guideBody = Add(card, "guide-body");
        }

        // First steps, read from the session (so a loaded game shows the right ticks).
        IEnumerable<(string text, bool done, Action go)> GuideSteps()
        {
            yield return ("정책을 하나 이상 시행한다", session.Active.Count > 0, () => GoTo(Page.Policy));
            yield return ("연구 과제를 시작한다", session.CurrentResearch != null || session.Done.Count > 0, () => GoTo(Page.Research));
            yield return ("▶로 시간을 흐르게 한다", session.Day > 0, null);
            yield return ("주민 사건에 대응한다", session.Seen.Count > 0, null);
            yield return ("첫 월간 결산을 확인한다", session.Day >= 31, () => GoTo(Page.Report));
        }

        void RefreshGuide()
        {
            if (guideBody == null || session == null)
                return;
            guideBody.Clear();
            guideBody.style.display = guideFolded ? DisplayStyle.None : DisplayStyle.Flex;
            var steps = GuideSteps().ToList();
            int done = steps.Count(s => s.done);
            // First steps show for the first two months at most; after that only the goal.
            bool learning = done < steps.Count && session.Day < 62;
            guideTitle.text = learning ? $"운영 가이드 · 처음 할 일 {done}/{steps.Count}" : $"{session.Year}막 {session.ActName} · 목표";
            if (guideFolded)
                return;

            if (learning)
            {
                bool nextMarked = false;
                foreach (var (text, isDone, go) in steps)
                {
                    var row = new Button(() => go?.Invoke());
                    row.AddToClassList("guide-step");
                    row.EnableInClassList("guide-step--done", isDone);
                    bool next = !isDone && !nextMarked;
                    nextMarked |= next;
                    row.EnableInClassList("guide-step--next", next);
                    row.SetEnabled(go != null || isDone || next);
                    Text(row, isDone ? "●" : next ? "▶" : "○", "guide-mark").pickingMode = PickingMode.Ignore;
                    Text(row, text, "guide-text").pickingMode = PickingMode.Ignore;
                    if (go != null && !isDone)
                        Text(row, "›", "guide-go").pickingMode = PickingMode.Ignore;
                    guideBody.Add(row);
                }
            }

            // The goals: this year's audit and this quarter's directive, then the danger line.
            var goal = Add(guideBody, "guide-goal");
            RenderGoalTracker(goal);
            float stability = session.Indices[Index.Stability];
            if (session.FallStreak > 0)
                Text(goal, $"경고: 사회 안정 {OpsTuning.FallStability:0} 미만 {session.FallStreak}/{OpsTuning.FallMonths}개월. 이어지면 단계가 내려간다.", "guide-warn tone--bad");
            else if (stability < 35f)
                Text(goal, $"사회 안정 {stability:0}: 0이 되면 붕괴한다. 안정부터 끌어올려야 한다.", "guide-warn tone--warn");
        }
    }
}
