namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Money Flow Index")]
	public class MFI : Indicator
	{
		#region Fields

		private readonly LineSeries _overbought = new("Overbought");
		private readonly LineSeries _oversold = new("Oversold");
		private int _lastBar;
		private ValueDataSeries _negativeFlow = new("NegFlow");

		private int _period;
		private ValueDataSeries _positiveFlow = new("PosFlow");
		private decimal _previousTypical;
		private ValueDataSeries _series = new("MFI");

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

		[Display(ResourceType = typeof(Resources), Name = "Overbought", GroupName = "Common", Order = 10)]
		public decimal Overbought
		{
			get => _overbought.Value;
			set
			{
				if (value < _oversold.Value)
					return;

				_overbought.Value = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Oversold", GroupName = "Common", Order = 20)]
		public decimal Oversold
		{
			get => _oversold.Value;
			set
			{
				if (value > _overbought.Value)
					return;

				_oversold.Value = value;
			}
		}

		#endregion

		#region ctor

		public MFI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 14;
			_lastBar = -1;
			_overbought.Color = _oversold.Color = Colors.Green;
			_overbought.Value = 80;
			_oversold.Value = 20;

			_series.ShowZeroValue = false;
			DataSeries[0] = _series;

			LineSeries.Add(_overbought);
			LineSeries.Add(_oversold);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var currentCandle = GetCandle(bar);
			var typical = (currentCandle.High + currentCandle.Low + currentCandle.Close) / 3.0m;

			if (bar == 0)
			{
				_previousTypical = typical;
				_series.SetPointOfEndLine(Period);
			}

			var moneyFlow = typical * currentCandle.Volume;

			if (typical > _previousTypical)
				_positiveFlow[bar] = moneyFlow;
			else
				_negativeFlow[bar] = moneyFlow;

			if (bar < Period)
			{
				_series[bar] = 0m;
				return;
			}

			var positiveFlow = _positiveFlow.CalcSum(Period, Math.Max(bar - Period, 0));
			var negativeFlow = _negativeFlow.CalcSum(Period, Math.Max(bar - Period, 0));

			if (negativeFlow == 0.0m)
				_series[bar] = 100.0m;
			else
			{
				var moneyRatio = positiveFlow / negativeFlow;
				_series[bar] = 100.0m - 100.0m / (1.0m + moneyRatio);
			}

			if (bar != _lastBar)
				_previousTypical = typical;

			_lastBar = bar;
		}

		#endregion
	}
}