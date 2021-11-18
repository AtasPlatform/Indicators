namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	[DisplayName("MACD Bollinger Bands - Improved")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45421-macd-bollinger-bands-improved")]
	public class MacdBbImproved : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _bottomBand = new(Resources.BottomBand);
		private readonly MACD _macd = new();
		private readonly SMA _sma = new();
		private readonly StdDev _stdDev = new();
		private readonly ValueDataSeries _topBand = new(Resources.TopBand);
		private int _stdDevCount;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int MacdPeriod
		{
			get => _macd.SignalPeriod;
			set
			{
				if (value <= 0)
					return;

				_macd.SignalPeriod = _stdDev.Period = _sma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 110)]
		public int MacdShortPeriod
		{
			get => _macd.ShortPeriod;
			set
			{
				if (value <= 0)
					return;

				_macd.ShortPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 120)]
		public int MacdLongPeriod
		{
			get => _macd.LongPeriod;
			set
			{
				if (value <= 0)
					return;

				_macd.LongPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "StdDev", GroupName = "Settings", Order = 130)]
		public int StdDev
		{
			get => _stdDevCount;
			set
			{
				if (value <= 0)
					return;

				_stdDevCount = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MacdBbImproved()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_topBand.Color = _bottomBand.Color = Colors.Purple;

			((ValueDataSeries)_macd.DataSeries[1]).LineDashStyle = LineDashStyle.Solid;
			_macd.LongPeriod = 26;
			_macd.ShortPeriod = 12;
			_macd.SignalPeriod = _stdDev.Period = _sma.Period = 9;
			_stdDevCount = 2;
			DataSeries[0] = _topBand;
			DataSeries.Add(_bottomBand);
			DataSeries.AddRange(_macd.DataSeries);
		}

		#endregion

		#region Protected methods

		#region Overrides of BaseIndicator

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		#endregion

		protected override void OnCalculate(int bar, decimal value)
		{
			_macd.Calculate(bar, value);

			var deltaMacd = ((ValueDataSeries)_macd.DataSeries[0])[bar] - ((ValueDataSeries)_macd.DataSeries[1])[bar];
			_sma.Calculate(bar, Math.Abs(deltaMacd));

			var macdMa = ((ValueDataSeries)_macd.DataSeries[1])[bar];

			var stdDev = _stdDev.Calculate(bar, macdMa);

			_topBand[bar] = macdMa + _sma[bar] + _stdDevCount * stdDev;
			_bottomBand[bar] = macdMa - _sma[bar] - _stdDevCount * stdDev;
		}

		#endregion
	}
}