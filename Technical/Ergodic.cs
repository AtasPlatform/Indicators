namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Ergodic")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45196-ergodic")]
	public class Ergodic : Indicator
	{
		#region Fields

		private readonly EMA _emaLong = new();
		private readonly EMA _emaLongAbs = new();

		private readonly EMA _emaShort = new();
		private readonly EMA _emaShortAbs = new();
		private readonly EMA _emaSignal = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 100)]
		public int ShortPeriod
		{
			get => _emaShort.Period;
			set
			{
				if (value <= 0)
					return;

				_emaShort.Period = _emaShortAbs.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 110)]
		public int LongPeriod
		{
			get => _emaLong.Period;
			set
			{
				if (value <= 0)
					return;

				_emaLong.Period = _emaLongAbs.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SignalPeriod", GroupName = "Settings", Order = 120)]
		public int SignalPeriod
		{
			get => _emaSignal.Period;
			set
			{
				if (value <= 0)
					return;

				_emaSignal.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Ergodic()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_emaShort.Period = _emaShortAbs.Period = 5;
			_emaLong.Period = _emaLongAbs.Period = 20;
			_emaSignal.Period = 5;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var diff = value - (decimal)SourceDataSeries[bar - 1];

			_emaLong.Calculate(bar, diff);
			_emaLongAbs.Calculate(bar, Math.Abs(diff));

			_emaShort.Calculate(bar, _emaLong[bar]);
			_emaShortAbs.Calculate(bar, _emaLongAbs[bar]);

			var tsi = _emaShort[bar] / _emaShortAbs[bar];

			_emaSignal.Calculate(bar, tsi);

			_renderSeries[bar] = tsi - _emaSignal[bar];
		}

		#endregion
	}
}