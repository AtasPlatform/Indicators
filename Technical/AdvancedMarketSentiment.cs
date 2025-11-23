using System;
using System.ComponentModel;
using System.Windows.Media;
using ATAS.Indicators;
using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical;
using Utils.Common.Logging;
using OFT.Attributes;

[DisplayName("Advanced Market Sentiment Indicator")]
[Category("Market Analysis")]
[Description("Combines CVD, Price, Delta, and Volume for comprehensive market sentiment analysis")]
public class AdvancedMarketSentiment : Indicator
{
    #region Private Fields
    private readonly ValueDataSeries _sentimentSeries;
    private readonly ValueDataSeries _smoothedSentiment;
    private readonly ValueDataSeries _momentumSeries;
    private readonly ValueDataSeries _volumeWeightedSentiment;
    
    private int _smoothingPeriod = 3;
    private int _momentumPeriod = 5;
    private double _volumeThreshold = 0.8;
    private double _sentimentThreshold = 0.3;

    // Add a private Logger instance to the class
    private readonly Logger _logger = new Logger();

    // Add this property to the class to fix CS0103
    private Color[] CandleColors => new Color[CurrentBar + 1];
    #endregion

    #region Public Properties
    [OFT.Attributes.Parameter]
    [DisplayName("Smoothing Period")]
    [Description("Period for sentiment smoothing")]
    public int SmoothingPeriod
    {
        get => _smoothingPeriod;
        set
        {
            _smoothingPeriod = Math.Max(1, value);
            RecalculateValues();
        }
    }

    [OFT.Attributes.Parameter]
    [DisplayName("Momentum Period")]
    [Description("Period for momentum calculation")]
    public int MomentumPeriod           
    {
        get => _momentumPeriod;
        set
        {
            _momentumPeriod = Math.Max(2, value);
            RecalculateValues();
        }
    }

    [OFT.Attributes.Parameter]
    [DisplayName("Volume Threshold")]
    [Description("Volume threshold for signal validation")]
    public double VolumeThreshold
    {
        get => _volumeThreshold;
        set
        {
            _volumeThreshold = Math.Max(0.1, Math.Min(1.0, value));
            RecalculateValues();
        }
    }

    [OFT.Attributes.Parameter]
    [DisplayName("Sentiment Threshold")]
    [Description("Threshold for strong sentiment signals")]
    public double SentimentThreshold
    {
        get => _sentimentThreshold;
        set
        {
            _sentimentThreshold = Math.Max(0.1, Math.Min(0.8, value));
            RecalculateValues();
        }
    }
    #endregion

    #region Constructor
    public AdvancedMarketSentiment()
        : base(true)
    {
        Panel = IndicatorDataProvider.NewPanel;
        
        _sentimentSeries = new ValueDataSeries("Raw Sentiment")
        {
            VisualType = VisualMode.Line,
            Color = Colors.Gray,
            Width = 1,
            ShowCurrentValue = true
        };
        
        _smoothedSentiment = new ValueDataSeries("Smoothed Sentiment")
        {
            VisualType = VisualMode.Line,
            Color = Colors.Blue,
            Width = 2,
            ShowCurrentValue = true
        };
        
        _momentumSeries = new ValueDataSeries("Momentum")
        {
            VisualType = VisualMode.Histogram,
            ShowCurrentValue = true
        };
        
        _volumeWeightedSentiment = new ValueDataSeries("Volume Weighted")
        {
            VisualType = VisualMode.Line,
            Color = Colors.Purple,
            Width = 1,
            ShowCurrentValue = true
        };

        DataSeries[0] = _sentimentSeries;
        DataSeries.Add(_smoothedSentiment);
        DataSeries.Add(_momentumSeries);
        DataSeries.Add(_volumeWeightedSentiment);
    }
    #endregion

