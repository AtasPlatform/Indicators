namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

	[DisplayName("MACD")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MACDDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602418")]
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
			Name = nameof(Strings.LongPeriod),
			GroupName = nameof(Strings.Settings),
            Description = nameof(Strings.LongPeriodDescription),
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
			Name = nameof(Strings.ShortPeriod),
			GroupName = nameof(Strings.Settings),
            Description = nameof(Strings.ShortPeriodDescription),
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
			Name = nameof(Strings.SignalPeriod),
			GroupName = nameof(Strings.Settings),
            Description = nameof(Strings.SignalPeriodDescription),
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
			DataSeries[0].DescriptionKey = nameof(Strings.BaseLineSettingsDescription);


            DataSeries.Add(new ValueDataSeries("SignalId", "Signal")
			{
				VisualType = VisualMode.Line,
				IgnoredByAlerts = true,
				DescriptionKey = nameof(Strings.SignalLineSettingsDescription)
			});

			DataSeries.Add(new ValueDataSeries("DifferenceId", "Difference")
			{
				VisualType = VisualMode.Histogram,
				Color = DefaultColors.Teal.Convert(),
				IgnoredByAlerts = true,
                DescriptionKey = nameof(Strings.MACDVsSignalDiffSettingsDescription) 
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