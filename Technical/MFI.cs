namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Money Flow Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38174-money-flow-index")]
	public class MFI : Indicator
	{
		#region Fields

		private readonly LineSeries _overbought = new("Overbought");
		private readonly LineSeries _oversold = new("Oversold");
		private ValueDataSeries _fakeSeries = new(Resources.FakeSeries);
		private ValueDataSeries _greenSeries = new(Resources.GreenSeries);
		private int _lastBar;
		private ValueDataSeries _negativeFlow = new("NegFlow");

		private int _period;
		private ValueDataSeries _positiveFlow = new("PosFlow");
		private decimal _previousTypical;
		private ValueDataSeries _sitSeries = new(Resources.SitSeries);
		private ValueDataSeries _weakSeries = new(Resources.WeakSeries);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 20)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

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

		#endregion

		#region ctor

		public MFI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DenyToChangePanel = true;

			_period = 14;
			_lastBar = -1;
			_overbought.Color = _oversold.Color = Colors.Green;
			_overbought.Value = 80;
			_oversold.Value = 20;

			_greenSeries.ShowZeroValue = _weakSeries.ShowZeroValue = _fakeSeries.ShowZeroValue = _sitSeries.ShowZeroValue = false;
			_greenSeries.IsHidden = _weakSeries.IsHidden = _fakeSeries.IsHidden = _sitSeries.IsHidden = true;
			_greenSeries.VisualType = _weakSeries.VisualType = _fakeSeries.VisualType = _sitSeries.VisualType = VisualMode.Histogram;

			_greenSeries.Color = Colors.Green;
			_weakSeries.Color = Colors.Gray;
			_fakeSeries.Color = Colors.DodgerBlue;
			_sitSeries.Color = Colors.DarkRed;

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