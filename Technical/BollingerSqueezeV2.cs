namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Bollinger Squeeze 2")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.BollingerSqueezeV2Description))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602634")]
	public class BollingerSqueezeV2 : Indicator
	{
        #region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
        private readonly ValueDataSeries _downSeries = new("DownSeries", Strings.Down) { IgnoredByAlerts = true };
        private readonly ValueDataSeries _upSeries = new("UpSeries", Strings.Up) { IgnoredByAlerts = true };

        private readonly BollingerBands _bb = new();
		private readonly EMA _emaMomentum = new();
		private readonly KeltnerChannel _kb = new();
		private readonly Momentum _momentum = new();

		private System.Drawing.Color _lowColor = DefaultColors.Maroon;
		private System.Drawing.Color _lowerColor = DefaultColors.Red;
		private System.Drawing.Color _upColor = DefaultColors.Green;
		private System.Drawing.Color _upperColor = DefaultColors.Lime;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Upper), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.UpperPositiveValueColorDescription), Order = 610)]
        public Color UpperColor
        {
	        get => _upperColor.Convert();
	        set
	        {
		        _upperColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Up), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.PositiveValueColorDescription), Order = 620)]
        public Color UpColor
        {
	        get => _upColor.Convert();
	        set
	        {
		        _upColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Low), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NegativeValueColorDescription), Order = 630)]
        public Color LowColor
        {
	        get => _lowColor.Convert();
	        set
	        {
		        _lowColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Lower), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.LowerNegativeValueColorDescription), Order = 640)]
        public Color LowerColor
        {
	        get => _lowerColor.Convert();
	        set
	        {
		        _lowerColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.BollingerBands), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 1000000)]
		public int BbPeriod
		{
			get => _bb.Period;
			set
			{
				_bb.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BBandsWidth), GroupName = nameof(Strings.BollingerBands), Description = nameof(Strings.DeviationRangeDescription), Order = 110)]
		[Range(0.000001, 1000000)]
		public decimal BbWidth
		{
			get => _bb.Width;
			set
			{
				_bb.Width = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.KeltnerChannel), Description = nameof(Strings.PeriodDescription), Order = 200)]
		[Range(1, 1000000)]
		public int KbPeriod
		{
			get => _kb.Period;
			set
			{
				_kb.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetMultiplier), GroupName = nameof(Strings.KeltnerChannel), Description = nameof(Strings.DeviationRangeDescription), Order = 210)]
		[Range(0.000001, 1000000)]
		public decimal KbMultiplier
		{
			get => _kb.Koef;
			set
			{
				_kb.Koef = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Momentum), Description = nameof(Strings.PeriodDescription), Order = 300)]
		[Range(1, 1000000)]
		public int MomentumPeriod
		{
			get => _momentum.Period;
			set
			{
				_momentum.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EMA), GroupName = nameof(Strings.Momentum), Description = nameof(Strings.PeriodDescription), Order = 310)]
		[Range(1, 1000000)]
		public int EmaMomentum
		{
			get => _emaMomentum.Period;
			set
			{
				_emaMomentum.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerSqueezeV2()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_bb.Period = 10;
			_bb.Width = 1;

			_kb.Period = 10;
			_kb.Koef = 1;

			_upSeries.Color = DefaultColors.Green.Convert();
			_downSeries.Color = Colors.Firebrick;

			_upSeries.VisualType = _downSeries.VisualType = VisualMode.Dots;
			_upSeries.Width = _downSeries.Width = 3;

			Add(_kb);

			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
			DataSeries.Add(_renderSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			_momentum.Calculate(bar, value);
			_bb.Calculate(bar, value);
			_renderSeries[bar] = _emaMomentum.Calculate(bar, _momentum[bar]);

			if (bar == 0)
				return;

			_renderSeries.Colors[bar] = _emaMomentum[bar] switch
			{
				> 0 when _emaMomentum[bar] >= _emaMomentum[bar - 1] => _upperColor,
				> 0 when _emaMomentum[bar] < _emaMomentum[bar - 1] => _upColor,
				< 0 when _emaMomentum[bar] <= _emaMomentum[bar - 1] => _lowerColor,
				< 0 when _emaMomentum[bar] > _emaMomentum[bar - 1] => _lowColor,
				_ => _renderSeries.Colors[bar]
			};

			var bbTop = ((ValueDataSeries)_bb.DataSeries[1])[bar];
			var bbBot = ((ValueDataSeries)_bb.DataSeries[2])[bar];

			var kbTop = ((ValueDataSeries)_kb.DataSeries[1])[bar];
			var kbBot = ((ValueDataSeries)_kb.DataSeries[2])[bar];

			if (bbTop > kbTop && bbBot < kbBot)
				_upSeries[bar] = 0.00001m;
			else
				_downSeries[bar] = 0.00001m;
		}

		#endregion
	}
}