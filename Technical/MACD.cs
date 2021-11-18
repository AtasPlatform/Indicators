namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("MACD")]
	[LocalizedDescription(typeof(Resources), "MACD")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/8125-macd")]
	public class MACD : Indicator
	{
		#region Fields

		private readonly EMA _long = new();
		private readonly EMA _short = new();
		private readonly EMA _signal = new();

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "LongPeriod",
			GroupName = "Common",
			Order = 20)]
		public int LongPeriod
		{
			get => _long.Period;
			set
			{
				if (value <= 0)
					return;

				_long.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "ShortPeriod",
			GroupName = "Common",
			Order = 20)]
		public int ShortPeriod
		{
			get => _short.Period;
			set
			{
				if (value <= 0)
					return;

				_short.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "SignalPeriod",
			GroupName = "Common",
			Order = 20)]
		public int SignalPeriod
		{
			get => _signal.Period;
			set
			{
				if (value <= 0)
					return;

				_signal.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MACD()
		{
			Panel = IndicatorDataProvider.NewPanel;

			((ValueDataSeries)DataSeries[0]).Color = Colors.Blue;

			DataSeries.Add(new ValueDataSeries("Signal")
			{
				VisualType = VisualMode.Line
			});

			DataSeries.Add(new ValueDataSeries("Difference")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.CadetBlue
			});

			LongPeriod = 26;
			ShortPeriod = 12;
			SignalPeriod = 9;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var macd = _short.Calculate(bar, value) - _long.Calculate(bar, value);
			var signal = _signal.Calculate(bar, macd);

			this[bar] = macd;
			DataSeries[1][bar] = signal;
			DataSeries[2][bar] = macd - signal;
		}

		#endregion
	}
}