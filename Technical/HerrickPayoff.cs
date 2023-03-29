namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Herrick Payoff Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45245-herrick-payoff-index")]
	public class HerrickPayoff : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _hpiSec = new("HpiSecondary");
		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization)
		{
			VisualType = VisualMode.Histogram,
			IsHidden = true,
			UseMinimizedModeIfEnabled = true
		};
		
		private decimal _divisor = 1;
        private int _smooth = 10;

        private System.Drawing.Color _negColor = DefaultColors.Red;
        private System.Drawing.Color _posColor = DefaultColors.Blue;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "BuyColor", GroupName = "Drawing", Order = 610)]
        public System.Windows.Media.Color PosColor
        {
	        get => _posColor.Convert();
	        set
	        {
		        _posColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "SellColor", GroupName = "Drawing", Order = 620)]
        public System.Windows.Media.Color NegColor
        {
	        get => _negColor.Convert();
	        set
	        {
		        _negColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Divisor", GroupName = "Settings", Order = 110)]
		[Range(0.00000001, 100000000)]
		public decimal Divisor
		{
			get => _divisor;
			set
			{
				_divisor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Smooth", GroupName = "Settings", Order = 120)]
		[Range(1, 10000)]
		public int Smooth
		{
			get => _smooth;
			set
			{
				_smooth = value;
				RecalculateValues();
			}
		}
		
		#endregion

		#region ctor

		public HerrickPayoff()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods
		
		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var highLow = (candle.High + candle.Low) / 2m;
			var prevHighLow = (prevCandle.High + prevCandle.Low) / 2m;
			var oi = candle.OI;

			var prevOi = prevCandle.OI;
			var calcOI = oi > 0 ? oi : prevOi;

			var maxOi = Math.Max(calcOI, prevOi);

			if (maxOi == 0)
				return;

			_hpiSec[bar] = InstrumentInfo.TickSize * candle.Volume * (highLow - prevHighLow) / _divisor *
				((1 + 2 * Math.Abs(calcOI - prevOi)) / maxOi);

			var lastValue = _renderSeries[bar - 1];
            
			var renderValue = maxOi > 0
	            ? lastValue + _smooth * (_hpiSec[bar] - _hpiSec[bar - 1])
	            : lastValue;

			_renderSeries[bar] = renderValue;
			_renderSeries.Colors[bar] = renderValue > 0 ? _posColor : _negColor;
		}

		#endregion
	}
}