using System;
using System.Collections.Generic;
using System.Linq;

namespace Sector.Ops
{
    // Building on a district's grid (the district screen): budget, crews, construction, and what the buildings do to
    // the district's housing, satisfaction and tension.
    public sealed partial class OpsSession
    {
        public float Credit = OpsTuning.StartCredit;
        public readonly Dictionary<int, DistrictPlan> Plans = DistrictLayouts.Zones.ToDictionary(z => z, DistrictLayouts.For);

        public DistrictPlan PlanOf(int zone) => Plans.TryGetValue(zone, out var plan) ? plan : null;

        public static bool IsDone(Placed p, int day) => day >= p.DoneDay;
        public bool IsDone(Placed p) => IsDone(p, Day);

        public int UnderConstruction(int zone) => PlanOf(zone)?.Items.Count(p => !IsDone(p)) ?? 0;

        // Construction crews come from the district's working people.
        public int Crews(int zone) => 1 + (int)(ZonePop[zone] / OpsTuning.PeoplePerCrew);

        public float CreditPerWeek =>
            OpsTuning.TaxPerPerson * TotalPopulation
            + Plans.Values.Sum(plan => plan.Items.Where(p => IsDone(p)).Sum(p => p.Def.Income * PowerFactor(plan, p)));

        // A new building far from every substation works at half strength; the old housing is on the old grid.
        public bool Powered(DistrictPlan plan, Placed p) =>
            p.Def.Existing || p.Def.PowerReach > 0 || !p.Def.NeedsPower ||
            plan.Items.Any(s => s.Def.PowerReach > 0 && IsDone(s) && s.Gap(p) < s.Def.PowerReach);

        public float PowerFactor(DistrictPlan plan, Placed p) => Powered(plan, p) ? 1f : 0.5f;

        // Where people can live: the base population in the old blocks still standing, plus new housing.
        public float Capacity(int zone)
        {
            var plan = PlanOf(zone);
            float basePop = Society.Zones[zone].Population;
            if (plan == null)
                return basePop;
            int old = plan.Items.Count(p => p.Def.Existing);
            float cap = plan.OldHomes > 0 ? basePop * old / plan.OldHomes : basePop;
            return cap + plan.Items.Where(p => IsDone(p)).Sum(p => p.Def.Housing * PowerFactor(plan, p));
        }

        // How people per home weigh: old blocks share the base population, new ones their housing.
        float HomeWeight(DistrictPlan plan, Placed p) =>
            p.Def.Existing ? Society.Zones[plan.Zone].Population / Math.Max(1, plan.OldHomes) : p.Def.Housing;

        // Comfort and noise reaching each home, averaged by the people living there (satisfaction points, tension).
        public (float comfort, float noise) Neighbourhood(int zone)
        {
            var plan = PlanOf(zone);
            if (plan == null)
                return (0f, 0f);
            var homes = plan.Items.Where(p => p.Def.IsHome && IsDone(p)).ToList();
            float weight = homes.Sum(h => HomeWeight(plan, h));
            if (weight <= 0f)
                return (0f, 0f);
            float comfort = 0f, noise = 0f;
            foreach (var home in homes)
            {
                float w = HomeWeight(plan, home) / weight, c = 0f, n = 0f;
                foreach (var other in plan.Items)
                {
                    if (other == home || !IsDone(other))
                        continue;
                    int gap = other.Gap(home);
                    if (other.Def.Comfort > 0f && gap < other.Def.Reach)
                        c += other.Def.Comfort * PowerFactor(plan, other);
                    if (other.Def.Noise > 0f && gap == 0)
                        n += other.Def.Noise;
                }
                comfort += w * Math.Min(c, 8f);
                noise += w * n;
            }
            return (comfort, noise);
        }

        // Satisfaction points the district's buildings add (comfort minus a little for noise).
        public float DistrictComfort(int zone)
        {
            var (comfort, noise) = Neighbourhood(zone);
            return comfort - noise * 2f;
        }

