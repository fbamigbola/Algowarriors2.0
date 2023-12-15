using System;
using System.Collections.Generic;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Parameters;

namespace QuantConnect.Algorithm.CSharp
{
    // NB : La version qui précede cet algo est MACDTrendRSIAlgoWarriorsPTFRebalanceStopLossJournalise
    public class AlgoWarriors : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private DateTime _previous;
        private Dictionary<string, MovingAverageConvergenceDivergence> _macdIndicators;
        private Dictionary<string, RelativeStrengthIndex> _rsiIndicators;
        private readonly string[] _symbols = { "AAPL", "BAC", "AIG", "IBM" };

        // Paramètre de tolérance
        [Parameter(name: "toleranceValue")]
        public decimal Tolerance = 0.0025m;

        public override void Initialize()
        {
            // Définir la période de démarrage et de fin
            SetStartDate(2001, 01, 01);
            SetEndDate(2022, 01, 01);

            // Ajouter les titres au portefeuille
            foreach (var symbol in _symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Hour);
            }

            // Initialiser les indicateurs MACD et RSI pour chaque titre
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
            // Vérifier si les données ont été mises à jour
            if (_previous.Date == Time.Date) return;

            // Parcourir chaque titre
            foreach (var symbol in _symbols)
            {
                var macd = _macdIndicators[symbol];
                var rsi = _rsiIndicators[symbol];

                // Vérifier si les indicateurs sont prêts
                if (!macd.IsReady || !rsi.IsReady) continue;

                var holding = Portfolio[symbol];

                var signalDeltaPercent = (macd - macd.Signal) / macd.Fast;

                // Intégrer le RSI dans les conditions d'achat et de vente
                if (holding.Quantity <= 0 && signalDeltaPercent > Tolerance && rsi > 50)
                {
                    // Entrer en position
                    SetHoldings(symbol, 0.25);
                }
                else if (holding.Quantity > 0 && signalDeltaPercent < -Tolerance && rsi < 50)
                {
                    // Liquider la position
                    Liquidate(symbol);
                }

                // Vérifier et liquider la position si le prix est en dessous du stop loss
                CheckStopLoss(symbol, data[symbol].Close, 0.75m);

                // Tracer le cours de clôture du titre
                Plot(symbol + "_Close", data[symbol].Close);
            }

            // Mettre à jour la variable de date précédente
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
                    // Liquider la position si le prix est inférieur au stop loss
                    Liquidate(symbol);
                }
            }
        }

        public override void OnEndOfDay()
        {
            // Rééquilibrer le portefeuille à la fin de chaque journée de trading
            if (_previous.Year != Time.Year)
            {
                RebalancePortfolio();
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            // Gérer les événements liés aux ordres (achat/vente)
            if (orderEvent.Status == OrderStatus.Filled)
            {
                string message = orderEvent.Direction == OrderDirection.Sell ? "Vendu" : "Acheté";
                var endMessage = $"{orderEvent.UtcTime.ToShortDateString()}, Prix : {this.CurrentSlice.Bars[orderEvent.Symbol].Close:N3}$; Portfolio : {Portfolio.CashBook[Portfolio.CashBook.AccountCurrency].Amount:N3}$, {Portfolio[orderEvent.Symbol].Quantity} actions, Valeur Totale : {Portfolio.TotalPortfolioValue:N3}$, Frais Totals : {Portfolio.TotalFees:N3}$";
                // Ignorer les petits ajustements d'ordres
                if (orderEvent.AbsoluteFillQuantity * orderEvent.FillPrice > 100)
                {
                    Log($"{message} {endMessage}");
                }
            }
        }

        private void RebalancePortfolio()
        {
            // Rééquilibrer le portefeuille en liquide toutes les positions et en entrant à nouveau
            foreach (var symbol in _symbols)
            {
                Liquidate(symbol);
            }

            foreach (var symbol in _symbols)
            {
                SetHoldings(symbol, 0.25);
            }
        }

        // Propriétés de l'interface IRegressionAlgorithmDefinition
        public bool CanRunLocally { get; } = true;

        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        public long DataPoints => 22137;

        public int AlgorithmHistoryDataPoints => 0;

        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            // Mettre à jour les statistiques attendues pour le portefeuille de 4 titres
        };
    }
}
