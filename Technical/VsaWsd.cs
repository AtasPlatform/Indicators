namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	[DisplayName("VSA – WSD Histogram")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38318-vsa-wsd-histogram")]
	public class VsaWsd : Indicator
	{
        #region Fields

		private decimal _tickSize;
        private readonly EMA _ema = new() { Period = 100 };

        private readonly ValueDataSeries _avgVolume = new("AvgVolumeId", "AvgVolume")
		{
			Color = Colors.Goldenrod,
			LineDashStyle = LineDashStyle.Dash,
			UseMinimizedModeIfEnabled = true
        };
        private readonly ValueDataSeries _dotsBuy = new("DotsBuyId", "DotsBuy")
		{
			Color = DefaultColors.Lime.Convert(),
			VisualType = VisualMode.Dots,
			LineDashStyle = LineDashStyle.Dot,
			Width = 5,
            ShowTooltip = false,
			ShowCurrentValue = false,
			ShowZeroValue = false,
			IgnoredByAlerts = true,
			ResetAlertsOnNewBar = true
        };
        private readonly ValueDataSeries _dotsNeutral = new("DotsNeutralId", "DotsNeutral")
        {
			Color = Colors.Gray,
			VisualType = VisualMode.Dots,
			LineDashStyle = LineDashStyle.Dot,
			Width = 5,
            ShowTooltip = false,
			ShowCurrentValue = false,
			ShowZeroValue = false,
			IgnoredByAlerts = true,
			ResetAlertsOnNewBar = true
        };
		private readonly ValueDataSeries _dotsSell = new("DotsSellId", "DotsSell")
		{
			Color = DefaultColors.Red.Convert(),
			VisualType = VisualMode.Dots,
			LineDashStyle = LineDashStyle.Dot,
			Width = 5,
			ShowTooltip = false,
			ShowCurrentValue = false,
			ShowZeroValue = false,
			IgnoredByAlerts = true,
			ResetAlertsOnNewBar = true
        };
		private readonly ValueDataSeries _highLow = new("HighLowId", "HighLow")
		{
			Color = DefaultColors.Blue.Convert(),
			VisualType = VisualMode.Histogram,
			Width = 2,
            UseMinimizedModeIfEnabled = true,
            IgnoredByAlerts = true,
            ResetAlertsOnNewBar = true
        };
		private readonly ValueDataSeries _lowerWick = new("LowerWickId", "LowerWick")
		{
			Color = DefaultColors.Red.Convert(),
			VisualType = VisualMode.Histogram,
			Width = 2,
            UseMinimizedModeIfEnabled = true,
            IgnoredByAlerts = true,
            ResetAlertsOnNewBar = true
        };
		private readonly ValueDataSeries _upperWick = new("UpperWickId", "UpperWick")
		{
			Color = DefaultColors.Lime.Convert(),
			VisualType = VisualMode.Histogram,
			Width = 2,
			UseMinimizedModeIfEnabled = true,
			IgnoredByAlerts = true,
			ResetAlertsOnNewBar = true
        };
		
		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 1)]
		[Range(1, 10000)]
		public int Period
		{
			get => _ema.Period;
			set
			{
				_ema.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public VsaWsd()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _highLow;
			DataSeries.Add(_upperWick);
			DataSeries.Add(_lowerWick);
			DataSeries.Add(_avgVolume);
			DataSeries.Add(_dotsBuy);
			DataSeries.Add(_dotsSell);
			DataSeries.Add(_dotsNeutral);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_dotsBuy.Clear();
				_dotsSell.Clear();
				_dotsNeutral.Clear();
				_tickSize = ChartInfo.PriceChartContainer.Step;
			}

			var candle = GetCandle(bar);
			var result = candle.High - candle.Low;

			_highLow[bar] = result / _tickSize;

			var dResult1 = candle.Open > candle.Close
				? candle.High - candle.Open
                : candle.High - candle.Close;
			
			var dResult2 = candle.Open > candle.Close
				? candle.Low - candle.Close
				: candle.Low - candle.Open;
			
			_upperWick[bar] = dResult1 / _tickSize;
			_lowerWick[bar] = dResult2 / _tickSize;

			var volume = (candle.High - candle.Low) / _tickSize;
			_avgVolume[bar] = _ema.Calculate(bar, volume);

			if (bar == 0)
				return;

			var prevCandle = GetCandle(bar - 1);

			if (candle.Close > prevCandle.Open && _highLow[bar] < _highLow[bar - 1])
			{
				_dotsBuy[bar] = _highLow[bar];
				_dotsSell[bar] = _dotsNeutral[bar] = 0;
			}
			else if (candle.Close < prevCandle.Open && _highLow[bar] < _highLow[bar - 1])
			{
				_dotsSell[bar] = _highLow[bar];
				_dotsBuy[bar] = _dotsNeutral[bar] = 0;
			}
			else
			{
				_dotsNeutral[bar] = _highLow[bar];
				_dotsBuy[bar] = _dotsSell[bar] = 0;
			}
		}

		#endregion
	}
}