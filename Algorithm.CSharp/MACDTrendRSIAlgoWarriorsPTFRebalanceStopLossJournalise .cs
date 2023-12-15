using System;
using System.Collections.Generic;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Parameters;

namespace QuantConnect.Algorithm.CSharp
{
    public class MACDTrendRSIAlgoWarriorsPTFRebalanceStopLossJournalise : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private DateTime _previous;
        private Dictionary<string, MovingAverageConvergenceDivergence> _macdIndicators;
        private Dictionary<string, RelativeStrengthIndex> _rsiIndicators;
        private readonly string[] _symbols = { "AAPL", "BAC", "AIG", "IBM" };

        [Parameter(name: "toleranceValue")]
        public decimal Tolerance = 0.0025m;

        public override void Initialize()
        {
            SetStartDate(2001, 01, 01);
            SetEndDate(2022, 01, 01);

            foreach (var symbol in _symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Hour);
            }

            _macdIndicators = new Dictionary<string, MovingAverageConvergenceDivergence>();
            _rsiIndicators = new Dictionary<string, RelativeStrengthIndex>();

            foreach (var symbol in _symbols)
            {
                _macdIndicators[symbol] = MACD(symbol, 12, 26, 9, MovingAverageType.Exponential, Resolution.Hour);
                _rsiIndicators[symbol] = RSI(symbol, 14, MovingAverageType.Simple, Resolution.Hour);
            }
        }

        public void OnData(TradeBars data)
        {
            if (_previous.Date == Time.Date) return;

            foreach (var symbol in _symbols)
            {
                var macd = _macdIndicators[symbol];
                var rsi = _rsiIndicators[symbol];

                if (!macd.IsReady || !rsi.IsReady) continue;

                var holding = Portfolio[symbol];

                var signalDeltaPercent = (macd - macd.Signal) / macd.Fast;

                // Integrate RSI into the buy and sell conditions
                if (holding.Quantity <= 0 && signalDeltaPercent > Tolerance && rsi > 50)
                {
                    // Enter the position
                    SetHoldings(symbol, 0.25);
                }
                else if (holding.Quantity > 0 && signalDeltaPercent < -Tolerance && rsi < 50)
                {
                    Liquidate(symbol);
                }

                // Check and liquidate the position if the price is below the stop loss
                CheckStopLoss(symbol, data[symbol].Close, 0.75m);

                //Plot(symbol + "_MACD", macd, macd.Signal);
                Plot(symbol + "_Close", data[symbol].Close);
                //Plot(symbol + "_Fast", macd.Fast);
                //Plot(symbol + "_Slow", macd.Slow);
                //Plot(symbol + "_RSI", rsi);
            }

            _previous = Time;
        }

        private void CheckStopLoss(string symbol, decimal currentPrice, decimal stopLossPercentage)
        {
            var holding = Portfolio[symbol];

            if (holding.Quantity > 0)
            {
                var stopLossPrice = holding.AveragePrice * stopLossPercentage;

                if (currentPrice < stopLossPrice)
                {
                    Liquidate(symbol);
                }
            }
        }

        public override void OnEndOfDay()
        {
            if (_previous.Year != Time.Year)
            {
                RebalancePortfolio();
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status == OrderStatus.Filled)
            {
                string message = orderEvent.Direction == OrderDirection.Sell ? "Sold" : "Purchased";
                var endMessage = $"{orderEvent.UtcTime.ToShortDateString()}, Price: {this.CurrentSlice.Bars[orderEvent.Symbol].Close:N3}$; Portfolio: {Portfolio.CashBook[Portfolio.CashBook.AccountCurrency].Amount:N3}$, {Portfolio[orderEvent.Symbol].Quantity} shares, Total Value: {Portfolio.TotalPortfolioValue:N3}$, Total Fees: {Portfolio.TotalFees:N3}$";
                // Skip small adjusting orders
                if (orderEvent.AbsoluteFillQuantity * orderEvent.FillPrice > 100)
                {
                    Log($"{message} {endMessage}");
                }
            }
        }

        private void RebalancePortfolio()
        {
            foreach (var symbol in _symbols)
            {
                Liquidate(symbol);
            }

            foreach (var symbol in _symbols)
            {
                SetHoldings(symbol, 0.25);
            }
        }

        public bool CanRunLocally { get; } = true;

        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        public long DataPoints => 22137;

        public int AlgorithmHistoryDataPoints => 0;

        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            // Update your expected statistics for the portfolio of 4 stocks
        };
    }
}
