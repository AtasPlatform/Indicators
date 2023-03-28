namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;
    using OFT.Attributes;

	[DisplayName("Bollinger Squeeze 2")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45177-bollinger-squeeze-2")]
	public class BollingerSqueezeV2 : Indicator
	{
        #region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
        private readonly BollingerBands _bb = new();
		private readonly ValueDataSeries _downSeries = new(Resources.Down) { IgnoredByAlerts = true };
		private readonly EMA _emaMomentum = new();
		private readonly KeltnerChannel _kb = new();
		private readonly Momentum _momentum = new();

		private readonly ValueDataSeries _upSeries = new(Resources.Up) { IgnoredByAlerts = true };

		private System.Drawing.Color _lowColor = DefaultColors.Maroon;
		private System.Drawing.Color _lowerColor = DefaultColors.Red;
		private System.Drawing.Color _upColor = DefaultColors.Green;
		private System.Drawing.Color _upperColor = DefaultColors.Lime;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Upper", GroupName = "Drawing", Order = 610)]
        public System.Windows.Media.Color UpperColor
        {
	        get => _upperColor.Convert();
	        set
	        {
		        _upperColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Up", GroupName = "Drawing", Order = 620)]
        public System.Windows.Media.Color UpColor
        {
	        get => _upColor.Convert();
	        set
	        {
		        _upColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Low", GroupName = "Drawing", Order = 630)]
        public System.Windows.Media.Color LowColor
        {
	        get => _lowColor.Convert();
	        set
	        {
		        _lowColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Lower", GroupName = "Drawing", Order = 640)]
        public System.Windows.Media.Color LowerColor
        {
	        get => _lowerColor.Convert();
	        set
	        {
		        _lowerColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "BollingerBands", Order = 100)]
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

		[Display(ResourceType = typeof(Resources), Name = "BBandsWidth", GroupName = "BollingerBands", Order = 110)]
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

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "KeltnerChannel", Order = 200)]
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

		[Display(ResourceType = typeof(Resources), Name = "OffsetMultiplier", GroupName = "KeltnerChannel", Order = 210)]
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

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Momentum", Order = 300)]
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

		[Display(ResourceType = typeof(Resources), Name = "EMA", GroupName = "Momentum", Order = 310)]
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