        // The district's own tension causes from its grid.
        public IEnumerable<(string name, float value)> DistrictCauses(int zone)
        {
            var plan = PlanOf(zone);
            if (plan == null)
                yield break;
            int building = UnderConstruction(zone);
            if (building > 0)
                yield return ($"공사 소음 ({building}곳)", building * OpsTuning.ConstructionNoise);
            if (plan.Displaced > 1f)
                yield return ($"철거로 이주한 주민 {plan.Displaced:0}명", Math.Min(3f, plan.Displaced / 300f));
            var (comfort, noise) = Neighbourhood(zone);
            if (noise > 0.05f)
                yield return ("상점·설비 소음", noise * 2f);
            if (comfort > 0.05f)
                yield return ("공원·주민 센터", -comfort * 0.3f);
            float watch = plan.Items.Where(p => IsDone(p)).Sum(p => p.Def.Watch * PowerFactor(plan, p));
            if (watch > 0.05f)
                yield return ("감시탑", -watch);
            int dark = plan.Items.Count(p => IsDone(p) && !Powered(plan, p));
            if (dark > 0)
                yield return ($"전력 없는 새 건물 {dark}곳", dark * 0.3f);
        }

        public bool CanPlace(int zone, string id, int x, int y, out string reason)
        {
            var plan = PlanOf(zone);
            var def = Buildings.Get(id);
            reason = null;
            if (plan == null || def == null || def.Existing)
                reason = "지을 수 없다";
            else if (x < 0 || y < 0 || x + def.W > DistrictPlan.Size || y + def.H > DistrictPlan.Size)
                reason = "부지 밖";
            else if (Enumerable.Range(x, def.W).Any(i => Enumerable.Range(y, def.H).Any(j => plan.IsRoad(i, j) || plan.At(i, j) != null)))
                reason = "이미 무언가 있다";
            else if (!TouchesRoad(plan, x, y, def.W, def.H))
                reason = "도로에 닿아야 한다";
            else if (Credit < def.Cost)
                reason = $"예산 부족 ({def.Cost:0} 필요)";
            else if (UnderConstruction(zone) >= Crews(zone))
                reason = $"공사 인력이 모두 일하는 중 ({Crews(zone)}팀)";
            return reason == null;
        }

        static bool TouchesRoad(DistrictPlan plan, int x, int y, int w, int h)
        {
            for (int i = x - 1; i <= x + w; i++)
            for (int j = y - 1; j <= y + h; j++)
            {
                bool inside = i >= x && i < x + w && j >= y && j < y + h;
                bool corner = (i == x - 1 || i == x + w) && (j == y - 1 || j == y + h);
                if (!inside && !corner && plan.IsRoad(i, j))
                    return true;
            }
            return false;
        }

        public bool Place(int zone, string id, int x, int y)
        {
            if (!CanPlace(zone, id, x, y, out _))
                return false;
            var def = Buildings.Get(id);
            Credit -= def.Cost;
            PlanOf(zone).Items.Add(new Placed { Id = id, X = x, Y = y, DoneDay = Day + def.Weeks * 7 });
            Write($"착공: {def.Name} ({Society.Zones[zone].Name}, {def.Weeks}주)");
            return true;
        }

        // What a building would change once finished at (x, y): measured by placing it for a moment as if done.
        public sealed class PlacementPreview
        {
            public float Capacity, Satisfaction, TensionTrend, Credit, FeltPerWeek, InfoPerWeek;
            public bool Powered;
            public int HomesHelped, HomesDisturbed;
            // While it is being built: extra tension per week, for this many weeks.
            public float BuildNoise;
            public int Weeks;
        }

        public PlacementPreview Preview(int zone, string id, int x, int y)
        {
            var plan = PlanOf(zone);
            var def = Buildings.Get(id);
            if (plan == null || def == null)
                return null;
            float cap = Capacity(zone), sat = ZoneSatisfaction(zone), trend = TensionTrend(zone), credit = CreditPerWeek;
            var temp = new Placed { Id = id, X = x, Y = y, DoneDay = Day };
            plan.Items.Add(temp);
            try
            {
                var homes = plan.Items.Where(p => p != temp && p.Def.IsHome && IsDone(p)).ToList();
                float power = PowerFactor(plan, temp);
                return new PlacementPreview
                {
                    Capacity = Capacity(zone) - cap,
                    Satisfaction = ZoneSatisfaction(zone) - sat,
                    TensionTrend = TensionTrend(zone) - trend,
                    Credit = CreditPerWeek - credit,
                    FeltPerWeek = -def.Watch * power * 0.25f,
                    InfoPerWeek = def.Info * power,
                    Powered = Powered(plan, temp),
                    HomesHelped = def.Comfort > 0f ? homes.Count(h => temp.Gap(h) < def.Reach) : 0,
                    HomesDisturbed = def.Noise > 0f ? homes.Count(h => temp.Gap(h) == 0) : 0,
                    BuildNoise = OpsTuning.ConstructionNoise * Society.Zones[zone].Spread,
                    Weeks = def.Weeks,
                };
            }
            finally
            {
                plan.Items.Remove(temp);
            }
        }

