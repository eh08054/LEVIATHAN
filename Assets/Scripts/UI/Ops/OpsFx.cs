using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sector.UI
{
    // Interaction feedback: a ripple under every button press, banners for things that happened (goals, policies,
    // research, delegation), a stamp when the monthly settlement arrives, a pulse on the clock each day and a
    // sweep across the top bar each week. Everything here is decoration and never blocks the pointer.
    public partial class OpsScreen
    {
        public enum BannerKind { Info, City, Core, Good, Bad }

        VisualElement fxLayer, bannerStack, topBar;

        void BuildFx()
        {
            fxLayer = Add(root, "fx-layer");
            fxLayer.pickingMode = PickingMode.Ignore;
            bannerStack = Add(fxLayer, "fx-banners");
            bannerStack.pickingMode = PickingMode.Ignore;
            topBar = root.Q(className: "topbar");
            root.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0 && PressedButton(e.target as VisualElement) != null)
                    ClickRipple(root.WorldToLocal(e.position));
            }, TrickleDown.TrickleDown);
        }

        static Button PressedButton(VisualElement e)
        {
            for (int i = 0; e != null && i < 5; i++, e = e.parent)
                if (e is Button b && b.enabledInHierarchy)
                    return b;
            return null;
        }

        void ClickRipple(Vector2 at)
        {
            var ring = Add(fxLayer, "fx-ripple");
            ring.pickingMode = PickingMode.Ignore;
            ring.style.left = at.x - 12f;
            ring.style.top = at.y - 12f;
            ring.schedule.Execute(() => ring.AddToClassList("fx-ripple--go")).StartingIn(16);
            ring.schedule.Execute(() => ring.RemoveFromHierarchy()).StartingIn(520);
        }

        // A card that slides in under the top bar, stays a few seconds and fades.
        void Banner(BannerKind kind, string kicker, string title, string detail = null, float seconds = 4.2f)
        {
            if (bannerStack == null)
                return;
            while (bannerStack.childCount >= 3)
                bannerStack.RemoveAt(0);
            var card = Add(bannerStack, "fx-banner fx-banner--" + kind.ToString().ToLowerInvariant());
            card.pickingMode = PickingMode.Ignore;
            Text(card, kicker, "fx-banner-kicker").pickingMode = PickingMode.Ignore;
            Text(card, title, "fx-banner-title").pickingMode = PickingMode.Ignore;
            if (!string.IsNullOrEmpty(detail))
                Text(card, detail, "fx-banner-detail").pickingMode = PickingMode.Ignore;
            card.schedule.Execute(() => card.AddToClassList("fx-banner--in")).StartingIn(16);
            card.schedule.Execute(() => card.RemoveFromClassList("fx-banner--in")).StartingIn((long)(seconds * 1000f));
            card.schedule.Execute(() => card.RemoveFromHierarchy()).StartingIn((long)(seconds * 1000f) + 450);
        }

        // The settlement stamp: the month and its grade, large in the middle, for a moment.
        void Stamp(string kicker, string title, string grade, long holdMs = 1500)
        {
            var stamp = Add(fxLayer, "fx-stamp");
            stamp.pickingMode = PickingMode.Ignore;
            var box = Add(stamp, "fx-stamp-box");
            box.pickingMode = PickingMode.Ignore;
            Text(box, kicker, "fx-stamp-kicker").pickingMode = PickingMode.Ignore;
            Text(box, title, "fx-stamp-title").pickingMode = PickingMode.Ignore;
            if (!string.IsNullOrEmpty(grade))
                Text(box, grade, "fx-stamp-grade num").pickingMode = PickingMode.Ignore;
            stamp.schedule.Execute(() => stamp.AddToClassList("fx-stamp--in")).StartingIn(16);
            stamp.schedule.Execute(() => stamp.RemoveFromClassList("fx-stamp--in")).StartingIn(holdMs);
            stamp.schedule.Execute(() => stamp.RemoveFromHierarchy()).StartingIn(holdMs + 500);
        }

        void Pulse(VisualElement element, string className, long ms = 260)
        {
            if (element == null)
                return;
            element.AddToClassList(className);
            element.schedule.Execute(() => element.RemoveFromClassList(className)).StartingIn(ms);
        }

        void OnDayPassed()
        {
            Pulse(dateLabel, "clock-date--tick");
            if (session.Day % 7 == 0 && topBar != null)
            {
                var sweep = Add(topBar, "fx-sweep");
                sweep.pickingMode = PickingMode.Ignore;
                sweep.schedule.Execute(() => sweep.AddToClassList("fx-sweep--go")).StartingIn(16);
                sweep.schedule.Execute(() => sweep.RemoveFromHierarchy()).StartingIn(1300);
            }
        }

        // Log lines that deserve a banner.
        void BannerForLog(string entry)
        {
            int space = entry.IndexOf(' ');
            var line = space > 0 ? entry.Substring(space + 1) : entry;
            string After(string prefix)
            {
                var rest = line.Substring(prefix.Length);
                int dash = rest.IndexOf(" — ", StringComparison.Ordinal);
                return dash >= 0 ? rest.Substring(0, dash) : rest;
            }
            if (line.StartsWith("정책 시행: "))
                Banner(BannerKind.Info, "정책 시행", After("정책 시행: "), null, 2.6f);
            else if (line.StartsWith("연구 완료: "))
            {
                int dash = line.IndexOf(" — ", StringComparison.Ordinal);
                Banner(BannerKind.Core, "연구 완료 · 개념 습득", After("연구 완료: "), dash >= 0 ? line.Substring(dash + 3) : null);
                sfx?.Play(Cue.Reveal);
            }
            else if (line.Contains(" 결산. "))
            {
                if (line.Contains("위임 단계 상승"))
                {
                    Banner(BannerKind.Good, "권한 위임", line.Substring(line.IndexOf("위임 단계 상승", StringComparison.Ordinal)));
                    sfx?.Play(Cue.Success);
                }
            }
            else if (line.StartsWith("새 분야 위임") || line.StartsWith("정책 슬롯 +1") || line.StartsWith("관리 구역 확장"))
                Banner(BannerKind.Good, "새 권한", line);
            else if (line.StartsWith("위임 단계 하락"))
                Banner(BannerKind.Bad, "권한 축소", line);
            else if (line.StartsWith("시설 건설: "))
                Banner(BannerKind.Info, "시설 건설", After("시설 건설: "), null, 2.6f);
        }
    }
}