    #region Core Calculation Methods
    /// <summary>
    /// Main sentiment calculation function
    /// Returns value between -1 (extremely bearish) to 1 (extremely bullish)
    /// </summary>
    public double CalculateSentiment(
        decimal cvdValue, 
        decimal open, decimal high, decimal low, decimal close,
        decimal delta, 
        decimal volume)
    {
        try
        {
            if (volume == 0)
                return 0;

            // 1. CVD-based sentiment (30% weight)
            double cvdSentiment = CalculateCVDSentiment(cvdValue, volume);
            
            // 2. Price action sentiment (25% weight)
            double priceSentiment = CalculatePriceSentiment(open, high, low, close);
            
            // 3. Delta-volume sentiment (30% weight)
            double deltaSentiment = CalculateDeltaSentiment(delta, volume);
            
            // 4. Volume confirmation (15% weight)
            double volumeConfirmation = CalculateVolumeConfirmation(volume, delta);

            // Weighted combination
            double rawSentiment = 
                cvdSentiment * 0.30 +
                priceSentiment * 0.25 +
                deltaSentiment * 0.30 +
                volumeConfirmation * 0.15;

            // Apply non-linear transformation for better signal quality
            return ApplyNonLinearTransformation(rawSentiment);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in CalculateSentiment: {ex.Message}");
            return 0;
        }
    }

    private double CalculateCVDSentiment(decimal cvdValue, decimal volume)
    {
        if (volume == 0) return 0;
        
        // Normalize CVD by volume to get relative strength
        double normalizedCVD = (double)(cvdValue / volume);
        
        // Apply sigmoid function to bound between -1 and 1
        return 2.0 / (1.0 + Math.Exp(-2.0 * normalizedCVD)) - 1.0;
    }

    private double CalculatePriceSentiment(decimal open, decimal high, decimal low, decimal close)
    {
        decimal range = high - low;
        if (range == 0) return 0;

        // Calculate multiple price factors
        decimal bodySize = Math.Abs(close - open);
        decimal upperShadow = high - Math.Max(open, close);
        decimal lowerShadow = Math.Min(open, close) - low;
        
        // Body strength (positive for bullish, negative for bearish)
        double bodyStrength = (double)((close - open) / range);
        
        // Shadow analysis (long lower shadow = bullish, long upper shadow = bearish)
        double shadowBias = (double)((lowerShadow - upperShadow) / range);
        
        // Close position in range
        double closePosition = (double)((close - low) / range - 0.5m) * 2;
        
        return (bodyStrength + shadowBias + closePosition) / 3.0;
    }

    private double CalculateDeltaSentiment(decimal delta, decimal volume)
    {
        if (volume == 0) return 0;
        
        double deltaRatio = (double)(delta / volume);
        
        // Enhanced delta analysis with volume context
        if (Math.Abs(deltaRatio) > _volumeThreshold)
        {
            // Strong delta signals get amplified
            return Math.Sign(deltaRatio) * Math.Min(1.0, Math.Abs(deltaRatio) * 1.2);
        }
        
        return deltaRatio;
    }

    private double CalculateVolumeConfirmation(decimal volume, decimal delta)
    {
        // Analyze if volume confirms the delta direction
        decimal avgVolume = CalculateAverageVolume(20); // 20-period average
        if (avgVolume == 0) return 0;
        
        double volumeRatio = (double)(volume / avgVolume);
        double deltaDirection = Math.Sign((double)delta);
        
        // High volume confirms the delta direction
        if (volumeRatio > 1.2)
            return deltaDirection * Math.Min(0.5, (volumeRatio - 1.0) * 0.5);
        
        // Low volume weakens the signal
        if (volumeRatio < 0.8)
            return -deltaDirection * Math.Min(0.3, (1.0 - volumeRatio) * 0.5);
        
        return 0;
    }

    private double ApplyNonLinearTransformation(double rawSentiment)
    {
        // Apply cubic transformation to emphasize strong signals
        double transformed = Math.Pow(rawSentiment, 3) * 0.7 + rawSentiment * 0.3;
        
        // Ensure bounds
        return Math.Max(-1.0, Math.Min(1.0, transformed));
    }
    #endregion

