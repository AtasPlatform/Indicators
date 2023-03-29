namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common;

	[DisplayName("Open Interest")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/8560-open-interest-o")]
	public class OpenInterest : Indicator
	{
		#region Nested types

		public enum OpenInterestMode
		{
			[Display(ResourceType = typeof(Resources), Name = "ByBar")]
			ByBar,

			[Display(ResourceType = typeof(Resources), Name = "Session")]
			Session,

			[Display(ResourceType = typeof(Resources), Name = "Cumulative")]
			Cumulative
		}

		#endregion

		#region Fields

		private readonly CandleDataSeries _filterSeries = new("Open interest filtered")
		{
			UpCandleColor = Colors.LightBlue,
			DownCandleColor = Colors.LightBlue,
			IsHidden = true,
			ScaleIt = false,
			ShowCurrentValue = false,
			ShowTooltip = false,
			UseMinimizedModeIfEnabled = true,
			ResetAlertsOnNewBar = true
		};

		private readonly CandleDataSeries _oi = new("OI")
		{
			UseMinimizedModeIfEnabled = true,
			ResetAlertsOnNewBar = true
		};
		private decimal _filter;
		private bool _minimizedMode;

		private OpenInterestMode _mode = OpenInterestMode.ByBar;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Mode")]
		public OpenInterestMode Mode
		{
			get => _mode;
			set
			{
				_mode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Minimizedmode")]
		public bool MinimizedMode
		{
			get => _minimizedMode;
			set
			{
				_minimizedMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Filters")]
		[Range(0, 100000000)]
		public decimal Filter
		{
			get => _filter;
			set
			{
				_filter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "FilterColor", GroupName = "Visualization")]
		public Color FilterColor
		{
			get => _filterSeries.UpCandleColor;
			set => _filterSeries.UpCandleColor = _filterSeries.DownCandleColor = value;
		}

		#endregion

		#region ctor

		public OpenInterest()
			: base(true)
		{
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.OnlyValueOnAxis;
			DataSeries[0].Name = "Value";
			DataSeries[0].UseMinimizedModeIfEnabled = true;
			DataSeries.Add(_oi);
			DataSeries.Add(_filterSeries);
			Panel = IndicatorDataProvider.NewPanel;
		}

        #endregion

        #region Protected methods

        protected override void OnApplyDefaultColors()
        {
	        if (ChartInfo is null)
		        return;

	        _oi.UpCandleColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
	        _oi.DownCandleColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
	        _oi.BorderColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
        }

        protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var currentCandle = GetCandle(bar);

			if (currentCandle.OI == 0)
				return;

			var prevCandle = GetCandle(bar - 1);
			var currentOpen = prevCandle.OI;
			var candle = _oi[bar];

			switch (_mode)
			{
				case OpenInterestMode.ByBar:
					if (_minimizedMode)
					{
						if (currentCandle.OI > currentOpen)
						{
							candle.Open = 0;
							candle.Close = currentCandle.OI - currentOpen;
							candle.High = currentCandle.MaxOI - currentOpen;
						}
						else
						{
							candle.Open = currentOpen - currentCandle.OI;
							candle.Close = 0;
							candle.High = currentOpen - currentCandle.MinOI;
						}
					}
					else
					{
						candle.Open = 0;
						candle.Close = currentCandle.OI - currentOpen;
						candle.High = currentCandle.MaxOI - currentOpen;
						candle.Low = currentCandle.MinOI - currentOpen;
					}

					break;

				case OpenInterestMode.Cumulative:
					candle.Open = currentOpen;
					candle.Close = currentCandle.OI;
					candle.High = currentCandle.MaxOI;
					candle.Low = currentCandle.MinOI;
					break;

				default:
					var prevValue = _oi[bar - 1].Close;
					var dOi = currentOpen - prevValue;

					if (IsNewSession(bar))
						dOi = currentOpen;

					candle.Open = currentOpen - dOi;
					candle.Close = currentCandle.OI - dOi;
					candle.High = currentCandle.MaxOI - dOi;
					candle.Low = currentCandle.MinOI - dOi;
					break;
			}

			this[bar] = candle.Close;

			var oiValue = Math.Abs(candle.Close);

			if (oiValue < Filter || Filter == 0)
				_filterSeries[bar].Open = _filterSeries[bar].Close = _filterSeries[bar].High = _filterSeries[bar].Low = candle.Open;
			else
				_filterSeries[bar] = candle.MemberwiseClone();
		}

		#endregion
	}
}