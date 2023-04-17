namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("KDJ")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45427-kdj")]
	public class KDJ : Indicator
	{
		#region Fields

		private readonly KdSlow _kdSlow = new()
		{
			SlowPeriodD = 10,
			PeriodK = 10,
			PeriodD = 10
		};

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization) { Color = DefaultColors.Blue.Convert() };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "PeriodK", GroupName = "ShortPeriod", Order = 100)]
		[Range(1, 10000)]
		public int PeriodK
		{
			get => _kdSlow.PeriodK;
			set
			{
				_kdSlow.PeriodK = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PeriodD", GroupName = "ShortPeriod", Order = 110)]
		[Range(1, 10000)]
        public int PeriodD
		{
			get => _kdSlow.PeriodD;
			set
			{
				_kdSlow.PeriodD = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PeriodD", GroupName = "LongPeriod", Order = 120)]
		[Range(1, 10000)]
        public int SlowPeriodD
		{
			get => _kdSlow.SlowPeriodD;
			set
			{
				_kdSlow.SlowPeriodD = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public KDJ()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			Add(_kdSlow);
			DataSeries[0] = _renderSeries;
			DataSeries.AddRange(_kdSlow.DataSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = 3 * ((ValueDataSeries)_kdSlow.DataSeries[0])[bar] -
				2 * ((ValueDataSeries)_kdSlow.DataSeries[1])[bar];
		}

		#endregion
	}
}