namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("KD - Slow")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45426-kd-slow")]
	public class KdSlow : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _dSeries = new(Resources.SMA)
		{
			Color = DefaultColors.Green.Convert(),
			IgnoredByAlerts = true
		};

		private readonly ValueDataSeries _kSeries = new(Resources.Line) { Color = DefaultColors.Red.Convert() };
        
		private readonly KdFast _kdFast = new()
		{
			PeriodD = 10,
			PeriodK = 10
		};

		private readonly SMA _dSma = new() { Period = 10 };
        private readonly SMA _kSma = new() { Period = 10 };

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "PeriodK", GroupName = "ShortPeriod", Order = 100)]
		[Range(1, 10000)]
		public int PeriodK
		{
			get => _kdFast.PeriodK;
			set
			{
				_kdFast.PeriodK = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PeriodD", GroupName = "ShortPeriod", Order = 110)]
		[Range(1, 10000)]
        public int PeriodD
		{
			get => _kdFast.PeriodD;
			set
			{
				_kdFast.PeriodD = _kSma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PeriodD", GroupName = "LongPeriod", Order = 120)]
		[Range(1, 10000)]
        public int SlowPeriodD
		{
			get => _dSma.Period;
			set
			{
				_dSma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public KdSlow()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			Add(_kdFast);
			DataSeries[0] = _kSeries;
			DataSeries.Add(_dSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_kSeries[bar] = _kSma.Calculate(bar, ((ValueDataSeries)_kdFast.DataSeries[0])[bar]);
			_dSeries[bar] = _dSma.Calculate(bar, ((ValueDataSeries)_kdFast.DataSeries[1])[bar]);
		}

		#endregion
	}
}