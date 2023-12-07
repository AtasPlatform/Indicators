namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Cumulative Adjusted Value")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CAVDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602359")]
	public class CAV : Indicator
	{
		#region Fields

		private readonly EMA _ema = new()
		{
			Period = 10
		};

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _ema.Period;
			set
			{
				_ema.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public CAV()
		{
			Panel = IndicatorDataProvider.NewPanel;
			var zeroLine = new LineSeries("ZeroVal", Strings.ZeroValue) 
			{
				Color = Colors.Gray, 
				Value = 0, 
				DescriptionKey = nameof(Strings.ZeroLineDescription)
			};

            LineSeries.Add(zeroLine);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var adjVal = value - _ema.Calculate(bar, value);

			if (bar == 0)
			{
				_renderSeries[bar] = adjVal;
				return;
			}

			_renderSeries[bar] = _renderSeries[bar - 1] + adjVal;
		}

		#endregion
	}
}