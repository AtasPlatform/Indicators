namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Outside Bar")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.OutsideBarDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602280")]
	public class OutsideBar : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
		{
			Color = DefaultColors.Blue.Convert(),
			VisualType = VisualMode.Dots,
			Width = 3
		};

		private bool _includeEqual;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IncludeEqualHighLow), GroupName = nameof(Strings.Settings), Description = nameof(Strings.IncludeEqualsValuesDescription), Order = 100)]
		public bool IncludeEqual
		{
			get => _includeEqual;
			set
			{
				_includeEqual = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public OutsideBar()
			: base(true)
		{
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_renderSeries.Clear();
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			switch (_includeEqual)
			{
				case false when candle.High > prevCandle.High && candle.Low < prevCandle.Low:
					_renderSeries[bar] = candle.High;
					return;
				case true when candle.High >= prevCandle.High && candle.Low <= prevCandle.Low:
					_renderSeries[bar] = candle.High;
					break;
			}
		}

		#endregion
	}
}