    #region Candle Coloring Function
    /// <summary>
    /// Colors candle based on sentiment value
    /// </summary>
    public Color GetCandleColor(double sentimentValue)
    {
        if (sentimentValue >= _sentimentThreshold)
        {
            // Strong bullish - bright green
            byte intensity = (byte)(200 + (55 * (sentimentValue - _sentimentThreshold) / (1 - _sentimentThreshold)));
            return Color.FromRgb(0, intensity, 0);
        }
        else if (sentimentValue <= -_sentimentThreshold)
        {
            // Strong bearish - bright red
            byte intensity = (byte)(200 + (55 * (Math.Abs(sentimentValue) - _sentimentThreshold) / (1 - _sentimentThreshold)));
            return Color.FromRgb(intensity, 0, 0);
        }
        else if (sentimentValue > 0.1)
        {
            // Moderate bullish - light green
            double factor = (sentimentValue - 0.1) / (_sentimentThreshold - 0.1);
            return Color.FromRgb((byte)(200 - 100 * factor), (byte)(220 - 20 * factor), (byte)(200 - 100 * factor));
        }
        else if (sentimentValue < -0.1)
        {
            // Moderate bearish - light red
            double factor = (Math.Abs(sentimentValue) - 0.1) / (_sentimentThreshold - 0.1);
            return Color.FromRgb((byte)(220 - 20 * factor), (byte)(200 - 100 * factor), (byte)(200 - 100 * factor));
        }
        else
        {
            // Neutral - gray
            return Colors.Gray;
        }
    }
    #endregion

    #region Indicator Calculation
    protected override void OnCalculate(int bar, decimal value)
    {
        try
        {
            if (bar < Math.Max(_smoothingPeriod, _momentumPeriod))
            {
                _sentimentSeries[bar] = 0;
                _smoothedSentiment[bar] = 0;
                _momentumSeries[bar] = 0;
                _volumeWeightedSentiment[bar] = 0;
                return;
            }

            var candle = GetCandle(bar);

            // Calculate current sentiment
            double currentSentiment = CalculateSentiment(
                GetCVD(bar), // You'll need to implement CVD data source
                candle.Open, candle.High, candle.Low, candle.Close,
                candle.Delta, 
                candle.Volume
            );

            _sentimentSeries[bar] = (decimal)currentSentiment;
            
            // Calculate smoothed sentiment
            double smoothed = CalculateSmoothedSentiment(bar);
            _smoothedSentiment[bar] = (decimal)smoothed;
            
            // Calculate momentum
            double momentum = CalculateMomentum(bar);
            _momentumSeries[bar] = (decimal)momentum;
            
            // Volume weighted sentiment
            _volumeWeightedSentiment[bar] = (decimal)(currentSentiment * (double)(candle.Volume / CalculateAverageVolume(10)));

            // Color the candle
            Color candleColor = GetCandleColor(currentSentiment);
            CandleColors[bar] = candleColor;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in OnCalculate for bar {bar}: {ex.Message}");
        }
    }

    private double CalculateSmoothedSentiment(int currentBar)
    {
        double sum = 0;
        int count = 0;
        
        for (int i = Math.Max(0, currentBar - _smoothingPeriod + 1); i <= currentBar; i++)
        {
            sum += (double)_sentimentSeries[i];
            count++;
        }
        
        return count > 0 ? sum / count : 0;
    }

    private double CalculateMomentum(int currentBar)
    {
        if (currentBar < _momentumPeriod)
            return 0;

        double current = (double)_smoothedSentiment[currentBar];
        double previous = (double)_smoothedSentiment[currentBar - _momentumPeriod];

        return current - previous;
    }

    private decimal CalculateAverageVolume(int period)
    {
        decimal sum = 0;
        int count = 0;
        
        for (int i = Math.Max(0, CurrentBar - period + 1); i <= CurrentBar; i++)
        {
            sum += GetCandle(i).Volume;
            count++;
        }
        
        return count > 0 ? sum / count : 1;
    }
    #endregion

    #region Utility Methods
    // You'll need to implement these based on your ATAS data source
    private decimal GetCVD(int bar)
    {
        // Implement based on your CVD data source
        // This could be from another indicator or direct market data
        return 0;
    }

    protected override void RecalculateValues()
    {
        RecalculateAllValues();
    }

    private void RecalculateAllValues()
    {
        // Recalculate all indicator values for all bars
        for (int bar = 0; bar <= CurrentBar; bar++)
        {
            OnCalculate(bar, 0);
        }
    }
    #endregion
}