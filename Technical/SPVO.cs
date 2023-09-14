namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Simple Percentage Volume Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/49309-volume-oscillator")]
	public class SPVO : Indicator
	{
		#region Fields

		private readonly SMA _longSma = new() { Period = 60 };
        private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private readonly SMA _shortSma = new() { Period = 20 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Settings), Order = 100)]
		[Range(2, 10000)]
		public int ShortPeriod
		{
			get => _shortSma.Period;
			set
			{
				_shortSma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Settings), Order = 110)]
		[Range(2, 10000)]
        public int LongPeriod
		{
			get => _longSma.Period;
			set
			{
				_longSma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SPVO() 
			:base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			LineSeries.Add(new LineSeries("Base", Strings.BaseLine)
			{
				Color = DefaultColors.Gray.Convert(),
				Value = 0
			});
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var volume = GetCandle(bar).Volume;
			_shortSma.Calculate(bar, volume);
			_longSma.Calculate(bar, volume);

			if (bar == 0)
			{
				_renderSeries.Clear();
				return;
			}
			
			_renderSeries[bar] = _longSma[bar] != 0
				? 100 * (_shortSma[bar] - _longSma[bar]) / _longSma[bar]
				: _renderSeries[bar - 1];
		}

		#endregion
	}
}