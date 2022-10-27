namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	[DisplayName("Money Flow Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38174-money-flow-index")]
	public class MFI : Indicator
	{
		#region Fields

		private LineSeries _overbought = new(Resources.Overbought)
		{
			Color = Colors.Green,
			Value = 80,
			IsHidden = true
        };
		private LineSeries _oversold = new(Resources.Oversold)
		{
			Color = Colors.Green,
			Value = 20,
			IsHidden = true
        };
		private ValueDataSeries _fakeSeries = new(Resources.FakeSeries)
		{
			Color = Colors.DodgerBlue,
			VisualType = VisualMode.Histogram,
            IsHidden = true,
            ShowZeroValue = false
		};
		private ValueDataSeries _greenSeries = new(Resources.GreenSeries)
		{
			Color = Colors.Green,
			VisualType = VisualMode.Histogram,
			IsHidden = true,
			ShowZeroValue = false
		};
		private int _lastBar = -1;
        private ValueDataSeries _negativeFlow = new("NegFlow");

		private int _period = 14;
        private ValueDataSeries _positiveFlow = new("PosFlow");
		private decimal _previousTypical;
		private ValueDataSeries _sitSeries = new(Resources.SitSeries)
		{
			Color = Colors.DarkRed,
			VisualType = VisualMode.Histogram,
            IsHidden = true,
            ShowZeroValue = false
		};
		private ValueDataSeries _weakSeries = new(Resources.WeakSeries)
		{
			Color = Colors.Gray,
			VisualType = VisualMode.Histogram,
            IsHidden = true,
            ShowZeroValue = false
		};
        private bool _drawLines = true;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 20)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				_previousTypical = -1;
				RecalculateValues();
			}
		}
		
		[Display(ResourceType = typeof(Resources), Name = "GreenSeriesColor", GroupName = "Visualization", Order = 200)]
		public Color GreenColor
		{
			get => _greenSeries.Color;
			set => _greenSeries.Color = value;
		}
		
		[Display(ResourceType = typeof(Resources), Name = "WeakSeriesColor", GroupName = "Visualization", Order = 210)]
		public Color WeakColor
		{
			get => _weakSeries.Color;
			set => _weakSeries.Color = value;
		}
		
		[Display(ResourceType = typeof(Resources), Name = "FakeSeriesColor", GroupName = "Visualization", Order = 220)]
		public Color FakeColor
		{
			get => _fakeSeries.Color;
			set => _fakeSeries.Color = value;
		}
		
		[Display(ResourceType = typeof(Resources), Name = "SitSeriesColor", GroupName = "Visualization", Order = 230)]
		public Color SitColor
		{
			get => _sitSeries.Color;
			set => _sitSeries.Color = value;
		}
		
		[Display(ResourceType = typeof(Resources), Name = "Show", GroupName = "Line", Order = 300)]
		public bool DrawLines
		{
			get => _drawLines;
			set
			{
				_drawLines = value;

				if (value)
				{
					if (LineSeries.Contains(_overbought))
						return;

					LineSeries.Add(_overbought);
					LineSeries.Add(_oversold);
				}
				else
				{
					LineSeries.Clear();
				}

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Overbought", GroupName = "Line", Order = 310)]
		public LineSeries OverboughtLine 
		{ 
			get => _overbought;
			set => _overbought = value;
		} 

		[Display(ResourceType = typeof(Resources), Name = "Oversold", GroupName = "Line", Order = 320)]
		public LineSeries OversoldLine
        { 
			get => _oversold;
			set => _oversold = value;
		} 
        #endregion

        #region ctor

        public MFI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DenyToChangePanel = true;
			
			DataSeries[0] = _greenSeries;
			DataSeries.Add(_weakSeries);
			DataSeries.Add(_fakeSeries);
			DataSeries.Add(_sitSeries);

			LineSeries.Add(_overbought);
			LineSeries.Add(_oversold);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			var typical = (candle.High + candle.Low + candle.Close) / 3.0m;

			if (bar == 0)
				_previousTypical = typical;

			var moneyFlow = typical * candle.Volume;

			if (typical > _previousTypical)
				_positiveFlow[bar] = moneyFlow;
			else
				_negativeFlow[bar] = moneyFlow;

			DataSeries.ForEach(x => ((ValueDataSeries)x)[bar] = 0);

			if (bar < Period)
				return;

			var positiveFlow = _positiveFlow.CalcSum(Period, Math.Max(bar - Period, 0));
			var negativeFlow = _negativeFlow.CalcSum(Period, Math.Max(bar - Period, 0));

			var renderValue = 100m;

			if (negativeFlow != 0.0m)
			{
				var moneyRatio = positiveFlow / negativeFlow;
				renderValue = 100.0m - 100.0m / (1.0m + moneyRatio);
			}

			if (bar != _lastBar)
				_previousTypical = typical;

			_lastBar = bar;

			if (bar == 0)
			{
				_greenSeries[bar] = renderValue;
				return;
			}

			var prevValue = _greenSeries[bar - 1];

			if (_greenSeries[bar - 1] == 0)
			{
				if (_weakSeries[bar - 1] != 0)
					prevValue = _weakSeries[bar - 1];
				else if (_fakeSeries[bar - 1] != 0)
					prevValue = _fakeSeries[bar - 1];
				else if (_sitSeries[bar - 1] != 0)
					prevValue = _sitSeries[bar - 1];
			}

			var prevCandle = GetCandle(bar - 1);

			if (renderValue >= prevValue)
			{
				if (candle.Ticks >= prevCandle.Ticks)
					_greenSeries[bar] = renderValue;
				else
					_fakeSeries[bar] = renderValue;
			}
			else
			{
				if (candle.Ticks >= prevCandle.Ticks)
					_sitSeries[bar] = renderValue;
				else
					_weakSeries[bar] = renderValue;
			}
		}

		#endregion
	}
}