        // Pulling down returns part of the cost; pulling down homes moves their people out (tension until they settle).
        public string CanDemolish(int zone, int x, int y)
        {
            var p = PlanOf(zone)?.At(x, y);
            return p == null ? "비어 있다" : null;
        }

        public bool DemolishAt(int zone, int x, int y)
        {
            var plan = PlanOf(zone);
            var p = plan?.At(x, y);
            if (p == null)
                return false;
            float before = Capacity(zone);
            plan.Items.Remove(p);
            if (IsDone(p))
            {
                Credit += p.Def.Cost * OpsTuning.DemolishRefund;
                if (p.Def.IsHome)
                {
                    // the people of this block who no longer fit anywhere in the district
                    float after = Capacity(zone);
                    float moved = Math.Max(0f, Math.Min(before - after, ZonePop[zone] - after));
                    plan.Displaced += moved;
                    ZonePop[zone] -= moved * 0.5f;   // half leave the district, half crowd in with neighbours
                }
            }
            else
                Credit += p.Def.Cost * 0.8f;   // stopped before it was finished
            Write($"철거: {p.Def.Name} ({Society.Zones[zone].Name})");
            return true;
        }

        // Finished buildings announce themselves (the UI shows the resident's line).
        public event Action<int, Placed> Completed;

        void DistrictDaily()
        {
            foreach (var plan in Plans.Values)
                foreach (var p in plan.Items.Where(p => p.DoneDay == Day && p.Def.Weeks > 0).ToList())
                {
                    Write($"완공: {p.Def.Name} ({Society.Zones[plan.Zone].Name})");
                    Completed?.Invoke(plan.Zone, p);
                }
        }

        void DistrictWeekly()
        {
            Credit += CreditPerWeek;
            foreach (var plan in Plans.Values)
            {
                plan.Displaced *= 0.8f;
                if (plan.Displaced < 1f)
                    plan.Displaced = 0f;
                float watch = plan.Items.Where(p => IsDone(p)).Sum(p => p.Def.Watch * PowerFactor(plan, p));
                Indices[Index.AutonomyFelt] = Math.Max(0f, Indices[Index.AutonomyFelt] - watch * 0.25f);
                float info = plan.Items.Where(p => IsDone(p)).Sum(p => p.Def.Info * PowerFactor(plan, p));
                Info += info;
                InfoEarned += info;
            }
        }

        void SaveDistricts(Action<string, string> line)
        {
            line("credit", F(Credit));
            line("plansize", DistrictPlan.Size.ToString(Inv));
            foreach (var plan in Plans.Values)
                line("plan", $"{plan.Zone}|{F(plan.Displaced)}|" +
                             string.Join(";", plan.Items.Select(p => $"{p.Id}:{p.X}:{p.Y}:{p.DoneDay}")));
        }

        void LoadDistricts(Func<string, string> get, IEnumerable<string> plans)
        {
            if (float.TryParse(get("credit"), System.Globalization.NumberStyles.Float, Inv, out var credit))
                Credit = credit;
            // a plan saved on a grid of another size (an older trial) is dropped: the district starts fresh
            if (get("plansize") != DistrictPlan.Size.ToString(Inv))
                return;
            foreach (var text in plans)
            {
                var parts = text.Split('|');
                int zone = int.Parse(parts[0], Inv);
                var plan = PlanOf(zone);
                if (plan == null || parts.Length < 3)
                    continue;
                plan.Displaced = P(parts[1]);
                plan.Items.Clear();
                foreach (var item in parts[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = item.Split(':');
                    if (Buildings.Get(f[0]) == null)
                        continue;
                    plan.Items.Add(new Placed { Id = f[0], X = int.Parse(f[1], Inv), Y = int.Parse(f[2], Inv), DoneDay = int.Parse(f[3], Inv) });
                }
            }
        }
    }
}
