using NetTopologySuite.Geometries;
using Urban.Application.OldServices;
using Urban.Application.Services;
using Urban.Domain.Geometry;

namespace Urban.Application.Upgrades
{
    public class LayoutManager
    {
        private static readonly Random _random = new();

        public void FillSectionsFloors(Section[] sections, double? grossFloorArea = null)
        {
            if (sections == null || sections.Length == 0) return;

            foreach (var section in sections)
            {
                section.Floors = section.MinFloors + _random.Next(section.MaxFloors - section.MinFloors + 1);
                section.UpdateProperties(_random.Next(2) == 0);
            }

            if (!grossFloorArea.HasValue) return;

            double currentArea = sections.Sum(s => s.Polygon.Area * s.Floors);
            bool shouldIncrease = currentArea < grossFloorArea.Value;
            var adjustable = sections.Where(s => shouldIncrease ? s.Floors < s.MaxFloors : s.Floors > s.MinFloors).ToList();

            while (adjustable.Any() && Math.Abs(currentArea - grossFloorArea.Value) / grossFloorArea.Value > 0.05)
            {
                int idx = _random.Next(adjustable.Count);
                var section = adjustable[idx];

                if (shouldIncrease)
                {
                    section.Floors++;
                    currentArea += section.Polygon.Area;
                    if (section.Floors >= section.MaxFloors) adjustable.RemoveAt(idx);
                }
                else
                {
                    section.Floors--;
                    currentArea -= section.Polygon.Area;
                    if (section.Floors <= section.MinFloors) adjustable.RemoveAt(idx);
                }

                section.UpdateProperties();
            }
        }

        public BlockLayout CreateLayout(string name, Polygon plot, LineString[] streets,
            List<Section> sections, List<Polygon> parks, InsolationCalculator insolationCalculator)
        {
            var usefulArea = sections.Sum(h => h.UsefulArea());
            var insolation = 0.0;
            var value = Math.Pow(insolation, 4) * usefulArea;

            return new BlockLayout
            {
                Name = name,
                Block = plot,
                Sections = sections.ToArray(),
                Parks = parks.ToArray(),
                Streets = streets ?? Array.Empty<LineString>(),
                Value = value,
                Insolation = insolation,
                UsefulArea = usefulArea,
                BuiltUpArea = sections.Sum(s => s.Polygon.Area),
                Cost = sections.Sum(s => s.Cost())
            };
        }
    }
}