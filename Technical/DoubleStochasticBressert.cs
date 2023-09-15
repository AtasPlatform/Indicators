namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Double Stochastic - Bressert")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45192-double-stochastic-bressert")]
	public class DoubleStochasticBressert : Indicator
	{
		#region Fields

		private readonly DoubleStochastic _ds = new()
		{
			Period = 10,
			SmaPeriod = 10
		};

		private readonly EMA _ema = new() { Period = 10 };
		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _ds.Period;
			set
			{
				_ds.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EMAPeriod), GroupName = nameof(Strings.Settings), Order = 110)]
		[Range(1, 10000)]
        public int SmaPeriod
		{
			get => _ds.SmaPeriod;
			set
			{
				_ds.SmaPeriod = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Smooth), GroupName = nameof(Strings.Settings), Order = 120)]
		[Range(1, 10000)]
        public int Smooth
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

		public DoubleStochasticBressert()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			Add(_ds);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = _ema.Calculate(bar, _ds[bar]);
		}

		#endregion
	}
}