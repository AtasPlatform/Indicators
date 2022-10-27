namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("McClellan Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/40050-mcclellan-oscillator")]
	public class McClellanOscillator : Indicator
	{
		#region Fields

		private readonly EMA _mEmaLong = new() { Period = 39 };
		private readonly EMA _mEmaShort = new() { Period = 19 };
		private readonly ValueDataSeries _renderSeries = new("McClellan Oscillator")
		{
			Color = Colors.LimeGreen,
			Width = 2,
			UseMinimizedModeIfEnabled = true
		};

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int ShortPeriod
		{
			get => _mEmaShort.Period;
			set
			{
				_mEmaShort.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
        public int LongPeriod
		{
			get => _mEmaLong.Period;
			set
			{
				_mEmaLong.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public McClellanOscillator()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = _mEmaShort.Calculate(bar, value) - _mEmaLong.Calculate(bar, value);
		}

		#endregion
	}
}