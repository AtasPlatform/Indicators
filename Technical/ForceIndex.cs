namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Force Index")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ForceIndexDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602387")]
	public class ForceIndex : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private bool _useEma;

		#endregion

		#region Properties

		[Range(1, 10000)]
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.EMAPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.EMAPeriodDescription), Order = 10)]
		public FilterInt PeriodFilter { get; set; } = new();

		[Browsable(false)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseMA), GroupName = nameof(Strings.Settings), Order = 100)]
		public bool UseEma
		{
			get => _useEma;
			set
			{
				_useEma = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Browsable(false)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod), GroupName = nameof(Strings.Settings), Order = 110)]
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

		public ForceIndex()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
			PeriodFilter.Value = _ema.Period;
        }

        #endregion

        #region Protected methods

        protected override void OnInitialize()
        {
			PeriodFilter.PropertyChanged += (o, e) =>
			{
                if (o is not FilterInt filter)
                    return;

                switch (e.PropertyName)
				{
					case nameof(filter.Enabled):
                        UseEma = filter.Enabled;
                        break;
                    case nameof(filter.Value):
                        Period = filter.Value;
                        break;
                }

				RedrawChart();
            };
        }

        protected override void OnCalculate(int bar, decimal value)
		{
			_ema.Calculate(bar, 0);

			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var force = candle.Volume * (candle.Close - prevCandle.Close);
			
			_renderSeries[bar] = _useEma 
				? _ema.Calculate(bar, force)
				: force;
		}

		#endregion
	}
}