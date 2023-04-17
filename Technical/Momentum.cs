namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("Momentum")]
	[LocalizedDescription(typeof(Resources), "Momentum")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/7083-momentum")]
	public class Momentum : Indicator
	{
		#region Fields

		private readonly SMA _sma = new();
		private readonly ValueDataSeries _smaSeries = new(Resources.SMA)
		{
			Color = DefaultColors.Blue.Convert(),
			UseMinimizedModeIfEnabled = true,
			IgnoredByAlerts = true
		};

		private int _period = 10;

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
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowSMA", GroupName = "SMA", Order = 200)]
		public bool ShowSma
		{
			get => _smaSeries.VisualType == VisualMode.Line;
			set => _smaSeries.VisualType = value ? VisualMode.Line : VisualMode.Hide;
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "SMA", Order = 210)]
		[Range(1, 10000)]
        public int SmaPeriod
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Momentum()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0].UseMinimizedModeIfEnabled = true;
			DataSeries.Add(_smaSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var start = Math.Max(0, bar - Period + 1);
			this[bar] = value - (decimal)SourceDataSeries[start];
			_smaSeries[bar] = _sma.Calculate(bar, this[bar]);
		}

		#endregion
	}
}