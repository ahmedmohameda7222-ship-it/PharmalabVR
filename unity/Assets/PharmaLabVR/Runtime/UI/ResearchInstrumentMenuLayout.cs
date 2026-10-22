using System;
using System.Collections.Generic;

namespace PharmaLabVR.UI
{
    // UI layout only: no chemistry, stock, or learner state is authored here.
    // Each row has one 46pt button on 56pt centers; page navigation is explicit.
    public sealed class ResearchMenuItem
    {
        public string Name { get; }
        public int Row { get; }
        public ResearchMenuItem(string name, int row) { Name = name; Row = row; }
    }

    public static class ResearchInstrumentMenuLayout
    {
        public static bool ShowGlobalSettings(int instrumentPage) =>
            instrumentPage == 0;

        public static readonly string[] AllNames =
        {
            "Pipette aspirate 0.05 ml", "Pipette dispense 0.05 ml",
            "Bottle pour 0.01 ml", "Rinse pipette", "Rinse bottle",
            "Stock setup", "Choose stock", "Prepare chosen stock",
            "Prepare rinse water", "Choose concentration",
            "More tools and lessons", "Prepare reserve stock",
            "Switch pipette to reserve", "Refill bottle 0.01 ml",
            "Prime burette 0.05 ml", "Strong acid lesson", "Mixed acid lesson",
            "Predict pH below 7", "Predict pH at least 7",
            "Back to instruments", "Exit tools"
        };

        // Panels: 1 operations; 2 stock preparation; 3 prediction;
        // 4 reserve stock and lesson selection. One ray-hit target per row.
        public static IReadOnlyList<ResearchMenuItem> Visible(int page,
            bool needsPreparation, bool canTransfer, bool reservePrepared,
            bool canFillBurette, bool canPredict, bool canDispense, bool hasRinseWater)
        {
            var items = new List<ResearchMenuItem>(7);
            void Add(string name, int row, bool show = true)
            {
                if (show) items.Add(new ResearchMenuItem(name, row));
            }
            switch (page)
            {
                case 1:
                    Add("Pipette aspirate 0.05 ml", 0, canTransfer);
                    Add("Pipette dispense 0.05 ml", 1, canTransfer && canDispense);
                    Add("Bottle pour 0.01 ml", 2, canTransfer);
                    Add("Rinse pipette", 3, canTransfer && hasRinseWater);
                    Add("Rinse bottle", 4, canTransfer && hasRinseWater);
                    Add("Stock setup", 5);
                    Add("Exit tools", 6);
                    break;
                case 2:
                    Add("Choose stock", 0, needsPreparation);
                    Add("Prepare chosen stock", 1, needsPreparation);
                    Add("Prepare rinse water", 2, needsPreparation);
                    Add("More tools and lessons", 3);
                    Add("Choose concentration", 4, needsPreparation);
                    Add("Back to instruments", 5);
                    Add("Exit tools", 6);
                    break;
                case 3:
                    Add("Predict pH below 7", 0, canPredict);
                    Add("Predict pH at least 7", 1, canPredict);
                    Add("Back to instruments", 5);
                    Add("Exit tools", 6);
                    break;
                case 4:
                    Add("Prepare reserve stock", 0, needsPreparation && !reservePrepared);
                    Add("Switch pipette to reserve", 0, canTransfer && reservePrepared);
                    Add("Refill bottle 0.01 ml", 1, canTransfer && reservePrepared);
                    Add("Prime burette 0.05 ml", 2, canTransfer && canFillBurette);
                    Add("Strong acid lesson", 3);
                    Add("Mixed acid lesson", 4);
                    Add("Back to instruments", 5);
                    Add("Exit tools", 6);
                    break;
            }
            return items;
        }
    }
}
