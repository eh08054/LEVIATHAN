using System;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Index = Sector.Ops.Index;

namespace Sector.UI
{
    // Goals on screen: the act card at each year's start, the quarterly directive choice, the goal tracker on the
    // overview's guide card, banners when a goal is met or missed, and the classified documents in the log.
    public partial class OpsScreen
    {
        // The act card shown so far (0 = none yet). A loaded game starts at its current year.
        [NonSerialized] int shownAct;

        // Called from Update when nothing else waits: the act card first, then the directive offer.
        void ShowGoalScreens()
        {
            if (overlayOpen || session.PendingEvent != null || session.PendingScene != null || session.Report != null || session.IsOver)
                return;
            if (shownAct < session.Year)
                ShowAct();
            else if (session.DirectiveOffer != null)
                ShowDirectives();
        }

        static string SourceClass(GoalSource source) => source == GoalSource.City ? "src--city" : "src--core";

        static string Quarter(DateTime date) => $"{date.Year}년 {(date.Month - 1) / 3 + 1}분기";

        string DueText(ActiveGoal goal)
        {
            var due = OpsSession.Start.AddDays(goal.DueDay);
            int days = goal.DueDay - session.Day;
            return $"{due.AddDays(-1):yyyy.MM.dd}까지 · D-{Math.Max(0, days)}";
        }

        // ------------------------------------------------------------------ act card

        void ShowAct()
        {
            shownAct = session.Year;
            paused = true;
            RefreshTime();
            int year = session.Year;
            var body = OpenOverlay($"{year}막 · {session.ActName}", "ops-modal--narrow act-modal", closable: false);
            sfx?.Play(Cue.Settle);
            var hero = Add(body, "act-hero");
            Text(hero, $"{OpsTuning.StartYear + year - 1}", "act-year num");
            var heroText = Add(hero, "act-hero-text");
            Text(heroText, $"ACT {year} / {OpsTuning.Years}", "act-kicker");
            Text(heroText, session.ActName, "act-name");
            Text(heroText, Goals.ActQuestions[year - 1], "act-question");

            var last = session.GoalResults.LastOrDefault(r => r.Def.Kind == GoalKind.Audit);
            if (last != null && last.Def.Year == year - 1)
            {
                var box = Add(body, "brief-box " + (last.Success ? "brief-box--good" : "brief-box--bad"));
                Text(box, $"지난해 감사 · {last.Def.Title} — {(last.Success ? "통과" : "미달")}", "brief-box-title");
                Text(box, last.Note ?? "", "ops-para ops-para--dim");
            }

            var audit = session.Audit;
            if (audit != null)
            {
                var def = audit.Def;
                var card = Add(body, "goal-card src--city");
                var head = Add(card, "goal-card-head");
                Text(head, "올해의 감사", "goal-src");
                Text(head, def.Title, "goal-card-title");
                Text(card, $"“{def.Text}”", "goal-quote");
                GoalBar(card, audit);
                Reward(card, "통과", def.RewardText, "tone-good");
                Reward(card, "미달", def.PenaltyText, "tone--bad");
                Text(card, DueText(audit), "goal-due");
            }
            var row = Add(body, "ops-row");
            Add(row, "spacer");
            MakeButton(row, "운영 시작", CloseOverlay, "ops-primary");
        }

        void Reward(VisualElement parent, string name, string text, string tone)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var row = Add(parent, "goal-reward");
            Text(row, name, "goal-reward-name " + tone);
            Text(row, text, "goal-reward-text");
        }

        void GoalBar(VisualElement parent, ActiveGoal goal)
        {
            var p = session.ProgressOf(goal);
            var row = Add(parent, "goal-progress");
            var bar = Add(row, "mini-bar mini-bar--wide");
            var fill = Add(bar, "mini-fill");
            fill.style.width = Length.Percent(p.Ratio * 100f);
            if (p.Met)
                fill.AddToClassList("mini-fill--good");
            Text(row, p.Text, "goal-progress-text num" + (p.Met ? " tone-good" : ""));
        }

        // ------------------------------------------------------------------ directive choice

