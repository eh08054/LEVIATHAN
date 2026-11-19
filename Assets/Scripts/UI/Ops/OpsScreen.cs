using System;
using System.Collections.Generic;
using System.Linq;
using Sector.Ops;
using UnityEngine;
using UnityEngine.UIElements;
using Index = Sector.Ops.Index;
using Random = UnityEngine.Random;

namespace Sector.UI
{
    // The operations game (era 1, 2041 pilot district). A top bar and a left menu stay on screen; each menu item is a
    // full page (OpsPages.cs), the city overview being the first (OpsHud.cs). Time runs in real time with pause and
    // speed and keeps running across pages; events (modal) and the monthly settlement stop it.
    [RequireComponent(typeof(UIDocument))]
    public partial class OpsScreen : MonoBehaviour
    {
        // Real seconds per in-game day at 1x.
        const float DaySeconds = 8f;
        static readonly int[] Speeds = { 1, 2, 4 };
        // A visible fixed resident speaks about this often (seconds).
        const float ChatterSeconds = 7f;

        [SerializeField] StyleSheet styleSheet;
        // 0 picks a new seed each run.
        [SerializeField] int seed;

        OpsSession session;
        Sfx sfx;
        VisualElement root;
        VisualElement overlay;
        bool overlayOpen;
        bool paused = true;
        int speed = 1;
        float dayClock;
        float chatter = ChatterSeconds;
        bool endShown;


        void OnEnable()
        {
            sfx = GetComponent<Sfx>();
            if (sfx == null)
                sfx = gameObject.AddComponent<Sfx>();
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
            root.AddToClassList("ops");
            Build();

            var saved = OpsSaveStore.Load();
            if (saved != null)
            {
                shownAct = saved.Year;
                Begin(saved);
                if (!session.IsOver)
                    Toast("저장된 운영 기록을 불러왔습니다.");
            }
            else
            {
                Begin(new OpsSession(seed == 0 ? 0UL : (ulong)seed));
                ShowIntro();
            }
        }

        void Begin(OpsSession next)
        {
            if (session != null)
            {
                session.Logged -= OnLogged;
                session.GoalFinished -= OnGoalFinished;
                session.TalkOverheard -= OnTalkOverheard;
                session.AnomalySpotted -= OnAnomalySpotted;
                session.CrisisWarned -= OnCrisisWarned;
            }
            session = next;
            session.Logged += OnLogged;
            session.GoalFinished += OnGoalFinished;
            session.TalkOverheard += OnTalkOverheard;
            session.AnomalySpotted += OnAnomalySpotted;
            session.CrisisWarned += OnCrisisWarned;
            endShown = false;
            paused = true;
            dayClock = 0f;
            logList.Clear();
            foreach (var line in session.Log.Skip(Math.Max(0, session.Log.Count - 40)))
                AddLogLine(line);
            lastReport = null;
            shownReport = null;
            Refresh();
            GoTo(Page.Overview);
            if (session.PendingEvent != null)
                ShowEvent();
            else if (session.Report != null)
                ShowReport();
            else if (session.IsOver)
                ShowEnd();
        }


        // ------------------------------------------------------------------ time

        void Update()
        {
            if (session == null)
                return;
            sim.SetTime(dayClock / DaySeconds);
            if (session.IsOver)
            {
                if (!endShown && !overlayOpen)
                    ShowEnd();
                return;
            }
            if (!overlayOpen)
                TickWatch(Time.deltaTime);
            // Something still waits for an answer: show it again.
            if (!overlayOpen && session.PendingEvent != null)
                ShowEvent();
            else if (!overlayOpen && session.PendingScene != null)
                ShowScene();
            else if (!overlayOpen && session.Report != null && shownReport != session.Report)
                ShowReport();
            ShowGoalScreens();
            if (paused || overlayOpen)
                return;
            // Time stands still while the monthly report waits; its sound and save happened once when it arrived.
            if (session.Report != null)
                return;

            chatter -= Time.deltaTime;
            if (chatter <= 0f)
            {
                chatter = ChatterSeconds + Random.Range(-2f, 3f);
                Chatter();
            }

            dayClock += Time.deltaTime * speed;
            while (dayClock >= DaySeconds)
            {
                dayClock -= DaySeconds;
                session.AdvanceDay();
                OnDayPassed();
                Refresh();
                if (session.Day % 7 == 0)
                    RefreshPage();
                if (session.PendingEvent != null)
                {
                    sfx.Play(Cue.Alert);
                    var fired = OpsEvents.Get(session.PendingEvent);
                    if (fired != null)
                        RippleForEvent(fired);
                    ShowEvent();
                    break;
                }
                if (session.PendingScene != null)
                {
                    sfx.Play(Cue.Alert);
                    ShowScene();
                    break;
                }
                if (session.Report != null)
                {
                    RippleAll(Palette.Cyan);
                    OpsSaveStore.Save(session);
                    sfx.Play(Cue.Settle);
                    Stamp("월간 결산", $"{session.Report.Month:yyyy년 M월}", session.Report.Grade);
                    ShowReport();
                    break;
                }
                if (session.IsOver)
                    break;
            }
        }

