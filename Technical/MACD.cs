namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using Utils.Common.Localization;

	[DisplayName("MACD")]
	[LocalizedDescription(typeof(Strings), "MACD")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/8125-macd")]
	public class MACD : Indicator
	{
		#region Fields

		private readonly EMA _long = new() { Period = 26 };
		private readonly EMA _short = new() { Period = 12 };
		private readonly EMA _signal = new() { Period = 9 };

        #endregion

        #region Properties

        [Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = "LongPeriod",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
		public int LongPeriod
		{
			get => _long.Period;
			set
			{
				_long.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = "ShortPeriod",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
        public int ShortPeriod
		{
			get => _short.Period;
			set
			{
				_short.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = "SignalPeriod",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
        public int SignalPeriod
		{
			get => _signal.Period;
			set
			{
				_signal.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MACD()
		{
			Panel = IndicatorDataProvider.NewPanel;

			((ValueDataSeries)DataSeries[0]).Color = DefaultColors.Blue.Convert();

            DataSeries.Add(new ValueDataSeries("SignalId", "Signal")
			{
				VisualType = VisualMode.Line,
				IgnoredByAlerts = true
			});

			DataSeries.Add(new ValueDataSeries("DifferenceId", "Difference")
			{
				VisualType = VisualMode.Histogram,
				Color = DefaultColors.Teal.Convert(),
				IgnoredByAlerts = true
			});
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