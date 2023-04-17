namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Color = System.Drawing.Color;

	[DisplayName("Weis Wave")]
	[Description("Weis Wave")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/17943-weis-wave")]
	public class WeissWave : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization)
		{
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true,
			ResetAlertsOnNewBar = true
		};

		private int _filter;
		private Color _filterColor = Color.LightBlue;
		private Color _negColor = DefaultColors.Red;
		private Color _posColor = DefaultColors.Green;

		#endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Up", GroupName = "Drawing", Order = 610)]
        public System.Windows.Media.Color PosColor
        {
	        get => _posColor.Convert();
	        set
	        {
		        _posColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Down", GroupName = "Drawing", Order = 620)]
        public System.Windows.Media.Color NegColor
        {
	        get => _negColor.Convert();
	        set
	        {
		        _negColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Filter", GroupName = "Drawing", Order = 630)]
        public System.Windows.Media.Color FilterColor
        {
	        get => _filterColor.Convert();
	        set
	        {
		        _filterColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [DisplayName("Filter")]
		public int Filter
		{
			get => _filter;
			set
			{
				_filter = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public WeissWave()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
            if (bar == 0)
			{
				_renderSeries[bar] = candle.Volume;
				_renderSeries.Colors[bar] = candle.Open < candle.Close ? _posColor : _negColor;
			}
			else
            {
	            var prevCandle = GetCandle(bar - 1);

	            var renderValue = Math.Sign(candle.Open - candle.Close) == Math.Sign(prevCandle.Open - prevCandle.Close) 
					? _renderSeries[bar - 1] + candle.Volume
					: candle.Volume;

	            _renderSeries[bar] = renderValue;
	            _renderSeries.Colors[bar] = candle.Open < candle.Close
		            ? _posColor
		            : _negColor;
            }

            if (_filter <= 0)
	            return;

            if (_renderSeries[bar] > _filter)
				_renderSeries.Colors[bar] = _filterColor;
		}

		#endregion
	}
}