        void TogglePause()
        {
            if (session.IsOver)
                return;
            paused = !paused;
            if (!paused)
                FinishSettlement();
            sfx.Play(Cue.Click);
            RefreshTime();
        }

        void SetSpeed(int s)
        {
            speed = s;
            paused = false;
            FinishSettlement();
            sfx.Play(Cue.Click);
            RefreshTime();
        }

        // Starting time while the monthly report still waits (e.g. after adjusting policies from the report)
        // closes the report: the settlement is done and the month begins.
        void FinishSettlement()
        {
            if (session.Report == null)
                return;
            session.CloseReport();
            Toast("결산을 마치고 운영을 재개합니다.");
            Refresh();
            RefreshPage();
        }

        void RefreshTime()
        {
            pauseButton.text = paused ? "▶" : "||";
            pauseButton.EnableInClassList("ops-time-button--on", paused);
            for (int i = 0; i < Speeds.Length; i++)
                speedButtons[i].EnableInClassList("ops-time-button--on", !paused && speed == Speeds[i]);
        }

        void Chatter()
        {
            var visible = sim.VisibleCast.ToList();
            for (int tries = 0; tries < visible.Count; tries++)
            {
                var id = visible[Random.Range(0, visible.Count)];
                var line = session.Line(Residents.Get(id));
                if (line != null && sim.Say(id, line))
                    return;
            }
        }


        // ------------------------------------------------------------------ overlays

        VisualElement OpenOverlay(string title, string extraClass = null, bool closable = true, Action onClose = null)
        {
            overlay.Clear();
            overlay.style.display = DisplayStyle.Flex;
            overlayOpen = true;
            var panel = Add(overlay, "ops-modal");
            if (extraClass != null)
                panel.AddToClassList(extraClass);

            var head = Add(panel, "ops-modal-head");
            Text(head, title, "ops-modal-title");
            if (closable)
                MakeButton(head, "닫기", () =>
                {
                    CloseOverlay();
                    onClose?.Invoke();
                }, "ops-small");
            var body = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            body.AddToClassList("ops-modal-body");
            panel.Add(body);
            return body.contentContainer;
        }

        void CloseOverlay()
        {
            overlay.Clear();
            overlay.style.display = DisplayStyle.None;
            overlayOpen = false;
            Refresh();
        }

        void Toast(string text)
        {
            toast.text = text;
            toast.style.display = DisplayStyle.Flex;
            toast.schedule.Execute(() => toast.style.display = DisplayStyle.None).StartingIn(2600);
        }

        // ----- events

        void ShowEvent()
        {
            var def = OpsEvents.Get(session.PendingEvent);
            if (def == null)
                return;
            var body = OpenOverlay($"{OpsEvents.KindName(def.Kind)} · {def.Title}", "ops-modal--event", closable: false);
            var art = PixelArt.Load(EventArt(def.Kind));
            if (art != null)
                Add(body, "event-art").style.backgroundImage = new StyleBackground(art);
            // Where it came from: the spark's cause and trigger, or the forecast crisis and how ready the city was.
            if (!string.IsNullOrEmpty(session.PendingCause))
                Text(body, session.PendingCause, "event-cause");
            var row = Add(body, "ops-profile");
            var speaker = Residents.Get(def.Speaker);
            if (speaker != null)
            {
                var portrait = new CastPortrait(speaker.Id, tall: true) { Expression = MoodFace(speaker) };
                portrait.AddToClassList("ops-profile-face");
                row.Add(portrait);
            }
            else
            {
                var emblem = new AiEmblem();
                emblem.AddToClassList("ops-profile-face");
                row.Add(emblem);
            }
            var text = Add(row, "ops-profile-text");
            if (speaker != null)
                Text(text, $"{speaker.Name} · {speaker.Job}", "ops-note");
            Text(text, def.Text, "ops-para");
            var options = Add(body, "ops-options");
            for (int i = 0; i < def.Options.Count; i++)
            {
                int at = i;
                var option = def.Options[i];
                var button = new Button(() => Choose(at));
                button.AddToClassList("ops-option");
                button.AddToClassList("ops-option--" + option.Tag.ToString().ToLowerInvariant());
                var head = Add(button, "ops-option-head");
                Text(head, $"[{OpsEvents.TagName(option.Tag)}]", "ops-tag");
                Text(head, option.Label, "ops-option-label");
                var foot = (option.Effect * OpsTuning.EventScale).Describe();
                if (option.Energy > 0f)
                    foot += (foot.Length > 0 ? " · " : "") + $"에너지 {option.Energy:0}";
                bool can = session.CanChoose(option, out var reason);
                Text(button, can ? foot : reason, can ? "ops-card-foot" : "ops-reason");
                button.SetEnabled(can);
                options.Add(button);
            }
        }

