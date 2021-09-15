namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("KDJ")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45427-kdj")]
	public class KDJ : Indicator
	{
		#region Fields

		private readonly KdSlow _kdSlow = new();

		private readonly ValueDataSeries _renderSeries = new(Strings.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "PeriodK", GroupName = "ShortPeriod", Order = 100)]
		public int PeriodK
		{
			get => _kdSlow.PeriodK;
			set
			{
				if (value <= 0)
					return;

				_kdSlow.PeriodK = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "PeriodD", GroupName = "ShortPeriod", Order = 110)]
		public int PeriodD
		{
			get => _kdSlow.PeriodD;
			set
			{
				if (value <= 0)
					return;

				_kdSlow.PeriodD = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "PeriodD", GroupName = "LongPeriod", Order = 120)]
		public int SlowPeriodD
		{
			get => _kdSlow.SlowPeriodD;
			set
			{
				if (value <= 0)
					return;

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

			_kdSlow.SlowPeriodD = _kdSlow.PeriodK = _kdSlow.PeriodD = 10;
			_renderSeries.Color = Colors.Blue;
			
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