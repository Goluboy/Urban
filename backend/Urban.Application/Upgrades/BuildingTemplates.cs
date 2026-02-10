namespace Urban.Application.Upgrades
{
    public static class BuildingTemplates
    {
        // Default values for floor calculations
        private const int DEFAULT_MIN_FLOORS = 1;
        private const int DEFAULT_MAX_FLOORS = 5;
        private const double DEFAULT_AREA_PER_FLOOR = 100.0; // Square units per floor

        // Education buildings have more floors
        private const int EDUCATION_MIN_FLOORS = 2;
        private const int EDUCATION_MAX_FLOORS = 8;
        private const double EDUCATION_AREA_PER_FLOOR = 150.0;

        // Medical buildings have specific requirements
        private const int MEDICAL_MIN_FLOORS = 3;
        private const int MEDICAL_MAX_FLOORS = 10;
        private const double MEDICAL_AREA_PER_FLOOR = 200.0;

        // Predefined building templates
        public static readonly BuildingTemplate Single = new BuildingTemplate("Single", BuildingCategory.Generic,
            DEFAULT_MIN_FLOORS, DEFAULT_MAX_FLOORS, DEFAULT_AREA_PER_FLOOR,
            (0, 0)
        );

        public static readonly BuildingTemplate Line2_H = new BuildingTemplate("Line2_H", BuildingCategory.Residential,
            DEFAULT_MIN_FLOORS, DEFAULT_MAX_FLOORS, DEFAULT_AREA_PER_FLOOR,
            (0, 0), (1, 0)
        );

        public static readonly BuildingTemplate Line2_V = new BuildingTemplate("Line2_V", BuildingCategory.Residential,
            DEFAULT_MIN_FLOORS, DEFAULT_MAX_FLOORS, DEFAULT_AREA_PER_FLOOR,
            (0, 0), (0, 1)
        );

        public static readonly BuildingTemplate Line3_H = new BuildingTemplate("Line3_H", BuildingCategory.Residential,
            DEFAULT_MIN_FLOORS, DEFAULT_MAX_FLOORS, DEFAULT_AREA_PER_FLOOR,
            (0, 0), (1, 0), (2, 0)
        );

        public static readonly BuildingTemplate Line3_V = new BuildingTemplate("Line3_V", BuildingCategory.Residential,
            DEFAULT_MIN_FLOORS, DEFAULT_MAX_FLOORS, DEFAULT_AREA_PER_FLOOR,
            (0, 0), (0, 1), (0, 2)
        );

        public static readonly BuildingTemplate Line4_H = new BuildingTemplate("Line4_H", BuildingCategory.Residential,
            DEFAULT_MIN_FLOORS, DEFAULT_MAX_FLOORS, DEFAULT_AREA_PER_FLOOR,
            (0, 0), (1, 0), (2, 0), (3, 0)
        );

        public static readonly BuildingTemplate Line4_V = new BuildingTemplate("Line4_V", BuildingCategory.Residential,
            DEFAULT_MIN_FLOORS, DEFAULT_MAX_FLOORS, DEFAULT_AREA_PER_FLOOR,
            (0, 0), (0, 1), (0, 2), (0, 3)
        );

        public static readonly BuildingTemplate L3_Standard = new BuildingTemplate("L3_Standard", BuildingCategory.Residential,
            DEFAULT_MIN_FLOORS, DEFAULT_MAX_FLOORS, DEFAULT_AREA_PER_FLOOR,
            (0, 0), (0, 1), (1, 0)
        );

        public static readonly BuildingTemplate L3_Mirror = new BuildingTemplate("L3_Mirror", BuildingCategory.Residential,
            DEFAULT_MIN_FLOORS, DEFAULT_MAX_FLOORS, DEFAULT_AREA_PER_FLOOR,
            (0, 0), (1, 0), (1, 1)
        );

        public static readonly BuildingTemplate L4_Example = new BuildingTemplate("L4_Example", BuildingCategory.Medical,
            MEDICAL_MIN_FLOORS, MEDICAL_MAX_FLOORS, MEDICAL_AREA_PER_FLOOR,
            (0, 0), (0, 1), (0, 2), (1, 2)
        );

        public static readonly BuildingTemplate L5 = new BuildingTemplate("L5", BuildingCategory.Education,
            EDUCATION_MIN_FLOORS, EDUCATION_MAX_FLOORS, EDUCATION_AREA_PER_FLOOR,
            (0, 0), (0, 1), (0, 2), (1, 2), (2, 2)
        );

        public static readonly BuildingTemplate Square2x2 = new BuildingTemplate("Square2x2", BuildingCategory.Residential,
            DEFAULT_MIN_FLOORS, DEFAULT_MAX_FLOORS, DEFAULT_AREA_PER_FLOOR,
            (0, 0), (1, 0), (0, 1), (1, 1)
        );

        public static readonly BuildingTemplate Square3x3 = new BuildingTemplate("Square3x3", BuildingCategory.Education,
            EDUCATION_MIN_FLOORS, EDUCATION_MAX_FLOORS, EDUCATION_AREA_PER_FLOOR,
            (0, 0), (1, 0), (2, 0),
            (0, 1), (1, 1), (2, 1),
            (0, 2), (1, 2), (2, 2)
        );

        public static readonly BuildingTemplate T4_Standard = new BuildingTemplate("T4_Standard", BuildingCategory.Medical,
            MEDICAL_MIN_FLOORS, MEDICAL_MAX_FLOORS, MEDICAL_AREA_PER_FLOOR,
            (0, 0), (1, 0), (2, 0), (1, 1)
        );

        // Get all available templates
        public static List<BuildingTemplate> GetAllTemplates()
        {
            return new List<BuildingTemplate>
            {
                Single,
                Line2_H,
                Line2_V,
                Line3_H,
                Line3_V,
                Line4_H,
                Line4_V,
                L3_Standard,
                L3_Mirror,
                L4_Example,
                L5,
                Square2x2,
                Square3x3,
                T4_Standard
            };
        }

        // Get templates by category
        public static List<BuildingTemplate> GetTemplatesByCategory(BuildingCategory category)
        {
            return GetAllTemplates()
                .Where(t => t.Category == category)
                .ToList();
        }

        // Get templates by size preference
        public static List<BuildingTemplate> GetTemplatesBySize(int minCells, int maxCells)
        {
            return GetAllTemplates()
                .Where(t => t.Cells.Length >= minCells && t.Cells.Length <= maxCells)
                .ToList();
        }
    }
}