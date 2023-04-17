namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	[DisplayName("MACD Bollinger Bands - Standard")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45419-macd-bollinger-bands-standard")]
	public class MacdBbStandart : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _bottomBand = new(Resources.BottomBand)
		{
			Color = Colors.Purple,
			IgnoredByAlerts = true
		};
		private readonly ValueDataSeries _topBand = new(Resources.TopBand)
		{
			Color = Colors.Purple,
			IgnoredByAlerts = true
        };

        private readonly MACD _macd = new()
		{
			LongPeriod = 26,
			ShortPeriod = 12,
			SignalPeriod = 9
		};

		private readonly StdDev _stdDev = new() { Period = 9 };
		private int _stdDevCount = 2;

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

				_macd.SignalPeriod = _stdDev.Period = value;
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

		public MacdBbStandart()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			((ValueDataSeries)_macd.DataSeries[1]).LineDashStyle = LineDashStyle.Solid;
			DataSeries[0] = _topBand;
			DataSeries.Add(_bottomBand);
			DataSeries.AddRange(_macd.DataSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}
		
		protected override void OnCalculate(int bar, decimal value)
		{
			_macd.Calculate(bar, value);
			var macdMa = ((ValueDataSeries)_macd.DataSeries[1])[bar];

			var stdDev = _stdDev.Calculate(bar, macdMa);

			_topBand[bar] = macdMa + _stdDevCount * stdDev;
			_bottomBand[bar] = macdMa - _stdDevCount * stdDev;
		}

		#endregion
	}
}