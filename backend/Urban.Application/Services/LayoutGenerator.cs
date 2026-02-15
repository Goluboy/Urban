using NetTopologySuite.Geometries;
using Urban.Application.Helpers;
using Urban.Application.Logging;
using Urban.Application.Upgrades;
using Urban.Domain.Geometry;

namespace Urban.Application.Services
{
	public class LayoutGenerator
	{
		public const double floorHeight = 3.2;

		static readonly Random _random = new();
		double[] _axes;
		
		private GeoLogger _geoLogger;
		

		public BlockLayout[] GenerateLayouts(Polygon plot, GeoLogger geoLogger, int? maxFloors = null, double? grossFloorArea = null)
		{
			using (TimeLogger.Measure("GenerateLayouts"))
			{
				_geoLogger = geoLogger;
						
				_geoLogger.LogSvg((plot, "stroke='green' stroke-width='1'"));

				var centroidPoint = new Point(plot.Centroid.Coordinate);
				var centroid = (Point)CoordinatesConverter.FromUtm(centroidPoint, 41, true);
		
				var insolationCalculator = new InsolationCalculator(centroid.Coordinate.Y, centroid.Coordinate.X); // e.g. 56.8, 60.6	

				int numberOfBlocks = 3;

				var effectiveMaxFloors = Math.Max(3, maxFloors ?? 32);


				// Параметры ограничений
				var restrictions = LayoutRestrictions.GetRestrictionsWithinDistance(plot, new LayoutRestrictions.RestrictionType[] { LayoutRestrictions.RestrictionType.Buildings }).ToArray();

               // restrictions = OknData.GetNearestRestrictions(plot).ToArray();
                restrictions = restrictions.Where(r => r.Geometry is Polygon && plot.Intersects(r.Geometry)).ToArray();
				if (restrictions.Any())
				{
					// Subtract restrictions from the block to get available buildable area
					var originalPlot = plot;
					plot = SubtractRestrictionsFromBlock(plot, restrictions);
					_geoLogger.LogSvg("Cleared from restrictions", (originalPlot, Styles.Plot), (plot, "stroke='red' stroke-width='1'"));
				}
				 

				var layouts = new List<BlockLayout>();
				var random = new Random(1);

				int number = 0;
				

				foreach (var projPairFactor in new[] { 0.6, 1.0, 1.4})
				{
					var parks = new List<Polygon>();
					var sections = new List<Section>();

					void AddSections(Polygon[] polygons, int minFloors, int maxFloors) =>
						sections.AddRange(polygons.Select(p => new Section { Polygon = p, MinFloors = minFloors, MaxFloors = maxFloors }));

					using (TimeLogger.Measure($"UrbanBlocksLayout({projPairFactor})"))
					{					
						
						var (entryPoints, streets, innerBlocks) = StreetGenerator.SplitPolygonByStreets(plot, projPairFactor);
						
						geoLogger.LogSvg("Urban blocks", (plot, Styles.Plot), (entryPoints, Styles.EntryPoint));
						geoLogger.LogSvg((plot, Styles.Plot), (streets, Styles.Street));
						geoLogger.LogSvg((plot, Styles.Plot), (innerBlocks, Styles.Blocks));

						innerBlocks = innerBlocks.Select(b => b.Buffer(-12)).OfType<Polygon>().ToArray();
						
						foreach (var block in innerBlocks)
						{
							if (block.Area < 300 || random.Next(10) == 0)
							{
								parks.Add(block);
								continue;
							}

							if (random.Next(3) < 2 && 
							    block.GetPolygonAngles().All(a => a > 70) && 
							    block.GetEdges().All(e => e.Length > 35) &&
							    block.Area < 20000)
							{
								AddSections(PerimeterSectionGenerator.GeneratePerimeterSectionsForNonRectangle(block, 10, 20, 16, 28), 
									2, Math.Min(16, effectiveMaxFloors));
								continue;
							}
							
							if (random.Next(4) == 0)
							{
								AddSections(SectionGenerator.GenerateStandaloneSections(block, 24, 24, 30), effectiveMaxFloors / 3, effectiveMaxFloors);
								AddSections(new [] { block }, 2, 3); // Стилобат
								continue;
							}

							if (random.Next(2) == 0)
							{
								AddSections(SectionGenerator.GenerateStandaloneSections(block, 18, 24, 50), effectiveMaxFloors / 3, effectiveMaxFloors);
							}
							else
							{
								AddSections(SectionGenerator.GenerateStandaloneSections(block, 24, 24, 30), effectiveMaxFloors / 3, effectiveMaxFloors);
							}
						};

						FillSectionsFloors(sections.ToArray(), grossFloorArea);
						
						var sectionPolygons = sections.Select(s => s.Polygon).ToArray();

						var layout = GenerateLayout($"Концепция {number++}", plot, streets, sections.ToArray(), insolationCalculator);
						layout.Parks = parks.ToArray();
						layouts.Add(layout);
						
						geoLogger.LogSvg((plot, Styles.Plot), (streets, Styles.Street), (sectionPolygons, Styles.Sections), (parks.ToArray(), Styles.Parks));
					}
				}
				
				geoLogger.LogMessage(TimeLogger.GetTimeInfo().ToString());

				return layouts.Where(l => l.Sections.Any()).ToArray();
			}
		}

