namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;
    using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Bollinger Squeeze")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43439-bollinger-squeeze")]
	public class BollingerSqueeze : Indicator
	{
		#region Fields

		private readonly BollingerBands _bb = new();
		private readonly KeltnerChannel _kb = new();
        private readonly ValueDataSeries _downRatio = new("DownRatio", Strings.LowRatio);
        private readonly ValueDataSeries _upRatio = new("UpRatio", Strings.HighRatio);

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.BollingerBands), Order = 100)]
		public int BbPeriod
		{
			get => _bb.Period;
			set
			{
				if (value <= 0)
					return;

				_bb.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BBandsWidth), GroupName = nameof(Strings.BollingerBands), Order = 110)]
		[Range(0, 100000000)]
		public decimal BbWidth
		{
			get => _bb.Width;
			set
			{
				if (value <= 0)
					return;

				_bb.Width = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.KeltnerChannel), Order = 200)]
		public int KbPeriod
		{
			get => _kb.Period;
			set
			{
				if (value <= 0)
					return;

				_kb.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetMultiplier), GroupName = nameof(Strings.KeltnerChannel), Order = 210)]
		[Range(0, 100000000)]
		public decimal KbMultiplier
		{
			get => _kb.Koef;
			set
			{
				if (value <= 0)
					return;

				_kb.Koef = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerSqueeze()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_bb.Period = 10;
			_bb.Width = 1;

			_kb.Period = 10;
			_kb.Koef = 1;

			_upRatio.VisualType = _downRatio.VisualType = VisualMode.Histogram;

			_upRatio.Color = DefaultColors.Green.Convert();
			_downRatio.Color = DefaultColors.Red.Convert();

			Add(_kb);

			DataSeries[0] = _upRatio;
			DataSeries.Add(_downRatio);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			_bb.Calculate(bar, value);
			var bbTop = ((ValueDataSeries)_bb.DataSeries[1])[bar];
			var bbBot = ((ValueDataSeries)_bb.DataSeries[2])[bar];

			var kbTop = ((ValueDataSeries)_kb.DataSeries[1])[bar];
			var kbBot = ((ValueDataSeries)_kb.DataSeries[2])[bar];

			var bandsRatio = 0m;

			if (bbTop - bbBot != 0)
				bandsRatio = (kbTop - kbBot) / (bbTop - bbBot) - 1;

			if (bandsRatio >= 0)
				_upRatio[bar] = bandsRatio;
			else
				_downRatio[bar] = bandsRatio;
		}

		#endregion
	}
}