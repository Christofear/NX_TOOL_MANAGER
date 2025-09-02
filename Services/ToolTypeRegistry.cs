using System.Collections.Generic;
using System.Linq;

namespace NX_TOOL_MANAGER.Services
{
    public class ToolTypeDefinition
    {
        public string UGT { get; }
        public string UGST { get; }
        public string UgTypeName { get; }
        public string UgSubtypeName { get; }
        public IToolDrawer Drawer { get; }

        public ToolTypeDefinition(string ugt, string ugst, string ugTypeName, string ugSubtypeName, IToolDrawer drawer)
        {
            UGT = ugt;
            UGST = ugst;
            UgTypeName = ugTypeName;
            UgSubtypeName = ugSubtypeName;
            Drawer = drawer;
        }
    }

    public static class ToolTypeRegistry
    {
        private static readonly List<ToolTypeDefinition> Definitions;

        static ToolTypeRegistry()
        {
            var fiveParameterDrawer = new FiveParameterDrawer();
            var sevenParameterDrawer = new SevenParameterDrawer();
            var tenParameterDrawer = new TenParameterDrawer();
            var ballDrawer = new BallDrawer();
            var chamferMillDrawer = new ChamferMillDrawer();
            var sphericalMillDrawer = new SphericalMillDrawer();
            var dovetailMillDrawer = new DovetailMillDrawer();
            var tSlotCutterDrawer = new TSlotCutterDrawer();
            var barrelCutterDrawer = new BarrelCutterDrawer();
            var millFormDrawer = new MillFormDrawer();
            var threadMillDrawer = new ThreadMillDrawer();
            var standardDrillDrawer = new StandardDrillDrawer();
            var coreDrillDrawer = new CoreDrillDrawer();
            var stepDrillDrawer = new StepDrillDrawer();
            var spotFaceDrawer = new SpotFaceDrawer();
            var spotDrillDrawer = new SpotDrillDrawer();
            var centerBellDrawer = new CenterBellDrawer();
            var boreDrawer = new BoreDrawer();
            var drillReamDrawer = new DrillReamDrawer();
            var counterBoreDrawer = new CounterBoreDrawer();
            var counterSinkDrawer = new CounterSinkDrawer();
            var tapDrawer = new TapDrawer();
            var backCounterSinkDrawer = new BackCounterSinkDrawer();
            var boringBarDrawer = new BoringBarDrawer();
            var chamferBoringBarDrawer = new ChamferBoringBarDrawer();

            Definitions = new List<ToolTypeDefinition>
            {
                // Mill
                new("01", "01", "Mill", "5 Parameter", fiveParameterDrawer),
                new("01", "02", "Mill", "7 Parameter", sevenParameterDrawer),
                new("01", "03", "Mill", "10 Parameter", tenParameterDrawer),
                new("01", "04", "Mill", "Ball", ballDrawer),
                new("01", "05", "Mill", "Chamfer Mill", chamferMillDrawer),
                new("01", "06", "Mill", "Spherical Mill", sphericalMillDrawer),
                new("01", "07", "Mill", "Dovetail Mill", dovetailMillDrawer),
                new("01", "15", "Mill", "Mill Form", millFormDrawer),
                
                // Other Mill-like
                new("08", "00", "T Slot Cut", "None", tSlotCutterDrawer),
                new("07", "00", "Barrel Cutter", "None", barrelCutterDrawer),
                new("02", "10", "Drill", "Thread Mill", threadMillDrawer),
                
                // Drill
                new("02", "00", "Drill", "Standard", standardDrillDrawer),
                new("02", "01", "Drill", "Center Bell", centerBellDrawer),
                new("02", "02", "Drill", "Counter Sink", counterSinkDrawer),
                new("02", "03", "Drill", "Spot Face", spotFaceDrawer),
                new("02", "04", "Drill", "Spot Drill", spotDrillDrawer),
                new("02", "05", "Drill", "Bore", boreDrawer),
                new("02", "06", "Drill", "Drill Ream", drillReamDrawer),
                new("02", "07", "Drill", "Counter Bore", counterBoreDrawer),
                new("02", "08", "Drill", "Tap", tapDrawer),
                new("02", "12", "Drill", "Step Drill", stepDrillDrawer),
                new("02", "13", "Drill", "Core Drill", coreDrillDrawer),
                new("02", "14", "Drill", "Back Counter Sink", backCounterSinkDrawer),
                new("02", "15", "Drill", "Boring Bar", boringBarDrawer),
                new("02", "16", "Drill", "Chamfer Boring Bar", chamferBoringBarDrawer),
            };
        }

        // UPDATED: This method now compares the numbers directly, ignoring leading zeros.
        public static ToolTypeDefinition Find(string ugt, string ugst)
        {
            // Safely convert the input strings to integers for comparison.
            if (!int.TryParse(ugt, out int ugtNum) || !int.TryParse(ugst, out int ugstNum))
            {
                return null; // Return nothing if the input is not a valid number.
            }

            return Definitions.FirstOrDefault(d =>
            {
                // Safely convert the definition's strings to integers.
                if (int.TryParse(d.UGT, out int defUgtNum) && int.TryParse(d.UGST, out int defUgstNum))
                {
                    // Compare the numbers, which makes "5" equal to "05".
                    return defUgtNum == ugtNum && defUgstNum == ugstNum;
                }
                return false;
            });
        }
    }
}

