namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Drawing;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Hull Moving Average")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.HMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602550")]
	public class HMA : Indicator
	{
		#region Fields

		private ValueDataSeries _renderSeries = new("RenderSeries", "Hull Moving Average");

		private readonly WMA _wmaHull = new();
		private readonly WMA _wmaPrice = new();

		private readonly WMA _wmaPriceHalf = new();
		private bool _coloredDirection = true;
		private Color _bullishColor = DefaultColors.Green;
		private Color _bearishColor = DefaultColors.Red;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _wmaPrice.Period;
			set
			{
				_wmaPrice.Period = value;
				_wmaHull.Period = Convert.ToInt32(Math.Sqrt(value));
				_wmaPriceHalf.Period = value / 2;
				RecalculateValues();
			}
		}


		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColoredDirection), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ColoredDirectionDescription), Order = 200)]
		[Range(1, 10000)]
		public bool ColoredDirection
        {
			get => _coloredDirection;
			set
			{
				_coloredDirection = value;

                RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BullishColorDescription), Order = 210)]
		public System.Windows.Media.Color BullishColor
		{
			get => _bullishColor.Convert();
			set
			{
				_bullishColor = value.Convert();
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BearishColorDescription), Order = 220)]
		public System.Windows.Media.Color BearishColor
		{
			get => _bearishColor.Convert();
			set
			{
				_bearishColor = value.Convert();
				RecalculateValues();
			}
		}
        #endregion

        #region ctor

        public HMA()
			: base(true)
		{
			DenyToChangePanel = true;
			Period = 16;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var wmaPriceHalf = _wmaPriceHalf.Calculate(bar, candle.Close);
			var wmaPrice = _wmaPrice.Calculate(bar, candle.Close);

			var wmaHull = _wmaHull.Calculate(bar, 2.0m * wmaPriceHalf - wmaPrice);
			_renderSeries[bar] = wmaHull;

			if (bar == 0 || !ColoredDirection)
				return;

			_renderSeries.Colors[bar] = _renderSeries[bar] > _renderSeries[bar - 1] 
				? _bullishColor
				: _bearishColor;
		}

        #endregion
    }
}