        void ShowDirectives()
        {
            paused = true;
            RefreshTime();
            var body = OpenOverlay($"{Quarter(session.Date)} · 지시 선택", "ops-modal--wide directive-modal", closable: false);
            var intro = Add(body, "directive-intro");
            Text(intro, "이번 분기에 받을 지시를 하나 고른다. 시청 지시는 위임에, 상위 AI 지시는 숨은 진척과 기밀 문서에 보탬이 된다. " +
                        "어느 쪽을 따랐는지는 기록된다.", "ops-para ops-para--dim");
            MakeButton(intro, "이번 분기는 받지 않는다", () =>
            {
                session.ChooseDirective(-1);
                CloseOverlay();
            }, "ops-small");
            var row = Add(body, "directive-row");
            var offer = session.DirectiveOffer.ToList();
            for (int i = 0; i < offer.Count; i++)
            {
                int at = i;
                var def = Goals.Get(offer[i]);
                var card = Add(row, "goal-card directive-card " + SourceClass(def.Source));
                var head = Add(card, "goal-card-head");
                Text(head, Goals.SourceName(def.Source), "goal-src");
                Text(head, def.Check == GoalCheck.Hold ? "기한 판정" : "달성 즉시", "goal-check");
                Text(card, def.Title, "goal-card-title");
                Text(card, $"“{def.Text}”", "goal-quote");
                var preview = new ActiveGoal
                {
                    Id = def.Id, StartDay = session.Day, Start = session.Indices.Copy(), StartPopulation = session.ManagedPopulation,
                    StartInfoEarned = session.InfoEarned, StartCrowd = session.EventCounts[(int)EventKind.Crowd],
                    StartDone = session.Done.Count,
                };
                GoalBar(card, preview);
                Reward(card, "보상", def.RewardText, "tone-good");
                Reward(card, "실패 시", def.PenaltyText, "tone--bad");
                Add(card, "spacer");
                MakeButton(card, "이 지시를 받는다", () =>
                {
                    session.ChooseDirective(at);
                    sfx?.Play(Cue.Assign);
                    CloseOverlay();
                    Banner(def.Source == GoalSource.City ? BannerKind.City : BannerKind.Core, "지시 수락", def.Title, def.RewardText);
                }, "ops-primary directive-accept");
            }
        }

        // ------------------------------------------------------------------ tracker on the guide card

