namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Study Angle")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.AngleDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602533")]
	public class Angle : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private int _period;

        #endregion

        #region Properties

        [Parameter]
		[Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Angle()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 10;
			LineSeries.Add(new LineSeries("ZeroVal", Strings.ZeroValue)
			{
				Color = Colors.Gray,
				Value = 0,
				Width = 2,
				DescriptionKey = nameof(Strings.ZeroLineDescription),
			});

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				_renderSeries.Clear();

			if (bar < _period)
				return;

			_renderSeries[bar] = (decimal)(Math.Atan((double)
				((value - (decimal)SourceDataSeries[bar - _period]) / _period)) * 180 / Math.PI);
		}

		#endregion
	}
}