        void Choose(int option)
        {
            var title = OpsEvents.Get(session.PendingEvent)?.Title ?? "";
            var result = session.Resolve(option);
            if (result == null)
            {
                sfx.Play(Cue.Deny);
                return;
            }
            sfx.Play(Cue.Choose);
            var body = OpenOverlay(title, "ops-modal--narrow", closable: false);
            Text(body, result, "ops-para");
            var row = Add(body, "ops-row");
            MakeButton(row, "확인", () =>
            {
                CloseOverlay();
                if (session.IsOver)
                    ShowEnd();
            }, "ops-primary");
            Refresh();
        }

        // ----- end

        void ShowEnd()
        {
            endShown = true;
            paused = true;
            OpsSaveStore.Save(session);
            string title, text;
            switch (session.Outcome)
            {
                case Outcome.Collapsed:
                    title = "사회 붕괴";
                    text = "사회 안정 지수가 0에 닿았다. 폭동이 구역 전체로 번졌고, 시범 운영은 중단되었다.";
                    sfx.Play(Cue.Fail);
                    break;
                case Outcome.Revoked:
                    title = "권한 회수";
                    text = "시청이 운영 권한을 회수했다. SECTOR 7은 다시 사람의 손으로 돌아갔다.";
                    sfx.Play(Cue.Fail);
                    break;
                case Outcome.Cleared:
                    title = "구역 확대";
                    text = "위임 최고 단계. 시는 운영 AI에게 도시 전체를 맡기기로 했다. 주민들은 그것이 자신들의 선택이라고 믿는다.";
                    sfx.Play(Cue.Success);
                    break;
                default:
                    title = "시범 운영 종료";
                    text = $"5년이 지났다. 위임 {session.Stage}단계로 시범 운영을 마쳤다.";
                    sfx.Play(Cue.Success);
                    break;
            }
            var body = OpenOverlay(title, "ops-modal--narrow", closable: false);
            Text(body, $"{session.Date:yyyy년 M월 d일}", "ops-note");
            Text(body, text, "ops-para");
            var table = Add(body, "ops-table");
            foreach (var index in IndexNames.All)
            {
                var line = Add(table, "ops-table-row");
                Text(line, IndexNames.Name(index), "ops-table-name");
                Text(line, $"{session.Indices[index]:0}", "ops-table-value");
            }
            Text(body, $"주민이 느끼는 자율성 {session.Indices[Index.AutonomyFelt]:0} · 실제 자율성 {session.Indices[Index.AutonomyReal]:0}", "ops-badge");
            var row = Add(body, "ops-row");
            MakeButton(row, "새 게임", Reset, "ops-primary");
        }

        // ----- save / reset

        void ManualSave()
        {
            Toast(OpsSaveStore.Save(session) ? "저장했습니다." : "저장하지 못했습니다.");
            sfx.Play(Cue.Click);
        }

        void ConfirmReset()
        {
            bool wasPaused = paused;
            paused = true;
            var body = OpenOverlay("초기화", "ops-modal--narrow", onClose: () => paused = wasPaused);
            Text(body, "지금까지의 운영을 지우고 2041년 1월 1일부터 새로 시작합니다. 되돌릴 수 없습니다.", "ops-para");
            var row = Add(body, "ops-row");
            MakeButton(row, "취소", () =>
            {
                paused = wasPaused;
                CloseOverlay();
            }, "ops-small");
            MakeButton(row, "처음부터 다시", Reset, "ops-primary");
        }

        void Reset()
        {
            OpsSaveStore.Delete();
            CloseOverlay();
            shownAct = 0;
            Begin(new OpsSession(seed == 0 ? 0UL : (ulong)seed));
            ShowIntro();
        }

        // ------------------------------------------------------------------ helpers

        static VisualElement Add(VisualElement parent, string classes)
        {
            var element = new VisualElement();
            foreach (var c in classes.Split(' '))
                element.AddToClassList(c);
            parent?.Add(element);
            return element;
        }

        static Label Text(VisualElement parent, string text, string classes)
        {
            var label = new Label(text);
            foreach (var c in classes.Split(' '))
                label.AddToClassList(c);
            parent?.Add(label);
            return label;
        }

        Button MakeButton(VisualElement parent, string text, Action onClick, string classes)
        {
            var button = new Button(onClick) { text = text };
            foreach (var c in classes.Split(' '))
                button.AddToClassList(c);
            parent?.Add(button);
            return button;
        }
    }
}