		public void FillSectionsFloors(Section[] sections, double? grossFloorArea = null)
		{
			if (sections == null || sections.Length == 0) return;
			
			// First pass: set random floors within min/max range
			foreach (var section in sections)
			{
				section.Floors = section.MinFloors + _random.Next(section.MaxFloors - section.MinFloors + 1);
				section.UpdateProperties(_random.Next(2) == 0);
			}
			
			// If no target gross floor area is specified, we're done
			if (!grossFloorArea.HasValue) return;
			
			// Second pass: adjust floors to meet target gross floor area
			int maxIterations = 100;
			double tolerance = 0.05; // 5% tolerance
			
			// Create a list of all sections that we can potentially adjust
			var adjustableSections = sections.ToList();
			
			double currentGrossFloorArea = sections.Sum(s => s.Polygon.Area * s.Floors);
			bool shouldIncrease = currentGrossFloorArea < grossFloorArea.Value;
			
			while (adjustableSections.Any())
			{
				int randomIndex = _random.Next(adjustableSections.Count);
				var section = adjustableSections[randomIndex];
				bool canAdjust = shouldIncrease 
					? section.Floors < section.MaxFloors
					: section.Floors > section.MinFloors;
				
				if (canAdjust)
				{
					if (shouldIncrease)
					{
						section.Floors++;
						currentGrossFloorArea += section.Polygon.Area;
						section.UpdateProperties();
						if (currentGrossFloorArea >= grossFloorArea.Value)
							break;
					}
					else
					{
						section.Floors--;
						currentGrossFloorArea -= section.Polygon.Area;
						section.UpdateProperties();
						if (currentGrossFloorArea <= grossFloorArea.Value)
							break;
					}
				}
				else
				{
					adjustableSections.RemoveAt(randomIndex);
				}
			}
		}

		public Section[] GenerateBuildings(Polygon[] sectionPolygons, int minFloors, int maxFloors, double? grossFloorArea = null)
		{
			return sectionPolygons.Select(p =>
			{
				var area = p.Area;
				
				// Calculate floors based on grossFloorArea target if provided, otherwise use random
				int floors;
				if (grossFloorArea.HasValue)
				{
					var totalPolygonArea = sectionPolygons.Sum(poly => poly.Area);
					// Calculate target floors based on this building's proportion of total area
					var buildingProportion = totalPolygonArea > 0 ? area / totalPolygonArea : 0.0;
					var targetGrossFloorAreaForBuilding = grossFloorArea.Value * buildingProportion;
					var targetFloors = area > 0 ? Math.Round(targetGrossFloorAreaForBuilding / area) : minFloors;
					floors = Math.Max(minFloors, Math.Min(maxFloors, (int)targetFloors));
					
					// Add some randomness while staying close to target
					var variation = Math.Max(1, floors / 4); // Allow up to 25% variation
					floors = Math.Max(minFloors, Math.Min(maxFloors, floors + _random.Next(-variation, variation + 1)));
				}
				else
				{
					// Use original random logic when no grossFloorArea constraint is specified
					floors = minFloors + _random.Next(maxFloors - minFloors + 1);
				}
				
				var section = new Section
				{
					Polygon = p,
					Floors = floors
				};
				
				section.UpdateProperties(_random.Next(2) == 0);
				return section;
			}).ToArray();
		}


		public BlockLayout GenerateLayout(string name, Polygon block, LineString[] streets, Section[] sections, InsolationCalculator insolationCalculator)
		{
			using (TimeLogger.Measure($"GenerateLayout({name})"))
            {
                var usefulArea = sections.Sum(h => h.UsefulArea());

				//insolationCalculator.CalculateInsolation(sections);

				// var totalWindows = sections.SelectMany(h => h.Bays).Sum(s => s.Floors);
				// var insolatedWindows = sections.SelectMany(h => h.Bays).Sum(s => Math.Max(0, s.Floors - s.ResultShadowHeight / floorHeight));
				var insolation = 0.0;// 1.0 * insolatedWindows / totalWindows;
				var value = Math.Pow(insolation, 4) * usefulArea;				

				return new BlockLayout
				{
					Name = name,
					Block = block,
					Sections = sections.ToArray(),
					Value = value,
					Insolation = insolation,
					UsefulArea = usefulArea,
					Streets = streets ?? Array.Empty<LineString>(),
				};
			}
		}

		private static Polygon SubtractRestrictionsFromBlock(Polygon block, Restriction[] restrictions)
		{
			var availableBlock = block;
			
			foreach (var restriction in restrictions)
			{
				if (restriction.Geometry is Polygon restrictionPolygon && restrictionPolygon.Intersects(block))
				{
					try
					{
						var difference = availableBlock.Difference(restrictionPolygon);						
						if (difference is Polygon singlePolygon)
						{
							availableBlock = singlePolygon;
						}
						else if (difference is MultiPolygon multiPolygon)
						{
							var largestPolygon = multiPolygon.Geometries
								.OfType<Polygon>()
								.OrderByDescending(p => p.Area)
								.FirstOrDefault();
							
							if (largestPolygon != null)
							{
								availableBlock = largestPolygon;
							}
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Warning: Failed to subtract restriction {restriction.Name}: {ex.Message}");
					}
				}
			}
			
			return availableBlock;
		}
	}
}








