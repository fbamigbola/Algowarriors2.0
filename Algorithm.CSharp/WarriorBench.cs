using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    public class WarriorBench : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol _spy = QuantConnect.Symbol.Create("SPY", SecurityType.Equity, Market.USA);

        public override void Initialize()
        {
            SetStartDate(2001, 1, 1);
            SetEndDate(2022, 1, 1);
            SetCash(100000);
            AddEquity("SPY", Resolution.Hour);
        }

        public override void OnData(Slice data)
        {
            if (!Portfolio.Invested)
            {
                SetHoldings(_spy, 1);
                Debug("Purchased Stock");
            }
        }

        public override void OnEndOfDay()
        {
            if (Time.Day == EndDate.Day && Time.Month == EndDate.Month && Time.Year == EndDate.Year)
            {
                // Liquidate all holdings at the end of the period
                Liquidate();
                Debug("Liquidated all holdings at the end of the period");
            }
        }

        public bool CanRunLocally { get; } = true;

        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        public long DataPoints => 3943;

        public int AlgorithmHistoryDataPoints => 0;

        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            // Update your expected statistics here
        };
    }
}