        void RenderGoalTracker(VisualElement parent)
        {
            void Item(ActiveGoal goal, string label)
            {
                var def = goal.Def;
                var item = Add(parent, "tracker " + SourceClass(def.Source));
                item.tooltip = "눌러서 목표 다시 보기";
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    sfx?.Play(Cue.Select);
                    ShowGoal(goal);
                });
                var head = Add(item, "tracker-head");
                Text(head, label, "goal-src");
                Text(head, def.Title, "tracker-title");
                Text(head, $"D-{Math.Max(0, goal.DueDay - session.Day)}", "tracker-due num");
                Text(head, "›", "tracker-more");
                GoalBar(item, goal);
            }
            if (session.Audit != null)
                Item(session.Audit, $"{session.Year}막 감사");
            if (session.Directive != null)
                Item(session.Directive, Goals.SourceName(session.Directive.Def.Source));
            else if (session.DirectiveOffer == null && !session.IsOver)
                Text(parent, "이번 분기 지시 없음 · 다음 분기 초에 새 지시", "ops-note");
            RenderCrisisTracker(parent);
        }

        static string GoalLabel(GoalDef def) =>
            def.Kind == GoalKind.Audit ? $"{def.Year}막 연말 감사" : $"{Goals.SourceName(def.Source)} 지시";

        // The full goal card: who asks, the request, progress now, the rule, reward, penalty and deadline.
        VisualElement GoalCard(VisualElement parent, ActiveGoal goal)
        {
            var def = goal.Def;
            var card = Add(parent, "goal-card " + SourceClass(def.Source));
            var head = Add(card, "goal-card-head");
            Text(head, GoalLabel(def), "goal-src");
            Text(head, def.Check == GoalCheck.Hold ? "기한 판정" : "달성 즉시", "goal-check");
            Text(card, def.Title, "goal-card-title");
            Text(card, $"“{def.Text}”", "goal-quote");
            GoalBar(card, goal);
            Text(card, def.Check == GoalCheck.Hold
                ? "기한 날의 결산에서 조건을 만족하고 있어야 한다."
                : "조건을 만족하는 순간 달성된다 (매주 확인).", "goal-rule");
            Reward(card, def.Kind == GoalKind.Audit ? "통과" : "보상", def.RewardText, "tone-good");
            Reward(card, def.Kind == GoalKind.Audit ? "미달" : "실패 시", def.PenaltyText, "tone--bad");
            Text(card, $"받은 날 {OpsSession.Start.AddDays(goal.StartDay):yyyy.MM.dd} · {DueText(goal)}", "goal-due");
            return card;
        }

        // Reread a goal in play (from the tracker or the log's goal tab).
        void ShowGoal(ActiveGoal goal)
        {
            var def = goal.Def;
            if (def == null || overlayOpen)
                return;
            bool wasPaused = paused;
            paused = true;
            RefreshTime();
            var body = OpenOverlay($"{GoalLabel(def)} · {def.Title}", "ops-modal--narrow", onClose: () =>
            {
                paused = wasPaused;
                RefreshTime();
            });
            if (def.Kind == GoalKind.Audit)
                Text(body, $"{session.Year}막 '{session.ActName}' · {Goals.ActQuestions[session.Year - 1]}", "ops-note");
            GoalCard(body, goal);
            var row = Add(body, "ops-row");
            MakeButton(row, "목표 기록 전체 보기", () =>
            {
                CloseOverlay();
                paused = wasPaused;
                subPage[Page.Log] = 1;
                GoTo(Page.Log);
            }, "ops-small");
            Add(row, "spacer");
            MakeButton(row, "확인", () =>
            {
                CloseOverlay();
                paused = wasPaused;
                RefreshTime();
            }, "ops-primary");
        }

        // The log page's goal tab: goals in play (click to reread) and every goal finished so far.
        void RenderGoalLog(VisualElement main)
        {
            var scroll = Scroll(main, "page-scroll glass");
            var list = scroll.contentContainer;
            Text(list, "진행 중인 목표", "section-title");
            var current = session.Goals.OrderBy(g => g.Def.Kind == GoalKind.Audit ? 0 : 1).ToList();
            if (current.Count == 0)
                Text(list, "진행 중인 목표가 없다.", "ops-note");
            foreach (var goal in current)
                GoalCard(list, goal);

            Text(list, $"지난 목표 · 시청 {session.CityDone}건 · 상위 AI {session.CoreDone}건 달성", "section-title");
            if (session.GoalResults.Count == 0)
                Text(list, "아직 끝난 목표가 없다.", "ops-note");
            foreach (var result in Enumerable.Reverse(session.GoalResults))
            {
                var def = result.Def;
                if (def == null)
                    continue;
                var row = Add(list, "goal-log " + SourceClass(def.Source));
                Text(row, $"{OpsSession.Start.AddDays(result.Day):yyyy.MM.dd}", "goal-log-date num");
                Text(row, GoalLabel(def), "goal-src");
                var text = Add(row, "goal-log-text");
                Text(text, def.Title, "goal-log-title");
                Text(text, result.Note ?? (result.Success ? def.RewardText : def.PenaltyText), "goal-log-note");
                Text(row, result.Success ? "달성" : "실패", "state-tag " + (result.Success ? "state-tag--good" : "state-tag--bad"));
            }
        }

        // ------------------------------------------------------------------ results

        void OnGoalFinished(GoalResult result)
        {
            var def = result.Def;
            if (result.Success)
            {
                sfx?.Play(Cue.Success);
                string detail = result.Note;
                var doc = def.GivesDocument ? session.Documents.LastOrDefault() : null;
                if (doc != null)
                    detail += $"\n기밀 문서 수신: {Goals.Document(doc)?.Title}";
                Banner(def.Source == GoalSource.City ? BannerKind.City : BannerKind.Core, "목표 달성", def.Title, detail);
            }
            else
            {
                sfx?.Play(Cue.Fail);
                Banner(BannerKind.Bad, "목표 실패", def.Title, result.Note);
            }
        }

        // ------------------------------------------------------------------ classified documents (log page tab)

        void RenderDocuments(VisualElement main)
        {
            var scroll = Scroll(main, "page-scroll glass");
            var list = scroll.contentContainer;
            Text(list, $"수신한 기밀 문서 {session.Documents.Count} / {Goals.Documents.Count} · 상위 AI 지시와 연말 감사를 달성하면 하나씩 도착한다.", "ops-note");
            for (int i = 0; i < Goals.Documents.Count; i++)
            {
                var doc = Goals.Documents[i];
                bool open = session.Documents.Contains(doc.Id);
                var card = Add(list, "doc-card" + (open ? "" : " doc-card--locked"));
                var head = Add(card, "doc-head");
                Text(head, $"C-{i + 1:00}", "doc-code num");
                Text(head, open ? doc.Title : "열람 권한 없음", "doc-title");
                Text(card, open ? doc.Text : "■■■■■■ ■■■ ■■■■ ■■■■■■■ ■■■.", "doc-text");
            }
        }
    }
}
